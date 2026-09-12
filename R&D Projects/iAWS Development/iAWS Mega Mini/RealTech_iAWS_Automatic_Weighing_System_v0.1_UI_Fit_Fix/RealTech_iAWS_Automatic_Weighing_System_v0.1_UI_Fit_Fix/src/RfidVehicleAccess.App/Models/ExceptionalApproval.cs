namespace RfidVehicleAccess.Models;

public enum MissingTripType
{
    MissingIn = 1,
    MissingOut = 2
}

public sealed class ExceptionalApprovalRequest
{
    public required MissingTripType MissingTripType { get; init; }
    public required LaneDirection CurrentLane { get; init; }
    public required string RfidNumber { get; init; }
    public required string VehicleNumber { get; init; }
    public required string AccessType { get; init; }
    public required string Explanation { get; init; }
}

public sealed class ExceptionalApprovalDetails
{
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string MobileNumber { get; init; }
    public bool IsAutomatic { get; init; }
    public DateTimeOffset ApprovedAt { get; init; } = DateTimeOffset.Now;
}
