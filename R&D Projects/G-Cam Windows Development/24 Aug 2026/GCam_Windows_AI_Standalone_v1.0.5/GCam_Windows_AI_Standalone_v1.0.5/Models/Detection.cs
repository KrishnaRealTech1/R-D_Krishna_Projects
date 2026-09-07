using OpenCvSharp;

namespace GCam.Windows.Models;

public enum EventKind
{
    Person,
    Vehicle,
    LicensePlate,
    Garbage
}

public sealed record Detection(
    EventKind Kind,
    string Label,
    float Confidence,
    Rect Box);

public sealed record EventRecord(
    Guid EventId,
    EventKind Kind,
    DateTimeOffset Timestamp,
    float Confidence,
    string? ImagePath,
    string? VideoPath,
    bool UploadQueued,
    string Status);
