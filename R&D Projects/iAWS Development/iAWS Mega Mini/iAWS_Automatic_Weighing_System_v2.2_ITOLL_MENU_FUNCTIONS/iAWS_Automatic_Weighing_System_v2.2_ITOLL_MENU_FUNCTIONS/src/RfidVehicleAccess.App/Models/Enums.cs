namespace RfidVehicleAccess.Models;

public enum LaneDirection
{
    In = 1,
    Out = 2
}

public enum RfidAccessType
{
    Paid = 1,
    Free = 2
}

public enum TripStatus
{
    PendingSync = 1,
    Synced = 2,
    ReconciliationRequired = 3,
    Cancelled = 4
}

public enum LaneState
{
    Idle = 1,
    VehicleDetected = 2,
    Processing = 3,
    Approved = 4,
    Rejected = 5,
    Error = 6
}

public enum SignalLightState
{
    Off = 0,
    Red = 1,
    Orange = 2,
    Green = 3
}

public enum LogChannel
{
    Status = 1,
    Server = 2,
    Api = 3
}
