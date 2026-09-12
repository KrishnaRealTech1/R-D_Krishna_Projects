using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess;

public partial class HardwareSettingsWindow : Window
{
    private static readonly int[] CommonBaudRates =
    [
        1200,
        2400,
        4800,
        9600,
        19200,
        38400,
        57600,
        115200
    ];

    private readonly AppOptions _options;
    private readonly HardwareGateway _hardwareGateway;
    private readonly AppConfigurationService _configurationService;

    public HardwareSettingsWindow(
        AppOptions options,
        HardwareGateway hardwareGateway,
        AppConfigurationService configurationService)
    {
        InitializeComponent();
        _options = options;
        _hardwareGateway = hardwareGateway;
        _configurationService = configurationService;

        LoadBaudRates();
        RefreshPortLists();
        LoadCurrentSettings();
        UpdateControlAvailability();
        ShowStatus(_hardwareGateway.CurrentStatus, _hardwareGateway.IsReady);
    }

    private void LoadBaudRates()
    {
        InBaudRateComboBox.ItemsSource = CommonBaudRates;
        OutBaudRateComboBox.ItemsSource = CommonBaudRates;
        ControlBaudRateComboBox.ItemsSource = CommonBaudRates;
        BluetoothBaudRateComboBox.ItemsSource = CommonBaudRates;
    }

    private void LoadCurrentSettings()
    {
        SimulationCheckBox.IsChecked = _options.Hardware.SimulationEnabled;

        SetComboText(InPortComboBox, _options.Hardware.InRfid.PortName);
        SetComboText(OutPortComboBox, _options.Hardware.OutRfid.PortName);
        SetComboText(ControlPortComboBox, _options.Hardware.Control.PortName);
        BluetoothEnabledCheckBox.IsChecked = _options.Hardware.BluetoothControlEnabled;
        SetComboText(BluetoothPortComboBox, _options.Hardware.BluetoothControl.PortName);
        WifiEnabledCheckBox.IsChecked = _options.Hardware.WifiControlEnabled;
        WifiHostTextBox.Text = _options.Hardware.WifiControl.Host;
        WifiPortTextBox.Text = _options.Hardware.WifiControl.Port.ToString();

        InBaudRateComboBox.Text = _options.Hardware.InRfid.BaudRate.ToString();
        OutBaudRateComboBox.Text = _options.Hardware.OutRfid.BaudRate.ToString();
        ControlBaudRateComboBox.Text = _options.Hardware.Control.BaudRate.ToString();
        BluetoothBaudRateComboBox.Text = _options.Hardware.BluetoothControl.BaudRate.ToString();
    }

    private void RefreshPorts_Click(object sender, RoutedEventArgs e)
    {
        var inPort = InPortComboBox.Text;
        var outPort = OutPortComboBox.Text;
        var controlPort = ControlPortComboBox.Text;
        var bluetoothPort = BluetoothPortComboBox.Text;

        RefreshPortLists();

        SetComboText(InPortComboBox, inPort);
        SetComboText(OutPortComboBox, outPort);
        SetComboText(ControlPortComboBox, controlPort);
        SetComboText(BluetoothPortComboBox, bluetoothPort);
        ShowStatus("Windows COM port list refreshed.", true);
    }

    private void RefreshPortLists()
    {
        var ports = _hardwareGateway.GetAvailablePortNames();
        InPortComboBox.ItemsSource = ports;
        OutPortComboBox.ItemsSource = ports;
        ControlPortComboBox.ItemsSource = ports;
        BluetoothPortComboBox.ItemsSource = ports;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;

        try
        {
            var settings = BuildSettingsFromControls();
            var result = await _hardwareGateway.ApplySettingsAsync(settings);
            await _configurationService.SaveAsync();

            ShowStatus(
                result.Success
                    ? $"Saved and applied. {result.Message}"
                    : $"Settings saved, but connection failed. {result.Message}",
                result.Success);

            if (result.Success)
            {
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, false);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private HardwareOptions BuildSettingsFromControls()
    {
        var simulationEnabled = SimulationCheckBox.IsChecked == true;
        var inPort = InPortComboBox.Text.Trim();
        var outPort = OutPortComboBox.Text.Trim();
        var controlPort = ControlPortComboBox.Text.Trim();
        var bluetoothEnabled = BluetoothEnabledCheckBox.IsChecked == true;
        var bluetoothPort = BluetoothPortComboBox.Text.Trim();
        var wifiEnabled = WifiEnabledCheckBox.IsChecked == true;
        var wifiHost = WifiHostTextBox.Text.Trim();
        var wifiPortText = WifiPortTextBox.Text.Trim();
        var wifiPort = 0;
        if (wifiEnabled && (!int.TryParse(wifiPortText, out wifiPort) || wifiPort is < 1 or > 65535))
            throw new InvalidOperationException("Enter a valid Wi-Fi terminal TCP port (1-65535).");
        if (wifiEnabled && string.IsNullOrWhiteSpace(wifiHost))
            throw new InvalidOperationException("Enter the Wi-Fi terminal IP/host.");

        if (!simulationEnabled)
        {
            if (string.IsNullOrWhiteSpace(inPort) ||
                string.IsNullOrWhiteSpace(outPort) ||
                string.IsNullOrWhiteSpace(controlPort) ||
                (bluetoothEnabled && string.IsNullOrWhiteSpace(bluetoothPort)))
            {
                throw new InvalidOperationException(
                    bluetoothEnabled
                        ? "Select IN RFID, OUT RFID, wired Control, and Bluetooth backup COM ports."
                        : "Select IN RFID, OUT RFID, and wired Control COM ports.");
            }

            var selectedPorts = new List<string> { inPort, outPort, controlPort };
            if (bluetoothEnabled)
            {
                selectedPorts.Add(bluetoothPort);
            }

            if (selectedPorts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedPorts.Count)
            {
                throw new InvalidOperationException(
                    "RFID, wired Control, and Bluetooth backup must use different COM ports.");
            }
        }

        return new HardwareOptions
        {
            SimulationEnabled = simulationEnabled,
            // Preserve the iAWS weighbridge/single-reader configuration when the
            // iTOLL COM settings are changed from this panel.
            RfidReader = _options.Hardware.RfidReader,
            WeightBridge = _options.Hardware.WeightBridge,
            LineTerminator = _options.Hardware.LineTerminator,
            ReconnectSeconds = _options.Hardware.ReconnectSeconds,
            InRfid = CopySerialSettings(
                _options.Hardware.InRfid,
                inPort,
                ParseBaudRate(InBaudRateComboBox.Text, "IN RFID")),
            OutRfid = CopySerialSettings(
                _options.Hardware.OutRfid,
                outPort,
                ParseBaudRate(OutBaudRateComboBox.Text, "OUT RFID")),
            Control = CopySerialSettings(
                _options.Hardware.Control,
                controlPort,
                ParseBaudRate(ControlBaudRateComboBox.Text, "Control Unit")),
            BluetoothControlEnabled = bluetoothEnabled,
            BluetoothControl = CopySerialSettings(
                _options.Hardware.BluetoothControl,
                bluetoothPort,
                ParseBaudRate(BluetoothBaudRateComboBox.Text, "Bluetooth Backup")),
            WifiControlEnabled = wifiEnabled,
            WifiControl = new WifiControlOptions
            {
                Host = wifiHost,
                Port = wifiEnabled ? wifiPort : _options.Hardware.WifiControl.Port,
                ConnectTimeoutMilliseconds = _options.Hardware.WifiControl.ConnectTimeoutMilliseconds
            },
            SensorMessages = new SensorMessageOptions
            {
                InHigh = _options.Hardware.SensorMessages.InHigh,
                InReleased = _options.Hardware.SensorMessages.InReleased,
                OutHigh = _options.Hardware.SensorMessages.OutHigh,
                OutReleased = _options.Hardware.SensorMessages.OutReleased
            }
        };
    }

    private static SerialPortOptions CopySerialSettings(
        SerialPortOptions source,
        string portName,
        int baudRate) =>
        new()
        {
            PortName = portName,
            ReadMode = string.IsNullOrWhiteSpace(source.ReadMode)
                ? "LineText"
                : source.ReadMode,
            BaudRate = baudRate,
            DataBits = source.DataBits,
            Parity = source.Parity,
            StopBits = source.StopBits
        };

    private static int ParseBaudRate(string value, string deviceName)
    {
        if (!int.TryParse(value.Trim(), out var baudRate) || baudRate <= 0)
        {
            throw new InvalidOperationException(
                $"Enter a valid baud rate for {deviceName}.");
        }

        return baudRate;
    }

    private void SimulationCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateControlAvailability();
    }

    private void BluetoothEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateControlAvailability();
    }

    private void WifiEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateControlAvailability();
    }

    private void UpdateControlAvailability()
    {
        var enabled = SimulationCheckBox.IsChecked != true;
        InPortComboBox.IsEnabled = enabled;
        OutPortComboBox.IsEnabled = enabled;
        ControlPortComboBox.IsEnabled = enabled;
        BluetoothEnabledCheckBox.IsEnabled = enabled;
        var bluetoothEnabled = enabled && BluetoothEnabledCheckBox.IsChecked == true;
        BluetoothPortComboBox.IsEnabled = bluetoothEnabled;
        BluetoothBaudRateComboBox.IsEnabled = bluetoothEnabled;
        WifiEnabledCheckBox.IsEnabled = enabled;
        var wifiEnabled = enabled && WifiEnabledCheckBox.IsChecked == true;
        WifiHostTextBox.IsEnabled = wifiEnabled;
        WifiPortTextBox.IsEnabled = wifiEnabled;
        InBaudRateComboBox.IsEnabled = enabled;
        OutBaudRateComboBox.IsEnabled = enabled;
        ControlBaudRateComboBox.IsEnabled = enabled;
    }

    private void ShowStatus(string message, bool success)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = success
            ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
            : new SolidColorBrush(Color.FromRgb(185, 28, 28));
        StatusBorder.Background = success
            ? new SolidColorBrush(Color.FromRgb(236, 253, 245))
            : new SolidColorBrush(Color.FromRgb(254, 242, 242));
        StatusBorder.BorderBrush = success
            ? new SolidColorBrush(Color.FromRgb(134, 239, 172))
            : new SolidColorBrush(Color.FromRgb(252, 165, 165));
    }

    private static void SetComboText(ComboBox comboBox, string value)
    {
        comboBox.Text = value;
    }
}
