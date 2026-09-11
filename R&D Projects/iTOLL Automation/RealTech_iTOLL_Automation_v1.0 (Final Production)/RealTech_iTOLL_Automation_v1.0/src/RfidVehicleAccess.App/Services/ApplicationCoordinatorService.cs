using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class ApplicationCoordinatorService : BackgroundService
{
    private readonly HardwareGateway _hardware;
    private readonly LaneProcessor _laneProcessor;
    private readonly AppLogger _logger;

    public ApplicationCoordinatorService(
        HardwareGateway hardware,
        LaneProcessor laneProcessor,
        AppLogger logger)
    {
        _hardware = hardware;
        _laneProcessor = laneProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hardware.VehicleSensorStateChanged += OnVehicleSensorStateChanged;
        _hardware.RfidReceived += OnRfidReceived;

        await _logger.StatusAsync("Vehicle access processing engine started.", stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            _hardware.VehicleSensorStateChanged -= OnVehicleSensorStateChanged;
            _hardware.RfidReceived -= OnRfidReceived;
        }
    }

    private void OnVehicleSensorStateChanged(
        object? sender,
        VehicleSensorStateChangedEventArgs eventArgs)
    {
        _ = RunSafelyAsync(() => _laneProcessor.RegisterSensorStateAsync(
            eventArgs.Direction,
            eventArgs.IsHigh));
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
            await _logger.StatusAsync($"Background processing error: {ex.Message}");
        }
    }
}
