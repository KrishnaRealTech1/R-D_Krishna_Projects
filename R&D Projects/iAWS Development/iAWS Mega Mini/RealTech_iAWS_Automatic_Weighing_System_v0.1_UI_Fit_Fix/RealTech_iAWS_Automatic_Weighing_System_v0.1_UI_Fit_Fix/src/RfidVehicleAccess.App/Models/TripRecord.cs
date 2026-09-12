namespace RfidVehicleAccess.Models;

public sealed class TripRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SiteId { get; init; }
    public required string DeviceId { get; init; }
    public required LaneDirection Direction { get; init; }
    public required string RfidNumber { get; init; }
    public required string VehicleNumber { get; init; }
    public string VehicleCategory { get; init; } = string.Empty;
    public string ContractorCode { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string BalanceType { get; set; } = "RFID";
    public string AuthorizationSource { get; set; } = "OFFLINE_LOCAL";
    public string AuthorizationRequestId { get; set; } = string.Empty;
    public string ServerDecision { get; set; } = string.Empty;
    public string ServerReason { get; set; } = string.Empty;
    public required RfidAccessType AccessType { get; init; }
    public Guid? EntryTripId { get; init; }
    public decimal PreviousBalance { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal NewBalance { get; set; }
    public decimal WeightKg { get; set; }
    public decimal TargetWeightKg { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string ImageCaptureStatus { get; set; } = "Pending";
    public string RemoteImagePath { get; set; } = string.Empty;
    public DateTimeOffset? ImageUploadedAt { get; set; }
    public string Camera1ImagePath { get; set; } = string.Empty;
    public string Camera1ImageCaptureStatus { get; set; } = "Pending";
    public string Camera1RemoteImagePath { get; set; } = string.Empty;
    public DateTimeOffset? Camera1ImageUploadedAt { get; set; }
    public string Camera2ImagePath { get; set; } = string.Empty;
    public string Camera2ImageCaptureStatus { get; set; } = "Pending";
    public string Camera2RemoteImagePath { get; set; } = string.Empty;
    public DateTimeOffset? Camera2ImageUploadedAt { get; set; }
    public string Camera3ImagePath { get; set; } = string.Empty;
    public string Camera3ImageCaptureStatus { get; set; } = "Pending";
    public string Camera3RemoteImagePath { get; set; } = string.Empty;
    public DateTimeOffset? Camera3ImageUploadedAt { get; set; }
    public string Camera4ImagePath { get; set; } = string.Empty;
    public string Camera4ImageCaptureStatus { get; set; } = "Pending";
    public string Camera4RemoteImagePath { get; set; } = string.Empty;
    public DateTimeOffset? Camera4ImageUploadedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.Now;
    public TripStatus Status { get; set; } = TripStatus.PendingSync;
    public int SyncAttemptCount { get; set; }
    public DateTimeOffset? LastSyncAttemptAt { get; set; }
    public DateTimeOffset? NextSyncAttemptAt { get; set; }
    public string LastSyncError { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string ExceptionalApprovalMode { get; set; } = string.Empty;
    public string ExceptionalApprovalReason { get; set; } = string.Empty;
    public string ExceptionalApproverName { get; set; } = string.Empty;
    public string ExceptionalApproverRole { get; set; } = string.Empty;
    public string ExceptionalApproverMobile { get; set; } = string.Empty;
    public DateTimeOffset? ExceptionalApprovedAt { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
}
