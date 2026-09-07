namespace GCam.Windows.Models;

public sealed record DashboardState(
    DateTimeOffset Time,
    string CameraStatus,
    string BufferStatus,
    string ModelStatus,
    string DetectionStatus,
    long DetectionsSeen,
    long EventsTriggered,
    string RuntimeStatus,
    string DashboardStatus,
    string TunnelStatus,
    string LocalDashboardUrl,
    string PublicDashboardUrl,
    EventRecord? LatestEvent);

public sealed record EventControlSnapshot(
    string Event,
    bool DetectionEnabled,
    double DetectionDelaySeconds,
    double CooldownSeconds,
    bool WarningAudioEnabled,
    bool ImageCaptureEnabled,
    bool VideoCaptureEnabled);

public sealed record DashboardControlsSnapshot(
    bool WarningMasterEnabled,
    IReadOnlyList<EventControlSnapshot> Events,
    int RollingBufferSeconds,
    int PreEventSeconds,
    int PostEventSeconds,
    bool UploadEnabled);

public sealed class EventControlUpdate
{
    public string Event { get; set; } = "";
    public bool? DetectionEnabled { get; set; }
    public double? DetectionDelaySeconds { get; set; }
    public double? CooldownSeconds { get; set; }
    public bool? WarningAudioEnabled { get; set; }
    public bool? ImageCaptureEnabled { get; set; }
    public bool? VideoCaptureEnabled { get; set; }
}

public sealed class FeatureControlUpdate
{
    public string Flag { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class BoolControlUpdate
{
    public bool Enabled { get; set; }
}

public sealed class TestEventRequest
{
    public string Event { get; set; } = "";
}

public sealed class DashboardAuthRequest
{
    public string Key { get; set; } = "";
}
