using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess;

public partial class HardwareSettingsWindow : Window
{
    private static readonly int[] CommonBaudRates =
        [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    private readonly AppOptions _options;
    private readonly HardwareGateway _hardwareGateway;
    private readonly WeighbridgeService _weighbridge;
    private readonly AppConfigurationService _configurationService;

    public HardwareSettingsWindow(
        AppOptions options,
        HardwareGateway hardwareGateway,
        WeighbridgeService weighbridge,
        AppConfigurationService configurationService)
    {
        InitializeComponent();
        _options = options;
        _hardwareGateway = hardwareGateway;
        _weighbridge = weighbridge;
        _configurationService = configurationService;

        LoadBaudRates();
        RefreshPortLists();
        LoadCurrentSettings();
        ShowStatus("Ready. Weight bridge target controls the shared IN/OUT process.", true);
    }

    private void LoadBaudRates()
    {
        InBaudRateComboBox.ItemsSource = CommonBaudRates;
        OutBaudRateComboBox.ItemsSource = CommonBaudRates;
        ControlBaudRateComboBox.ItemsSource = CommonBaudRates;
        BluetoothBaudRateComboBox.ItemsSource = CommonBaudRates;
        WeightBaudRateComboBox.ItemsSource = CommonBaudRates;
    }

    private void LoadCurrentSettings()
    {
        SimulationCheckBox.IsChecked = _options.Hardware.SimulationEnabled;

        SetComboText(InPortComboBox, _options.Hardware.InRfid.PortName);
        SetComboText(OutPortComboBox, _options.Hardware.OutRfid.PortName);
        SetComboText(ControlPortComboBox, _options.Hardware.Control.PortName);
        SetComboText(BluetoothPortComboBox, _options.Hardware.BluetoothControl.PortName);
        BluetoothEnabledCheckBox.IsChecked = _options.Hardware.BluetoothControlEnabled;

        InBaudRateComboBox.Text = _options.Hardware.InRfid.BaudRate.ToString(CultureInfo.InvariantCulture);
        OutBaudRateComboBox.Text = _options.Hardware.OutRfid.BaudRate.ToString(CultureInfo.InvariantCulture);
        ControlBaudRateComboBox.Text = _options.Hardware.Control.BaudRate.ToString(CultureInfo.InvariantCulture);
        BluetoothBaudRateComboBox.Text = _options.Hardware.BluetoothControl.BaudRate.ToString(CultureInfo.InvariantCulture);

        SetComboText(WeightPortComboBox, _options.Weighbridge.Serial.PortName);
        WeightBaudRateComboBox.Text = _options.Weighbridge.Serial.BaudRate.ToString(CultureInfo.InvariantCulture);
        TargetWeightTextBox.Text = _options.Weighbridge.TargetWeightKg.ToString("0.###", CultureInfo.InvariantCulture);
        ResetWeightTextBox.Text = _options.Weighbridge.ResetWeightKg.ToString("0.###", CultureInfo.InvariantCulture);
        StableReadCountTextBox.Text = _options.Weighbridge.StableReadCount.ToString(CultureInfo.InvariantCulture);
        WeightPrefixTextBox.Text = _options.Weighbridge.DataPrefix;
        WeightUnitTextBox.Text = _options.Weighbridge.UnitText;
    }

    private void RefreshPorts_Click(object sender, RoutedEventArgs e)
    {
        var values = new[]
        {
            InPortComboBox.Text,
            OutPortComboBox.Text,
            ControlPortComboBox.Text,
            BluetoothPortComboBox.Text,
            WeightPortComboBox.Text
        };

        RefreshPortLists();
        SetComboText(InPortComboBox, values[0]);
        SetComboText(OutPortComboBox, values[1]);
        SetComboText(ControlPortComboBox, values[2]);
        SetComboText(BluetoothPortComboBox, values[3]);
        SetComboText(WeightPortComboBox, values[4]);
        ShowStatus("Windows COM port list refreshed.", true);
    }

    private void RefreshPortLists()
    {
        var ports = _hardwareGateway.GetAvailablePortNames();
        InPortComboBox.ItemsSource = ports;
        OutPortComboBox.ItemsSource = ports;
        ControlPortComboBox.ItemsSource = ports;
        BluetoothPortComboBox.ItemsSource = ports;
        WeightPortComboBox.ItemsSource = ports;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            var hardware = BuildHardwareSettings();
            var weighbridge = BuildWeighbridgeSettings();
            ValidateDistinctPorts(hardware, weighbridge);

            var hardwareResult = await _hardwareGateway.ApplySettingsAsync(hardware);
            var weightResult = await _weighbridge.ApplySettingsAsync(weighbridge);
            await _configurationService.SaveAsync();

            ShowStatus(
                $"Saved. Hardware: {hardwareResult.Message} | Weight bridge: {weightResult.Message}",
                hardwareResult.Success && weightResult.Success);

            if (hardwareResult.Success && weightResult.Success)
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

    private HardwareOptions BuildHardwareSettings()
    {
        var hardware = _options.Hardware;
        hardware.SimulationEnabled = SimulationCheckBox.IsChecked == true;
        hardware.InRfid.PortName = RequiredText(InPortComboBox.Text, "IN RFID COM");
        hardware.OutRfid.PortName = RequiredText(OutPortComboBox.Text, "OUT RFID COM");
        hardware.Control.PortName = RequiredText(ControlPortComboBox.Text, "Control COM");
        hardware.BluetoothControlEnabled = BluetoothEnabledCheckBox.IsChecked == true;
        hardware.BluetoothControl.PortName = BluetoothPortComboBox.Text.Trim();

        hardware.InRfid.BaudRate = ParsePositiveInt(InBaudRateComboBox.Text, "IN RFID baud rate");
        hardware.OutRfid.BaudRate = ParsePositiveInt(OutBaudRateComboBox.Text, "OUT RFID baud rate");
        hardware.Control.BaudRate = ParsePositiveInt(ControlBaudRateComboBox.Text, "Control baud rate");
        hardware.BluetoothControl.BaudRate = ParsePositiveInt(BluetoothBaudRateComboBox.Text, "Bluetooth baud rate");
        return hardware;
    }

    private WeighbridgeOptions BuildWeighbridgeSettings()
    {
        var target = ParseNonNegativeDecimal(TargetWeightTextBox.Text, "Target weight");
        var reset = ParseNonNegativeDecimal(ResetWeightTextBox.Text, "Reset weight");
        if (target <= 0m)
        {
            throw new InvalidOperationException("Target weight must be greater than 0 KG.");
        }
        if (reset >= target)
        {
            throw new InvalidOperationException("Reset weight must be lower than target weight.");
        }

        return new WeighbridgeOptions
        {
            Enabled = true,
            TargetWeightKg = target,
            ResetWeightKg = reset,
            StableReadCount = ParsePositiveInt(StableReadCountTextBox.Text, "Stable read count"),
            DataPrefix = RequiredText(WeightPrefixTextBox.Text, "Weight serial prefix"),
            UnitText = RequiredText(WeightUnitTextBox.Text, "Weight unit"),
            Serial = new SerialPortOptions
            {
                PortName = RequiredText(WeightPortComboBox.Text, "Weight bridge COM"),
                BaudRate = ParsePositiveInt(WeightBaudRateComboBox.Text, "Weight bridge baud rate"),
                DataBits = _options.Weighbridge.Serial.DataBits,
                Parity = _options.Weighbridge.Serial.Parity,
                StopBits = _options.Weighbridge.Serial.StopBits,
                ReadMode = "LineText"
            }
        };
    }

    private static void ValidateDistinctPorts(HardwareOptions hardware, WeighbridgeOptions weighbridge)
    {
        if (hardware.SimulationEnabled)
        {
            return;
        }

        var ports = new List<(string Name, string Port)>
        {
            ("IN RFID", hardware.InRfid.PortName),
            ("OUT RFID", hardware.OutRfid.PortName),
            ("Control", hardware.Control.PortName),
            ("Weight bridge", weighbridge.Serial.PortName)
        };
        if (hardware.BluetoothControlEnabled && !string.IsNullOrWhiteSpace(hardware.BluetoothControl.PortName))
        {
            ports.Add(("Bluetooth backup", hardware.BluetoothControl.PortName));
        }

        var duplicate = ports
            .GroupBy(item => item.Port.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"COM port {duplicate.Key} is assigned more than once: {string.Join(", ", duplicate.Select(x => x.Name))}.");
        }
    }

    private static int ParsePositiveInt(string text, string label) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"{label} must be a positive whole number.");

    private static decimal ParseNonNegativeDecimal(string text, string label) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
            ? value
            : throw new InvalidOperationException($"{label} must be a valid non-negative number.");

    private static string RequiredText(string? text, string label) =>
        !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : throw new InvalidOperationException($"{label} is required.");

    private static void SetComboText(ComboBox comboBox, string value) => comboBox.Text = value ?? string.Empty;

    private void ShowStatus(string message, bool success)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = success
            ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
            : new SolidColorBrush(Color.FromRgb(185, 28, 28));
    }
}
