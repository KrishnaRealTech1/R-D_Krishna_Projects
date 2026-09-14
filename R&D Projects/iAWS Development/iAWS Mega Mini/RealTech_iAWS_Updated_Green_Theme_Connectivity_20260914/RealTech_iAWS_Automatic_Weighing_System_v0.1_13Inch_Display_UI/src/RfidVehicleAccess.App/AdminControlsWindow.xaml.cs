using System.Windows;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class AdminControlsWindow : Window
{
    public AdminControlsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
