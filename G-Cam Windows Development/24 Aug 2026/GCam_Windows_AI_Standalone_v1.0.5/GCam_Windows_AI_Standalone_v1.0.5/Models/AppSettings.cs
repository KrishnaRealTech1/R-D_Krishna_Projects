namespace GCam.Windows.Models;

public sealed class AppSettings
{
    public CameraSettings Camera { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public EvidenceSettings Evidence { get; set; } = new();
    public WarningSettings Warning { get; set; } = new();
    public SftpSettings Sftp { get; set; } = new();
    public CloudflareSettings Cloudflare { get; set; } = new();
}

public sealed class CameraSettings
{
    public string LiveRtspUrl { get; set; } = "rtsp://user:password@camera-ip:554/substream";
    public string EvidenceRtspUrl { get; set; } = "rtsp://user:password@camera-ip:554/mainstream";
    public int PreviewWidth { get; set; } = 960;
    public int DetectionFps { get; set; } = 5;
}

public sealed class AiSettings
{
    public DetectorSettings PersonVehicle { get; set; } = new()
    {
        Enabled = true,
        ModelPath = "models/person_vehicle.onnx",
        LabelsPath = "models/coco80.txt",
        Confidence = 0.50f,
        InputSize = 640,
        UseDirectMl = true
    };
    public DetectorSettings LicensePlate { get; set; } = new()
    {
        Enabled = true,
        ModelPath = "models/license_plate.onnx",
        LabelsPath = "models/license_plate.txt",
        Confidence = 0.50f,
        InputSize = 640,
        UseDirectMl = true
    };
    public DetectorSettings Garbage { get; set; } = new()
    {
        Enabled = true,
        ModelPath = "models/garbage.onnx",
        LabelsPath = "models/garbage.txt",
        Confidence = 0.50f,
        InputSize = 640,
        UseDirectMl = true
    };

    public EventTypeSettings Person { get; set; } = EventTypeSettings.DefaultEnabled();
    public EventTypeSettings Vehicle { get; set; } = EventTypeSettings.DefaultEnabled();
    public EventTypeSettings LicensePlateEvent { get; set; } = EventTypeSettings.DefaultEnabled();
    public EventTypeSettings GarbageEvent { get; set; } = EventTypeSettings.DefaultEnabled(warning: false);
}

public sealed class DetectorSettings
{
    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = "";
    public string LabelsPath { get; set; } = "";
    public float Confidence { get; set; } = 0.50f;
    public float NmsThreshold { get; set; } = 0.45f;
    public int InputSize { get; set; } = 640;
    public bool UseDirectMl { get; set; } = true;
}

public sealed class EventTypeSettings
{
    public bool DetectionEnabled { get; set; } = true;
    public double DetectionDelaySeconds { get; set; } = 1.5;
    public double CooldownSeconds { get; set; } = 15;
    public bool WarningAudioEnabled { get; set; } = true;
    public bool ImageCaptureEnabled { get; set; } = true;
    public bool VideoCaptureEnabled { get; set; } = true;

    public static EventTypeSettings DefaultEnabled(bool warning = true) => new()
    {
        DetectionEnabled = true,
        DetectionDelaySeconds = 1.5,
        CooldownSeconds = 15,
        WarningAudioEnabled = warning,
        ImageCaptureEnabled = true,
        VideoCaptureEnabled = true
    };
}

public sealed class EvidenceSettings
{
    public int RollingBufferSeconds { get; set; } = 30;
    public int BufferSegmentSeconds { get; set; } = 1;
    public int PreEventSeconds { get; set; } = 10;
    public int PostEventSeconds { get; set; } = 20;
    public string RootFolder { get; set; } = "%USERPROFILE%\\Documents\\GCam\\Evidence";
    public bool UploadEnabled { get; set; } = false;
}

public sealed class WarningSettings
{
    public bool MasterEnabled { get; set; } = true;
    public string PersonWav { get; set; } = "assets/person_warning.wav";
    public string VehicleWav { get; set; } = "assets/vehicle_warning.wav";
    public string LicensePlateWav { get; set; } = "assets/lpd_warning.wav";
    public string GarbageWav { get; set; } = "assets/garbage_warning.wav";
}

public sealed class SftpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string BaseDirectory { get; set; } = "/upload/gcam";
}

public sealed class CloudflareSettings
{
    // The old Python app listened on 0.0.0.0:8080. This Windows build keeps that
    // behavior configurable so LAN access and a local Cloudflare origin both work.
    public bool DashboardEnabled { get; set; } = true;
    public string BindAddress { get; set; } = "0.0.0.0";
    public int DashboardPort { get; set; } = 8080;

    // Remote controls are protected by a local control key. The browser dashboard
    // exchanges this key for an HttpOnly session cookie; the key is not embedded in HTML.
    public string ControlApiKey { get; set; } = Guid.NewGuid().ToString("N");

    public bool TunnelEnabled { get; set; } = false;
    public bool TunnelAutoStart { get; set; } = false;
    public string PublicBaseUrl { get; set; } = "";
    public string Protocol { get; set; } = "auto";
    public int MetricsPort { get; set; } = 20241;
}
