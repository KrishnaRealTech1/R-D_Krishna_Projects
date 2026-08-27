using System.IO;
using System.Diagnostics;
using GCam.Windows.Infrastructure;
using GCam.Windows.Models;

namespace GCam.Windows.Services;

public sealed class RollingBufferService : IDisposable
{
    private Process? _bufferProcess;
    private readonly object _lock = new();
    private string _bufferDir = "";
    private string _ffmpeg = "";
    private AppSettings? _settings;

    public string Status { get; private set; } = "Stopped";

    public void Start(AppSettings settings)
    {
        Stop();
        _settings = settings;
        _ffmpeg = FfmpegLocator.Resolve();
        _bufferDir = Path.Combine(Path.GetTempPath(), "GCam", "rolling-buffer");
        Directory.CreateDirectory(_bufferDir);
        foreach (var f in Directory.EnumerateFiles(_bufferDir, "*.ts")) TryDelete(f);

        int segment = Math.Max(1, settings.Evidence.BufferSegmentSeconds);
        int wrap = Math.Max(2, (int)Math.Ceiling((double)settings.Evidence.RollingBufferSeconds / segment));
        string pattern = Path.Combine(_bufferDir, "segment_%03d.ts");
        string args = $"-hide_banner -loglevel warning -rtsp_transport tcp -i {Q(settings.Camera.EvidenceRtspUrl)} " +
                      $"-map 0:v:0 -an -c copy -f segment -segment_time {segment} -segment_wrap {wrap} -reset_timestamps 1 {Q(pattern)}";
        _bufferProcess = StartProcess(_ffmpeg, args, hidden: true);
        Status = $"Running {settings.Evidence.RollingBufferSeconds}s rolling buffer ({segment}s segments)";
    }

    public async Task<string?> CreateEventVideoAsync(EventKind kind, Guid eventId, DateTimeOffset triggerTime, CancellationToken ct = default)
    {
        if (_settings is null) return null;
        int pre = Math.Min(_settings.Evidence.PreEventSeconds, _settings.Evidence.RollingBufferSeconds);
        int post = Math.Max(1, _settings.Evidence.PostEventSeconds);
        string root = EvidenceRoot(_settings);
        string day = triggerTime.LocalDateTime.ToString("yyyy-MM-dd");
        string finalDir = Path.Combine(root, day, kind.ToString(), "Video");
        Directory.CreateDirectory(finalDir);
        string tmp = Path.Combine(Path.GetTempPath(), "GCam", "events", eventId.ToString("N"));
        Directory.CreateDirectory(tmp);

        var cutoff = triggerTime.UtcDateTime.AddSeconds(-pre - Math.Max(1, _settings.Evidence.BufferSegmentSeconds));
        var preSegments = Directory.EnumerateFiles(_bufferDir, "*.ts")
            .Select(p => new FileInfo(p))
            .Where(f => f.LastWriteTimeUtc >= cutoff && f.LastWriteTimeUtc <= triggerTime.UtcDateTime.AddSeconds(2))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToArray();

        var copied = new List<string>();
        foreach (var seg in preSegments)
        {
            try
            {
                string dest = Path.Combine(tmp, $"pre_{copied.Count:000}.ts");
                File.Copy(seg.FullName, dest, true);
                copied.Add(dest);
            }
            catch { }
        }

        string postTs = Path.Combine(tmp, "post.ts");
        string postArgs = $"-hide_banner -loglevel warning -rtsp_transport tcp -i {Q(_settings.Camera.EvidenceRtspUrl)} " +
                          $"-t {post} -map 0:v:0 -an -c copy -f mpegts -y {Q(postTs)}";
        using (var postProc = StartProcess(_ffmpeg, postArgs, hidden: true))
        {
            await postProc.WaitForExitAsync(ct);
            if (postProc.ExitCode != 0 || !File.Exists(postTs)) return null;
        }
        copied.Add(postTs);

        string concat = Path.Combine(tmp, "concat.txt");
        await File.WriteAllLinesAsync(concat, copied.Select(p => $"file '{p.Replace("'", "'\\''")}'"), ct);
        string final = Path.Combine(finalDir, $"{triggerTime.LocalDateTime:yyyyMMdd_HHmmss_fff}_{kind}_{eventId:N}.mp4");
        string concatArgs = $"-hide_banner -loglevel warning -f concat -safe 0 -i {Q(concat)} -c copy -movflags +faststart -y {Q(final)}";
        using (var concatProc = StartProcess(_ffmpeg, concatArgs, hidden: true))
        {
            await concatProc.WaitForExitAsync(ct);
            if (concatProc.ExitCode != 0 || !File.Exists(final))
            {
                // Fallback re-encode handles incompatible timestamps/codecs between copied segments.
                string fallback = $"-hide_banner -loglevel warning -f concat -safe 0 -i {Q(concat)} -c:v libx264 -preset veryfast -crf 23 -an -movflags +faststart -y {Q(final)}";
                using var enc = StartProcess(_ffmpeg, fallback, hidden: true);
                await enc.WaitForExitAsync(ct);
                if (enc.ExitCode != 0 || !File.Exists(final)) return null;
            }
        }

        _ = Task.Run(() => { try { Directory.Delete(tmp, true); } catch { } });
        return final;
    }

    public static string EvidenceRoot(AppSettings settings)
    {
        var expanded = Environment.ExpandEnvironmentVariables(settings.Evidence.RootFolder);
        Directory.CreateDirectory(expanded);
        return expanded;
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_bufferProcess is { HasExited: false })
            {
                try { _bufferProcess.Kill(entireProcessTree: true); _bufferProcess.WaitForExit(1000); } catch { }
            }
            _bufferProcess?.Dispose();
            _bufferProcess = null;
            Status = "Stopped";
        }
    }

    private static Process StartProcess(string exe, string args, bool hidden)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = hidden,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        return Process.Start(psi) ?? throw new InvalidOperationException("Unable to start FFmpeg.");
    }

    private static string Q(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
    private static void TryDelete(string p) { try { File.Delete(p); } catch { } }
    public void Dispose() => Stop();
}
