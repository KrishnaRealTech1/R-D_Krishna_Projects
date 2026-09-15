using System.Windows;
using System.Windows.Media;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Plays configured operator/driver audio cues from the dedicated iAWS Audio folder.
/// Playback requests are serialized so RFID and completion announcements never overlap.
/// </summary>
public sealed class AudioPlaybackService
{
    private static readonly TimeSpan MaximumSinglePlaybackTime = TimeSpan.FromMinutes(5);

    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _playbackLock = new(1, 1);

    public AudioPlaybackService(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public void QueueRfidDetected()
    {
        _ = PlayCueSafelyAsync(
            "RFID detected",
            () => _options.Audio.RfidDetected);
    }

    public void QueueProcessCompleted()
    {
        _ = PlayCueSafelyAsync(
            "Process completed / barrier open",
            () => _options.Audio.ProcessCompleted);
    }

    private async Task PlayCueSafelyAsync(
        string cueName,
        Func<AudioCueOptions> getCue)
    {
        try
        {
            if (!_options.Audio.Enabled)
            {
                return;
            }

            var cue = getCue();
            if (!cue.Enabled || string.IsNullOrWhiteSpace(cue.FilePath))
            {
                return;
            }

            string path;
            try
            {
                path = PathResolver.ResolveFromAppBase(cue.FilePath);
            }
            catch (Exception ex)
            {
                await _logger.StatusAsync($"Audio '{cueName}' path is invalid: {ex.Message}");
                return;
            }

            if (!File.Exists(path))
            {
                await _logger.StatusAsync($"Audio '{cueName}' file not found: {path}");
                return;
            }

            var repeatCount = Math.Clamp(cue.RepeatCount, 1, 100);
            await _playbackLock.WaitAsync();
            try
            {
                for (var playNumber = 1; playNumber <= repeatCount; playNumber++)
                {
                    await PlayOnceAsync(path);
                }
            }
            finally
            {
                _playbackLock.Release();
            }
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Audio '{cueName}' playback failed: {ex.Message}");
        }
    }

    private static async Task PlayOnceAsync(string filePath)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The WPF application dispatcher is unavailable.");

        MediaPlayer? player = null;
        EventHandler? mediaOpened = null;
        EventHandler? mediaEnded = null;
        EventHandler<ExceptionEventArgs>? mediaFailed = null;
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await dispatcher.InvokeAsync(() =>
        {
            player = new MediaPlayer
            {
                Volume = 1.0
            };

            mediaOpened = (_, _) => player.Play();
            mediaEnded = (_, _) => completion.TrySetResult(true);
            mediaFailed = (_, args) => completion.TrySetException(
                args.ErrorException ?? new InvalidOperationException("Windows Media Player could not decode the audio file."));

            player.MediaOpened += mediaOpened;
            player.MediaEnded += mediaEnded;
            player.MediaFailed += mediaFailed;
            player.Open(new Uri(filePath, UriKind.Absolute));
        });

        try
        {
            await completion.Task.WaitAsync(MaximumSinglePlaybackTime);
        }
        finally
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (player is null)
                {
                    return;
                }

                if (mediaOpened is not null)
                {
                    player.MediaOpened -= mediaOpened;
                }

                if (mediaEnded is not null)
                {
                    player.MediaEnded -= mediaEnded;
                }

                if (mediaFailed is not null)
                {
                    player.MediaFailed -= mediaFailed;
                }

                player.Stop();
                player.Close();
            });
        }
    }
}
