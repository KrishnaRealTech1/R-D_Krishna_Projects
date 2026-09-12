using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class IawsSettingsWindow : Window
{
    private readonly AppOptions _options;
    private readonly IawsHardwareGateway _hardware;
    private readonly IawsCameraStreamService _cameraStreams;
    private readonly AppConfigurationService _configurationService;
    private readonly IawsMainViewModel _viewModel;

    public IawsSettingsWindow(
        AppOptions options,
        IawsHardwareGateway hardware,
        IawsCameraStreamService cameraStreams,
        AppConfigurationService configurationService,
        IawsMainViewModel viewModel)
    {
        InitializeComponent();
        _options = options;
        _hardware = hardware;
        _cameraStreams = cameraStreams;
        _configurationService = configurationService;
        _viewModel = viewModel;
        RefreshPortLists();
        LoadSettings();
    }

    private void LoadSettings()
    {
        ApiEndpointTextBox.Text = _options.Iaws.ApiEndpoint;
        ApiTimeoutTextBox.Text = _options.Iaws.ApiTimeoutSeconds.ToString();
        SitePrefixTextBox.Text = _options.Iaws.SitePrefix;
        MaterialTypeTextBox.Text = _options.Iaws.MaterialType;
        TriggerWeightTextBox.Text = _options.Iaws.TriggerWeightKg.ToString("0.##");
        ResetWeightTextBox.Text = _options.Iaws.ResetWeightKg.ToString("0.##");
        RfidFreshnessTextBox.Text = _options.Iaws.RfidFreshnessSeconds.ToString();
        RequireAllCamerasCheckBox.IsChecked = _options.Iaws.RequireAllCameras;

        SimulationCheckBox.IsChecked = _options.Hardware.SimulationEnabled;
        RfidPortComboBox.Text = _options.Hardware.RfidReader.PortName;
        RfidBaudTextBox.Text = _options.Hardware.RfidReader.BaudRate.ToString();
        SelectComboValue(RfidReadModeComboBox, _options.Hardware.RfidReader.ReadMode);
        WeightPortComboBox.Text = _options.Hardware.WeightBridge.PortName;
        WeightBaudTextBox.Text = _options.Hardware.WeightBridge.BaudRate.ToString();
        WeightPatternTextBox.Text = _options.Hardware.WeightBridge.WeightPattern;

        FrontEnabledCheckBox.IsChecked = _options.Cameras.Front.Enabled;
        FrontUrlTextBox.Text = _options.Cameras.Front.RtspUrl;
        BackEnabledCheckBox.IsChecked = _options.Cameras.Back.Enabled;
        BackUrlTextBox.Text = _options.Cameras.Back.RtspUrl;
        LeftEnabledCheckBox.IsChecked = _options.Cameras.Left.Enabled;
        LeftUrlTextBox.Text = _options.Cameras.Left.RtspUrl;
        RightEnabledCheckBox.IsChecked = _options.Cameras.Right.Enabled;
        RightUrlTextBox.Text = _options.Cameras.Right.RtspUrl;
        ImageFolderTextBox.Text = _options.Cameras.LocalImageFolder;
    }

    private void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPortLists();

    private void RefreshPortLists()
    {
        var rfid = RfidPortComboBox?.Text;
        var weight = WeightPortComboBox?.Text;
        var ports = _hardware.GetAvailablePortNames();
        RfidPortComboBox.ItemsSource = ports;
        WeightPortComboBox.ItemsSource = ports;
        if (!string.IsNullOrWhiteSpace(rfid)) RfidPortComboBox.Text = rfid;
        if (!string.IsNullOrWhiteSpace(weight)) WeightPortComboBox.Text = weight;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            var apiEndpoint = ApiEndpointTextBox.Text.Trim();
            if (!Uri.TryCreate(apiEndpoint, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a full API URL, for example https://host/iaws_raw/mega/insert.");

            if (!decimal.TryParse(TriggerWeightTextBox.Text.Trim(), out var triggerWeight) || triggerWeight < 0)
                throw new InvalidOperationException("Enter a valid trigger weight in kg.");
            if (!decimal.TryParse(ResetWeightTextBox.Text.Trim(), out var resetWeight) || resetWeight < 0)
                throw new InvalidOperationException("Enter a valid reset weight in kg.");
            if (resetWeight >= triggerWeight && triggerWeight > 0)
                throw new InvalidOperationException("Reset weight must be lower than trigger weight.");
            if (!int.TryParse(ApiTimeoutTextBox.Text.Trim(), out var apiTimeout) || apiTimeout < 2 || apiTimeout > 300)
                throw new InvalidOperationException("API timeout must be between 2 and 300 seconds.");
            if (!int.TryParse(RfidFreshnessTextBox.Text.Trim(), out var rfidFreshness) || rfidFreshness < 1 || rfidFreshness > 600)
                throw new InvalidOperationException("RFID freshness must be between 1 and 600 seconds.");
            if (!int.TryParse(RfidBaudTextBox.Text.Trim(), out var rfidBaud) || rfidBaud <= 0)
                throw new InvalidOperationException("Enter a valid RFID baud rate.");
            if (!int.TryParse(WeightBaudTextBox.Text.Trim(), out var weightBaud) || weightBaud <= 0)
                throw new InvalidOperationException("Enter a valid weighbridge baud rate.");

            var weightPattern = WeightPatternTextBox.Text.Trim();
            _ = new Regex(weightPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

            var simulation = SimulationCheckBox.IsChecked == true;
            if (!simulation && (string.IsNullOrWhiteSpace(RfidPortComboBox.Text) || string.IsNullOrWhiteSpace(WeightPortComboBox.Text)))
                throw new InvalidOperationException("Select both RFID and weighbridge COM ports.");
            if (!simulation && string.Equals(RfidPortComboBox.Text.Trim(), WeightPortComboBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("RFID and weighbridge must use different COM ports.");

            _options.Iaws.ApiEndpoint = apiEndpoint;
            _options.Iaws.ApiTimeoutSeconds = apiTimeout;
            _options.Iaws.SitePrefix = SitePrefixTextBox.Text.Trim();
            _options.Iaws.MaterialType = MaterialTypeTextBox.Text.Trim();
            _options.Iaws.TriggerWeightKg = triggerWeight;
            _options.Iaws.ResetWeightKg = resetWeight;
            _options.Iaws.RfidFreshnessSeconds = rfidFreshness;
            _options.Iaws.RequireAllCameras = RequireAllCamerasCheckBox.IsChecked == true;

            _options.Hardware.SimulationEnabled = simulation;
            _options.Hardware.RfidReader.PortName = RfidPortComboBox.Text.Trim();
            _options.Hardware.RfidReader.BaudRate = rfidBaud;
            _options.Hardware.RfidReader.ReadMode = GetComboText(RfidReadModeComboBox, "UhfCfFrame");
            _options.Hardware.WeightBridge.PortName = WeightPortComboBox.Text.Trim();
            _options.Hardware.WeightBridge.BaudRate = weightBaud;
            _options.Hardware.WeightBridge.WeightPattern = weightPattern;

            ApplyCamera(_options.Cameras.Front, FrontEnabledCheckBox, FrontUrlTextBox);
            ApplyCamera(_options.Cameras.Back, BackEnabledCheckBox, BackUrlTextBox);
            ApplyCamera(_options.Cameras.Left, LeftEnabledCheckBox, LeftUrlTextBox);
            ApplyCamera(_options.Cameras.Right, RightEnabledCheckBox, RightUrlTextBox);
            _options.Cameras.LocalImageFolder = ImageFolderTextBox.Text.Trim();

            await _configurationService.SaveAsync();
            await _hardware.ReloadAsync();
            _cameraStreams.ReloadConfiguration();
            _viewModel.RefreshConfigurationBindings();

            StatusTextBlock.Text = "Configuration saved and applied.";
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 40, 40));
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private static void ApplyCamera(CameraLaneOptions camera, CheckBox enabled, TextBox url)
    {
        camera.Enabled = enabled.IsChecked == true;
        camera.RtspUrl = url.Text.Trim();
    }

    private static string GetComboText(ComboBox comboBox, string fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item && item.Content is string content)
            return content;
        return string.IsNullOrWhiteSpace(comboBox.Text) ? fallback : comboBox.Text.Trim();
    }

    private static void SelectComboValue(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        comboBox.Text = value;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
