using System.Globalization;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class IawsWeighingProcessor : IHostedService, IDisposable
{
    private readonly AppOptions _options;
    private readonly IawsHardwareGateway _hardware;
    private readonly IawsCameraCaptureService _captureService;
    private readonly IawsApiClient _apiClient;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();

    private string _latestRfid = string.Empty;
    private DateTimeOffset _latestRfidAt = DateTimeOffset.MinValue;
    private decimal _currentWeight;
    private bool _cycleArmed = true;
    private bool _processing;
    private bool _triggerPending;

    public IawsWeighingProcessor(
        AppOptions options,
        IawsHardwareGateway hardware,
        IawsCameraCaptureService captureService,
        IawsApiClient apiClient,
        AppLogger logger)
    {
        _options = options;
        _hardware = hardware;
        _captureService = captureService;
        _apiClient = apiClient;
        _logger = logger;
    }

    public event EventHandler<IawsTransactionState>? StateChanged;

    public IawsTransactionState CurrentState { get; private set; } = new(
        "READY",
        string.Empty,
        0m,
        null,
        null,
        false,
        "Waiting for weight to reach the configured trigger.",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hardware.WeightChanged += OnWeightChanged;
        _hardware.RfidReceived += OnRfidReceived;
        PublishState(
            "READY",
            false,
            "Waiting for vehicle weight.",
            string.Empty,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _hardware.WeightChanged -= OnWeightChanged;
        _hardware.RfidReceived -= OnRfidReceived;
        _shutdown.Cancel();
        return Task.CompletedTask;
    }

    public void ResetCycle()
    {
        _latestRfid = string.Empty;
        _latestRfidAt = DateTimeOffset.MinValue;
        _cycleArmed = true;
        _triggerPending = false;
        PublishState(
            "READY",
            false,
            "Cycle reset manually. Waiting for configured weight.",
            string.Empty,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private void OnWeightChanged(object? sender, WeightChangedEventArgs e)
    {
        _currentWeight = e.WeightKg;

        if (!_processing && !_cycleArmed &&
            _currentWeight <= Math.Max(0m, _options.Iaws.ResetWeightKg))
        {
            _cycleArmed = true;
            _triggerPending = false;
            _latestRfid = string.Empty;
            _latestRfidAt = DateTimeOffset.MinValue;
            PublishState(
                "READY",
                false,
                "Weighbridge cleared. Ready for next vehicle.",
                string.Empty,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
            return;
        }

        if (_processing || !_cycleArmed)
        {
            return;
        }

        if (_currentWeight < Math.Max(0m, _options.Iaws.TriggerWeightKg))
        {
            _triggerPending = false;
            return;
        }

        _triggerPending = true;
        if (!HasFreshRfid())
        {
            PublishState(
                "WAITING FOR RFID",
                false,
                $"Weight reached {_currentWeight:0.##} kg. Waiting for RFID.",
                string.Empty,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
            return;
        }

        StartProcessing();
    }

    private void OnRfidReceived(object? sender, RfidReadEventArgs e)
    {
        _latestRfid = e.Rfid;
        _latestRfidAt = e.Timestamp;

        if (_processing || !_cycleArmed)
        {
            return;
        }

        if (_currentWeight >= Math.Max(0m, _options.Iaws.TriggerWeightKg))
        {
            _triggerPending = true;
            StartProcessing();
            return;
        }

        PublishState(
            "RFID READY",
            false,
            $"RFID {_latestRfid} detected. Waiting for trigger weight.",
            string.Empty,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private bool HasFreshRfid()
    {
        if (string.IsNullOrWhiteSpace(_latestRfid))
        {
            return false;
        }

        var freshness = TimeSpan.FromSeconds(Math.Max(1, _options.Iaws.RfidFreshnessSeconds));
        return DateTimeOffset.Now - _latestRfidAt <= freshness;
    }

    private void StartProcessing()
    {
        if (_processing || !_cycleArmed || !_triggerPending || !HasFreshRfid())
        {
            return;
        }

        _processing = true;
        _cycleArmed = false;
        _triggerPending = false;
        _ = ProcessAsync(_shutdown.Token);
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await _processLock.WaitAsync(cancellationToken);
        var startedAt = DateTimeOffset.Now;
        var rfid = _latestRfid;
        var weight = _currentWeight;

        try
        {
            PublishState(
                "CAPTURING 4 CAMERAS",
                false,
                "Weight threshold reached. Capturing Front / Back / Left / Right images.",
                string.Empty,
                startedAt,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            await _logger.StatusAsync(
                $"PROCESS START | RFID={rfid} | Weight={weight:0.##} kg | Trigger={_options.Iaws.TriggerWeightKg:0.##} kg",
                cancellationToken);

            var capturedAt = GetIstNow();
            var captures = await _captureService.CaptureAllAsync(capturedAt, cancellationToken);

            if (_options.Iaws.RequireAllCameras && !captures.AllCaptured)
            {
                var message = "Transaction stopped because one or more required camera snapshots failed.";
                await _logger.StatusAsync(message, cancellationToken);
                PublishState(
                    "CAMERA ERROR",
                    false,
                    message,
                    string.Empty,
                    startedAt,
                    DateTimeOffset.Now,
                    captures.FrontFileName,
                    captures.BackFileName,
                    captures.LeftFileName,
                    captures.RightFileName);
                return;
            }

            PublishState(
                "SENDING API",
                false,
                "Sending weighing event using the configured REST API.",
                string.Empty,
                startedAt,
                null,
                captures.FrontFileName,
                captures.BackFileName,
                captures.LeftFileName,
                captures.RightFileName);

            var payload = new IawsApiPayload
            {
                Rfid = rfid,
                Action = "WEIGH",
                Weight = weight.ToString("0.##", CultureInfo.InvariantCulture),
                Front = captures.FrontCaptured ? captures.FrontFileName : string.Empty,
                Back = captures.BackCaptured ? captures.BackFileName : string.Empty,
                Left = captures.LeftCaptured ? captures.LeftFileName : string.Empty,
                Right = captures.RightCaptured ? captures.RightFileName : string.Empty,
                Dates = capturedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                MaterialType = string.IsNullOrWhiteSpace(_options.Iaws.MaterialType)
                    ? null
                    : _options.Iaws.MaterialType.Trim()
            };

            var apiResult = await _apiClient.SendAsync(payload, cancellationToken);
            var completedAt = DateTimeOffset.Now;
            var status = apiResult.Success ? "PROCESSED" : "API FAILED";

            PublishState(
                status,
                apiResult.Success,
                apiResult.Message,
                apiResult.ResponseBody,
                startedAt,
                completedAt,
                captures.FrontFileName,
                captures.BackFileName,
                captures.LeftFileName,
                captures.RightFileName);

            await _logger.StatusAsync(
                $"PROCESS END | {status} | RFID={rfid} | Weight={weight:0.##} kg",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown.
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Processing failed: {ex.Message}", cancellationToken);
            PublishState(
                "ERROR",
                false,
                ex.Message,
                string.Empty,
                startedAt,
                DateTimeOffset.Now,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
        finally
        {
            _processing = false;
            _processLock.Release();
        }
    }

    private static DateTime GetIstNow()
    {
        try
        {
            var india = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, india);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.Now;
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.Now;
        }
    }

    private void PublishState(
        string status,
        bool success,
        string message,
        string apiResponse,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        string front,
        string back,
        string left,
        string right)
    {
        CurrentState = new IawsTransactionState(
            status,
            _latestRfid,
            _currentWeight,
            startedAt,
            completedAt,
            success,
            message,
            apiResponse,
            front,
            back,
            left,
            right);
        StateChanged?.Invoke(this, CurrentState);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _processLock.Dispose();
        _shutdown.Dispose();
    }
}
