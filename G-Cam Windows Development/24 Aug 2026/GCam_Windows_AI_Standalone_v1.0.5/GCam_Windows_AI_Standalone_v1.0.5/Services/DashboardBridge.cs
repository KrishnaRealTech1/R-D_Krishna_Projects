using GCam.Windows.Models;

namespace GCam.Windows.Services;

public sealed class DashboardBridge
{
    public required Func<DashboardState> GetState { get; init; }
    public required Func<DashboardControlsSnapshot> GetControls { get; init; }
    public required Func<IReadOnlyList<EventRecord>> GetEvents { get; init; }
    public required Func<byte[]?> GetSnapshotJpeg { get; init; }
    public required Action<EventControlUpdate> UpdateEventControl { get; init; }
    public required Action<string, bool> SetFeatureFlag { get; init; }
    public required Action<bool> SetWarningMaster { get; init; }
    public required Func<EventKind, bool> TriggerTestEvent { get; init; }
    public required Action StartRuntime { get; init; }
    public required Action StopRuntime { get; init; }
}
