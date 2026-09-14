namespace RfidVehicleAccess.Models;

public sealed class VehicleRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RfidNumber { get; set; }
    public required string VehicleNumber { get; set; }
    public string SourceSite { get; set; } = string.Empty;
    public string VehicleCategory { get; set; } = string.Empty;
    public string ContractorCode { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public decimal? CategoryDebitAmount { get; set; }
    public RfidAccessType AccessType { get; set; } = RfidAccessType.Free;
    public decimal Balance { get; set; }
    public decimal? EmptyWeight { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastServerSyncAt { get; set; }
}
