namespace RfidVehicleAccess.Models;

public enum LogChannel
{
    Status = 1,
    Api = 2
}

public sealed record AppLogEntry(
    DateTimeOffset Timestamp,
    LogChannel Channel,
    string Message)
{
    public string DisplayText => $"{Timestamp:dd-MM-yy HH:mm:ss.fff}: [{Channel.ToString().ToUpperInvariant()}] {Message}";
}
