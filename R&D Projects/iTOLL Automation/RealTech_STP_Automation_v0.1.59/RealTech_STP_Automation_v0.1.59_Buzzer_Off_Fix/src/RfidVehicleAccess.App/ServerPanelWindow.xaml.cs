using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class ServerPanelWindow : Window
{
    private static readonly JsonSerializerOptions CloneSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppOptions _options;
    private readonly AppConfigurationService _configurationService;
    private readonly HardwareGateway _hardwareGateway;
    private readonly CameraStreamService _cameraStreams;
    private readonly MainViewModel _viewModel;
    private readonly StorageCleanupWorker _storageCleanup;

    private readonly Dictionary<string, TextBox> _textFields =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checkFields =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComboBox> _comboFields =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PasswordBox> _passwordFields =
        new(StringComparer.Ordinal);
    private readonly ObservableCollection<VehicleApiSourceEditor> _apiSources = [];
    private DataGrid? _apiSourcesGrid;

    public ServerPanelWindow(
        AppOptions options,
        AppConfigurationService configurationService,
        HardwareGateway hardwareGateway,
        CameraStreamService cameraStreams,
        MainViewModel viewModel,
        StorageCleanupWorker storageCleanup)
    {
        InitializeComponent();
        ApplyResponsiveWindowBounds();
        _options = options;
        _configurationService = configurationService;
        _hardwareGateway = hardwareGateway;
        _cameraStreams = cameraStreams;
        _viewModel = viewModel;
        _storageCleanup = storageCleanup;

        BuildSettingsTabs();
        LoadCurrentSettings();
    }

    private void ApplyResponsiveWindowBounds()
    {
        const double outerMargin = 24;
        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(520, workArea.Width - outerMargin);
        var availableHeight = Math.Max(380, workArea.Height - outerMargin);

        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        Width = Math.Min(1040, availableWidth);
        Height = Math.Min(760, availableHeight);
    }

    private void BuildSettingsTabs()
    {
        SettingsTabs.Items.Add(CreateDeviceAndProcessingTab());
        SettingsTabs.Items.Add(CreateHardwareTab());
        SettingsTabs.Items.Add(CreateCamerasTab());
        SettingsTabs.Items.Add(CreateServerTab());
        SettingsTabs.Items.Add(CreateStorageConnectivityImportTab());
        SettingsTabs.Items.Add(CreateApprovalAndSecurityTab());
    }

    private TabItem CreateDeviceAndProcessingTab()
    {
        var device = CreateGroup("Device Identity", grid =>
        {
            AddTextField(grid, "Device.SiteId", "Site ID", "Site identifier included in every transaction.");
            AddTextField(grid, "Device.DeviceId", "Device ID", "Unique gate workstation identifier.");
            AddTextField(grid, "Device.LaneId", "Lane ID", "Logical lane or gate identifier.");
            AddTextField(grid, "Device.DeviceName", "Device Name", "Friendly name published to the server.");
        });

        var processing = CreateGroup("Trip Processing", grid =>
        {
            AddTextField(grid, "Processing.ProcessWaitSeconds", "Process Wait Seconds");
            AddTextField(grid, "Processing.BarrierAndGreenDelaySeconds", "Barrier / Green Delay Seconds");
            AddTextField(grid, "Processing.DuplicateReadSeconds", "Duplicate Read Window Seconds");
            AddTextField(grid, "Processing.SensorValiditySeconds", "Sensor Validity Seconds");
            AddTextField(grid, "Processing.VehicleDisplayResetSeconds", "Vehicle Display Reset Seconds");
            AddTextField(
                grid,
                "Processing.AllowedRfidPrefixes",
                "Allowed RFID Prefixes",
                "Enter one prefix per line.",
                multiline: true);
            AddCheckField(
                grid,
                "Processing.RfidPrefixValidationEnabled",
                "Validate RFID Prefix",
                "Reject RFID values that do not start with an allowed prefix.");
            AddCheckField(
                grid,
                "Processing.CaseSensitivePrefixes",
                "Case-sensitive Prefixes");
            AddComboField(grid, "Processing.ProcessingMode", "Processing Mode", ["OFFLINE_ONLY", "ONLINE_OFFLINE"]);
            AddComboField(grid, "Processing.OfflineBalanceMode", "Balance Support Mode", ["BOTH", "RFID", "CONTRACTOR"]);
            AddTextField(grid, "Processing.OnlineAuthorizationTimeoutSeconds", "Online Authorization Timeout Seconds");
        });

        return CreateTab("Device & Processing", device, processing);
    }

    private TabItem CreateHardwareTab()
    {
        var general = CreateGroup("Hardware General", grid =>
        {
            AddCheckField(
                grid,
                "Hardware.SimulationEnabled",
                "Simulation Mode",
                "When enabled, physical COM ports are not opened.");
            AddTextField(
                grid,
                "Hardware.LineTerminator",
                "Line Terminator",
                "Escape sequences such as \\r\\n are supported.");
            AddTextField(grid, "Hardware.ReconnectSeconds", "Reconnect Seconds");
        });

        var frontendIndicators = CreateGroup("Frontend (UI) Hardware Indicators", grid =>
        {
            AddCheckField(
                grid,
                "FrontendIndicators.RedEnabled",
                "Enable RED Indicator",
                "UI only. RED hardware commands are still sent when disabled.");
            AddCheckField(
                grid,
                "FrontendIndicators.GreenEnabled",
                "Enable GREEN Indicator",
                "UI only. GREEN hardware commands are still sent when disabled.");
            AddCheckField(
                grid,
                "FrontendIndicators.OrangeEnabled",
                "Enable ORANGE Indicator",
                "UI only. ORANGE hardware commands are still sent when disabled.");
            AddCheckField(
                grid,
                "FrontendIndicators.BuzzerEnabled",
                "Enable BUZZER Indicator",
                "UI only. BUZZER hardware commands are still sent when disabled.");
        });

        var inRfid = CreateSerialPortGroup("IN RFID Reader", "Hardware.InRfid");
        var outRfid = CreateSerialPortGroup("OUT RFID Reader", "Hardware.OutRfid");
        var control = CreateSerialPortGroup("Control Unit", "Hardware.Control");

        var sensors = CreateGroup("Sensor Messages", grid =>
        {
            AddTextField(grid, "Hardware.SensorMessages.InHigh", "IN Sensor HIGH Message");
            AddTextField(grid, "Hardware.SensorMessages.InReleased", "IN Sensor Released Message");
            AddTextField(grid, "Hardware.SensorMessages.OutHigh", "OUT Sensor HIGH Message");
            AddTextField(grid, "Hardware.SensorMessages.OutReleased", "OUT Sensor Released Message");
        });

        return CreateTab(
            "Hardware",
            general,
            frontendIndicators,
            inRfid,
            outRfid,
            control,
            sensors);
    }

    private GroupBox CreateSerialPortGroup(string title, string prefix)
    {
        return CreateGroup(title, grid =>
        {
            AddTextField(grid, $"{prefix}.PortName", "COM Port");
            AddComboField(
                grid,
                $"{prefix}.ReadMode",
                "Read Mode",
                ["UhfCfFrame", "LineText"]);
            AddTextField(grid, $"{prefix}.BaudRate", "Baud Rate");
            AddTextField(grid, $"{prefix}.DataBits", "Data Bits");
            AddComboField(
                grid,
                $"{prefix}.Parity",
                "Parity",
                ["None", "Odd", "Even", "Mark", "Space"]);
            AddComboField(
                grid,
                $"{prefix}.StopBits",
                "Stop Bits",
                ["None", "One", "Two", "OnePointFive"]);
        });
    }

    private TabItem CreateCamerasTab()
    {
        var general = CreateGroup("Camera Storage", grid =>
        {
            AddTextField(grid, "Cameras.LocalImageFolder", "Local Image Folder");
            AddTextField(grid, "Cameras.ImageFilePrefix", "Image File Prefix");
            AddTextField(grid, "Cameras.ImageTimestampFormat", "Image Timestamp Format");
        });

        var inCamera = CreateCameraGroup("IN Camera", "Cameras.In");
        var outCamera = CreateCameraGroup("OUT Camera", "Cameras.Out");
        return CreateTab("Cameras", general, inCamera, outCamera);
    }

    private GroupBox CreateCameraGroup(string title, string prefix)
    {
        return CreateGroup(title, grid =>
        {
            AddCheckField(grid, $"{prefix}.Enabled", "Camera Enabled");
            AddTextField(grid, $"{prefix}.RtspUrl", "RTSP URL");
            AddTextField(grid, $"{prefix}.SnapshotWidth", "Snapshot Width");
            AddTextField(grid, $"{prefix}.SnapshotHeight", "Snapshot Height");
            AddTextField(grid, $"{prefix}.NetworkCachingMilliseconds", "Network Cache Milliseconds");
            AddTextField(grid, $"{prefix}.ReconnectSeconds", "Reconnect Seconds");
            AddTextField(grid, $"{prefix}.StreamWatchdogSeconds", "Stream Watchdog Seconds");
            AddCheckField(grid, $"{prefix}.UseTcp", "Use RTSP over TCP");
            AddCheckField(grid, $"{prefix}.EnableHardwareDecoding", "Enable Hardware Decoding");
        });
    }

    private TabItem CreateServerTab()
    {
        var general = CreateGroup("Synchronization", grid =>
        {
            AddCheckField(
                grid,
                "Server.Enabled",
                "Enable Server Synchronization",
                "Local gate processing continues when synchronization is disabled.");
            AddTextField(grid, "Server.SyncIntervalSeconds", "Sync Interval Seconds");
            AddTextField(grid, "Server.MaxBatchSize", "Maximum Batch Size");
            AddTextField(grid, "Server.InitialRetryDelaySeconds", "Initial Retry Delay Seconds");
            AddTextField(grid, "Server.MaxRetryDelaySeconds", "Maximum Retry Delay Seconds");
        });

        var mqtt = CreateGroup("MQTT", grid =>
        {
            AddTextField(grid, "Server.Mqtt.BrokerHost", "Broker Host");
            AddTextField(grid, "Server.Mqtt.Port", "Port");
            AddCheckField(grid, "Server.Mqtt.UseTls", "Use TLS");
            AddTextField(grid, "Server.Mqtt.Username", "Username");
            AddPasswordField(grid, "Server.Mqtt.Password", "Password");
            AddTextField(grid, "Server.Mqtt.ClientId", "Client ID");
            AddTextField(grid, "Server.Mqtt.BaseTopic", "Base Topic");
            AddTextField(grid, "Server.Mqtt.PublishTopic", "Publish Topic");
            AddComboField(grid, "Server.Mqtt.QualityOfService", "Quality of Service", ["0", "1"]);
            AddCheckField(grid, "Server.Mqtt.Retain", "Retain Published Message");
            AddTextField(grid, "Server.Mqtt.KeepAliveSeconds", "Keep Alive Seconds");
            AddTextField(grid, "Server.Mqtt.ConnectTimeoutSeconds", "Connect Timeout Seconds");
            AddTextField(grid, "Server.Mqtt.PublishTimeoutSeconds", "Publish Timeout Seconds");
            AddCheckField(
                grid,
                "Server.Mqtt.RechargeSyncEnabled",
                "Enable RFID Recharge Subscription",
                "Receives server recharge commands and updates local Paid RFID balances.");
            AddTextField(grid, "Server.Mqtt.RechargeSubscribeTopic", "Recharge Subscribe Topic");
            AddTextField(grid, "Server.Mqtt.RechargeAckTopic", "Recharge Acknowledgement Topic");
            AddTextField(grid, "Server.Mqtt.RechargeSubscriberClientId", "Recharge Subscriber Client ID");
            AddTextField(grid, "Server.Mqtt.RechargeReconnectSeconds", "Recharge Reconnect Seconds");
            AddTextField(grid, "Server.Mqtt.TripAuthorizationRequestTopic", "Trip Authorization Request Topic");
            AddTextField(grid, "Server.Mqtt.TripAuthorizationResponseTopic", "Trip Authorization Response Topic");
        });

        var upload = CreateGroup("Image Upload", grid =>
        {
            AddComboField(grid, "Server.ImageUpload.Mode", "Mode", ["Disabled", "Sftp"]);
            AddTextField(grid, "Server.ImageUpload.Host", "SFTP Host");
            AddTextField(grid, "Server.ImageUpload.Port", "SFTP Port");
            AddTextField(grid, "Server.ImageUpload.Username", "Username");
            AddPasswordField(grid, "Server.ImageUpload.Password", "Password");
            AddTextField(grid, "Server.ImageUpload.RemoteDirectory", "Remote Directory");
            AddTextField(grid, "Server.ImageUpload.ConnectionTimeoutSeconds", "Connection Timeout Seconds");
            AddTextField(grid, "Server.ImageUpload.OperationTimeoutSeconds", "Operation Timeout Seconds");
            AddTextField(grid, "Server.ImageUpload.TotalTimeoutSeconds", "Total Upload Timeout Seconds");
        });

        return CreateTab("Server", general, mqtt, upload);
    }

    private TabItem CreateStorageConnectivityImportTab()
    {
        var storage = CreateGroup("Storage", grid =>
        {
            AddTextField(
                grid,
                "Storage.DatabaseFile",
                "Database File",
                "A database path change is used after restarting the application.");
            AddTextField(grid, "Storage.StatusLogFile", "Status Log File");
            AddTextField(grid, "Storage.ServerLogFile", "Server Log File");
            AddCheckField(
                grid,
                "Storage.AutoDeleteEnabled",
                "Enable Automatic Image / Log Deletion",
                "Runs at startup, after Save & Apply, and once every 24 hours.");
            AddTextField(
                grid,
                "Storage.ImageRetentionDays",
                "Image Retention Days",
                "Default 30. Image files older than this are deleted automatically.");
            AddTextField(
                grid,
                "Storage.LogRetentionDays",
                "Log Retention Days",
                "Default 30. Status and server log files older than this are deleted automatically.");
        });

        var connectivity = CreateGroup("Internet Connectivity", grid =>
        {
            AddTextField(grid, "Connectivity.CheckIntervalSeconds", "Check Interval Seconds");
            AddTextField(grid, "Connectivity.RequestTimeoutSeconds", "Request Timeout Seconds");
            AddTextField(
                grid,
                "Connectivity.CheckEndpoints",
                "Check Endpoints",
                "Enter one HTTP or HTTPS endpoint per line.",
                multiline: true);
        });

        var import = CreateGroup("Vehicle Import", grid =>
        {
            AddComboField(grid, "Import.DefaultAccessType", "Default Access Type", ["Free", "Paid"]);
            AddTextField(grid, "Import.DefaultOpeningBalance", "Default Opening Balance");
            AddCheckField(grid, "Import.UpdateExistingRecords", "Update Existing Records");
            AddCheckField(grid, "Import.EnforceRfidPrefixValidation", "Enforce RFID Prefix Validation");
            AddCheckField(grid, "Import.SkipInvalidRows", "Skip Invalid Rows");
            AddCheckField(
                grid,
                "Import.ApiAutoSyncEnabled",
                "Enable Vehicle API Auto Sync",
                "Synchronizes all enabled Vehicle CSV API sources automatically.");
            AddTextField(
                grid,
                "Import.ApiAutoSyncIntervalSeconds",
                "Vehicle API Auto Sync Interval Seconds",
                "Default 30. This is how often a new sync cycle starts. It is separate from each API timeout.");
        });

        var apiSources = CreateVehicleApiSourcesGroup();
        return CreateTab("Storage & Import", storage, connectivity, import, apiSources);
    }

    private GroupBox CreateVehicleApiSourcesGroup()
    {
        _apiSourcesGrid = new DataGrid
        {
            ItemsSource = _apiSources,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            Height = 280,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _apiSourcesGrid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Enabled",
            Width = 72,
            Binding = new Binding(nameof(VehicleApiSourceEditor.Enabled))
        });
        _apiSourcesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Priority",
            Width = 72,
            Binding = new Binding(nameof(VehicleApiSourceEditor.Priority))
        });
        _apiSourcesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Source Name",
            Width = 160,
            Binding = new Binding(nameof(VehicleApiSourceEditor.Name))
        });
        _apiSourcesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Endpoint",
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(VehicleApiSourceEditor.Endpoint))
        });
        _apiSourcesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Username",
            Width = 150,
            Binding = new Binding(nameof(VehicleApiSourceEditor.Username))
        });
        _apiSourcesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Timeout (s)",
            Width = 92,
            Binding = new Binding(nameof(VehicleApiSourceEditor.RequestTimeoutSeconds))
        });

        var addButton = new Button
        {
            Content = "Add API",
            MinWidth = 100,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        addButton.Click += (_, _) => AddApiSourceRow();

        var deleteButton = new Button
        {
            Content = "Delete Selected API",
            MinWidth = 150,
            Padding = new Thickness(12, 6, 12, 6)
        };
        deleteButton.Click += (_, _) => DeleteSelectedApiSourceRow();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        buttons.Children.Add(addButton);
        buttons.Children.Add(deleteButton);

        var description = new TextBlock
        {
            Text = "Each enabled source receives POST JSON: { \"username\": \"value\" }. " +
                   "Priority 1 is highest. Lower-priority sources are imported first, so the highest-priority source wins conflicting values.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var panel = new StackPanel();
        panel.Children.Add(description);
        panel.Children.Add(_apiSourcesGrid);
        panel.Children.Add(buttons);

        return new GroupBox
        {
            Header = "Vehicle CSV API Sources",
            Margin = new Thickness(4, 5, 4, 8),
            Padding = new Thickness(10),
            Content = panel
        };
    }

    private void AddApiSourceRow()
    {
        var nextPriority = _apiSources.Count == 0
            ? 1
            : _apiSources
                .Select(source => int.TryParse(
                    source.Priority,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var priority)
                    ? priority
                    : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

        var source = new VehicleApiSourceEditor
        {
            Enabled = true,
            Priority = nextPriority.ToString(CultureInfo.InvariantCulture),
            Name = $"Vehicle CSV API {_apiSources.Count + 1}",
            RequestTimeoutSeconds = "30"
        };
        _apiSources.Add(source);
        _apiSourcesGrid?.ScrollIntoView(source);
        if (_apiSourcesGrid is not null)
        {
            _apiSourcesGrid.SelectedItem = source;
        }
    }

    private void DeleteSelectedApiSourceRow()
    {
        if (_apiSourcesGrid?.SelectedItem is VehicleApiSourceEditor selected)
        {
            _apiSources.Remove(selected);
        }
    }

    private TabItem CreateApprovalAndSecurityTab()
    {
        var exceptional = CreateGroup("Exceptional Approval", grid =>
        {
            AddCheckField(
                grid,
                "ExceptionalApproval.InEnabled",
                "Enable IN Exceptional Approval Popup");
            AddCheckField(
                grid,
                "ExceptionalApproval.OutEnabled",
                "Enable OUT Exceptional Approval Popup");
        });

        var automatic = CreateGroup("Auto Approval", grid =>
        {
            AddCheckField(
                grid,
                "AutoApproval.InEnabled",
                "Auto Approve IN Missing OUT");
            AddCheckField(
                grid,
                "AutoApproval.OutEnabled",
                "Auto Approve OUT Missing IN");
        });

        var security = CreateGroup("Panel Passwords", grid =>
        {
            AddPasswordField(
                grid,
                "Security.AdminControlsPassword",
                "Admin Controls Password");
            AddPasswordField(
                grid,
                "Security.ServerPanelPassword",
                "Server Panel Password",
                "Default password: rts123!@#");
        });

        return CreateTab("Approval & Security", exceptional, automatic, security);
    }

    private static TabItem CreateTab(string header, params GroupBox[] groups)
    {
        var stackPanel = new StackPanel
        {
            Margin = new Thickness(8)
        };

        foreach (var group in groups)
        {
            stackPanel.Children.Add(group);
        }

        return new TabItem
        {
            Header = header,
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = stackPanel
            }
        };
    }

    private static GroupBox CreateGroup(string header, Action<Grid> buildRows)
    {
        var grid = new Grid
        {
            Margin = new Thickness(2)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(2, GridUnitType.Star),
            MinWidth = 170,
            MaxWidth = 260
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(5, GridUnitType.Star),
            MinWidth = 250
        });

        buildRows(grid);

        return new GroupBox
        {
            Header = header,
            Margin = new Thickness(4, 5, 4, 8),
            Padding = new Thickness(10),
            Content = grid
        };
    }

    private void AddTextField(
        Grid grid,
        string key,
        string label,
        string? description = null,
        bool multiline = false)
    {
        var textBox = new TextBox
        {
            MinHeight = multiline ? 82 : 34,
            Margin = new Thickness(4),
            Padding = new Thickness(7, 5, 7, 5),
            VerticalContentAlignment = multiline
                ? VerticalAlignment.Top
                : VerticalAlignment.Center,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = multiline
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled
        };

        _textFields.Add(key, textBox);
        AddEditorRow(grid, label, description, textBox);
    }

    private void AddPasswordField(
        Grid grid,
        string key,
        string label,
        string? description = null)
    {
        var passwordBox = new PasswordBox
        {
            MinHeight = 34,
            Margin = new Thickness(4),
            Padding = new Thickness(7, 5, 7, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            PasswordChar = '●'
        };

        _passwordFields.Add(key, passwordBox);
        AddEditorRow(grid, label, description, passwordBox);
    }

    private void AddCheckField(
        Grid grid,
        string key,
        string label,
        string? description = null)
    {
        var checkBox = new CheckBox
        {
            Margin = new Thickness(8, 8, 4, 8),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        _checkFields.Add(key, checkBox);
        AddEditorRow(grid, label, description, checkBox);
    }

    private void AddComboField(
        Grid grid,
        string key,
        string label,
        IEnumerable<string> values,
        string? description = null)
    {
        var comboBox = new ComboBox
        {
            MinHeight = 34,
            Margin = new Thickness(4),
            Padding = new Thickness(7, 4, 7, 4),
            IsEditable = true,
            IsTextSearchEnabled = true,
            ItemsSource = values.ToArray()
        };

        _comboFields.Add(key, comboBox);
        AddEditorRow(grid, label, description, comboBox);
    }

    private static void AddEditorRow(
        Grid grid,
        string label,
        string? description,
        FrameworkElement editor)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        var labelPanel = new StackPanel
        {
            Margin = new Thickness(4, 7, 10, 7),
            VerticalAlignment = VerticalAlignment.Center
        };
        labelPanel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            labelPanel.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetRow(labelPanel, row);
        Grid.SetColumn(labelPanel, 0);
        grid.Children.Add(labelPanel);

        Grid.SetRow(editor, row);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
    }

    private void LoadCurrentSettings()
    {
        SetText("Device.SiteId", _options.Device.SiteId);
        SetText("Device.DeviceId", _options.Device.DeviceId);
        SetText("Device.LaneId", _options.Device.LaneId);
        SetText("Device.DeviceName", _options.Device.DeviceName);

        SetText("Processing.ProcessWaitSeconds", _options.Processing.ProcessWaitSeconds);
        SetText("Processing.BarrierAndGreenDelaySeconds", _options.Processing.BarrierAndGreenDelaySeconds);
        SetText("Processing.DuplicateReadSeconds", _options.Processing.DuplicateReadSeconds);
        SetText("Processing.SensorValiditySeconds", _options.Processing.SensorValiditySeconds);
        SetText("Processing.VehicleDisplayResetSeconds", _options.Processing.VehicleDisplayResetSeconds);
        SetText("Processing.AllowedRfidPrefixes", string.Join(Environment.NewLine, _options.Processing.AllowedRfidPrefixes));
        SetCheck("Processing.RfidPrefixValidationEnabled", _options.Processing.RfidPrefixValidationEnabled);
        SetCheck("Processing.CaseSensitivePrefixes", _options.Processing.CaseSensitivePrefixes);
        SetCombo("Processing.ProcessingMode", _options.Processing.ProcessingMode);
        SetCombo("Processing.OfflineBalanceMode", _options.Processing.OfflineBalanceMode);
        SetText("Processing.OnlineAuthorizationTimeoutSeconds", _options.Processing.OnlineAuthorizationTimeoutSeconds);

        SetCheck("Hardware.SimulationEnabled", _options.Hardware.SimulationEnabled);
        SetText("Hardware.LineTerminator", _options.Hardware.LineTerminator);
        SetText("Hardware.ReconnectSeconds", _options.Hardware.ReconnectSeconds);
        LoadSerialPort("Hardware.InRfid", _options.Hardware.InRfid);
        LoadSerialPort("Hardware.OutRfid", _options.Hardware.OutRfid);
        LoadSerialPort("Hardware.Control", _options.Hardware.Control);
        SetText("Hardware.SensorMessages.InHigh", _options.Hardware.SensorMessages.InHigh);
        SetText("Hardware.SensorMessages.InReleased", _options.Hardware.SensorMessages.InReleased);
        SetText("Hardware.SensorMessages.OutHigh", _options.Hardware.SensorMessages.OutHigh);
        SetText("Hardware.SensorMessages.OutReleased", _options.Hardware.SensorMessages.OutReleased);
        SetCheck("FrontendIndicators.RedEnabled", _options.FrontendIndicators.RedEnabled);
        SetCheck("FrontendIndicators.GreenEnabled", _options.FrontendIndicators.GreenEnabled);
        SetCheck("FrontendIndicators.OrangeEnabled", _options.FrontendIndicators.OrangeEnabled);
        SetCheck("FrontendIndicators.BuzzerEnabled", _options.FrontendIndicators.BuzzerEnabled);

        SetText("Cameras.LocalImageFolder", _options.Cameras.LocalImageFolder);
        SetText("Cameras.ImageFilePrefix", _options.Cameras.ImageFilePrefix);
        SetText("Cameras.ImageTimestampFormat", _options.Cameras.ImageTimestampFormat);
        LoadCamera("Cameras.In", _options.Cameras.In);
        LoadCamera("Cameras.Out", _options.Cameras.Out);

        SetCheck("Server.Enabled", _options.Server.Enabled);
        SetText("Server.SyncIntervalSeconds", _options.Server.SyncIntervalSeconds);
        SetText("Server.MaxBatchSize", _options.Server.MaxBatchSize);
        SetText("Server.InitialRetryDelaySeconds", _options.Server.InitialRetryDelaySeconds);
        SetText("Server.MaxRetryDelaySeconds", _options.Server.MaxRetryDelaySeconds);
        SetText("Server.Mqtt.BrokerHost", _options.Server.Mqtt.BrokerHost);
        SetText("Server.Mqtt.Port", _options.Server.Mqtt.Port);
        SetCheck("Server.Mqtt.UseTls", _options.Server.Mqtt.UseTls);
        SetText("Server.Mqtt.Username", _options.Server.Mqtt.Username);
        SetPassword("Server.Mqtt.Password", _options.Server.Mqtt.Password);
        SetText("Server.Mqtt.ClientId", _options.Server.Mqtt.ClientId);
        SetText("Server.Mqtt.BaseTopic", _options.Server.Mqtt.BaseTopic);
        SetText("Server.Mqtt.PublishTopic", _options.Server.Mqtt.PublishTopic);
        SetCombo("Server.Mqtt.QualityOfService", _options.Server.Mqtt.QualityOfService.ToString(CultureInfo.InvariantCulture));
        SetCheck("Server.Mqtt.Retain", _options.Server.Mqtt.Retain);
        SetText("Server.Mqtt.KeepAliveSeconds", _options.Server.Mqtt.KeepAliveSeconds);
        SetText("Server.Mqtt.ConnectTimeoutSeconds", _options.Server.Mqtt.ConnectTimeoutSeconds);
        SetText("Server.Mqtt.PublishTimeoutSeconds", _options.Server.Mqtt.PublishTimeoutSeconds);
        SetCheck("Server.Mqtt.RechargeSyncEnabled", _options.Server.Mqtt.RechargeSyncEnabled);
        SetText("Server.Mqtt.RechargeSubscribeTopic", _options.Server.Mqtt.RechargeSubscribeTopic);
        SetText("Server.Mqtt.RechargeAckTopic", _options.Server.Mqtt.RechargeAckTopic);
        SetText("Server.Mqtt.RechargeSubscriberClientId", _options.Server.Mqtt.RechargeSubscriberClientId);
        SetText("Server.Mqtt.RechargeReconnectSeconds", _options.Server.Mqtt.RechargeReconnectSeconds);
        SetText("Server.Mqtt.TripAuthorizationRequestTopic", _options.Server.Mqtt.TripAuthorizationRequestTopic);
        SetText("Server.Mqtt.TripAuthorizationResponseTopic", _options.Server.Mqtt.TripAuthorizationResponseTopic);

        SetCombo("Server.ImageUpload.Mode", _options.Server.ImageUpload.Mode);
        SetText("Server.ImageUpload.Host", _options.Server.ImageUpload.Host);
        SetText("Server.ImageUpload.Port", _options.Server.ImageUpload.Port);
        SetText("Server.ImageUpload.Username", _options.Server.ImageUpload.Username);
        SetPassword("Server.ImageUpload.Password", _options.Server.ImageUpload.Password);
        SetText("Server.ImageUpload.RemoteDirectory", _options.Server.ImageUpload.RemoteDirectory);
        SetText("Server.ImageUpload.ConnectionTimeoutSeconds", _options.Server.ImageUpload.ConnectionTimeoutSeconds);
        SetText("Server.ImageUpload.OperationTimeoutSeconds", _options.Server.ImageUpload.OperationTimeoutSeconds);
        SetText("Server.ImageUpload.TotalTimeoutSeconds", _options.Server.ImageUpload.TotalTimeoutSeconds);

        SetText("Storage.DatabaseFile", _options.Storage.DatabaseFile);
        SetText("Storage.StatusLogFile", _options.Storage.StatusLogFile);
        SetText("Storage.ServerLogFile", _options.Storage.ServerLogFile);
        SetCheck("Storage.AutoDeleteEnabled", _options.Storage.AutoDeleteEnabled);
        SetText("Storage.ImageRetentionDays", _options.Storage.ImageRetentionDays);
        SetText("Storage.LogRetentionDays", _options.Storage.LogRetentionDays);

        SetText("Connectivity.CheckIntervalSeconds", _options.Connectivity.CheckIntervalSeconds);
        SetText("Connectivity.RequestTimeoutSeconds", _options.Connectivity.RequestTimeoutSeconds);
        SetText("Connectivity.CheckEndpoints", string.Join(Environment.NewLine, _options.Connectivity.CheckEndpoints));

        SetCombo("Import.DefaultAccessType", _options.Import.DefaultAccessType);
        SetText("Import.DefaultOpeningBalance", FormatDecimal(_options.Import.DefaultOpeningBalance));
        SetCheck("Import.UpdateExistingRecords", _options.Import.UpdateExistingRecords);
        SetCheck("Import.EnforceRfidPrefixValidation", _options.Import.EnforceRfidPrefixValidation);
        SetCheck("Import.SkipInvalidRows", _options.Import.SkipInvalidRows);
        SetCheck("Import.ApiAutoSyncEnabled", _options.Import.ApiAutoSyncEnabled);
        SetText(
            "Import.ApiAutoSyncIntervalSeconds",
            _options.Import.ApiAutoSyncIntervalSeconds);

        _apiSources.Clear();
        foreach (var source in _options.Import.ApiSources
                     .OrderBy(item => item.Priority)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            _apiSources.Add(new VehicleApiSourceEditor
            {
                Name = source.Name,
                Enabled = source.Enabled,
                Priority = source.Priority.ToString(CultureInfo.InvariantCulture),
                Endpoint = source.Endpoint,
                Username = source.Username,
                RequestTimeoutSeconds = source.RequestTimeoutSeconds.ToString(
                    CultureInfo.InvariantCulture)
            });
        }

        SetCheck("ExceptionalApproval.InEnabled", _options.ExceptionalApproval.InEnabled);
        SetCheck("ExceptionalApproval.OutEnabled", _options.ExceptionalApproval.OutEnabled);
        SetCheck("AutoApproval.InEnabled", _options.AutoApproval.InEnabled);
        SetCheck("AutoApproval.OutEnabled", _options.AutoApproval.OutEnabled);
        SetPassword("Security.AdminControlsPassword", _options.Security.AdminControlsPassword);
        SetPassword("Security.ServerPanelPassword", _options.Security.ServerPanelPassword);
    }

    private void LoadSerialPort(string prefix, SerialPortOptions settings)
    {
        SetText($"{prefix}.PortName", settings.PortName);
        SetCombo($"{prefix}.ReadMode", settings.ReadMode);
        SetText($"{prefix}.BaudRate", settings.BaudRate);
        SetText($"{prefix}.DataBits", settings.DataBits);
        SetCombo($"{prefix}.Parity", settings.Parity);
        SetCombo($"{prefix}.StopBits", settings.StopBits);
    }

    private void LoadCamera(string prefix, CameraLaneOptions settings)
    {
        SetCheck($"{prefix}.Enabled", settings.Enabled);
        SetText($"{prefix}.RtspUrl", settings.RtspUrl);
        SetText($"{prefix}.SnapshotWidth", settings.SnapshotWidth);
        SetText($"{prefix}.SnapshotHeight", settings.SnapshotHeight);
        SetText($"{prefix}.NetworkCachingMilliseconds", settings.NetworkCachingMilliseconds);
        SetText($"{prefix}.ReconnectSeconds", settings.ReconnectSeconds);
        SetText($"{prefix}.StreamWatchdogSeconds", settings.StreamWatchdogSeconds);
        SetCheck($"{prefix}.UseTcp", settings.UseTcp);
        SetCheck($"{prefix}.EnableHardwareDecoding", settings.EnableHardwareDecoding);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        var original = CloneOptions(_options);

        try
        {
            _apiSourcesGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
            _apiSourcesGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            var updated = BuildOptionsFromFields();
            ValidateOptions(updated);

            ApplyNonHardwareOptions(updated, _options);
            var hardwareResult = await _hardwareGateway.ApplySettingsAsync(updated.Hardware);
            await _configurationService.SaveAsync();

            _cameraStreams.ReloadConfiguration();
            _viewModel.RefreshExceptionalApprovalSettings();
            _storageCleanup.RequestCleanup();

            var databaseRestartRequired = !string.Equals(
                original.Storage.DatabaseFile,
                updated.Storage.DatabaseFile,
                StringComparison.OrdinalIgnoreCase);

            var message = hardwareResult.Success
                ? "Configuration saved and applied successfully."
                : $"Configuration saved. Hardware connection warning: {hardwareResult.Message}";

            if (databaseRestartRequired)
            {
                message += " Restart the application to use the new database file.";
            }

            ShowStatus(message, hardwareResult.Success);
            MessageBox.Show(
                message,
                "Server Panel",
                MessageBoxButton.OK,
                hardwareResult.Success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ApplyNonHardwareOptions(original, _options);

            try
            {
                await _hardwareGateway.ApplySettingsAsync(original.Hardware);
                _cameraStreams.ReloadConfiguration();
                _viewModel.RefreshExceptionalApprovalSettings();
            }
            catch
            {
                // Keep the original save error as the actionable message.
            }

            ShowStatus(ex.Message, false);
            MessageBox.Show(
                ex.Message,
                "Unable to Save Server Panel Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        LoadCurrentSettings();
        ShowStatus("Current in-memory configuration values reloaded.", true);
    }

    private AppOptions BuildOptionsFromFields()
    {
        var updated = CloneOptions(_options);

        updated.Device.SiteId = ReadRequiredText("Device.SiteId", "Site ID");
        updated.Device.DeviceId = ReadRequiredText("Device.DeviceId", "Device ID");
        updated.Device.LaneId = ReadRequiredText("Device.LaneId", "Lane ID");
        updated.Device.DeviceName = ReadRequiredText("Device.DeviceName", "Device Name");

        updated.Processing.ProcessWaitSeconds = ReadInt("Processing.ProcessWaitSeconds", "Process Wait Seconds", 0);
        updated.Processing.BarrierAndGreenDelaySeconds = ReadInt("Processing.BarrierAndGreenDelaySeconds", "Barrier / Green Delay Seconds", 0);
        updated.Processing.DuplicateReadSeconds = ReadInt("Processing.DuplicateReadSeconds", "Duplicate Read Window Seconds", 0);
        updated.Processing.SensorValiditySeconds = ReadInt("Processing.SensorValiditySeconds", "Sensor Validity Seconds", 1);
        updated.Processing.VehicleDisplayResetSeconds = ReadInt("Processing.VehicleDisplayResetSeconds", "Vehicle Display Reset Seconds", 1);
        updated.Processing.AllowedRfidPrefixes = ReadLines("Processing.AllowedRfidPrefixes");
        updated.Processing.RfidPrefixValidationEnabled = ReadCheck("Processing.RfidPrefixValidationEnabled");
        updated.Processing.CaseSensitivePrefixes = ReadCheck("Processing.CaseSensitivePrefixes");
        updated.Processing.ProcessingMode = ReadCombo("Processing.ProcessingMode");
        updated.Processing.OfflineBalanceMode = ReadCombo("Processing.OfflineBalanceMode");
        updated.Processing.OnlineAuthorizationTimeoutSeconds = ReadInt("Processing.OnlineAuthorizationTimeoutSeconds", "Online Authorization Timeout Seconds", 2, 120);

        updated.Hardware.SimulationEnabled = ReadCheck("Hardware.SimulationEnabled");
        updated.Hardware.LineTerminator = ReadRequiredText("Hardware.LineTerminator", "Line Terminator");
        updated.Hardware.ReconnectSeconds = ReadInt("Hardware.ReconnectSeconds", "Hardware Reconnect Seconds", 1);
        updated.Hardware.InRfid = ReadSerialPort("Hardware.InRfid", "IN RFID Reader");
        updated.Hardware.OutRfid = ReadSerialPort("Hardware.OutRfid", "OUT RFID Reader");
        updated.Hardware.Control = ReadSerialPort("Hardware.Control", "Control Unit");
        updated.Hardware.SensorMessages.InHigh = ReadRequiredText("Hardware.SensorMessages.InHigh", "IN Sensor HIGH Message");
        updated.Hardware.SensorMessages.InReleased = ReadRequiredText("Hardware.SensorMessages.InReleased", "IN Sensor Released Message");
        updated.Hardware.SensorMessages.OutHigh = ReadRequiredText("Hardware.SensorMessages.OutHigh", "OUT Sensor HIGH Message");
        updated.Hardware.SensorMessages.OutReleased = ReadRequiredText("Hardware.SensorMessages.OutReleased", "OUT Sensor Released Message");
        updated.FrontendIndicators.RedEnabled = ReadCheck("FrontendIndicators.RedEnabled");
        updated.FrontendIndicators.GreenEnabled = ReadCheck("FrontendIndicators.GreenEnabled");
        updated.FrontendIndicators.OrangeEnabled = ReadCheck("FrontendIndicators.OrangeEnabled");
        updated.FrontendIndicators.BuzzerEnabled = ReadCheck("FrontendIndicators.BuzzerEnabled");

        updated.Cameras.LocalImageFolder = ReadRequiredText("Cameras.LocalImageFolder", "Local Image Folder");
        updated.Cameras.ImageFilePrefix = ReadRequiredText("Cameras.ImageFilePrefix", "Image File Prefix");
        updated.Cameras.ImageTimestampFormat = ReadRequiredText("Cameras.ImageTimestampFormat", "Image Timestamp Format");
        updated.Cameras.In = ReadCamera("Cameras.In", "IN Camera");
        updated.Cameras.Out = ReadCamera("Cameras.Out", "OUT Camera");

        updated.Server.Enabled = ReadCheck("Server.Enabled");
        updated.Server.SyncIntervalSeconds = ReadInt("Server.SyncIntervalSeconds", "Sync Interval Seconds", 1);
        updated.Server.MaxBatchSize = ReadInt("Server.MaxBatchSize", "Maximum Batch Size", 1);
        updated.Server.InitialRetryDelaySeconds = ReadInt(
            "Server.InitialRetryDelaySeconds",
            "Initial Retry Delay Seconds",
            1);
        updated.Server.MaxRetryDelaySeconds = ReadInt(
            "Server.MaxRetryDelaySeconds",
            "Maximum Retry Delay Seconds",
            updated.Server.InitialRetryDelaySeconds);
        updated.Server.Mqtt.BrokerHost = ReadText("Server.Mqtt.BrokerHost");
        updated.Server.Mqtt.Port = ReadInt("Server.Mqtt.Port", "MQTT Port", 1, 65535);
        updated.Server.Mqtt.UseTls = ReadCheck("Server.Mqtt.UseTls");
        updated.Server.Mqtt.Username = ReadText("Server.Mqtt.Username");
        updated.Server.Mqtt.Password = ReadPassword("Server.Mqtt.Password");
        updated.Server.Mqtt.ClientId = ReadText("Server.Mqtt.ClientId");
        updated.Server.Mqtt.BaseTopic = ReadText("Server.Mqtt.BaseTopic");
        updated.Server.Mqtt.PublishTopic = ReadText("Server.Mqtt.PublishTopic");
        updated.Server.Mqtt.QualityOfService = ReadComboInt("Server.Mqtt.QualityOfService", "MQTT Quality of Service", 0, 1);
        updated.Server.Mqtt.Retain = ReadCheck("Server.Mqtt.Retain");
        updated.Server.Mqtt.KeepAliveSeconds = ReadInt("Server.Mqtt.KeepAliveSeconds", "MQTT Keep Alive Seconds", 0);
        updated.Server.Mqtt.ConnectTimeoutSeconds = ReadInt("Server.Mqtt.ConnectTimeoutSeconds", "MQTT Connect Timeout Seconds", 1);
        updated.Server.Mqtt.PublishTimeoutSeconds = ReadInt("Server.Mqtt.PublishTimeoutSeconds", "MQTT Publish Timeout Seconds", 1);
        updated.Server.Mqtt.RechargeSyncEnabled = ReadCheck("Server.Mqtt.RechargeSyncEnabled");
        updated.Server.Mqtt.RechargeSubscribeTopic = ReadText("Server.Mqtt.RechargeSubscribeTopic");
        updated.Server.Mqtt.RechargeAckTopic = ReadText("Server.Mqtt.RechargeAckTopic");
        updated.Server.Mqtt.RechargeSubscriberClientId = ReadText("Server.Mqtt.RechargeSubscriberClientId");
        updated.Server.Mqtt.RechargeReconnectSeconds = ReadInt(
            "Server.Mqtt.RechargeReconnectSeconds",
            "MQTT Recharge Reconnect Seconds",
            1);
        updated.Server.Mqtt.TripAuthorizationRequestTopic = ReadText("Server.Mqtt.TripAuthorizationRequestTopic");
        updated.Server.Mqtt.TripAuthorizationResponseTopic = ReadText("Server.Mqtt.TripAuthorizationResponseTopic");

        updated.Server.ImageUpload.Mode = ReadCombo("Server.ImageUpload.Mode");
        updated.Server.ImageUpload.Host = ReadText("Server.ImageUpload.Host");
        updated.Server.ImageUpload.Port = ReadInt("Server.ImageUpload.Port", "SFTP Port", 1, 65535);
        updated.Server.ImageUpload.Username = ReadText("Server.ImageUpload.Username");
        updated.Server.ImageUpload.Password = ReadPassword("Server.ImageUpload.Password");
        updated.Server.ImageUpload.RemoteDirectory = ReadText("Server.ImageUpload.RemoteDirectory");
        updated.Server.ImageUpload.ConnectionTimeoutSeconds = ReadInt("Server.ImageUpload.ConnectionTimeoutSeconds", "SFTP Connection Timeout Seconds", 1);
        updated.Server.ImageUpload.OperationTimeoutSeconds = ReadInt("Server.ImageUpload.OperationTimeoutSeconds", "SFTP Operation Timeout Seconds", 1);
        updated.Server.ImageUpload.TotalTimeoutSeconds = ReadInt(
            "Server.ImageUpload.TotalTimeoutSeconds",
            "SFTP Total Upload Timeout Seconds",
            1);

        updated.Storage.DatabaseFile = ReadRequiredText("Storage.DatabaseFile", "Database File");
        updated.Storage.StatusLogFile = ReadRequiredText("Storage.StatusLogFile", "Status Log File");
        updated.Storage.ServerLogFile = ReadRequiredText("Storage.ServerLogFile", "Server Log File");
        updated.Storage.AutoDeleteEnabled = ReadCheck("Storage.AutoDeleteEnabled");
        updated.Storage.ImageRetentionDays = ReadInt(
            "Storage.ImageRetentionDays",
            "Image Retention Days",
            1,
            3650);
        updated.Storage.LogRetentionDays = ReadInt(
            "Storage.LogRetentionDays",
            "Log Retention Days",
            1,
            3650);

        updated.Connectivity.CheckIntervalSeconds = ReadInt("Connectivity.CheckIntervalSeconds", "Connectivity Check Interval Seconds", 1);
        updated.Connectivity.RequestTimeoutSeconds = ReadInt("Connectivity.RequestTimeoutSeconds", "Connectivity Request Timeout Seconds", 1);
        updated.Connectivity.CheckEndpoints = ReadLines("Connectivity.CheckEndpoints");

        updated.Import.DefaultAccessType = ReadCombo("Import.DefaultAccessType");
        updated.Import.DefaultOpeningBalance = ReadDecimal("Import.DefaultOpeningBalance", "Default Opening Balance", 0m);
        updated.Import.UpdateExistingRecords = ReadCheck("Import.UpdateExistingRecords");
        updated.Import.EnforceRfidPrefixValidation = ReadCheck("Import.EnforceRfidPrefixValidation");
        updated.Import.SkipInvalidRows = ReadCheck("Import.SkipInvalidRows");
        updated.Import.ApiAutoSyncEnabled = ReadCheck("Import.ApiAutoSyncEnabled");
        updated.Import.ApiAutoSyncIntervalSeconds = ReadInt(
            "Import.ApiAutoSyncIntervalSeconds",
            "Vehicle API Auto Sync Interval Seconds",
            1,
            86400);
        updated.Import.ApiSources = ReadApiSources();

        var primaryApiSource = updated.Import.ApiSources
            .Where(source => source.Enabled)
            .OrderBy(source => source.Priority)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? updated.Import.ApiSources
                .OrderBy(source => source.Priority)
                .FirstOrDefault();
        if (primaryApiSource is not null)
        {
            updated.Import.ApiEndpoint = primaryApiSource.Endpoint;
            updated.Import.ApiUsername = primaryApiSource.Username;
            updated.Import.ApiRequestTimeoutSeconds =
                primaryApiSource.RequestTimeoutSeconds;
        }

        updated.ExceptionalApproval.InEnabled = ReadCheck("ExceptionalApproval.InEnabled");
        updated.ExceptionalApproval.OutEnabled = ReadCheck("ExceptionalApproval.OutEnabled");
        updated.AutoApproval.InEnabled = ReadCheck("AutoApproval.InEnabled");
        updated.AutoApproval.OutEnabled = ReadCheck("AutoApproval.OutEnabled");
        updated.Security.AdminControlsPassword = ReadRequiredPassword(
            "Security.AdminControlsPassword",
            "Admin Controls Password");
        updated.Security.ServerPanelPassword = ReadRequiredPassword(
            "Security.ServerPanelPassword",
            "Server Panel Password");

        return updated;
    }

    private List<VehicleApiSourceOptions> ReadApiSources()
    {
        _apiSourcesGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
        _apiSourcesGrid?.CommitEdit(DataGridEditingUnit.Row, true);

        var sources = new List<VehicleApiSourceOptions>(_apiSources.Count);
        for (var index = 0; index < _apiSources.Count; index++)
        {
            var editor = _apiSources[index];
            var displayRow = index + 1;
            var name = editor.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"Vehicle CSV API row {displayRow}: Source Name is required.");
            }

            if (!int.TryParse(
                    editor.Priority,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var priority) ||
                priority is < 1 or > 10000)
            {
                throw new InvalidOperationException(
                    $"Vehicle CSV API '{name}': Priority must be a whole number between 1 and 10000.");
            }

            if (!int.TryParse(
                    editor.RequestTimeoutSeconds,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var timeoutSeconds) ||
                timeoutSeconds is < 1 or > 300)
            {
                throw new InvalidOperationException(
                    $"Vehicle CSV API '{name}': Timeout must be between 1 and 300 seconds.");
            }

            sources.Add(new VehicleApiSourceOptions
            {
                Name = name,
                Enabled = editor.Enabled,
                Priority = priority,
                Endpoint = editor.Endpoint.Trim(),
                Username = editor.Username.Trim(),
                RequestTimeoutSeconds = timeoutSeconds
            });
        }

        return sources;
    }

    private SerialPortOptions ReadSerialPort(string prefix, string displayName) =>
        new()
        {
            PortName = ReadRequiredText($"{prefix}.PortName", $"{displayName} COM Port"),
            ReadMode = ReadCombo($"{prefix}.ReadMode"),
            BaudRate = ReadInt($"{prefix}.BaudRate", $"{displayName} Baud Rate", 1),
            DataBits = ReadInt($"{prefix}.DataBits", $"{displayName} Data Bits", 5, 8),
            Parity = ReadCombo($"{prefix}.Parity"),
            StopBits = ReadCombo($"{prefix}.StopBits")
        };

    private CameraLaneOptions ReadCamera(string prefix, string displayName) =>
        new()
        {
            Enabled = ReadCheck($"{prefix}.Enabled"),
            RtspUrl = ReadText($"{prefix}.RtspUrl"),
            SnapshotWidth = ReadInt($"{prefix}.SnapshotWidth", $"{displayName} Snapshot Width", 0),
            SnapshotHeight = ReadInt($"{prefix}.SnapshotHeight", $"{displayName} Snapshot Height", 0),
            NetworkCachingMilliseconds = ReadInt($"{prefix}.NetworkCachingMilliseconds", $"{displayName} Network Cache Milliseconds", 0),
            ReconnectSeconds = ReadInt($"{prefix}.ReconnectSeconds", $"{displayName} Reconnect Seconds", 1),
            StreamWatchdogSeconds = ReadInt($"{prefix}.StreamWatchdogSeconds", $"{displayName} Stream Watchdog Seconds", 1),
            UseTcp = ReadCheck($"{prefix}.UseTcp"),
            EnableHardwareDecoding = ReadCheck($"{prefix}.EnableHardwareDecoding")
        };

    private static void ValidateOptions(AppOptions options)
    {
        if (options.Processing.RfidPrefixValidationEnabled &&
            options.Processing.AllowedRfidPrefixes.Count == 0)
        {
            throw new InvalidOperationException(
                "Add at least one allowed RFID prefix or disable RFID prefix validation.");
        }

        if (!options.Hardware.SimulationEnabled)
        {
            var distinctPorts = new[]
                {
                    options.Hardware.InRfid.PortName,
                    options.Hardware.OutRfid.PortName,
                    options.Hardware.Control.PortName
                }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctPorts != 3)
            {
                throw new InvalidOperationException(
                    "IN RFID, OUT RFID, and Control Unit must use different COM ports.");
            }
        }

        ValidateCamera(options.Cameras.In, "IN Camera");
        ValidateCamera(options.Cameras.Out, "OUT Camera");

        if (options.Server.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.Server.Mqtt.BrokerHost))
            {
                throw new InvalidOperationException(
                    "MQTT Broker Host is required when server synchronization is enabled.");
            }

            var topic = string.IsNullOrWhiteSpace(options.Server.Mqtt.PublishTopic)
                ? options.Server.Mqtt.BaseTopic
                : options.Server.Mqtt.PublishTopic;
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException(
                    "MQTT Base Topic or Publish Topic is required when server synchronization is enabled.");
            }

            if (options.Server.Mqtt.RechargeSyncEnabled &&
                (string.IsNullOrWhiteSpace(options.Server.Mqtt.RechargeSubscribeTopic) ||
                 string.IsNullOrWhiteSpace(options.Server.Mqtt.RechargeAckTopic)))
            {
                throw new InvalidOperationException(
                    "Recharge Subscribe Topic and Recharge Acknowledgement Topic are required when RFID recharge synchronization is enabled.");
            }
        }

        if (IsSftpMode(options.Server.ImageUpload.Mode))
        {
            if (string.IsNullOrWhiteSpace(options.Server.ImageUpload.Host) ||
                string.IsNullOrWhiteSpace(options.Server.ImageUpload.Username))
            {
                throw new InvalidOperationException(
                    "SFTP Host and Username are required when image upload mode is Sftp.");
            }
        }

        if (options.Import.ApiSources.Count == 0)
        {
            throw new InvalidOperationException(
                "Add at least one Vehicle CSV API source. A source may remain disabled when automatic synchronization is not required.");
        }

        if (options.Import.ApiAutoSyncEnabled &&
            options.Import.ApiSources.All(source => !source.Enabled))
        {
            throw new InvalidOperationException(
                "Enable at least one Vehicle CSV API source or disable Vehicle API Auto Sync.");
        }

        var duplicateApiName = options.Import.ApiSources
            .GroupBy(source => source.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateApiName is not null)
        {
            throw new InvalidOperationException(
                $"Vehicle CSV API source name '{duplicateApiName.Key}' is duplicated.");
        }

        foreach (var source in options.Import.ApiSources.Where(source => source.Enabled))
        {
            if (!Uri.TryCreate(source.Endpoint, UriKind.Absolute, out var apiUri) ||
                (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Vehicle CSV API Endpoint for '{source.Name}' must be a valid HTTP or HTTPS URL.");
            }

            if (string.IsNullOrWhiteSpace(source.Username))
            {
                throw new InvalidOperationException(
                    $"Vehicle CSV API Username is required for '{source.Name}'.");
            }
        }
    }

    private static void ValidateCamera(CameraLaneOptions camera, string displayName)
    {
        if (!camera.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{displayName} must have a valid RTSP URL when enabled.");
        }
    }

    private static bool IsSftpMode(string mode) =>
        string.Equals(mode, "Sftp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase);

    private string ReadText(string key) => _textFields[key].Text.Trim();

    private string ReadRequiredText(string key, string displayName)
    {
        var value = ReadText(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{displayName} is required.");
        }

        return value;
    }

    private string ReadPassword(string key) => _passwordFields[key].Password;

    private string ReadRequiredPassword(string key, string displayName)
    {
        var value = ReadPassword(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{displayName} is required.");
        }

        return value;
    }

    private bool ReadCheck(string key) => _checkFields[key].IsChecked == true;

    private string ReadCombo(string key)
    {
        var comboBox = _comboFields[key];
        var value = comboBox.Text;
        if (string.IsNullOrWhiteSpace(value) && comboBox.SelectedItem is string selected)
        {
            value = selected;
        }

        return value.Trim();
    }

    private int ReadComboInt(
        string key,
        string displayName,
        int minimum,
        int maximum)
    {
        var value = ReadCombo(key);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{displayName} must be between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private int ReadInt(
        string key,
        string displayName,
        int minimum,
        int maximum = int.MaxValue)
    {
        var value = ReadText(key);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{displayName} must be a whole number between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private decimal ReadDecimal(
        string key,
        string displayName,
        decimal minimum)
    {
        var value = ReadText(key);
        var parsedSuccessfully = decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed);

        if (!parsedSuccessfully)
        {
            parsedSuccessfully = decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out parsed);
        }

        if (!parsedSuccessfully || parsed < minimum)
        {
            throw new InvalidOperationException(
                $"{displayName} must be a number greater than or equal to {minimum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }

    private List<string> ReadLines(string key) =>
        _textFields[key].Text
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void SetText(string key, object? value)
    {
        _textFields[key].Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void SetCheck(string key, bool value)
    {
        _checkFields[key].IsChecked = value;
    }

    private void SetPassword(string key, string value)
    {
        _passwordFields[key].Password = value ?? string.Empty;
    }

    private void SetCombo(string key, string value)
    {
        var comboBox = _comboFields[key];
        comboBox.SelectedItem = null;
        comboBox.Text = string.Empty;

        var matchingItem = comboBox.Items
            .OfType<string>()
            .FirstOrDefault(item => string.Equals(
                item,
                value,
                StringComparison.OrdinalIgnoreCase));

        if (matchingItem is not null)
        {
            comboBox.SelectedItem = matchingItem;
        }
        else
        {
            comboBox.Text = value;
        }
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static AppOptions CloneOptions(AppOptions source)
    {
        var json = JsonSerializer.Serialize(source, CloneSerializerOptions);
        return JsonSerializer.Deserialize<AppOptions>(json, CloneSerializerOptions)
               ?? throw new InvalidOperationException("Unable to copy the application configuration.");
    }

    private static void ApplyNonHardwareOptions(AppOptions source, AppOptions destination)
    {
        destination.Device = source.Device;
        destination.Processing = source.Processing;
        destination.Cameras = source.Cameras;
        destination.Server = source.Server;
        destination.Connectivity = source.Connectivity;
        destination.Import = source.Import;
        destination.Storage = source.Storage;
        destination.ExceptionalApproval = source.ExceptionalApproval;
        destination.AutoApproval = source.AutoApproval;
        destination.FrontendIndicators = source.FrontendIndicators;
        destination.Security = source.Security;
    }

    private sealed class VehicleApiSourceEditor
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Priority { get; set; } = "1";
        public string Endpoint { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RequestTimeoutSeconds { get; set; } = "30";
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
}
