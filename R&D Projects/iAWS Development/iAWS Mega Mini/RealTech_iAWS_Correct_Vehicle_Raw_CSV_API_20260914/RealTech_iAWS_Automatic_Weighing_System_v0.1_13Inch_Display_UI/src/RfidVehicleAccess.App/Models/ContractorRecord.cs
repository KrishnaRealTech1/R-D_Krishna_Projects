namespace RfidVehicleAccess.Models;

public sealed class ContractorRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string ContractorCode { get; set; }
    public required string ContractorName { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastServerSyncAt { get; set; }
}
