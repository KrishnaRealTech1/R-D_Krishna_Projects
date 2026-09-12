namespace RfidVehicleAccess.Services;

public sealed class AppOptions
{
    public DeviceOptions Device { get; set; } = new();
    public ProcessingOptions Processing { get; set; } = new();
    public HardwareOptions Hardware { get; set; } = new();
    public WeighbridgeOptions Weighbridge { get; set; } = new();
    public CameraOptions Cameras { get; set; } = new();
    public ServerOptions Server { get; set; } = new();
    public ConnectivityOptions Connectivity { get; set; } = new();
    public ImportOptions Import { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public ExceptionalApprovalOptions ExceptionalApproval { get; set; } = new();
    public AutoApprovalOptions AutoApproval { get; set; } = new();
    public FrontendIndicatorOptions FrontendIndicators { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
}

public sealed class ExceptionalApprovalOptions
{
    public bool InEnabled { get; set; } = true;
    public bool OutEnabled { get; set; } = true;
}

public sealed class AutoApprovalOptions
{
    public bool InEnabled { get; set; }
    public bool OutEnabled { get; set; }
}

public sealed class FrontendIndicatorOptions
{
    public bool RedEnabled { get; set; } = true;
    public bool GreenEnabled { get; set; } = true;
    public bool OrangeEnabled { get; set; } = true;
    public bool BuzzerEnabled { get; set; } = true;
}

public sealed class SecurityOptions
{
    public string AdminControlsPassword { get; set; } = "7799";
    public string ServerPanelPassword { get; set; } = "rts123!@#";
}

public sealed class DeviceOptions
{
    public string SiteId { get; set; } = "SITE-001";
    public string DeviceId { get; set; } = "GATE-PC-001";
    public string LaneId { get; set; } = "MAIN-GATE";
    public string DeviceName { get; set; } = "RealTech iAWS";
}

public sealed class ProcessingOptions
{
    public int ProcessWaitSeconds { get; set; } = 3;
    public int BarrierAndGreenDelaySeconds { get; set; } = 5;
    public int DuplicateReadSeconds { get; set; } = 10;
    public int SensorValiditySeconds { get; set; } = 30;
    public int VehicleDisplayResetSeconds { get; set; } = 20;
    public List<string> AllowedRfidPrefixes { get; set; } = ["E2"];
    public bool RfidPrefixValidationEnabled { get; set; } = true;
    public bool CaseSensitivePrefixes { get; set; }
}

public sealed class WeighbridgeOptions
{
    public bool Enabled { get; set; } = true;
    public decimal TargetWeightKg { get; set; } = 5000m;
    public decimal ResetWeightKg { get; set; } = 100m;
    public int StableReadCount { get; set; } = 1;
    public string DataPrefix { get; set; } = "wn";
    public string UnitText { get; set; } = "kg";
    public SerialPortOptions Serial { get; set; } = new()
    {
        PortName = "COM12",
        ReadMode = "LineText",
        BaudRate = 9600
    };
}

public sealed class HardwareOptions
{
    public bool SimulationEnabled { get; set; } = true;
    public string LineTerminator { get; set; } = "\\r\\n";
    public int ReconnectSeconds { get; set; } = 5;
    public SerialPortOptions InRfid { get; set; } = new()
    {
        ReadMode = "UhfCfFrame"
    };

    public SerialPortOptions OutRfid { get; set; } = new()
    {
        ReadMode = "UhfCfFrame"
    };

    public SerialPortOptions Control { get; set; } = new()
    {
        ReadMode = "LineText"
    };

    // Backup IND link. Windows Bluetooth SPP devices appear as a normal COM port.
    // The primary wired Control port is always preferred; this port is used only
    // when the wired control link is unavailable.
    public bool BluetoothControlEnabled { get; set; } = true;
    public SerialPortOptions BluetoothControl { get; set; } = new()
    {
        PortName = "COM11",
        ReadMode = "LineText",
        BaudRate = 9600
    };

    // Third-level IND failover over a Wi-Fi serial terminal (raw TCP/Telnet-style socket).
    // Priority is always: Wired COM -> Bluetooth COM -> Wi-Fi TCP.
    public bool WifiControlEnabled { get; set; } = true;
    public WifiControlOptions WifiControl { get; set; } = new();

    public SensorMessageOptions SensorMessages { get; set; } = new();
}

public sealed class WifiControlOptions
{
    public string Host { get; set; } = "192.168.22.102";
    public int Port { get; set; } = 23;
    public int ConnectTimeoutMilliseconds { get; set; } = 1500;
}

public sealed class SensorMessageOptions
{
    public string InHigh { get; set; } = "IN Detected";
    public string InReleased { get; set; } = "IN Realeased";
    public string OutHigh { get; set; } = "OUT Detected";
    public string OutReleased { get; set; } = "OUT Realeased";
}

public sealed class SerialPortOptions
{
    public string PortName { get; set; } = "COM1";
    public string ReadMode { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
}

public sealed class CameraOptions
{
    public CameraLaneOptions Camera1 { get; set; } = new();
    public CameraLaneOptions Camera2 { get; set; } = new();
    public CameraLaneOptions Camera3 { get; set; } = new();
    public CameraLaneOptions Camera4 { get; set; } = new();

    public string LocalImageFolder { get; set; } = "%REALTECH_SYSTEMS%\\Images";
    public string ImageFilePrefix { get; set; } = "RealTech iAWS";
    public string ImageTimestampFormat { get; set; } = "yyyy-MM-dd_HH-mm-ss-fff";

    public CameraLaneOptions Get(int cameraNumber) => cameraNumber switch
    {
        1 => Camera1,
        2 => Camera2,
        3 => Camera3,
        4 => Camera4,
        _ => throw new ArgumentOutOfRangeException(nameof(cameraNumber), cameraNumber, "Camera number must be 1-4.")
    };
}

public sealed class CameraLaneOptions
{
    public bool Enabled { get; set; }
    public string RtspUrl { get; set; } = string.Empty;
    public int SnapshotWidth { get; set; } = 1920;
    public int SnapshotHeight { get; set; } = 1080;
    public int NetworkCachingMilliseconds { get; set; } = 1200;
    public int ReconnectSeconds { get; set; } = 5;
    public int StreamWatchdogSeconds { get; set; } = 12;
    public bool UseTcp { get; set; } = true;
    public bool EnableHardwareDecoding { get; set; }
}

public sealed class ServerOptions
{
    public bool Enabled { get; set; }
    public int SyncIntervalSeconds { get; set; } = 15;
    public int MaxBatchSize { get; set; } = 25;
    public int InitialRetryDelaySeconds { get; set; } = 15;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public MqttOptions Mqtt { get; set; } = new();
    public ImageUploadOptions ImageUpload { get; set; } = new();
}

public sealed class MqttOptions
{
    public string BrokerHost { get; set; } = string.Empty;
    public int Port { get; set; } = 8883;
    public bool UseTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string BaseTopic { get; set; } = "IAWS-001/Device_Response";
    public string PublishTopic { get; set; } = "IAWS-001/Device_Response";
    public int QualityOfService { get; set; } = 1;
    public bool Retain { get; set; }
    public int KeepAliveSeconds { get; set; } = 30;
    public int ConnectTimeoutSeconds { get; set; } = 10;
    public int PublishTimeoutSeconds { get; set; } = 10;
}


public sealed class ImageUploadOptions
{
    // Disabled, Sftp, or Ftp. Legacy Enable/Enabled values continue to mean Sftp.
    public string Mode { get; set; } = "Disabled";
    // For FTP mode, true enables explicit TLS/SSL (AUTH TLS / FTPS) on the normal FTP port.
    public bool UseTls { get; set; }
    // Optional SHA-256 certificate fingerprint used to securely pin an FTPS server certificate.
    // When this matches, name/chain validation errors are accepted for that exact certificate.
    public string TlsCertificateSha256Fingerprint { get; set; } = string.Empty;
    // Emergency compatibility option for legacy FTPS servers. Keep false whenever possible.
    public bool AllowInvalidTlsCertificate { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemoteDirectory { get; set; } = "/vehicle-images";
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public int OperationTimeoutSeconds { get; set; } = 60;
    public int TotalTimeoutSeconds { get; set; } = 75;
}

public sealed class ConnectivityOptions
{
    public int CheckIntervalSeconds { get; set; } = 15;
    public int RequestTimeoutSeconds { get; set; } = 4;
    public List<string> CheckEndpoints { get; set; } =
    [
        "https://www.msftconnecttest.com/connecttest.txt",
        "https://connectivitycheck.gstatic.com/generate_204"
    ];
}

public sealed class ImportOptions
{
    public string DefaultAccessType { get; set; } = "Free";
    public decimal DefaultOpeningBalance { get; set; }
    public bool UpdateExistingRecords { get; set; } = true;
    public bool EnforceRfidPrefixValidation { get; set; }
    public bool SkipInvalidRows { get; set; } = true;

    public bool ApiAutoSyncEnabled { get; set; } = true;
    public int ApiAutoSyncIntervalSeconds { get; set; } = 30;
    public List<VehicleApiSourceOptions> ApiSources { get; set; } = [];

    // Legacy single-source settings are retained for backward compatibility.
    // New configuration is stored in ApiSources.
    public string ApiEndpoint { get; set; } =
        "https://gov.igps.io/aws_admin/Server/csv_link.php";
    public string ApiUsername { get; set; } = "tambaram";
    public int ApiRequestTimeoutSeconds { get; set; } = 30;
}

public sealed class VehicleApiSourceOptions
{
    public string Name { get; set; } = "Vehicle CSV API";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 1;
    public string Endpoint { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class StorageOptions
{
    public string DatabaseFile { get; set; } = "Data\\vehicle-access.db";
    public string StatusLogFile { get; set; } = "%REALTECH_SYSTEMS%\\Logs\\Status\\status.log";
    public string ServerLogFile { get; set; } = "%REALTECH_SYSTEMS%\\Logs\\Server\\server.log";
    public bool AutoDeleteEnabled { get; set; } = true;
    public int ImageRetentionDays { get; set; } = 30;
    public int LogRetentionDays { get; set; } = 30;
}
