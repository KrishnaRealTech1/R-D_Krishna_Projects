using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class ServerPanelWindow : Window
{
    private static readonly int[] CommonBaudRates = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    private readonly AppOptions _options;
    private readonly AppConfigurationService _configurationService;
    private readonly HardwareGateway _hardwareGateway;
    private readonly WeighbridgeService _weighbridge;
    private readonly CameraStreamService _cameraStreams;
    private readonly MainViewModel _viewModel;

    public ServerPanelWindow(
        AppOptions options,
        AppConfigurationService configurationService,
        HardwareGateway hardwareGateway,
        WeighbridgeService weighbridge,
        CameraStreamService cameraStreams,
        MainViewModel viewModel)
    {
        InitializeComponent();
        _options = options;
        _configurationService = configurationService;
        _hardwareGateway = hardwareGateway;
        _weighbridge = weighbridge;
        _cameraStreams = cameraStreams;
        _viewModel = viewModel;

        WeightBaudComboBox.ItemsSource = CommonBaudRates;
        WeightPortComboBox.ItemsSource = _hardwareGateway.GetAvailablePortNames();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        SiteIdTextBox.Text = _options.Device.SiteId;
        DeviceIdTextBox.Text = _options.Device.DeviceId;
        LaneIdTextBox.Text = _options.Device.LaneId;
        DeviceNameTextBox.Text = _options.Device.DeviceName;

        TargetWeightTextBox.Text = _options.Weighbridge.TargetWeightKg.ToString("0.###", CultureInfo.InvariantCulture);
        ResetWeightTextBox.Text = _options.Weighbridge.ResetWeightKg.ToString("0.###", CultureInfo.InvariantCulture);
        WeightPortComboBox.Text = _options.Weighbridge.Serial.PortName;
        WeightBaudComboBox.Text = _options.Weighbridge.Serial.BaudRate.ToString(CultureInfo.InvariantCulture);
        WeightPrefixTextBox.Text = _options.Weighbridge.DataPrefix;
        WeightUnitTextBox.Text = _options.Weighbridge.UnitText;
        StableReadCountTextBox.Text = _options.Weighbridge.StableReadCount.ToString(CultureInfo.InvariantCulture);
        ProcessWaitTextBox.Text = _options.Processing.ProcessWaitSeconds.ToString(CultureInfo.InvariantCulture);
        BarrierDelayTextBox.Text = _options.Processing.BarrierAndGreenDelaySeconds.ToString(CultureInfo.InvariantCulture);

        LoadCamera(1, Camera1EnabledCheckBox, Camera1UrlTextBox);
        LoadCamera(2, Camera2EnabledCheckBox, Camera2UrlTextBox);
        LoadCamera(3, Camera3EnabledCheckBox, Camera3UrlTextBox);
        LoadCamera(4, Camera4EnabledCheckBox, Camera4UrlTextBox);
        ImageFolderTextBox.Text = _options.Cameras.LocalImageFolder;
        ImagePrefixTextBox.Text = _options.Cameras.ImageFilePrefix;

        ServerEnabledCheckBox.IsChecked = _options.Server.Enabled;
        BrokerHostTextBox.Text = _options.Server.Mqtt.BrokerHost;
        BrokerPortTextBox.Text = _options.Server.Mqtt.Port.ToString(CultureInfo.InvariantCulture);
        MqttUsernameTextBox.Text = _options.Server.Mqtt.Username;
        MqttPasswordBox.Password = _options.Server.Mqtt.Password;
        MqttClientIdTextBox.Text = _options.Server.Mqtt.ClientId;
        MqttTlsCheckBox.IsChecked = _options.Server.Mqtt.UseTls;
        PublishTopicTextBox.Text = _options.Server.Mqtt.PublishTopic;
        SyncIntervalTextBox.Text = _options.Server.SyncIntervalSeconds.ToString(CultureInfo.InvariantCulture);

        SelectComboValue(UploadModeComboBox, _options.Server.ImageUpload.Mode);
        UploadHostTextBox.Text = _options.Server.ImageUpload.Host;
        UploadPortTextBox.Text = _options.Server.ImageUpload.Port.ToString(CultureInfo.InvariantCulture);
        UploadUsernameTextBox.Text = _options.Server.ImageUpload.Username;
        UploadPasswordBox.Password = _options.Server.ImageUpload.Password;
        RemoteDirectoryTextBox.Text = _options.Server.ImageUpload.RemoteDirectory;
        UploadTlsCheckBox.IsChecked = _options.Server.ImageUpload.UseTls;
    }

    private void LoadCamera(int number, CheckBox enabled, TextBox url)
    {
        var camera = _options.Cameras.Get(number);
        enabled.IsChecked = camera.Enabled;
        url.Text = camera.RtspUrl;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            _options.Device.SiteId = Required(SiteIdTextBox.Text, "Site ID");
            _options.Device.DeviceId = Required(DeviceIdTextBox.Text, "Device ID");
            _options.Device.LaneId = Required(LaneIdTextBox.Text, "Lane ID");
            _options.Device.DeviceName = Required(DeviceNameTextBox.Text, "Device Name");

            var target = ParsePositiveDecimal(TargetWeightTextBox.Text, "Target weight");
            var reset = ParseNonNegativeDecimal(ResetWeightTextBox.Text, "Reset weight");
            if (reset >= target)
            {
                throw new InvalidOperationException("Reset weight must be lower than target weight.");
            }

            var updatedWeight = new WeighbridgeOptions
            {
                Enabled = true,
                TargetWeightKg = target,
                ResetWeightKg = reset,
                StableReadCount = ParsePositiveInt(StableReadCountTextBox.Text, "Stable read count"),
                DataPrefix = Required(WeightPrefixTextBox.Text, "Weight data prefix"),
                UnitText = Required(WeightUnitTextBox.Text, "Weight unit"),
                Serial = new SerialPortOptions
                {
                    PortName = Required(WeightPortComboBox.Text, "Weight COM"),
                    BaudRate = ParsePositiveInt(WeightBaudComboBox.Text, "Weight baud rate"),
                    DataBits = _options.Weighbridge.Serial.DataBits,
                    Parity = _options.Weighbridge.Serial.Parity,
                    StopBits = _options.Weighbridge.Serial.StopBits,
                    ReadMode = "LineText"
                }
            };

            _options.Processing.ProcessWaitSeconds = ParseNonNegativeInt(ProcessWaitTextBox.Text, "Process wait");
            _options.Processing.BarrierAndGreenDelaySeconds = ParseNonNegativeInt(BarrierDelayTextBox.Text, "Barrier delay");

            SaveCamera(1, Camera1EnabledCheckBox, Camera1UrlTextBox);
            SaveCamera(2, Camera2EnabledCheckBox, Camera2UrlTextBox);
            SaveCamera(3, Camera3EnabledCheckBox, Camera3UrlTextBox);
            SaveCamera(4, Camera4EnabledCheckBox, Camera4UrlTextBox);
            _options.Cameras.LocalImageFolder = Required(ImageFolderTextBox.Text, "Image folder");
            _options.Cameras.ImageFilePrefix = Required(ImagePrefixTextBox.Text, "Image file prefix");

            _options.Server.Enabled = ServerEnabledCheckBox.IsChecked == true;
            _options.Server.SyncIntervalSeconds = ParsePositiveInt(SyncIntervalTextBox.Text, "Sync interval");
            _options.Server.Mqtt.BrokerHost = BrokerHostTextBox.Text.Trim();
            _options.Server.Mqtt.Port = ParsePositiveInt(BrokerPortTextBox.Text, "MQTT port");
            _options.Server.Mqtt.Username = MqttUsernameTextBox.Text.Trim();
            _options.Server.Mqtt.Password = MqttPasswordBox.Password;
            _options.Server.Mqtt.ClientId = Required(MqttClientIdTextBox.Text, "MQTT client ID");
            _options.Server.Mqtt.UseTls = MqttTlsCheckBox.IsChecked == true;
            _options.Server.Mqtt.PublishTopic = Required(PublishTopicTextBox.Text, "MQTT publish topic");
            _options.Server.Mqtt.BaseTopic = _options.Server.Mqtt.PublishTopic;

            _options.Server.ImageUpload.Mode = SelectedComboValue(UploadModeComboBox, "Disabled");
            _options.Server.ImageUpload.Host = UploadHostTextBox.Text.Trim();
            _options.Server.ImageUpload.Port = ParsePositiveInt(UploadPortTextBox.Text, "Image upload port");
            _options.Server.ImageUpload.Username = UploadUsernameTextBox.Text.Trim();
            _options.Server.ImageUpload.Password = UploadPasswordBox.Password;
            _options.Server.ImageUpload.RemoteDirectory = RemoteDirectoryTextBox.Text.Trim();
            _options.Server.ImageUpload.UseTls = UploadTlsCheckBox.IsChecked == true;

            var weightResult = await _weighbridge.ApplySettingsAsync(updatedWeight);
            _cameraStreams.ReloadConfiguration();
            await _configurationService.SaveAsync();
            _viewModel.RefreshWeighbridgeSettings();

            StatusTextBlock.Text = $"Saved and applied. {weightResult.Message}";
            StatusTextBlock.Foreground = weightResult.Success
                ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
                : new SolidColorBrush(Color.FromRgb(185, 28, 28));
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void SaveCamera(int number, CheckBox enabled, TextBox url)
    {
        var camera = _options.Cameras.Get(number);
        camera.Enabled = enabled.IsChecked == true;
        camera.RtspUrl = url.Text.Trim();
        if (camera.Enabled && string.IsNullOrWhiteSpace(camera.RtspUrl))
        {
            throw new InvalidOperationException($"Camera {number} is enabled but RTSP URL is empty.");
        }
    }

    private static void SelectComboValue(ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static string SelectedComboValue(ComboBox combo, string fallback) =>
        (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

    private static string Required(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{label} is required.");

    private static int ParsePositiveInt(string text, string label) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"{label} must be a positive whole number.");

    private static int ParseNonNegativeInt(string text, string label) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw new InvalidOperationException($"{label} must be zero or greater.");

    private static decimal ParsePositiveDecimal(string text, string label) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0m
            ? value
            : throw new InvalidOperationException($"{label} must be greater than zero.");

    private static decimal ParseNonNegativeDecimal(string text, string label) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
            ? value
            : throw new InvalidOperationException($"{label} must be zero or greater.");
}
