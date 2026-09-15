using System.Windows;

namespace RfidVehicleAccess;

public partial class StartupLoadingWindow : Window
{
    public StartupLoadingWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(string status, string detail, double percentage)
    {
        StatusText.Text = status;
        DetailText.Text = detail;
        LoadingProgressBar.Value = Math.Clamp(percentage, 0d, 100d);
        PercentageText.Text = $"{LoadingProgressBar.Value:0}%";
    }
}
