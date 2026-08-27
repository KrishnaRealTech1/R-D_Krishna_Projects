namespace RfidVehicleAccess.Models;

public sealed record AppLogEntry(
    DateTimeOffset Timestamp,
    LogChannel Channel,
    string Message)
{
    public string DisplayText => $"{Timestamp:dd-MM-yy HH:mm:ss.fff}: {Message}";
}
