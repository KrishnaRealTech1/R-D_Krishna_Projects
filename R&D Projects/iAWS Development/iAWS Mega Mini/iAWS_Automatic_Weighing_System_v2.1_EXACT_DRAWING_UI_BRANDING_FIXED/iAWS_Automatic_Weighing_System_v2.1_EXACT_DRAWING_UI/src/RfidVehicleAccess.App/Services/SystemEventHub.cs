using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class SystemEventHub
{
    public event EventHandler<AppLogEntry>? LogAdded;
    public void PublishLog(AppLogEntry entry) => LogAdded?.Invoke(this, entry);
}
