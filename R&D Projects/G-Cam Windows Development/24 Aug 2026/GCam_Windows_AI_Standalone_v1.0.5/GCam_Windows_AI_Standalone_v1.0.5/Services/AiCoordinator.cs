using GCam.Windows.Models;
using OpenCvSharp;

namespace GCam.Windows.Services;

public sealed class AiCoordinator : IDisposable
{
    private readonly AppSettings _settings;
    private readonly DetectionGate _gate = new();
    private readonly YoloOnnxDetector _personVehicleYolo;
    private readonly MobileNetSsdDetector _personVehicleFallback;
    private readonly YoloOnnxDetector _plate;
    private readonly YoloOnnxDetector _garbage;
    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;

    public string ModelStatus
    {
        get
        {
            string pv = _personVehicleYolo.IsLoaded
                ? "YOLO ONNX loaded"
                : _personVehicleFallback.Status;
            return $"P/V: {pv} | LPD: {_plate.Status} | Garbage: {_garbage.Status}";
        }
    }

    public AiCoordinator(AppSettings settings)
    {
        _settings = settings;
        _personVehicleYolo = new YoloOnnxDetector(settings.Ai.PersonVehicle, MapPersonVehicle);
        _personVehicleFallback = new MobileNetSsdDetector(settings.Ai.PersonVehicle.Confidence);
        _plate = new YoloOnnxDetector(settings.Ai.LicensePlate, label => EventKind.LicensePlate);
        _garbage = new YoloOnnxDetector(settings.Ai.Garbage, label => EventKind.Garbage);
    }

    public IReadOnlyList<Detection> Detect(Mat frame)
    {
        var minInterval = TimeSpan.FromSeconds(1.0 / Math.Max(1, _settings.Camera.DetectionFps));
        if (DateTimeOffset.UtcNow - _lastRun < minInterval) return Array.Empty<Detection>();
        _lastRun = DateTimeOffset.UtcNow;

        var all = new List<Detection>();
        if (_settings.Ai.Person.DetectionEnabled || _settings.Ai.Vehicle.DetectionEnabled)
        {
            var pv = _personVehicleYolo.IsLoaded
                ? _personVehicleYolo.Detect(frame)
                : _personVehicleFallback.Detect(frame);
            all.AddRange(pv);
        }
        if (_settings.Ai.LicensePlateEvent.DetectionEnabled) all.AddRange(_plate.Detect(frame));
        if (_settings.Ai.GarbageEvent.DetectionEnabled) all.AddRange(_garbage.Detect(frame));
        return all;
    }

    public IReadOnlyList<Detection> TriggeredDetections(IReadOnlyList<Detection> detections)
    {
        var now = DateTimeOffset.UtcNow;
        var triggered = new List<Detection>();
        foreach (var kind in Enum.GetValues<EventKind>())
        {
            var cfg = GetSettings(kind);
            if (!cfg.DetectionEnabled) continue;
            var best = detections.Where(d => d.Kind == kind).OrderByDescending(d => d.Confidence).FirstOrDefault();
            if (_gate.ShouldTrigger(kind, best is not null, cfg, now) && best is not null) triggered.Add(best);
        }
        return triggered;
    }

    private EventTypeSettings GetSettings(EventKind k) => k switch
    {
        EventKind.Person => _settings.Ai.Person,
        EventKind.Vehicle => _settings.Ai.Vehicle,
        EventKind.LicensePlate => _settings.Ai.LicensePlateEvent,
        EventKind.Garbage => _settings.Ai.GarbageEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(k))
    };

    private static EventKind? MapPersonVehicle(string label)
    {
        label = label.Trim().ToLowerInvariant();
        if (label is "person") return EventKind.Person;
        if (label is "car" or "truck" or "bus" or "motorcycle" or "motorbike" or "bicycle") return EventKind.Vehicle;
        return null;
    }

    public void Dispose()
    {
        _personVehicleYolo.Dispose();
        _personVehicleFallback.Dispose();
        _plate.Dispose();
        _garbage.Dispose();
    }
}
