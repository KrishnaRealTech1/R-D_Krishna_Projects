using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class SystemEventHub
{
    public event EventHandler<AppLogEntry>? LogAdded;
    public event EventHandler<LaneDisplayState>? LaneStateChanged;
    public event EventHandler? CountersChanged;
    public event EventHandler<InternetConnectivityStatus>? InternetConnectivityChanged;
    public event EventHandler<ServerConnectivityStatus>? ServerConnectivityChanged;

    public void PublishLog(AppLogEntry entry) => LogAdded?.Invoke(this, entry);

    public void PublishLaneState(LaneDisplayState state) =>
        LaneStateChanged?.Invoke(this, state);

    public void PublishCountersChanged() => CountersChanged?.Invoke(this, EventArgs.Empty);

    public void PublishInternetConnectivity(InternetConnectivityStatus status) =>
        InternetConnectivityChanged?.Invoke(this, status);

    public void PublishServerConnectivity(ServerConnectivityStatus status) =>
        ServerConnectivityChanged?.Invoke(this, status);
}
