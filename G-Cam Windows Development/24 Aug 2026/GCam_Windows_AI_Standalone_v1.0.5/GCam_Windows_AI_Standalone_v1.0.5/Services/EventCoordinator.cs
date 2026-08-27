using System.IO;
using GCam.Windows.Models;
using OpenCvSharp;

namespace GCam.Windows.Services;

public sealed class EventCoordinator
{
    private readonly AppSettings _settings;
    private readonly RollingBufferService _buffer;
    private readonly WarningAudioService _audio;
    private readonly SftpUploader _uploader;
    private readonly SemaphoreSlim _videoJobs = new(2);

    public event Action<EventRecord>? EventUpdated;

    public EventCoordinator(AppSettings settings, RollingBufferService buffer, WarningAudioService audio, SftpUploader uploader)
    {
        _settings = settings; _buffer = buffer; _audio = audio; _uploader = uploader;
    }

    public void Trigger(EventKind kind, Detection detection, Mat frame)
    {
        var cfg = GetEventSettings(kind);
        if (cfg.WarningAudioEnabled) _audio.Play(kind, _settings);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        string? image = null;
        string triggerStatus = "Triggered";

        if (cfg.ImageCaptureEnabled)
        {
            try
            {
                string root = RollingBufferService.EvidenceRoot(_settings);
                string dir = Path.Combine(root, now.LocalDateTime.ToString("yyyy-MM-dd"), kind.ToString(), "Image");
                Directory.CreateDirectory(dir);
                image = Path.Combine(dir, $"{now.LocalDateTime:yyyyMMdd_HHmmss_fff}_{kind}_{id:N}.jpg");
                using var annotated = frame.Clone();
                Cv2.Rectangle(annotated, detection.Box, Scalar.Red, 3);
                Cv2.PutText(annotated, $"{detection.Label} {detection.Confidence:P0}",
                    new OpenCvSharp.Point(detection.Box.X, Math.Max(24, detection.Box.Y - 8)),
                    HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);
                if (!Cv2.ImWrite(image, annotated) || !File.Exists(image))
                {
                    image = null;
                    triggerStatus = "Triggered - image write failed";
                }
            }
            catch (Exception ex)
            {
                image = null;
                triggerStatus = "Triggered - image failed: " + ex.Message;
            }
        }

        EventUpdated?.Invoke(new EventRecord(id, kind, now, detection.Confidence, image, null, false, triggerStatus));
        if (cfg.VideoCaptureEnabled)
            _ = CaptureVideoAndUploadAsync(id, kind, now, detection.Confidence, image);
        else if (_settings.Evidence.UploadEnabled)
            _ = UploadOnlyAsync(id, kind, now, detection.Confidence, image);
    }

    private async Task CaptureVideoAndUploadAsync(Guid id, EventKind kind, DateTimeOffset now, float conf, string? image)
    {
        await _videoJobs.WaitAsync();
        try
        {
            EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, null, false, "Recording event clip"));
            string? video;
            try
            {
                video = await _buffer.CreateEventVideoAsync(kind, id, now);
            }
            catch (Exception ex)
            {
                EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, null, false, "Video failed: " + ex.Message));
                return;
            }

            bool queued = _settings.Evidence.UploadEnabled;
            EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, video, queued, video is null ? "Video failed - check Evidence RTSP/buffer" : "Saved"));
            if (queued)
            {
                try
                {
                    await _uploader.UploadEvidenceAsync(_settings, kind, image, video);
                    EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, video, false, "Uploaded"));
                }
                catch (Exception ex)
                {
                    EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, video, true, "Upload failed: " + ex.Message));
                }
            }
        }
        finally { _videoJobs.Release(); }
    }

    private async Task UploadOnlyAsync(Guid id, EventKind kind, DateTimeOffset now, float conf, string? image)
    {
        try
        {
            await _uploader.UploadEvidenceAsync(_settings, kind, image);
            EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, null, false, "Uploaded"));
        }
        catch (Exception ex)
        {
            EventUpdated?.Invoke(new EventRecord(id, kind, now, conf, image, null, true, "Upload failed: " + ex.Message));
        }
    }

    private EventTypeSettings GetEventSettings(EventKind kind) => kind switch
    {
        EventKind.Person => _settings.Ai.Person,
        EventKind.Vehicle => _settings.Ai.Vehicle,
        EventKind.LicensePlate => _settings.Ai.LicensePlateEvent,
        EventKind.Garbage => _settings.Ai.GarbageEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
