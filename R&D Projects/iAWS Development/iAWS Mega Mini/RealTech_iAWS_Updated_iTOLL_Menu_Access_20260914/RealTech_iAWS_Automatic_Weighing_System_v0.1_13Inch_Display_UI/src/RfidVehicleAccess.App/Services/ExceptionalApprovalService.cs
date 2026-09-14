using System.Windows;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class ExceptionalApprovalService : IExceptionalApprovalService
{
    private readonly SemaphoreSlim _dialogLock = new(1, 1);

    public async Task<ExceptionalApprovalDetails?> RequestApprovalAsync(
        ExceptionalApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _dialogLock.WaitAsync(cancellationToken);

        try
        {
            var application = Application.Current
                ?? throw new InvalidOperationException(
                    "The exceptional approval dialog cannot open before the application starts.");
            var dispatcher = application.Dispatcher;

            if (dispatcher.CheckAccess())
            {
                return ShowDialog(application, request);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var operation = dispatcher.InvokeAsync(() => ShowDialog(application, request));
            return await operation.Task;
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    private static ExceptionalApprovalDetails? ShowDialog(
        Application application,
        ExceptionalApprovalRequest request)
    {
        var dialog = new ExceptionalApprovalWindow(request);
        if (application.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true
            ? dialog.Approval
            : null;
    }
}
