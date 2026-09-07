using LibVLCSharp.Shared;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class CameraStreamService : IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly LibVLC _libVlc;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _inSnapshotLock = new(1, 1);
    private readonly SemaphoreSlim _outSnapshotLock = new(1, 1);
    private readonly object _restartSync = new();
    private readonly LanePlaybackState _inPlaybackState = new();
    private readonly LanePlaybackState _outPlaybackState = new();

    private Task? _watchdogTask;
    private bool _inRestartScheduled;
    private bool _outRestartScheduled;
    private bool _started;
    private bool _isStopping;
    private bool _disposed;

    public CameraStreamService(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;

        var libVlcDirectory = LibVlcRuntimeLocator.Locate();
        var pluginsDirectory = Path.Combine(libVlcDirectory, "plugins");
        if (Directory.Exists(pluginsDirectory))
        {
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDirectory);
        }

        Core.Initialize(libVlcDirectory);
        _libVlc = new LibVLC(
            "--no-audio",
            "--no-video-title-show",
            "--quiet");

        InMediaPlayer = CreatePlayer(LaneDirection.In);
        OutMediaPlayer = CreatePlayer(LaneDirection.Out);
    }

    public MediaPlayer InMediaPlayer { get; }
    public MediaPlayer OutMediaPlayer { get; }

    public string InStatus { get; private set; } = "Not started";
    public string OutStatus { get; private set; } = "Not started";

    public event EventHandler<CameraStatusChangedEventArgs>? StatusChanged;

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _isStopping = false;
        PlayLane(LaneDirection.In);
        PlayLane(LaneDirection.Out);
        _watchdogTask = RunWatchdogAsync(_shutdown.Token);
    }

    public void Stop()
    {
        if (_isStopping)
        {
            return;
        }

        _isStopping = true;
        _shutdown.Cancel();

        StopPlayer(InMediaPlayer);
        StopPlayer(OutMediaPlayer);

        PublishStatus(LaneDirection.In, "Stopped");
        PublishStatus(LaneDirection.Out, "Stopped");
    }

    public void ReloadConfiguration()
    {
        if (_disposed || !_started || _isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        InMediaPlayer.EnableHardwareDecoding =
            _options.Cameras.In.EnableHardwareDecoding;
        OutMediaPlayer.EnableHardwareDecoding =
            _options.Cameras.Out.EnableHardwareDecoding;

        ClearRestartScheduled(LaneDirection.In);
        ClearRestartScheduled(LaneDirection.Out);
        ReloadLaneConfiguration(LaneDirection.In);
        ReloadLaneConfiguration(LaneDirection.Out);
    }

    private void ReloadLaneConfiguration(LaneDirection direction)
    {
        StopPlayer(GetPlayer(direction));
        GetPlaybackState(direction).Reset();

        if (!GetOptions(direction).Enabled)
        {
            PublishStatus(direction, "Camera disabled");
            return;
        }

        PlayLane(direction);
    }

    public async Task<bool> CaptureSnapshotAsync(
        LaneDirection direction,
        string path,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var player = GetPlayer(direction);
        var snapshotLock = direction == LaneDirection.In
            ? _inSnapshotLock
            : _outSnapshotLock;

        await snapshotLock.WaitAsync(cancellationToken);
        try
        {
            if (!player.IsPlaying)
            {
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var snapshotStarted = player.TakeSnapshot(
                0,
                path,
                (uint)Math.Max(0, width),
                (uint)Math.Max(0, height));

            if (!snapshotStarted)
            {
                return false;
            }

            var timeoutAt = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeoutAt)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    return true;
                }

                await Task.Delay(100, cancellationToken);
            }

            return false;
        }
        finally
        {
            snapshotLock.Release();
        }
    }

    private MediaPlayer CreatePlayer(LaneDirection direction)
    {
        var camera = GetOptions(direction);
        var player = new MediaPlayer(_libVlc)
        {
            // Some Hikvision/Intel/Direct3D combinations display only the first
            // decoded frame. Software decoding is the reliable default for the
            // two 1080p gate streams; it remains configurable per camera.
            EnableHardwareDecoding = camera.EnableHardwareDecoding
        };

        player.Opening += (_, _) => PublishStatus(direction, "Connecting...");
        player.Playing += (_, _) =>
        {
            GetPlaybackState(direction).Reset();
            PublishStatus(direction, "LIVE");
        };
        player.TimeChanged += (_, eventArgs) =>
            GetPlaybackState(direction).RecordProgress(eventArgs.Time);
        player.EncounteredError += (_, _) =>
        {
            PublishStatus(direction, "Connection failed - retrying");
            ScheduleRestart(direction);
        };
        player.EndReached += (_, _) =>
        {
            PublishStatus(direction, "Stream ended - retrying");
            ScheduleRestart(direction);
        };

        return player;
    }

    private void PlayLane(LaneDirection direction)
    {
        if (_isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        var camera = GetOptions(direction);
        if (!camera.Enabled)
        {
            PublishStatus(direction, "Camera disabled");
            return;
        }

        if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var streamUri))
        {
            PublishStatus(direction, "Invalid RTSP URL");
            _ = _logger.StatusAsync($"{direction.ToString().ToUpperInvariant()} camera has an invalid RTSP URL.");
            return;
        }

        try
        {
            var player = GetPlayer(direction);
            if (player.IsPlaying)
            {
                player.Stop();
            }

            GetPlaybackState(direction).Reset();

            using var media = new Media(_libVlc, streamUri);
            media.AddOption(":no-audio");

            if (camera.UseTcp)
            {
                media.AddOption(":rtsp-tcp");
            }

            var cacheMilliseconds = Math.Max(250, camera.NetworkCachingMilliseconds);
            media.AddOption($":network-caching={cacheMilliseconds}");
            media.AddOption($":live-caching={cacheMilliseconds}");

            if (!camera.EnableHardwareDecoding)
            {
                media.AddOption(":avcodec-hw=none");
            }

            PublishStatus(direction, "Connecting...");
            if (!player.Play(media))
            {
                PublishStatus(direction, "Unable to start - retrying");
                ScheduleRestart(direction);
            }
        }
        catch (Exception ex)
        {
            PublishStatus(direction, "Camera error - retrying");
            _ = _logger.StatusAsync(
                $"{direction.ToString().ToUpperInvariant()} camera start failed: {ex.Message}");
            ScheduleRestart(direction);
        }
    }

    private async Task RunWatchdogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                CheckForStalledStream(LaneDirection.In);
                CheckForStalledStream(LaneDirection.Out);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private void CheckForStalledStream(LaneDirection direction)
    {
        if (_isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        var camera = GetOptions(direction);
        var player = GetPlayer(direction);
        if (!camera.Enabled || !player.IsPlaying)
        {
            return;
        }

        var currentTime = player.Time;
        var playbackState = GetPlaybackState(direction);
        playbackState.RecordProgress(currentTime);

        var watchdogSeconds = Math.Max(8, camera.StreamWatchdogSeconds);
        if (!playbackState.IsStalled(TimeSpan.FromSeconds(watchdogSeconds)))
        {
            return;
        }

        PublishStatus(direction, "Video stalled - reconnecting");
        _ = _logger.StatusAsync(
            $"{direction.ToString().ToUpperInvariant()} camera stopped advancing for " +
            $"{watchdogSeconds} seconds. Restarting RTSP playback.");
        playbackState.Reset();
        ScheduleRestart(direction);
    }

    private void ScheduleRestart(LaneDirection direction)
    {
        if (_isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        lock (_restartSync)
        {
            if (direction == LaneDirection.In)
            {
                if (_inRestartScheduled)
                {
                    return;
                }

                _inRestartScheduled = true;
            }
            else
            {
                if (_outRestartScheduled)
                {
                    return;
                }

                _outRestartScheduled = true;
            }
        }

        _ = RestartAfterDelayAsync(direction);
    }

    private async Task RestartAfterDelayAsync(LaneDirection direction)
    {
        try
        {
            var camera = GetOptions(direction);
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, camera.ReconnectSeconds)),
                _shutdown.Token);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal shutdown.
            ClearRestartScheduled(direction);
            return;
        }

        ClearRestartScheduled(direction);
        PlayLane(direction);
    }

    private void ClearRestartScheduled(LaneDirection direction)
    {
        lock (_restartSync)
        {
            if (direction == LaneDirection.In)
            {
                _inRestartScheduled = false;
            }
            else
            {
                _outRestartScheduled = false;
            }
        }
    }

    private CameraLaneOptions GetOptions(LaneDirection direction) =>
        direction == LaneDirection.In ? _options.Cameras.In : _options.Cameras.Out;

    private MediaPlayer GetPlayer(LaneDirection direction) =>
        direction == LaneDirection.In ? InMediaPlayer : OutMediaPlayer;

    private LanePlaybackState GetPlaybackState(LaneDirection direction) =>
        direction == LaneDirection.In ? _inPlaybackState : _outPlaybackState;

    private void PublishStatus(LaneDirection direction, string status)
    {
        if (direction == LaneDirection.In)
        {
            InStatus = status;
        }
        else
        {
            OutStatus = status;
        }

        StatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(direction, status));
    }

    private static void StopPlayer(MediaPlayer player)
    {
        try
        {
            player.Stop();
        }
        catch
        {
            // Continue shutdown even if the native player is already unavailable.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isStopping = true;
        _shutdown.Cancel();

        StopPlayer(InMediaPlayer);
        StopPlayer(OutMediaPlayer);

        InMediaPlayer.Dispose();
        OutMediaPlayer.Dispose();
        _libVlc.Dispose();
        _inSnapshotLock.Dispose();
        _outSnapshotLock.Dispose();
        _shutdown.Dispose();
    }

    private sealed class LanePlaybackState
    {
        private readonly object _sync = new();
        private long _lastMediaTime = long.MinValue;
        private DateTime _lastProgressUtc = DateTime.UtcNow;
        private bool _hasUsableTime;

        public void Reset()
        {
            lock (_sync)
            {
                _lastMediaTime = long.MinValue;
                _lastProgressUtc = DateTime.UtcNow;
                _hasUsableTime = false;
            }
        }

        public void RecordProgress(long mediaTime)
        {
            // Some live streams return -1 until their first usable timestamp.
            if (mediaTime < 0)
            {
                return;
            }

            lock (_sync)
            {
                if (!_hasUsableTime || mediaTime != _lastMediaTime)
                {
                    _lastMediaTime = mediaTime;
                    _lastProgressUtc = DateTime.UtcNow;
                    _hasUsableTime = true;
                }
            }
        }

        public bool IsStalled(TimeSpan threshold)
        {
            lock (_sync)
            {
                return _hasUsableTime && DateTime.UtcNow - _lastProgressUtc >= threshold;
            }
        }
    }
}

public sealed class CameraStatusChangedEventArgs(
    LaneDirection direction,
    string status) : EventArgs
{
    public LaneDirection Direction { get; } = direction;
    public string Status { get; } = status;
}
