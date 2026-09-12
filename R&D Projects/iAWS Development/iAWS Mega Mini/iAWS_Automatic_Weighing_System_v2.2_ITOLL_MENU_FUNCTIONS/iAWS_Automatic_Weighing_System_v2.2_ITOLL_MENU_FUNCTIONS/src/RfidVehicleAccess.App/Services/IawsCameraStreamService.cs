using LibVLCSharp.Shared;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class IawsCameraStreamService : IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly LibVLC _libVlc;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<IawsCameraPosition, MediaPlayer> _players = [];
    private readonly Dictionary<IawsCameraPosition, SemaphoreSlim> _snapshotLocks = [];
    private readonly Dictionary<IawsCameraPosition, string> _statuses = [];
    private readonly HashSet<IawsCameraPosition> _restartScheduled = [];
    private readonly object _restartSync = new();
    private bool _started;
    private bool _disposed;

    public IawsCameraStreamService(AppOptions options, AppLogger logger)
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

        foreach (var position in Enum.GetValues<IawsCameraPosition>())
        {
            _players[position] = CreatePlayer(position);
            _snapshotLocks[position] = new SemaphoreSlim(1, 1);
            _statuses[position] = "Not started";
        }
    }

    public MediaPlayer FrontMediaPlayer => _players[IawsCameraPosition.Front];
    public MediaPlayer BackMediaPlayer => _players[IawsCameraPosition.Back];
    public MediaPlayer LeftMediaPlayer => _players[IawsCameraPosition.Left];
    public MediaPlayer RightMediaPlayer => _players[IawsCameraPosition.Right];

    public event EventHandler<IawsCameraStatusChangedEventArgs>? StatusChanged;

    public string GetStatus(IawsCameraPosition position) =>
        _statuses.TryGetValue(position, out var status) ? status : "Unknown";

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        foreach (var position in Enum.GetValues<IawsCameraPosition>())
        {
            Play(position);
        }
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _shutdown.Cancel();
        foreach (var position in Enum.GetValues<IawsCameraPosition>())
        {
            StopPlayer(_players[position]);
            PublishStatus(position, "Stopped");
        }
    }

    public void ReloadConfiguration()
    {
        if (_disposed || !_started)
        {
            return;
        }

        foreach (var position in Enum.GetValues<IawsCameraPosition>())
        {
            var player = _players[position];
            var camera = GetOptions(position);
            player.EnableHardwareDecoding = camera.EnableHardwareDecoding;
            StopPlayer(player);
            lock (_restartSync)
            {
                _restartScheduled.Remove(position);
            }
            Play(position);
        }
    }

    public async Task<bool> CaptureSnapshotAsync(
        IawsCameraPosition position,
        string path,
        CancellationToken cancellationToken = default)
    {
        var camera = GetOptions(position);
        var player = _players[position];
        var snapshotLock = _snapshotLocks[position];

        await snapshotLock.WaitAsync(cancellationToken);
        try
        {
            if (!camera.Enabled || !player.IsPlaying)
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

            if (!player.TakeSnapshot(
                    0,
                    path,
                    (uint)Math.Max(0, camera.SnapshotWidth),
                    (uint)Math.Max(0, camera.SnapshotHeight)))
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

    private MediaPlayer CreatePlayer(IawsCameraPosition position)
    {
        var player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = GetOptions(position).EnableHardwareDecoding
        };

        player.Opening += (_, _) => PublishStatus(position, "Connecting...");
        player.Playing += (_, _) => PublishStatus(position, "LIVE");
        player.EncounteredError += (_, _) =>
        {
            PublishStatus(position, "Connection failed - retrying");
            ScheduleRestart(position);
        };
        player.EndReached += (_, _) =>
        {
            PublishStatus(position, "Stream ended - retrying");
            ScheduleRestart(position);
        };

        return player;
    }

    private void Play(IawsCameraPosition position)
    {
        if (_shutdown.IsCancellationRequested || _disposed)
        {
            return;
        }

        var camera = GetOptions(position);
        if (!camera.Enabled)
        {
            PublishStatus(position, "Camera disabled");
            return;
        }

        if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var streamUri))
        {
            PublishStatus(position, "Invalid RTSP URL");
            return;
        }

        try
        {
            var player = _players[position];
            StopPlayer(player);
            using var media = new Media(_libVlc, streamUri);
            media.AddOption(":no-audio");
            if (camera.UseTcp)
            {
                media.AddOption(":rtsp-tcp");
            }

            var cache = Math.Max(250, camera.NetworkCachingMilliseconds);
            media.AddOption($":network-caching={cache}");
            media.AddOption($":live-caching={cache}");
            if (!camera.EnableHardwareDecoding)
            {
                media.AddOption(":avcodec-hw=none");
            }

            PublishStatus(position, "Connecting...");
            if (!player.Play(media))
            {
                PublishStatus(position, "Unable to start - retrying");
                ScheduleRestart(position);
            }
        }
        catch (Exception ex)
        {
            PublishStatus(position, "Camera error - retrying");
            _ = _logger.StatusAsync($"{position} camera start failed: {ex.Message}");
            ScheduleRestart(position);
        }
    }

    private void ScheduleRestart(IawsCameraPosition position)
    {
        lock (_restartSync)
        {
            if (_restartScheduled.Contains(position) || _shutdown.IsCancellationRequested)
            {
                return;
            }
            _restartScheduled.Add(position);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = Math.Max(2, GetOptions(position).ReconnectSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delay), _shutdown.Token);
                Play(position);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            finally
            {
                lock (_restartSync)
                {
                    _restartScheduled.Remove(position);
                }
            }
        });
    }

    private void PublishStatus(IawsCameraPosition position, string status)
    {
        _statuses[position] = status;
        StatusChanged?.Invoke(this, new IawsCameraStatusChangedEventArgs(position, status));
    }

    private CameraLaneOptions GetOptions(IawsCameraPosition position) => position switch
    {
        IawsCameraPosition.Front => _options.Cameras.Front,
        IawsCameraPosition.Back => _options.Cameras.Back,
        IawsCameraPosition.Left => _options.Cameras.Left,
        IawsCameraPosition.Right => _options.Cameras.Right,
        _ => _options.Cameras.Front
    };

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
            // Best effort.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        foreach (var player in _players.Values)
        {
            StopPlayer(player);
            player.Dispose();
        }
        foreach (var snapshotLock in _snapshotLocks.Values)
        {
            snapshotLock.Dispose();
        }
        _libVlc.Dispose();
        _shutdown.Dispose();
    }
}
