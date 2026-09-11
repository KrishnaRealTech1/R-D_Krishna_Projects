using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public interface IExceptionalApprovalService
{
    Task<ExceptionalApprovalDetails?> RequestApprovalAsync(
        ExceptionalApprovalRequest request,
        CancellationToken cancellationToken = default);
}
