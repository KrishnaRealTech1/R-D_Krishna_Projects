using GCam.Windows.Models;

namespace GCam.Windows.Services;

public sealed class DetectionGate
{
    private sealed class State
    {
        public DateTimeOffset? FirstSeen;
        public DateTimeOffset LastSeen;
        public DateTimeOffset LastTriggered = DateTimeOffset.MinValue;
    }

    private readonly Dictionary<EventKind, State> _states = Enum.GetValues<EventKind>().ToDictionary(k => k, _ => new State());
    private readonly object _lock = new();

    public bool ShouldTrigger(EventKind kind, bool present, EventTypeSettings cfg, DateTimeOffset now)
    {
        lock (_lock)
        {
            var s = _states[kind];
            if (!present)
            {
                if ((now - s.LastSeen).TotalSeconds > 1.0) s.FirstSeen = null;
                return false;
            }

            s.LastSeen = now;
            s.FirstSeen ??= now;
            if ((now - s.FirstSeen.Value).TotalSeconds < cfg.DetectionDelaySeconds) return false;
            if ((now - s.LastTriggered).TotalSeconds < cfg.CooldownSeconds) return false;

            s.LastTriggered = now;
            s.FirstSeen = now;
            return true;
        }
    }
}
