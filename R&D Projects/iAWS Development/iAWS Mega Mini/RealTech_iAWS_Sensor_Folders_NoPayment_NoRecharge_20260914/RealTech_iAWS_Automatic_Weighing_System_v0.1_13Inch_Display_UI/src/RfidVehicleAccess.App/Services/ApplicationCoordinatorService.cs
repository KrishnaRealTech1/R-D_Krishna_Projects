using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Connects the single weighbridge, RFID readers and optional vehicle sensors to the iAWS lane engine.
/// Weight arms one shared cycle; when the sensor interface is enabled the matching lane
/// sensor must also be active before the first valid RFID can win that cycle.
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
        _hardware.VehicleSensorStateChanged += OnVehicleSensorStateChanged;

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
            _hardware.VehicleSensorStateChanged -= OnVehicleSensorStateChanged;
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

    private void OnVehicleSensorStateChanged(
        object? sender,
        VehicleSensorStateChangedEventArgs eventArgs)
    {
        _ = RunSafelyAsync(() =>
            _laneProcessor.RegisterSensorStateAsync(eventArgs.Direction, eventArgs.IsHigh));
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
