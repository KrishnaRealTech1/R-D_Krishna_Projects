using System.Windows;
using System.Windows.Input;

namespace RfidVehicleAccess;

public partial class AdminPasswordWindow : Window
{
    private readonly string _expectedPassword;

    public AdminPasswordWindow(
        string expectedPassword,
        string panelName = "Admin Controls",
        string? description = null)
    {
        InitializeComponent();
        _expectedPassword = expectedPassword;
        Title = $"RealTech iAWS - Automatic Weighing System - {panelName} Authentication";
        HeadingTextBlock.Text = panelName;
        DescriptionTextBlock.Text = description ??
            "Enter the admin password to open manual hardware controls.";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AdminPasswordBox.Focus();
    }

    private void AdminPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        ValidatePassword();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        ValidatePassword();
    }

    private void ValidatePassword()
    {
        if (string.Equals(
                AdminPasswordBox.Password,
                _expectedPassword,
                StringComparison.Ordinal))
        {
            DialogResult = true;
            return;
        }

        ValidationMessage.Visibility = Visibility.Visible;
        AdminPasswordBox.Clear();
        AdminPasswordBox.Focus();
    }
}
