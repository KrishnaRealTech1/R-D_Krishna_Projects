using System.Windows;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class AdminControlsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public AdminControlsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ApplyResponsiveWindowBounds();
        _viewModel = viewModel;
        _viewModel.RefreshExceptionalApprovalSettings();
        DataContext = _viewModel;
    }

    private void ApplyResponsiveWindowBounds()
    {
        const double outerMargin = 24;
        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(560, workArea.Width - outerMargin);
        var availableHeight = Math.Max(420, workArea.Height - outerMargin);

        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        Width = Math.Min(860, availableWidth);
        Height = Math.Min(720, availableHeight);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Reapply the persisted values when the panel closes. This also rolls
        // back any checkbox changes when the operator closes without saving.
        _viewModel.RefreshExceptionalApprovalSettings();
        base.OnClosed(e);
    }
}
