using LibVLCSharp.Shared;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Manages the four editable RTSP cameras shown on the iAWS dashboard.
/// Cameras are intentionally independent from IN/OUT direction: every accepted
/// weighing transaction captures all enabled cameras.
/// </summary>
public sealed class CameraStreamService : IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly LibVLC _libVlc;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<int, MediaPlayer> _players = [];
    private readonly Dictionary<int, SemaphoreSlim> _snapshotLocks = [];
    private readonly Dictionary<int, PlaybackState> _playbackStates = [];
    private readonly HashSet<int> _restartScheduled = [];
    private readonly object _restartSync = new();

    private Task? _watchdogTask;
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
        _libVlc = new LibVLC("--no-audio", "--no-video-title-show", "--quiet");

        for (var cameraNumber = 1; cameraNumber <= 4; cameraNumber++)
        {
            _snapshotLocks[cameraNumber] = new SemaphoreSlim(1, 1);
            _playbackStates[cameraNumber] = new PlaybackState();
            _players[cameraNumber] = CreatePlayer(cameraNumber);
            _statuses[cameraNumber] = "Not started";
        }
    }

    private readonly Dictionary<int, string> _statuses = [];

    public MediaPlayer Camera1MediaPlayer => _players[1];
    public MediaPlayer Camera2MediaPlayer => _players[2];
    public MediaPlayer Camera3MediaPlayer => _players[3];
    public MediaPlayer Camera4MediaPlayer => _players[4];


    public string Camera1Status => GetStatus(1);
    public string Camera2Status => GetStatus(2);
    public string Camera3Status => GetStatus(3);
    public string Camera4Status => GetStatus(4);

    public event EventHandler<CameraStatusChangedEventArgs>? StatusChanged;

    public MediaPlayer GetMediaPlayer(int cameraNumber) =>
        _players.TryGetValue(cameraNumber, out var player)
            ? player
            : throw new ArgumentOutOfRangeException(nameof(cameraNumber), cameraNumber, "Camera number must be 1-4.");

    public string GetStatus(int cameraNumber) =>
        _statuses.TryGetValue(cameraNumber, out var status) ? status : "Unknown";

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _isStopping = false;
        for (var cameraNumber = 1; cameraNumber <= 4; cameraNumber++)
        {
            PlayCamera(cameraNumber);
        }

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
        foreach (var cameraNumber in Enumerable.Range(1, 4))
        {
            StopPlayer(_players[cameraNumber]);
            PublishStatus(cameraNumber, "Stopped");
        }
    }

    public void ReloadConfiguration()
    {
        if (_disposed || !_started || _isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        for (var cameraNumber = 1; cameraNumber <= 4; cameraNumber++)
        {
            var camera = _options.Cameras.Get(cameraNumber);
            _players[cameraNumber].EnableHardwareDecoding = camera.EnableHardwareDecoding;
            lock (_restartSync)
            {
                _restartScheduled.Remove(cameraNumber);
            }

            StopPlayer(_players[cameraNumber]);
            _playbackStates[cameraNumber].Reset();
            PlayCamera(cameraNumber);
        }
    }

    public async Task<bool> CaptureSnapshotAsync(
        int cameraNumber,
        string path,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var player = GetMediaPlayer(cameraNumber);
        var snapshotLock = _snapshotLocks[cameraNumber];
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

            var started = player.TakeSnapshot(
                0,
                path,
                (uint)Math.Max(0, width),
                (uint)Math.Max(0, height));
            if (!started)
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

    private MediaPlayer CreatePlayer(int cameraNumber)
    {
        var camera = _options.Cameras.Get(cameraNumber);
        var player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = camera.EnableHardwareDecoding
        };

        player.Opening += (_, _) => PublishStatus(cameraNumber, "Connecting...");
        player.Playing += (_, _) =>
        {
            _playbackStates[cameraNumber].Reset();
            PublishStatus(cameraNumber, "LIVE");
        };
        player.TimeChanged += (_, eventArgs) =>
            _playbackStates[cameraNumber].RecordProgress(eventArgs.Time);
        player.EncounteredError += (_, _) =>
        {
            PublishStatus(cameraNumber, "Connection failed - retrying");
            ScheduleRestart(cameraNumber);
        };
        player.EndReached += (_, _) =>
        {
            PublishStatus(cameraNumber, "Stream ended - retrying");
            ScheduleRestart(cameraNumber);
        };

        return player;
    }

    private void PlayCamera(int cameraNumber)
    {
        if (_isStopping || _shutdown.IsCancellationRequested)
        {
            return;
        }

        var camera = _options.Cameras.Get(cameraNumber);
        if (!camera.Enabled)
        {
            PublishStatus(cameraNumber, "Camera disabled");
            return;
        }

        if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var streamUri) ||
            !string.Equals(streamUri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            PublishStatus(cameraNumber, "Invalid RTSP URL");
            _ = _logger.StatusAsync($"Camera {cameraNumber} has an invalid RTSP URL.");
            return;
        }

        try
        {
            var player = _players[cameraNumber];
            StopPlayer(player);
            _playbackStates[cameraNumber].Reset();

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

            PublishStatus(cameraNumber, "Connecting...");
            if (!player.Play(media))
            {
                PublishStatus(cameraNumber, "Unable to start - retrying");
                ScheduleRestart(cameraNumber);
            }
        }
        catch (Exception ex)
        {
            PublishStatus(cameraNumber, "Camera error - retrying");
            _ = _logger.StatusAsync($"Camera {cameraNumber} stream error: {ex.Message}");
            ScheduleRestart(cameraNumber);
        }
    }

    private void ScheduleRestart(int cameraNumber)
    {
        lock (_restartSync)
        {
            if (_restartScheduled.Contains(cameraNumber) || _isStopping || _shutdown.IsCancellationRequested)
            {
                return;
            }

            _restartScheduled.Add(cameraNumber);
        }

        _ = RestartAfterDelayAsync(cameraNumber, _shutdown.Token);
    }

    private async Task RestartAfterDelayAsync(int cameraNumber, CancellationToken cancellationToken)
    {
        try
        {
            var delaySeconds = Math.Max(1, _options.Cameras.Get(cameraNumber).ReconnectSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                PlayCamera(cameraNumber);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            lock (_restartSync)
            {
                _restartScheduled.Remove(cameraNumber);
            }
        }
    }

    private async Task RunWatchdogAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                for (var cameraNumber = 1; cameraNumber <= 4; cameraNumber++)
                {
                    var camera = _options.Cameras.Get(cameraNumber);
                    if (!camera.Enabled)
                    {
                        continue;
                    }

                    var player = _players[cameraNumber];
                    var threshold = TimeSpan.FromSeconds(Math.Max(5, camera.StreamWatchdogSeconds));
                    if (player.IsPlaying && _playbackStates[cameraNumber].IsStalled(threshold))
                    {
                        PublishStatus(cameraNumber, "Video stalled - reconnecting");
                        ScheduleRestart(cameraNumber);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private void PublishStatus(int cameraNumber, string status)
    {
        _statuses[cameraNumber] = status;
        StatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(cameraNumber, status));
    }

    private static void StopPlayer(MediaPlayer player)
    {
        try
        {
            if (player.IsPlaying)
            {
                player.Stop();
            }
        }
        catch
        {
            // Continue shutdown/reconnect if native VLC is already unavailable.
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

        foreach (var cameraNumber in Enumerable.Range(1, 4))
        {
            StopPlayer(_players[cameraNumber]);
            _players[cameraNumber].Dispose();
            _snapshotLocks[cameraNumber].Dispose();
        }

        _libVlc.Dispose();
        _shutdown.Dispose();
    }

    private sealed class PlaybackState
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
    int cameraNumber,
    string status) : EventArgs
{
    public int CameraNumber { get; } = cameraNumber;
    public string Status { get; } = status;
}
