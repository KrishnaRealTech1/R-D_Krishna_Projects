namespace RfidVehicleAccess.Models;

public sealed class LaneDisplayState
{
    public LaneDirection Direction { get; init; }
    public LaneState State { get; set; } = LaneState.Idle;
    public string RfidNumber { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string Message { get; set; } = "Waiting for vehicle";
    public DateTimeOffset? SensorDetectedAt { get; set; }
    public string LastControlCommand { get; set; } = string.Empty;
}
