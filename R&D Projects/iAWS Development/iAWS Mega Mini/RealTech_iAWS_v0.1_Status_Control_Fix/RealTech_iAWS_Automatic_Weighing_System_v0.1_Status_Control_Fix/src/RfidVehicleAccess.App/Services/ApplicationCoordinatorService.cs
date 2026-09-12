using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Connects the single weighbridge and the two RFID readers to the iAWS lane engine.
/// Weight arms one shared cycle; the first valid IN/OUT RFID event wins that cycle.
/// </summary>
public sealed class ApplicationCoordinatorService : BackgroundService
{
    private readonly HardwareGateway _hardware;
    private readonly WeighbridgeService _weighbridge;
    private readonly LaneProcessor _laneProcessor;
    private readonly AppLogger _logger;

    public ApplicationCoordinatorService(
        HardwareGateway hardware,
        WeighbridgeService weighbridge,
        LaneProcessor laneProcessor,
        AppLogger logger)
    {
        _hardware = hardware;
        _weighbridge = weighbridge;
        _laneProcessor = laneProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _weighbridge.WeightChanged += OnWeightChanged;
        _hardware.RfidReceived += OnRfidReceived;

        await _logger.StatusAsync(
            "RealTech iAWS v0.1 processing engine started: one weighbridge, first RFID wins.",
            stoppingToken);

        try
        {
            // Feed the initial value too, which is useful after a settings/service restart.
            await _laneProcessor.UpdateWeightAsync(_weighbridge.CurrentWeightKg, stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            _weighbridge.WeightChanged -= OnWeightChanged;
            _hardware.RfidReceived -= OnRfidReceived;
        }
    }

    private void OnWeightChanged(object? sender, WeightChangedEventArgs eventArgs)
    {
        _ = RunSafelyAsync(() => _laneProcessor.UpdateWeightAsync(eventArgs.WeightKg));
    }

    private void OnRfidReceived(
        object? sender,
        (LaneDirection Direction, string Rfid) eventData)
    {
        _ = RunSafelyAsync(() =>
            _laneProcessor.ProcessRfidAsync(eventData.Direction, eventData.Rfid));
    }

    private async Task RunSafelyAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"iAWS background processing error: {ex.Message}");
        }
    }
}
