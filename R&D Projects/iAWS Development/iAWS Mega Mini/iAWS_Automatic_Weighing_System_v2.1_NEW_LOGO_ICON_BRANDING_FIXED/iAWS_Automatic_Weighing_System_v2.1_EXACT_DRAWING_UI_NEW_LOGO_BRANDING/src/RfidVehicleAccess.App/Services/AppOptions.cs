namespace RfidVehicleAccess.Services;

public sealed class AppOptions
{
    public DeviceOptions Device { get; set; } = new();
    public IawsOptions Iaws { get; set; } = new();
    public HardwareOptions Hardware { get; set; } = new();
    public CameraOptions Cameras { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
}

public sealed class DeviceOptions
{
    public string SiteId { get; set; } = "SITE-001";
    public string DeviceId { get; set; } = "IAWS-001";
    public string LaneId { get; set; } = "WEIGHBRIDGE-01";
    public string DeviceName { get; set; } = "iAWS Automatic Weighing System";
}

public sealed class IawsOptions
{
    public string ApiEndpoint { get; set; } = "https://YOUR-SERVER/iaws_raw/mega/insert";
    public int ApiTimeoutSeconds { get; set; } = 30;
    public decimal TriggerWeightKg { get; set; } = 500m;
    public decimal ResetWeightKg { get; set; } = 100m;
    public int RfidFreshnessSeconds { get; set; } = 30;
    public string SitePrefix { get; set; } = "AMMAN_KOIL_THAMBARAM";
    public string MaterialType { get; set; } = string.Empty;
    public bool RequireAllCameras { get; set; } = true;
}

public sealed class HardwareOptions
{
    public bool SimulationEnabled { get; set; } = true;
    public int ReconnectSeconds { get; set; } = 5;
    public SerialPortOptions RfidReader { get; set; } = new()
    {
        PortName = "COM3",
        BaudRate = 115200,
        ReadMode = "UhfCfFrame"
    };
    public WeightBridgeOptions WeightBridge { get; set; } = new();
}

public sealed class SerialPortOptions
{
    public string PortName { get; set; } = "COM3";
    public string ReadMode { get; set; } = "UhfCfFrame";
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
}

public sealed class WeightBridgeOptions
{
    public string PortName { get; set; } = "COM10";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string WeightPattern { get; set; } = @"[-+]?\d+(?:\.\d+)?";
}

public sealed class CameraOptions
{
    public CameraLaneOptions Front { get; set; } = new();
    public CameraLaneOptions Back { get; set; } = new();
    public CameraLaneOptions Left { get; set; } = new();
    public CameraLaneOptions Right { get; set; } = new();
    public string LocalImageFolder { get; set; } = "%IAWS_SYSTEM%\\Images";
}

public sealed class CameraLaneOptions
{
    public bool Enabled { get; set; } = true;
    public string RtspUrl { get; set; } = string.Empty;
    public int SnapshotWidth { get; set; } = 1920;
    public int SnapshotHeight { get; set; } = 1080;
    public int NetworkCachingMilliseconds { get; set; } = 1200;
    public int ReconnectSeconds { get; set; } = 5;
    public bool UseTcp { get; set; } = true;
    public bool EnableHardwareDecoding { get; set; }
}

public sealed class StorageOptions
{
    public string StatusLogFile { get; set; } = "%IAWS_SYSTEM%\\Logs\\Status\\status.log";
    public string ApiLogFile { get; set; } = "%IAWS_SYSTEM%\\Logs\\Api\\api.log";
}
