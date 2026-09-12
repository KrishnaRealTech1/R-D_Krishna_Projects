namespace RfidVehicleAccess.Models;

public sealed class VehicleCategoryPriceRecord
{
    public required string CategoryName { get; init; }
    public decimal DebitAmount { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
