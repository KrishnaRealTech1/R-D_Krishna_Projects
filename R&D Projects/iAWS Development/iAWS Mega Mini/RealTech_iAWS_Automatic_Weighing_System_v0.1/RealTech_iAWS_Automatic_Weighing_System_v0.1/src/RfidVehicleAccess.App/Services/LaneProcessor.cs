using System.Collections.Concurrent;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

/// <summary>
/// iAWS single-weighbridge transaction engine.
///
/// One physical weighbridge is shared by IN and OUT. When the configured target
/// weight is reached the cycle becomes ARMED. The first valid RFID event from
/// either reader atomically locks the direction for the whole weighing cycle;
/// every RFID event from the other reader is ignored until the vehicle leaves
/// and the weight falls to the configured reset threshold.
/// </summary>
public sealed class LaneProcessor
{
    private readonly AppOptions _options;
    private readonly RfidValidator _rfidValidator;
    private readonly VehicleRepository _vehicleRepository;
    private readonly TripRepository _tripRepository;
    private readonly CameraSnapshotService _camera;
    private readonly HardwareGateway _hardware;
    private readonly AppLogger _logger;
    private readonly SystemEventHub _eventHub;

    private readonly object _cycleSync = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastReads =
        new(StringComparer.OrdinalIgnoreCase);

    private decimal _currentWeightKg;
    private bool _isArmed;
    private bool _isProcessing;
    private LaneDirection? _directionLock;
    private string _lockedRfid = string.Empty;
    private TaskCompletionSource<bool>? _weightResetSignal;

    public LaneProcessor(
        AppOptions options,
        RfidValidator rfidValidator,
        VehicleRepository vehicleRepository,
        TripRepository tripRepository,
        CameraSnapshotService camera,
        HardwareGateway hardware,
        AppLogger logger,
        SystemEventHub eventHub)
    {
        _options = options;
        _rfidValidator = rfidValidator;
        _vehicleRepository = vehicleRepository;
        _tripRepository = tripRepository;
        _camera = camera;
        _hardware = hardware;
        _logger = logger;
        _eventHub = eventHub;
    }

    public decimal CurrentWeightKg
    {
        get
        {
            lock (_cycleSync)
            {
                return _currentWeightKg;
            }
        }
    }

    public LaneDirection? LockedDirection
    {
        get
        {
            lock (_cycleSync)
            {
                return _directionLock;
            }
        }
    }

    public bool IsArmed
    {
        get
        {
            lock (_cycleSync)
            {
                return _isArmed;
            }
        }
    }

    public async Task UpdateWeightAsync(
        decimal weightKg,
        CancellationToken cancellationToken = default)
    {
        var target = Math.Max(0m, _options.Weighbridge.TargetWeightKg);
        var reset = Math.Max(0m, _options.Weighbridge.ResetWeightKg);
        bool becameArmed = false;
        bool resetIdleCycle = false;
        TaskCompletionSource<bool>? releaseSignal = null;

        lock (_cycleSync)
        {
            _currentWeightKg = Math.Max(0m, weightKg);

            if (!_isProcessing && !_isArmed && _currentWeightKg >= target && target > 0m)
            {
                _isArmed = true;
                becameArmed = true;
            }

            if (_isProcessing && _currentWeightKg <= reset)
            {
                releaseSignal = _weightResetSignal;
            }
            else if (!_isProcessing && _isArmed && _currentWeightKg <= reset)
            {
                ResetCycleStateNoLock();
                resetIdleCycle = true;
            }
        }

        releaseSignal?.TrySetResult(true);

        if (becameArmed)
        {
            await _logger.StatusAsync(
                $"WEIGHBRIDGE TARGET REACHED: {weightKg:0.###} kg >= {target:0.###} kg. " +
                "System ARMED; first IN/OUT RFID will own this weighing cycle.",
                cancellationToken);

            PublishWaitingState(
                LaneDirection.In,
                $"Weight target reached ({weightKg:0.###} kg) - waiting for first RFID");
            PublishWaitingState(
                LaneDirection.Out,
                $"Weight target reached ({weightKg:0.###} kg) - waiting for first RFID");
        }
        else if (resetIdleCycle)
        {
            await _logger.StatusAsync(
                $"WEIGHBRIDGE reset at {weightKg:0.###} kg. Armed cycle cleared before RFID detection.",
                cancellationToken);
            PublishIdleStates("Waiting for target weight");
        }
    }

    /// <summary>
    /// Legacy sensor input is deliberately ignored in iAWS. Weight is the only
    /// transaction trigger. This method remains only so older test utilities do
    /// not break when opening the project.
    /// </summary>
    public Task RegisterSensorStateAsync(
        LaneDirection direction,
        bool isHigh,
        CancellationToken cancellationToken = default) =>
        _logger.StatusAsync(
            $"{LaneText(direction)} vehicle sensor event ignored by iAWS; single weighbridge weight is the process trigger.",
            cancellationToken);

    public async Task ProcessRfidAsync(
        LaneDirection direction,
        string rawRfid,
        CancellationToken cancellationToken = default)
    {
        var lane = LaneText(direction);
        var rfid = _rfidValidator.Normalize(rawRfid);
        if (!_rfidValidator.IsAllowed(rfid))
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored because it is empty or does not match the configured RFID prefix: {rfid}",
                cancellationToken);
            return;
        }

        if (IsDuplicate(direction, rfid))
        {
            await _logger.StatusAsync($"{lane} duplicate RFID ignored: {rfid}", cancellationToken);
            return;
        }

        decimal lockedWeight;
        LaneDirection? winningDirection;
        string winningRfid;
        bool accepted = false;
        string ignoreReason = string.Empty;

        lock (_cycleSync)
        {
            lockedWeight = _currentWeightKg;
            winningDirection = _directionLock;
            winningRfid = _lockedRfid;

            if (!_isArmed)
            {
                ignoreReason =
                    $"weight target not reached ({lockedWeight:0.###}/{_options.Weighbridge.TargetWeightKg:0.###} kg)";
            }
            else if (_directionLock.HasValue)
            {
                ignoreReason =
                    $"{LaneText(_directionLock.Value)} RFID already won this weighbridge cycle ({_lockedRfid})";
            }
            else if (_isProcessing)
            {
                ignoreReason = "weighbridge cycle is already processing";
            }
            else
            {
                _directionLock = direction;
                _lockedRfid = rfid;
                _isProcessing = true;
                _weightResetSignal = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                winningDirection = direction;
                winningRfid = rfid;
                accepted = true;
            }
        }

        if (!accepted)
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored: {rfid}. Reason: {ignoreReason}.",
                cancellationToken);
            return;
        }

        _lastReads[$"{direction}:{rfid}"] = DateTimeOffset.Now;
        var otherDirection = direction == LaneDirection.In ? LaneDirection.Out : LaneDirection.In;
        await _logger.StatusAsync(
            $"FIRST RFID LOCKED: {lane} won the single-weighbridge cycle with RFID {rfid} at " +
            $"{lockedWeight:0.###} kg. {LaneText(otherDirection)} RFID input is ignored until weight reset.",
            cancellationToken);

        PublishLane(
            direction,
            LaneState.Processing,
            rfid,
            string.Empty,
            $"{lane} selected first - processing {lockedWeight:0.###} kg");
        PublishLane(
            otherDirection,
            LaneState.VehicleDetected,
            string.Empty,
            string.Empty,
            $"Ignored - {lane} RFID detected first");

        await _processLock.WaitAsync(cancellationToken);
        try
        {
            await ProcessLockedCycleAsync(direction, rfid, lockedWeight, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync(
                $"{lane} iAWS weighing process failed: {ex.Message}",
                CancellationToken.None);
            PublishLane(direction, LaneState.Error, rfid, string.Empty, ex.Message);
            await SafeReturnLaneToIdleAsync(direction);
        }
        finally
        {
            lock (_cycleSync)
            {
                // If the weight is already at/below reset, the next target crossing may arm
                // immediately. Otherwise keep the cycle locked until the current vehicle is
                // physically removed from the single weighbridge.
                if (_currentWeightKg <= Math.Max(0m, _options.Weighbridge.ResetWeightKg))
                {
                    ResetCycleStateNoLock();
                }
                else
                {
                    _isProcessing = false;
                    _isArmed = true;
                }
            }

            _processLock.Release();
        }
    }

    private async Task ProcessLockedCycleAsync(
        LaneDirection direction,
        string rfid,
        decimal lockedWeight,
        CancellationToken cancellationToken)
    {
        var lane = LaneText(direction);
        var vehicle = await _vehicleRepository.GetByRfidAsync(rfid, cancellationToken);
        var vehicleNumber = string.IsNullOrWhiteSpace(vehicle?.VehicleNumber)
            ? "NOT REGISTERED"
            : vehicle.VehicleNumber.Trim();

        await _hardware.SendControlCommandAsync(
            string.IsNullOrWhiteSpace(vehicleNumber)
                ? $"{lane} RED"
                : $"{lane} RED {vehicleNumber}",
            cancellationToken);

        PublishLane(
            direction,
            LaneState.Processing,
            rfid,
            vehicleNumber,
            $"Capturing 4 cameras at {lockedWeight:0.###} kg");

        var waitSeconds = Math.Max(0, _options.Processing.ProcessWaitSeconds);
        if (waitSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
        }

        var trip = new TripRecord
        {
            SiteId = _options.Device.SiteId,
            DeviceId = _options.Device.DeviceId,
            Direction = direction,
            RfidNumber = rfid,
            VehicleNumber = vehicleNumber,
            VehicleCategory = vehicle?.VehicleCategory ?? string.Empty,
            AccessType = vehicle?.AccessType ?? RfidAccessType.Free,
            PreviousBalance = 0m,
            DebitAmount = 0m,
            NewBalance = 0m,
            BalanceType = "NONE",
            AuthorizationSource = "WEIGHBRIDGE",
            ServerDecision = "CAPTURED",
            WeightKg = lockedWeight,
            TargetWeightKg = _options.Weighbridge.TargetWeightKg,
            ProcessedAt = DateTimeOffset.Now,
            Status = TripStatus.PendingSync,
            Notes = "iAWS v0.1 single-weighbridge transaction."
        };

        await _logger.StatusAsync(
            $"{lane} camera capture started for transaction {trip.Id}; capturing Camera 1-4.",
            cancellationToken);
        var captures = await _camera.CaptureAllAsync(direction, trip.Id, cancellationToken);
        ApplyCaptures(trip, captures);

        // Keep legacy image fields pointing at Camera 1 for compatibility with any
        // older server tooling while the v0.1 payload publishes all four cameras.
        trip.ImagePath = trip.Camera1ImagePath;
        trip.ImageCaptureStatus = trip.Camera1ImageCaptureStatus;

        await _tripRepository.AddAsync(trip, cancellationToken);
        _eventHub.PublishCountersChanged();

        PublishLane(
            direction,
            LaneState.Approved,
            rfid,
            vehicleNumber,
            $"{lane} WEIGHT CAPTURED - {lockedWeight:0.###} KG");
        await _logger.StatusAsync(
            $"{lane} iAWS transaction {trip.Id} saved locally. RFID={rfid}; " +
            $"weight={lockedWeight:0.###} kg; target={trip.TargetWeightKg:0.###} kg; cameras=4. " +
            "Data/images will be synchronized to the server asynchronously.",
            cancellationToken);

        await RunApprovedBarrierSequenceAsync(direction, cancellationToken);
        PublishIdleStates("Waiting for next vehicle / target weight");
    }

    private async Task RunApprovedBarrierSequenceAsync(
        LaneDirection direction,
        CancellationToken cancellationToken)
    {
        var lane = LaneText(direction);
        Task releaseTask;
        lock (_cycleSync)
        {
            releaseTask = _weightResetSignal?.Task ?? Task.CompletedTask;
        }

        try
        {
            await _hardware.SendControlCommandAsync($"{lane} ALL OFF", cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} ORG", cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} Buzzer", cancellationToken);
            await _hardware.SendControlCommandAsync($"OPEN {lane} BB", cancellationToken);

            await _logger.StatusAsync(
                $"{lane} barrier OPEN. Waiting for single-weighbridge weight to fall to " +
                $"{_options.Weighbridge.ResetWeightKg:0.###} kg or below.",
                cancellationToken);

            await releaseTask.WaitAsync(cancellationToken);

            var delaySeconds = Math.Max(0, _options.Processing.BarrierAndGreenDelaySeconds);
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }

            await _hardware.SendControlCommandAsync($"CLOSE {lane} BB", cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} Buzzer OFF", cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} ALL OFF", cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} GRN", cancellationToken);

            await _logger.StatusAsync(
                $"{lane} weighing cycle completed. Barrier CLOSED; direction lock released after weight reset.",
                cancellationToken);
        }
        finally
        {
            await _hardware.SendControlCommandAsync($"{lane} Buzzer OFF", CancellationToken.None);
        }
    }

    private static void ApplyCaptures(
        TripRecord trip,
        IReadOnlyList<CameraCaptureResult> captures)
    {
        foreach (var capture in captures)
        {
            switch (capture.CameraNumber)
            {
                case 1:
                    trip.Camera1ImagePath = capture.Path;
                    trip.Camera1ImageCaptureStatus = capture.Status;
                    break;
                case 2:
                    trip.Camera2ImagePath = capture.Path;
                    trip.Camera2ImageCaptureStatus = capture.Status;
                    break;
                case 3:
                    trip.Camera3ImagePath = capture.Path;
                    trip.Camera3ImageCaptureStatus = capture.Status;
                    break;
                case 4:
                    trip.Camera4ImagePath = capture.Path;
                    trip.Camera4ImageCaptureStatus = capture.Status;
                    break;
            }
        }
    }

    private bool IsDuplicate(LaneDirection direction, string rfid)
    {
        var seconds = Math.Max(0, _options.Processing.DuplicateReadSeconds);
        if (seconds == 0)
        {
            return false;
        }

        var key = $"{direction}:{rfid}";
        return _lastReads.TryGetValue(key, out var lastRead) &&
               DateTimeOffset.Now - lastRead <= TimeSpan.FromSeconds(seconds);
    }

    private void ResetCycleStateNoLock()
    {
        _isArmed = false;
        _isProcessing = false;
        _directionLock = null;
        _lockedRfid = string.Empty;
        _weightResetSignal = null;
    }

    private async Task SafeReturnLaneToIdleAsync(LaneDirection direction)
    {
        try
        {
            await _hardware.SetLaneIdleStateAsync(direction, CancellationToken.None);
        }
        catch
        {
            // HardwareGateway logs transport failures; do not block state cleanup.
        }
    }

    private void PublishWaitingState(LaneDirection direction, string message) =>
        PublishLane(direction, LaneState.VehicleDetected, string.Empty, string.Empty, message);

    private void PublishIdleStates(string message)
    {
        PublishLane(LaneDirection.In, LaneState.Idle, string.Empty, string.Empty, message);
        PublishLane(LaneDirection.Out, LaneState.Idle, string.Empty, string.Empty, message);
    }

    private void PublishLane(
        LaneDirection direction,
        LaneState state,
        string rfid,
        string vehicleNumber,
        string message)
    {
        _eventHub.PublishLaneState(new LaneDisplayState
        {
            Direction = direction,
            State = state,
            RfidNumber = rfid,
            VehicleNumber = vehicleNumber,
            Message = message,
            SensorDetectedAt = null
        });
    }

    private static string LaneText(LaneDirection direction) =>
        direction == LaneDirection.In ? "IN" : "OUT";
}
