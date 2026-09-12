namespace RfidVehicleAccess.Models;

public enum IawsCameraPosition
{
    Front = 1,
    Back = 2,
    Left = 3,
    Right = 4
}

// EventArgs must be normal classes. C# records can only inherit from object
// or another record, so using `record ... : EventArgs` causes CS8864.
public sealed class WeightChangedEventArgs : EventArgs
{
    public WeightChangedEventArgs(decimal weightKg, DateTimeOffset timestamp)
    {
        WeightKg = weightKg;
        Timestamp = timestamp;
    }

    public decimal WeightKg { get; }
    public DateTimeOffset Timestamp { get; }
}

public sealed class RfidReadEventArgs : EventArgs
{
    public RfidReadEventArgs(string rfid, DateTimeOffset timestamp)
    {
        Rfid = rfid;
        Timestamp = timestamp;
    }

    public string Rfid { get; }
    public DateTimeOffset Timestamp { get; }
}

public sealed class IawsCameraStatusChangedEventArgs : EventArgs
{
    public IawsCameraStatusChangedEventArgs(IawsCameraPosition position, string status)
    {
        Position = position;
        Status = status;
    }

    public IawsCameraPosition Position { get; }
    public string Status { get; }
}

public sealed record IawsTransactionState(
    string Status,
    string Rfid,
    decimal WeightKg,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool Success,
    string Message,
    string ApiResponse,
    string FrontImage,
    string BackImage,
    string LeftImage,
    string RightImage);
