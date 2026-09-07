namespace RfidVehicleAccess.Models;

public sealed class RfidRechargeCommand
{
    public string SchemaVersion { get; set; } = "1.0";
    public string MessageType { get; set; } = string.Empty;
    public string RechargeId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string RfidNumber { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public decimal? PreviousBalance { get; set; }
    public decimal RechargeAmount { get; set; }
    public decimal? NewBalance { get; set; }
    public DateTimeOffset? RechargedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class RechargeApplyResult
{
    public required string RechargeId { get; init; }
    public required string RfidNumber { get; init; }
    public string VehicleNumber { get; init; } = string.Empty;
    public required string Status { get; init; }
    public bool Success { get; init; }
    public bool IsDuplicate { get; init; }
    public decimal RechargeAmount { get; init; }
    public decimal? PreviousBalance { get; init; }
    public decimal? NewBalance { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class RfidRechargeAcknowledgement
{
    public string SchemaVersion { get; set; } = "1.0";
    public string MessageType { get; set; } = "rfidRechargeAck";
    public string RechargeId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string LaneId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string RfidNumber { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Duplicate { get; set; }
    public decimal RechargeAmount { get; set; }
    public decimal? PreviousBalance { get; set; }
    public decimal? NewBalance { get; set; }
    public decimal? ServerExpectedNewBalance { get; set; }
    public bool? ServerBalanceMatched { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
