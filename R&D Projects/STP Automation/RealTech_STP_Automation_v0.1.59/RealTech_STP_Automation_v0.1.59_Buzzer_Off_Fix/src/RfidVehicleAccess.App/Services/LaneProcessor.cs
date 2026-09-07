using System.Collections.Concurrent;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class LaneProcessor
{
    private enum PendingRfidRegistration
    {
        Stored,
        Duplicate,
        Busy,
        SensorCycleAlreadyMatched
    }

    private sealed class LaneSensorTracker
    {
        private readonly object _sync = new();
        private bool _isHigh;
        private DateTimeOffset? _highDetectedAt;
        private string? _pendingRfid;
        private DateTimeOffset? _pendingRfidAt;
        private bool _isProcessing;
        private bool _sensorCycleAlreadyMatched;
        private long _releaseSequence;
        private long _processStartedReleaseSequence;
        private TaskCompletionSource<bool>? _approvedReleaseSignal;
        private bool _approvedBarrierOpened;
        private bool _approvedReleaseObserved;

        public DateTimeOffset? HighDetectedAt
        {
            get
            {
                lock (_sync)
                {
                    return _highDetectedAt;
                }
            }
        }

        public bool IsHigh
        {
            get
            {
                lock (_sync)
                {
                    return _isHigh;
                }
            }
        }

        public bool RegisterDetected()
        {
            lock (_sync)
            {
                if (_isHigh)
                {
                    return false;
                }

                _isHigh = true;
                _highDetectedAt = DateTimeOffset.Now;
                _sensorCycleAlreadyMatched = false;
                return true;
            }
        }

        public bool RegisterReleaseCommand()
        {
            TaskCompletionSource<bool>? completedSignal = null;

            lock (_sync)
            {
                // A Realeased/Released line is a lane-completion event, not merely a
                // sampled LOW state. Accept it whenever this lane has a detected or
                // processing cycle. This also lets a repeated release line unblock an
                // approved barrier cycle if the local sensor state was already LOW.
                var hasActiveCycle =
                    _isHigh ||
                    _isProcessing ||
                    _approvedReleaseSignal is not null;

                if (!hasActiveCycle)
                {
                    return false;
                }

                if (_isHigh)
                {
                    _isHigh = false;
                    _highDetectedAt = null;
                    _pendingRfid = null;
                    _pendingRfidAt = null;
                    _sensorCycleAlreadyMatched = false;
                    _releaseSequence++;
                }
                else if (_isProcessing &&
                         _releaseSequence <= _processStartedReleaseSequence)
                {
                    // Defensive recovery: processing proves that this lane previously
                    // had a matched HIGH cycle. Record the release even if an earlier
                    // state update already made the local state LOW.
                    _releaseSequence = _processStartedReleaseSequence + 1;
                }

                if (_approvedReleaseSignal is not null)
                {
                    _approvedReleaseObserved = true;
                    if (_approvedBarrierOpened)
                    {
                        completedSignal = _approvedReleaseSignal;
                    }
                }
            }

            completedSignal?.TrySetResult(true);
            return true;
        }

        public PendingRfidRegistration RegisterPendingRfid(
            string rfid,
            TimeSpan maximumAge)
        {
            lock (_sync)
            {
                RemoveExpiredPendingRfid(DateTimeOffset.Now, maximumAge);

                if (_isProcessing)
                {
                    return PendingRfidRegistration.Busy;
                }

                if (_isHigh && _sensorCycleAlreadyMatched)
                {
                    return PendingRfidRegistration.SensorCycleAlreadyMatched;
                }

                if (string.Equals(_pendingRfid, rfid, StringComparison.OrdinalIgnoreCase))
                {
                    return PendingRfidRegistration.Duplicate;
                }

                _pendingRfid = rfid;
                _pendingRfidAt = DateTimeOffset.Now;
                return PendingRfidRegistration.Stored;
            }
        }

        public bool DiscardPendingRfid(string rfid)
        {
            lock (_sync)
            {
                if (!string.Equals(_pendingRfid, rfid, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                _pendingRfid = null;
                _pendingRfidAt = null;
                return true;
            }
        }

        public bool TryBeginMatchedProcess(
            TimeSpan maximumPendingRfidAge,
            out string rfid)
        {
            lock (_sync)
            {
                RemoveExpiredPendingRfid(DateTimeOffset.Now, maximumPendingRfidAge);

                if (_isProcessing ||
                    !_isHigh ||
                    _sensorCycleAlreadyMatched ||
                    string.IsNullOrWhiteSpace(_pendingRfid))
                {
                    rfid = string.Empty;
                    return false;
                }

                rfid = _pendingRfid!;
                _pendingRfid = null;
                _pendingRfidAt = null;
                _isProcessing = true;
                _sensorCycleAlreadyMatched = true;
                _processStartedReleaseSequence = _releaseSequence;
                return true;
            }
        }

        public void EndMatchedProcess()
        {
            lock (_sync)
            {
                _isProcessing = false;
            }
        }

        public void ArmApprovedBarrierCycle()
        {
            lock (_sync)
            {
                _approvedReleaseSignal = CreateSignal();
                _approvedBarrierOpened = false;

                // Remember a release that arrived after this sensor/RFID cycle was
                // matched, even when another HIGH event followed before OPEN.
                _approvedReleaseObserved =
                    _releaseSequence > _processStartedReleaseSequence || !_isHigh;
            }
        }

        public void MarkApprovedBarrierOpened()
        {
            TaskCompletionSource<bool>? completedSignal = null;

            lock (_sync)
            {
                if (_approvedReleaseSignal is null)
                {
                    throw new InvalidOperationException(
                        "The approved barrier cycle was not armed.");
                }

                _approvedBarrierOpened = true;
                if (_approvedReleaseObserved)
                {
                    completedSignal = _approvedReleaseSignal;
                }
            }

            completedSignal?.TrySetResult(true);
        }

        public Task WaitForApprovedReleaseAsync(CancellationToken cancellationToken)
        {
            Task signalTask;

            lock (_sync)
            {
                signalTask = _approvedReleaseSignal?.Task
                    ?? throw new InvalidOperationException(
                        "The approved barrier cycle was not armed.");
            }

            return signalTask.WaitAsync(cancellationToken);
        }

        public void EndApprovedBarrierCycle()
        {
            lock (_sync)
            {
                _approvedReleaseSignal = null;
                _approvedBarrierOpened = false;
                _approvedReleaseObserved = false;
            }
        }

        private void RemoveExpiredPendingRfid(
            DateTimeOffset now,
            TimeSpan maximumAge)
        {
            if (!_pendingRfidAt.HasValue || now - _pendingRfidAt.Value <= maximumAge)
            {
                return;
            }

            _pendingRfid = null;
            _pendingRfidAt = null;
        }

        private static TaskCompletionSource<bool> CreateSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly AppOptions _options;
    private readonly RfidValidator _rfidValidator;
    private readonly PaymentPriceResolver _paymentPriceResolver;
    private readonly VehicleRepository _vehicleRepository;
    private readonly TripRepository _tripRepository;
    private readonly ContractorRepository _contractorRepository;
    private readonly OnlineTripAuthorizationService _onlineAuthorization;
    private readonly CameraSnapshotService _camera;
    private readonly HardwareGateway _hardware;
    private readonly IExceptionalApprovalService _exceptionalApprovalService;
    private readonly AppLogger _logger;
    private readonly SystemEventHub _eventHub;
    private readonly IReadOnlyDictionary<LaneDirection, LaneSensorTracker> _sensorTrackers =
        new Dictionary<LaneDirection, LaneSensorTracker>
        {
            [LaneDirection.In] = new(),
            [LaneDirection.Out] = new()
        };
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastReads = new();
    private readonly ConcurrentDictionary<string, LaneDirection> _activeRfidProcesses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<LaneDirection, CancellationTokenSource> _resetTokens = new();
    private readonly IReadOnlyDictionary<LaneDirection, SemaphoreSlim> _laneLocks =
        new Dictionary<LaneDirection, SemaphoreSlim>
        {
            [LaneDirection.In] = new(1, 1),
            [LaneDirection.Out] = new(1, 1)
        };

    public LaneProcessor(
        AppOptions options,
        RfidValidator rfidValidator,
        PaymentPriceResolver paymentPriceResolver,
        VehicleRepository vehicleRepository,
        TripRepository tripRepository,
        ContractorRepository contractorRepository,
        OnlineTripAuthorizationService onlineAuthorization,
        CameraSnapshotService camera,
        HardwareGateway hardware,
        IExceptionalApprovalService exceptionalApprovalService,
        AppLogger logger,
        SystemEventHub eventHub)
    {
        _options = options;
        _rfidValidator = rfidValidator;
        _paymentPriceResolver = paymentPriceResolver;
        _vehicleRepository = vehicleRepository;
        _tripRepository = tripRepository;
        _contractorRepository = contractorRepository;
        _onlineAuthorization = onlineAuthorization;
        _camera = camera;
        _hardware = hardware;
        _exceptionalApprovalService = exceptionalApprovalService;
        _logger = logger;
        _eventHub = eventHub;
    }

    public async Task RegisterSensorStateAsync(
        LaneDirection direction,
        bool isHigh,
        CancellationToken cancellationToken = default)
    {
        var tracker = _sensorTrackers[direction];
        var lane = LaneText(direction);

        if (isHigh)
        {
            if (!tracker.RegisterDetected())
            {
                return;
            }

            CancelScheduledReset(direction);
            await _logger.StatusAsync(
                $"{lane} sensor changed to HIGH. Waiting for RFID if it has not already been read.",
                cancellationToken);
            PublishLane(direction, LaneState.VehicleDetected, string.Empty, string.Empty,
                "Vehicle sensor detected - waiting to match RFID");

            await TryStartMatchedProcessAsync(direction, cancellationToken);
            return;
        }

        if (!tracker.RegisterReleaseCommand())
        {
            await _logger.StatusAsync(
                $"{lane} Realeased command ignored because there is no active lane cycle.",
                cancellationToken);
            return;
        }

        // The cross-lane claim only protects the period in which this physical lane
        // still owns the vehicle. Release it as soon as the lane sensor reports
        // Realeased/Released so the same vehicle can legitimately be detected by the
        // opposite lane even while this lane is finishing its post-release barrier delay.
        var releasedRfidClaim = ReleaseRfidClaimForDirection(direction);

        await _logger.StatusAsync(
            $"{lane} Realeased command received and registered for the active lane cycle.",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(releasedRfidClaim))
        {
            await _logger.StatusAsync(
                $"{lane} cross-lane RFID claim released on physical lane release: " +
                $"{releasedRfidClaim}. The same RFID may now be processed by the opposite lane.",
                cancellationToken);
        }
    }

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
                $"{lane} RFID ignored because the value is empty or has an invalid prefix: {rfid}",
                cancellationToken);
            return;
        }

        if (_activeRfidProcesses.TryGetValue(rfid, out var activeDirection))
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored because the same RFID is already processing in " +
                $"{LaneText(activeDirection)}: {rfid}",
                cancellationToken);
            return;
        }

        if (IsDuplicate(direction, rfid))
        {
            await _logger.StatusAsync(
                $"{lane} duplicate RFID ignored: {rfid}",
                cancellationToken);
            return;
        }

        var tracker = _sensorTrackers[direction];
        var registration = tracker.RegisterPendingRfid(rfid, GetPairingWindow());

        // Close the race where the opposite lane claims this RFID while this lane
        // is between the initial active-process check and buffering the RFID.
        if (_activeRfidProcesses.TryGetValue(rfid, out activeDirection))
        {
            tracker.DiscardPendingRfid(rfid);
            await _logger.StatusAsync(
                $"{lane} RFID ignored because the same RFID started processing in " +
                $"{LaneText(activeDirection)} while it was being buffered: {rfid}",
                cancellationToken);
            return;
        }

        if (registration == PendingRfidRegistration.Busy)
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored because the lane is already processing: {rfid}",
                cancellationToken);
            return;
        }

        if (registration == PendingRfidRegistration.SensorCycleAlreadyMatched)
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored because the current sensor HIGH cycle is already matched: {rfid}",
                cancellationToken);
            return;
        }

        if (registration == PendingRfidRegistration.Stored)
        {
            await _logger.StatusAsync(
                $"{lane} RFID detected and buffered: {rfid}. " +
                "Waiting for the vehicle sensor if it is not already HIGH.",
                cancellationToken);

            if (!tracker.IsHigh)
            {
                CancelScheduledReset(direction);
                PublishLane(direction, LaneState.VehicleDetected, rfid, string.Empty,
                    "RFID detected - waiting for vehicle sensor");
            }
        }

        await TryStartMatchedProcessAsync(direction, cancellationToken);
    }

    private async Task TryStartMatchedProcessAsync(
        LaneDirection direction,
        CancellationToken cancellationToken)
    {
        var laneLock = _laneLocks[direction];
        if (!await laneLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        var tracker = _sensorTrackers[direction];
        var lane = LaneText(direction);
        var processStarted = false;
        var rfidClaimed = false;
        var statusTimerStarted = false;
        var matchedRfid = string.Empty;

        try
        {
            if (!tracker.TryBeginMatchedProcess(GetPairingWindow(), out matchedRfid))
            {
                return;
            }

            processStarted = true;

            // IN and OUT have independent lane locks, so the same tag can otherwise
            // start two processes at nearly the same instant. Claim the RFID globally
            // before any payment, trip creation, barrier or server work begins.
            if (!_activeRfidProcesses.TryAdd(matchedRfid, direction))
            {
                var competingDirection = _activeRfidProcesses.TryGetValue(
                    matchedRfid,
                    out var currentDirection)
                    ? LaneText(currentDirection)
                    : "the other lane";

                await _logger.StatusAsync(
                    $"{lane} simultaneous cross-lane RFID ignored: {matchedRfid}. " +
                    $"The RFID is already being processed in {competingDirection}.",
                    cancellationToken);
                PublishLane(
                    direction,
                    LaneState.VehicleDetected,
                    matchedRfid,
                    string.Empty,
                    $"Same RFID already processing in {competingDirection} - ignored");
                ScheduleLaneReset(direction);
                return;
            }

            rfidClaimed = true;
            DiscardOppositeLanePendingRfid(direction, matchedRfid);

            // A Realeased/Released command can race with the atomic RFID claim.
            // If the physical lane is already LOW, do not keep a stale cross-lane
            // ownership claim until the remaining transaction/barrier work completes.
            if (!tracker.IsHigh && TryReleaseRfidClaim(matchedRfid, direction))
            {
                rfidClaimed = false;
                await _logger.StatusAsync(
                    $"{lane} cross-lane RFID claim released immediately because the " +
                    $"sensor was already released: {matchedRfid}.",
                    cancellationToken);
            }

            CancelScheduledReset(direction);
            _logger.StartStatusProcessTimer(lane, matchedRfid);
            statusTimerStarted = true;

            await _logger.StatusAsync(
                $"{lane} process timer started. Sensor and RFID are both detected for {matchedRfid}.",
                cancellationToken);
            PublishLane(direction, LaneState.VehicleDetected, matchedRfid, string.Empty,
                "Sensor and RFID matched - starting process");

            await ProcessUnderLockAsync(direction, matchedRfid, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync(
                $"{LaneText(direction)} process error: {ex.Message}",
                cancellationToken);
            PublishLane(direction, LaneState.Error, matchedRfid, string.Empty, ex.Message);
            await _hardware.SetLaneIdleStateAsync(direction, cancellationToken);
            ScheduleLaneReset(direction);
        }
        finally
        {
            if (processStarted)
            {
                if (statusTimerStarted)
                {
                    var elapsed = _logger.GetStatusProcessElapsed(lane) ?? TimeSpan.Zero;
                    try
                    {
                        await _logger.StatusAsync(
                            $"{lane} process timer stopped. Total process time: " +
                            $"{FormatProcessElapsed(elapsed)}.",
                            CancellationToken.None);
                    }
                    catch
                    {
                        // A logging failure must never keep the lane locked.
                    }
                    finally
                    {
                        _logger.StopStatusProcessTimer(lane);
                    }
                }

                tracker.EndMatchedProcess();
            }

            if (rfidClaimed)
            {
                // Remove only this lane's claim. The opposite lane may already have
                // acquired the same RFID after this lane's physical Realeased event.
                // A plain TryRemove(key) here could accidentally remove that newer claim.
                TryReleaseRfidClaim(matchedRfid, direction);
            }

            laneLock.Release();
        }
    }

    private async Task ProcessUnderLockAsync(
        LaneDirection direction,
        string rfid,
        CancellationToken cancellationToken)
    {
        var lane = LaneText(direction);

        if (!_rfidValidator.IsAllowed(rfid))
        {
            await _logger.StatusAsync(
                $"{lane} RFID ignored because the value is empty or has an invalid prefix: {rfid}",
                cancellationToken);
            return;
        }

        _lastReads[$"{direction}:{rfid}"] = DateTimeOffset.Now;
        await _logger.StatusAsync($"{lane} RFID detected: {rfid}", cancellationToken);
        await _logger.StatusAsync(
            $"{lane} vehicle lookup started for RFID {rfid}.",
            cancellationToken);

        var vehicle = await _vehicleRepository.GetByRfidAsync(rfid, cancellationToken);
        if (vehicle is null)
        {
            // Send a display-friendly replacement vehicle number so the control
            // unit can show the rejected scan in the same two-stage RED flow.
            const string unregisteredVehicleNumber = "NOT Registered";
            await _hardware.SendControlCommandAsync(
                $"{lane} RED {unregisteredVehicleNumber}",
                cancellationToken);
            await RejectAsync(direction, rfid, "NOT REGISTERED",
                "RFID is not registered", cancellationToken);
            return;
        }

        var vehicleNumber = vehicle.VehicleNumber.Trim();
        var redCommand = string.IsNullOrWhiteSpace(vehicleNumber)
            ? $"{lane} RED"
            : $"{lane} RED {vehicleNumber}";
        await _hardware.SendControlCommandAsync(redCommand, cancellationToken);

        if (!vehicle.IsActive)
        {
            await RejectAsync(direction, rfid, vehicle.VehicleNumber,
                "RFID is inactive", cancellationToken);
            return;
        }

        PublishLane(direction, LaneState.Processing, rfid, vehicle.VehicleNumber,
            $"{vehicle.AccessType} RFID - processing");
        await _logger.StatusAsync(
            $"{lane} vehicle identified: {vehicle.VehicleNumber}, type: {vehicle.AccessType}.",
            cancellationToken);

        if (direction == LaneDirection.In)
        {
            await ProcessEntryAsync(vehicle, cancellationToken);
        }
        else
        {
            await ProcessExitAsync(vehicle, cancellationToken);
        }
    }

    private async Task ProcessEntryAsync(
        VehicleRecord vehicle,
        CancellationToken cancellationToken)
    {
        var balanceMode = _options.Processing.OfflineBalanceMode?.Trim() ?? "BOTH";
        var hasContractorAssignment = !string.IsNullOrWhiteSpace(vehicle.ContractorCode);
        var useContractorBalance = string.Equals(balanceMode, "CONTRACTOR", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(balanceMode, "BOTH", StringComparison.OrdinalIgnoreCase) && hasContractorAssignment);
        ContractorRecord? contractor = null;
        if (useContractorBalance)
        {
            contractor = await _contractorRepository.GetByCodeAsync(vehicle.ContractorCode, cancellationToken);
            if (contractor is null || !contractor.IsActive)
            {
                await RejectAsync(LaneDirection.In, vehicle.RfidNumber, vehicle.VehicleNumber,
                    "Access denied - contractor is missing or inactive in local cache", cancellationToken);
                return;
            }
        }

        var previousBalance = useContractorBalance ? contractor!.Balance : vehicle.Balance;
        var payment = _paymentPriceResolver.Resolve(vehicle.VehicleCategory);
        var cachedCategoryDebit = vehicle.CategoryDebitAmount ??
            await _contractorRepository.GetCategoryDebitAmountAsync(vehicle.VehicleCategory, cancellationToken);
        var debitAmount = vehicle.AccessType == RfidAccessType.Free
            ? 0m
            : cachedCategoryDebit ?? payment.Price;

        await _logger.StatusAsync(
            $"IN balance validation started for {vehicle.VehicleNumber}. " +
            $"Category: {vehicle.VehicleCategory}; balance mode: {(useContractorBalance ? "CONTRACTOR" : "RFID")}; " +
            $"current balance: {previousBalance:0.00}; debit: {debitAmount:0.00}.", cancellationToken);

        var onlineDecision = await _onlineAuthorization.AuthorizeAsync(
            LaneDirection.In, vehicle, previousBalance, debitAmount, cancellationToken);
        if (onlineDecision.Available && !onlineDecision.Approved)
        {
            await RejectAsync(LaneDirection.In, vehicle.RfidNumber, vehicle.VehicleNumber,
                string.IsNullOrWhiteSpace(onlineDecision.Reason) ? "Trip rejected by server" : onlineDecision.Reason, cancellationToken);
            return;
        }
        if (onlineDecision.Available && onlineDecision.DebitAmount.HasValue)
        {
            debitAmount = Math.Max(0m, onlineDecision.DebitAmount.Value);
        }

        // Offline fallback preserves the existing one-negative-trip rule: a non-negative
        // balance may approve one trip even when the debit makes it negative; once it is
        // already negative, the next paid trip is denied.
        if (!onlineDecision.Available && vehicle.AccessType == RfidAccessType.Paid && previousBalance < 0m)
        {
            await RejectAsync(LaneDirection.In, vehicle.RfidNumber, vehicle.VehicleNumber,
                $"Access denied - negative balance {previousBalance:0.00}", cancellationToken);
            return;
        }

        var activeEntry = await _tripRepository.GetActiveEntryAsync(
            vehicle.RfidNumber,
            cancellationToken);
        await _logger.StatusAsync(
            activeEntry is null
                ? $"IN trip-history check completed for {vehicle.VehicleNumber}: no active IN trip."
                : $"IN trip-history check completed for {vehicle.VehicleNumber}: " +
                  $"active IN trip {activeEntry.Id} requires missing OUT notification and exceptional closure.",
            cancellationToken);
        ExceptionalApprovalDetails? approval = null;

        if (activeEntry is not null)
        {
            if (_options.AutoApproval.InEnabled)
            {
                approval = await CreateAutoApprovalAsync(
                    MissingTripType.MissingOut,
                    LaneDirection.In,
                    vehicle,
                    cancellationToken);
            }
            else
            {
                if (!_options.ExceptionalApproval.InEnabled)
                {
                    await RejectAsync(
                        LaneDirection.In,
                        vehicle.RfidNumber,
                        vehicle.VehicleNumber,
                        "Missing OUT trip - IN auto and exceptional approval are disabled by Admin",
                        cancellationToken);
                    return;
                }

                approval = await RequestExceptionalApprovalAsync(
                    MissingTripType.MissingOut,
                    LaneDirection.In,
                    vehicle,
                    "A previous IN trip is still active, so its matching OUT trip is missing. " +
                    "Exceptional approval will close the previous active IN internally, approve the current IN trip, and notify the server that the OUT trip was missing. No synthetic OUT trip will be created.",
                    cancellationToken);

                if (approval is null)
                {
                    await RejectAsync(
                        LaneDirection.In,
                        vehicle.RfidNumber,
                        vehicle.VehicleNumber,
                        "Missing OUT trip - exceptional approval was cancelled",
                        cancellationToken);
                    return;
                }
            }
        }

        var newBalance = previousBalance - debitAmount;
        await _logger.StatusAsync(
            $"IN process wait timer started for {_options.Processing.ProcessWaitSeconds} seconds.",
            cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(_options.Processing.ProcessWaitSeconds), cancellationToken);
        await _logger.StatusAsync(
            "IN process wait timer completed.",
            cancellationToken);

        var processedAt = DateTimeOffset.Now;
        var trip = CreateTrip(
            LaneDirection.In,
            vehicle,
            null,
            previousBalance,
            debitAmount,
            newBalance,
            processedAt,
            approval is null
                ? null
                : BuildExceptionalNotes(
                    MissingTripType.MissingOut,
                    approval,
                    "Current IN trip approved. Missing OUT was reported to the server; no synthetic OUT trip was created."));
        trip.ContractorCode = vehicle.ContractorCode;
        trip.ContractorName = vehicle.ContractorName;
        trip.BalanceType = useContractorBalance ? "CONTRACTOR" : "RFID";
        trip.AuthorizationSource = onlineDecision.Available ? "ONLINE_SERVER" : "OFFLINE_LOCAL";
        trip.AuthorizationRequestId = onlineDecision.RequestId;
        trip.ServerDecision = onlineDecision.Available ? "APPROVED" : "UNAVAILABLE_FALLBACK";
        trip.ServerReason = onlineDecision.Reason;

        if (approval is not null)
        {
            ApplyExceptionalApproval(trip, MissingTripType.MissingOut, approval);
        }

        await _logger.StatusAsync(
            $"IN camera capture started for transaction {trip.Id}.",
            cancellationToken);
        var capture = await _camera.CaptureAsync(LaneDirection.In, trip.Id, cancellationToken);
        trip.ImagePath = capture.Path;
        trip.ImageCaptureStatus = capture.Status;
        await _logger.StatusAsync(
            $"IN camera capture completed for transaction {trip.Id}. " +
            $"Status: {capture.Status}; path: {capture.Path}.",
            cancellationToken);

        await _logger.StatusAsync(
            $"IN local database transaction started for {vehicle.VehicleNumber}.",
            cancellationToken);
        (bool Applied, decimal PreviousBalance, decimal NewBalance) balanceResult;
        if (useContractorBalance)
        {
            balanceResult = await _tripRepository.AddEntryWithContractorBalanceUpdateAsync(
                trip, vehicle.ContractorCode, debitAmount,
                activeEntry is not null && approval is not null ? activeEntry.Id : null,
                enforceNegativeGuard: !onlineDecision.Available, cancellationToken: cancellationToken);
        }
        else if (activeEntry is null || approval is null)
        {
            balanceResult = await _tripRepository.AddEntryWithBalanceUpdateAsync(
                trip, vehicle.Id, debitAmount,
                enforceNegativeGuard: !onlineDecision.Available, cancellationToken: cancellationToken);
        }
        else
        {
            balanceResult = await _tripRepository.AddEntryAfterMissingOutWithBalanceUpdateAsync(
                activeEntry.Id, trip, vehicle.Id, debitAmount,
                enforceNegativeGuard: !onlineDecision.Available, cancellationToken: cancellationToken);
        }

        if (!balanceResult.Applied)
        {
            await RejectAsync(
                LaneDirection.In,
                vehicle.RfidNumber,
                vehicle.VehicleNumber,
                $"Access denied - negative balance {balanceResult.PreviousBalance:0.00}",
                cancellationToken);
            return;
        }

        previousBalance = balanceResult.PreviousBalance;
        newBalance = balanceResult.NewBalance;
        await _logger.StatusAsync(
            $"IN local database transaction committed. Previous balance: " +
            $"{previousBalance:0.00}; new balance: {newBalance:0.00}.",
            cancellationToken);

        var standardMessage = vehicle.AccessType == RfidAccessType.Free
            ? "FREE ACCESS APPROVED"
            : newBalance < 0m
                ? $"ONE-TIME ACCESS APPROVED - BALANCE {newBalance:0.00}"
                : $"ACCESS APPROVED - BALANCE {newBalance:0.00}";
        var message = approval is null
            ? standardMessage
            : approval.IsAutomatic
                ? $"AUTO IN APPROVED - MISSING OUT REPORTED - BALANCE {newBalance:0.00}"
                : $"EXCEPTIONAL IN APPROVED - MISSING OUT REPORTED - BALANCE {newBalance:0.00}";

        PublishLane(LaneDirection.In, LaneState.Approved, vehicle.RfidNumber,
            vehicle.VehicleNumber, message);
        await _logger.StatusAsync(
            approval is null
                ? $"IN trip saved locally for {vehicle.VehicleNumber}. {message}"
                : $"IN missing OUT reported and current physical trip saved for " +
                  $"{vehicle.VehicleNumber}. {GetApprovalLogText(approval)}.",
            cancellationToken);
        _eventHub.PublishCountersChanged();

        await RunApprovedBarrierSequenceAsync(LaneDirection.In, cancellationToken);
        ScheduleLaneReset(LaneDirection.In);
    }

    private async Task ProcessExitAsync(
        VehicleRecord vehicle,
        CancellationToken cancellationToken)
    {
        var balanceMode = _options.Processing.OfflineBalanceMode?.Trim() ?? "BOTH";
        var hasContractorAssignment = !string.IsNullOrWhiteSpace(vehicle.ContractorCode);
        var useContractorBalance = string.Equals(balanceMode, "CONTRACTOR", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(balanceMode, "BOTH", StringComparison.OrdinalIgnoreCase) && hasContractorAssignment);
        var localBalance = vehicle.Balance;
        if (useContractorBalance)
        {
            var contractor = await _contractorRepository.GetByCodeAsync(vehicle.ContractorCode, cancellationToken);
            if (contractor is null || !contractor.IsActive)
            {
                await RejectAsync(LaneDirection.Out, vehicle.RfidNumber, vehicle.VehicleNumber,
                    "Access denied - contractor is missing or inactive in local cache", cancellationToken);
                return;
            }
            localBalance = contractor.Balance;
        }

        var onlineDecision = await _onlineAuthorization.AuthorizeAsync(
            LaneDirection.Out, vehicle, localBalance, 0m, cancellationToken);
        if (onlineDecision.Available && !onlineDecision.Approved)
        {
            await RejectAsync(LaneDirection.Out, vehicle.RfidNumber, vehicle.VehicleNumber,
                string.IsNullOrWhiteSpace(onlineDecision.Reason) ? "Trip rejected by server" : onlineDecision.Reason, cancellationToken);
            return;
        }
        await _logger.StatusAsync(
            $"OUT trip-history check started for {vehicle.VehicleNumber}.",
            cancellationToken);
        var activeEntry = await _tripRepository.GetActiveEntryAsync(
            vehicle.RfidNumber,
            cancellationToken);
        await _logger.StatusAsync(
            activeEntry is null
                ? $"OUT trip-history check completed for {vehicle.VehicleNumber}: no active IN trip."
                : $"OUT trip-history check completed for {vehicle.VehicleNumber}: " +
                  $"matched active IN trip {activeEntry.Id}.",
            cancellationToken);
        ExceptionalApprovalDetails? approval = null;

        if (activeEntry is null)
        {
            if (_options.AutoApproval.OutEnabled)
            {
                approval = await CreateAutoApprovalAsync(
                    MissingTripType.MissingIn,
                    LaneDirection.Out,
                    vehicle,
                    cancellationToken);
            }
            else
            {
                if (!_options.ExceptionalApproval.OutEnabled)
                {
                    await RejectAsync(
                        LaneDirection.Out,
                        vehicle.RfidNumber,
                        vehicle.VehicleNumber,
                        "Missing IN trip - OUT auto and exceptional approval are disabled by Admin",
                        cancellationToken);
                    return;
                }

                approval = await RequestExceptionalApprovalAsync(
                    MissingTripType.MissingIn,
                    LaneDirection.Out,
                    vehicle,
                    "No active IN trip exists for this vehicle. Exceptional approval will approve " +
                    "the current OUT trip and notify the server that the IN trip was missing. " +
                    "No synthetic IN trip will be created.",
                    cancellationToken);

                if (approval is null)
                {
                    await RejectAsync(
                        LaneDirection.Out,
                        vehicle.RfidNumber,
                        vehicle.VehicleNumber,
                        "Missing IN trip - exceptional approval was cancelled",
                        cancellationToken);
                    return;
                }
            }
        }

        await _logger.StatusAsync(
            $"OUT process wait timer started for {_options.Processing.ProcessWaitSeconds} seconds.",
            cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(_options.Processing.ProcessWaitSeconds), cancellationToken);
        await _logger.StatusAsync(
            "OUT process wait timer completed.",
            cancellationToken);

        var processedAt = DateTimeOffset.Now;
        var entryTripId = activeEntry?.Id;

        var trip = CreateTrip(
            LaneDirection.Out,
            vehicle,
            entryTripId,
            vehicle.Balance,
            0m,
            vehicle.Balance,
            processedAt,
            approval is null
                ? null
                : BuildExceptionalNotes(
                    MissingTripType.MissingIn,
                    approval,
                    "Current OUT trip approved. Missing IN was reported to the server; no synthetic IN trip was created."));
        trip.ContractorCode = vehicle.ContractorCode;
        trip.ContractorName = vehicle.ContractorName;
        trip.BalanceType = useContractorBalance ? "CONTRACTOR" : "RFID";
        trip.AuthorizationSource = onlineDecision.Available ? "ONLINE_SERVER" : "OFFLINE_LOCAL";
        trip.AuthorizationRequestId = onlineDecision.RequestId;
        trip.ServerDecision = onlineDecision.Available ? "APPROVED" : "UNAVAILABLE_FALLBACK";
        trip.ServerReason = onlineDecision.Reason;

        if (approval is not null)
        {
            ApplyExceptionalApproval(trip, MissingTripType.MissingIn, approval);
        }

        await _logger.StatusAsync(
            $"OUT camera capture started for transaction {trip.Id}.",
            cancellationToken);
        var capture = await _camera.CaptureAsync(LaneDirection.Out, trip.Id, cancellationToken);
        trip.ImagePath = capture.Path;
        trip.ImageCaptureStatus = capture.Status;
        await _logger.StatusAsync(
            $"OUT camera capture completed for transaction {trip.Id}. " +
            $"Status: {capture.Status}; path: {capture.Path}.",
            cancellationToken);

        await _logger.StatusAsync(
            $"OUT local database transaction started for {vehicle.VehicleNumber}.",
            cancellationToken);
        await _tripRepository.AddAsync(trip, cancellationToken);

        await _logger.StatusAsync(
            $"OUT local database transaction committed for {vehicle.VehicleNumber}.",
            cancellationToken);

        var message = approval is null
            ? "EXIT APPROVED"
            : approval.IsAutomatic
                ? "AUTO OUT APPROVED - MISSING IN REPORTED"
                : "EXCEPTIONAL OUT APPROVED - MISSING IN REPORTED";
        PublishLane(LaneDirection.Out, LaneState.Approved, vehicle.RfidNumber,
            vehicle.VehicleNumber, message);
        await _logger.StatusAsync(
            approval is null
                ? $"OUT trip saved locally for {vehicle.VehicleNumber}. No debit applied."
                : $"OUT missing IN reported and current physical trip saved for " +
                  $"{vehicle.VehicleNumber}. {GetApprovalLogText(approval)}.",
            cancellationToken);
        _eventHub.PublishCountersChanged();

        await RunApprovedBarrierSequenceAsync(LaneDirection.Out, cancellationToken);
        ScheduleLaneReset(LaneDirection.Out);
    }

    private async Task<ExceptionalApprovalDetails> CreateAutoApprovalAsync(
        MissingTripType missingTripType,
        LaneDirection currentLane,
        VehicleRecord vehicle,
        CancellationToken cancellationToken)
    {
        var missingTripLabel = GetMissingTripLabel(missingTripType);
        var lane = LaneText(currentLane);
        var approval = new ExceptionalApprovalDetails
        {
            Name = "SYSTEM",
            Role = $"{lane} AUTO APPROVAL",
            MobileNumber = "N/A",
            IsAutomatic = true,
            ApprovedAt = DateTimeOffset.Now
        };

        PublishLane(
            currentLane,
            LaneState.Processing,
            vehicle.RfidNumber,
            vehicle.VehicleNumber,
            $"{missingTripLabel.ToUpperInvariant()} - AUTO APPROVED");
        await _logger.StatusAsync(
            $"{lane} detected {missingTripLabel} for {vehicle.VehicleNumber}. " +
            "Admin auto approval is enabled; skipping the approval popup. The missing trip " +
            "will be reported inside the current physical trip MQTT message only.",
            cancellationToken);

        return approval;
    }

    private async Task<ExceptionalApprovalDetails?> RequestExceptionalApprovalAsync(
        MissingTripType missingTripType,
        LaneDirection currentLane,
        VehicleRecord vehicle,
        string explanation,
        CancellationToken cancellationToken)
    {
        var missingTripLabel = GetMissingTripLabel(missingTripType);
        PublishLane(
            currentLane,
            LaneState.Processing,
            vehicle.RfidNumber,
            vehicle.VehicleNumber,
            $"{missingTripLabel.ToUpperInvariant()} - EXCEPTIONAL APPROVAL REQUIRED");
        await _logger.StatusAsync(
            $"{LaneText(currentLane)} detected {missingTripLabel} for " +
            $"{vehicle.VehicleNumber}. Opening exceptional approval window.",
            cancellationToken);

        var approval = await _exceptionalApprovalService.RequestApprovalAsync(
            new ExceptionalApprovalRequest
            {
                MissingTripType = missingTripType,
                CurrentLane = currentLane,
                RfidNumber = vehicle.RfidNumber,
                VehicleNumber = vehicle.VehicleNumber,
                AccessType = vehicle.AccessType.ToString(),
                Explanation = explanation
            },
            cancellationToken);

        if (approval is null)
        {
            await _logger.StatusAsync(
                $"{LaneText(currentLane)} exceptional approval cancelled for " +
                $"{vehicle.VehicleNumber} ({missingTripLabel}).",
                cancellationToken);
            return null;
        }

        await _logger.StatusAsync(
            $"{LaneText(currentLane)} exceptional approval submitted by " +
            $"{approval.Name} ({approval.Role}) for {vehicle.VehicleNumber} " +
            $"({missingTripLabel}).",
            cancellationToken);
        return approval;
    }

    private static void ApplyExceptionalApproval(
        TripRecord trip,
        MissingTripType missingTripType,
        ExceptionalApprovalDetails approval)
    {
        trip.ExceptionalApprovalMode = approval.IsAutomatic ? "Auto" : "Manual";
        trip.ExceptionalApprovalReason = GetMissingTripLabel(missingTripType);
        trip.ExceptionalApproverName = approval.Name;
        trip.ExceptionalApproverRole = approval.Role;
        trip.ExceptionalApproverMobile = approval.MobileNumber;
        trip.ExceptionalApprovedAt = approval.ApprovedAt;
    }

    private static string BuildExceptionalNotes(
        MissingTripType missingTripType,
        ExceptionalApprovalDetails approval,
        string recordDescription)
    {
        var approvalMode = approval.IsAutomatic ? "Auto" : "Manual";
        return "Processed locally; server upload is asynchronous. " +
               $"Exceptional approval: Mode={approvalMode}; " +
               $"Reason={GetMissingTripLabel(missingTripType)}; " +
               $"ApproverName={approval.Name}; ApproverRole={approval.Role}; " +
               $"ApproverMobile={approval.MobileNumber}; " +
               $"ApprovedAt={approval.ApprovedAt:O}; Record={recordDescription}";
    }

    private static string GetApprovalLogText(ExceptionalApprovalDetails approval) =>
        approval.IsAutomatic
            ? $"Auto approved by {approval.Name} ({approval.Role})"
            : $"Approved by {approval.Name} ({approval.Role})";

    private static string GetMissingTripLabel(MissingTripType missingTripType) =>
        missingTripType == MissingTripType.MissingIn
            ? "Missing IN Trip"
            : "Missing OUT Trip";

    private async Task RejectAsync(
        LaneDirection direction,
        string rfid,
        string vehicleNumber,
        string reason,
        CancellationToken cancellationToken)
    {
        PublishLane(direction, LaneState.Rejected, rfid, vehicleNumber, reason);
        await _logger.StatusAsync(
            $"{LaneText(direction)} access rejected for RFID {rfid}: {reason}.",
            cancellationToken);
        await _hardware.SetLaneIdleStateAsync(direction, cancellationToken);
        ScheduleLaneReset(direction);
    }

    private async Task RunApprovedBarrierSequenceAsync(
        LaneDirection direction,
        CancellationToken cancellationToken)
    {
        var lane = LaneText(direction);
        var sensorTracker = _sensorTrackers[direction];

        // Arm before sending OPEN so a Realeased command arriving at the same instant
        // cannot be missed. MarkApprovedBarrierOpened prevents the timer from starting
        // until the OPEN command has actually been issued.
        sensorTracker.ArmApprovedBarrierCycle();

        try
        {
            await _logger.StatusAsync(
                $"{lane} barrier sequence step 1/4: sending ALL OFF.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} ALL OFF", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} barrier sequence step 2/4: turning ORG ON.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} ORG", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} barrier sequence step 3/4: activating buzzer.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} Buzzer", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} barrier sequence step 4/4: opening barrier.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"OPEN {lane} BB", cancellationToken);

            sensorTracker.MarkApprovedBarrierOpened();

            await _logger.StatusAsync(
                $"{lane} processing completed. ORG is ON and the barrier is OPEN. " +
                $"Waiting for the {lane} Realeased command.",
                cancellationToken);

            await sensorTracker.WaitForApprovedReleaseAsync(cancellationToken);

            var delaySeconds = Math.Max(0, _options.Processing.BarrierAndGreenDelaySeconds);
            await _logger.StatusAsync(
                $"{lane} Realeased command accepted. Completion timer started for " +
                $"{delaySeconds} second(s). ORG remains ON and the barrier remains OPEN.",
                cancellationToken);

            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }

            await _logger.StatusAsync(
                $"{lane} completion sequence step 1/4: closing barrier.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"CLOSE {lane} BB", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} completion sequence step 2/4: turning buzzer OFF.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} Buzzer OFF", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} completion sequence step 3/4: sending ALL OFF.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} ALL OFF", cancellationToken);
            await _logger.StatusAsync(
                $"{lane} completion sequence step 4/4: turning GRN ON.",
                cancellationToken);
            await _hardware.SendControlCommandAsync($"{lane} GRN", cancellationToken);

            await _logger.StatusAsync(
                $"{lane} completion timer expired. Barrier is CLOSED, BUZZER and ORG are OFF, " +
                "and GRN is ON.",
                cancellationToken);
        }
        finally
        {
            // Safety cleanup: if cancellation/exception interrupts the normal sequence after
            // BUZZER ON, make one uncancelled best-effort attempt to clear the latched buzzer.
            // SendControlCommandAsync handles/logs transport errors internally.
            await _hardware.SendControlCommandAsync($"{lane} Buzzer OFF", CancellationToken.None);
            sensorTracker.EndApprovedBarrierCycle();
        }
    }

    private TripRecord CreateTrip(
        LaneDirection direction,
        VehicleRecord vehicle,
        Guid? entryTripId,
        decimal previousBalance,
        decimal debitAmount,
        decimal newBalance,
        DateTimeOffset? processedAt = null,
        string? notes = null)
    {
        return new TripRecord
        {
            SiteId = _options.Device.SiteId,
            DeviceId = _options.Device.DeviceId,
            Direction = direction,
            RfidNumber = vehicle.RfidNumber,
            VehicleNumber = vehicle.VehicleNumber,
            VehicleCategory = vehicle.VehicleCategory,
            AccessType = vehicle.AccessType,
            EntryTripId = entryTripId,
            PreviousBalance = previousBalance,
            DebitAmount = debitAmount,
            NewBalance = newBalance,
            ProcessedAt = processedAt ?? DateTimeOffset.Now,
            Status = TripStatus.PendingSync,
            Notes = notes ?? "Processed locally; server upload is asynchronous."
        };
    }

    private TimeSpan GetPairingWindow() =>
        TimeSpan.FromSeconds(Math.Max(1, _options.Processing.SensorValiditySeconds));

    private bool IsDuplicate(LaneDirection direction, string rfid)
    {
        var key = $"{direction}:{rfid}";
        return _lastReads.TryGetValue(key, out var lastRead) &&
               DateTimeOffset.Now - lastRead <=
               TimeSpan.FromSeconds(_options.Processing.DuplicateReadSeconds);
    }

    private bool TryReleaseRfidClaim(
        string rfid,
        LaneDirection ownerDirection)
    {
        // ConcurrentDictionary's ICollection<KeyValuePair<...>>.Remove implementation
        // removes the entry only when both the RFID key and lane value still match.
        // This is important after physical release: the opposite lane may acquire the
        // same RFID before the first lane's async processing reaches its finally block.
        var claims = (ICollection<KeyValuePair<string, LaneDirection>>)_activeRfidProcesses;
        return claims.Remove(new KeyValuePair<string, LaneDirection>(rfid, ownerDirection));
    }

    private string? ReleaseRfidClaimForDirection(LaneDirection ownerDirection)
    {
        foreach (var claim in _activeRfidProcesses)
        {
            if (claim.Value != ownerDirection)
            {
                continue;
            }

            if (TryReleaseRfidClaim(claim.Key, ownerDirection))
            {
                return claim.Key;
            }
        }

        return null;
    }

    private void DiscardOppositeLanePendingRfid(
        LaneDirection processingDirection,
        string rfid)
    {
        var oppositeDirection = processingDirection == LaneDirection.In
            ? LaneDirection.Out
            : LaneDirection.In;

        _sensorTrackers[oppositeDirection].DiscardPendingRfid(rfid);
    }

    private void ScheduleLaneReset(LaneDirection direction)
    {
        CancelScheduledReset(direction);

        var resetTokenSource = new CancellationTokenSource();
        _resetTokens[direction] = resetTokenSource;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.Processing.VehicleDisplayResetSeconds)),
                    resetTokenSource.Token);
                PublishLane(direction, LaneState.Idle, string.Empty, string.Empty,
                    "Waiting for vehicle");
            }
            catch (OperationCanceledException)
            {
                // A new vehicle arrived before the display-reset timer expired.
            }
            finally
            {
                if (_resetTokens.TryGetValue(direction, out var current) &&
                    ReferenceEquals(current, resetTokenSource))
                {
                    _resetTokens.TryRemove(direction, out _);
                }
                resetTokenSource.Dispose();
            }
        });
    }

    private void CancelScheduledReset(LaneDirection direction)
    {
        if (_resetTokens.TryRemove(direction, out var existing))
        {
            existing.Cancel();
        }
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
            SensorDetectedAt = _sensorTrackers[direction].HighDetectedAt
        });
    }

    private static string FormatProcessElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.fff")
            : elapsed.ToString(@"mm\:ss\.fff");

    private static string LaneText(LaneDirection direction) =>
        direction == LaneDirection.In ? "IN" : "OUT";
}
