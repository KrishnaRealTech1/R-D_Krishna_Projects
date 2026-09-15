using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class AppLogger
{
    private sealed record StatusProcessClock(string RfidNumber, Stopwatch Stopwatch);

    private readonly AppOptions _options;
    private readonly SystemEventHub _eventHub;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, StatusProcessClock> _statusProcessClocks =
        new(StringComparer.OrdinalIgnoreCase);

    public AppLogger(AppOptions options, SystemEventHub eventHub)
    {
        _options = options;
        _eventHub = eventHub;
    }

    public Task StatusAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync(
            LogChannel.Status,
            DecorateStatusMessage(message),
            cancellationToken);

    public Task ServerAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync(LogChannel.Server, message, cancellationToken);

    public void StartStatusProcessTimer(string lane, string rfidNumber)
    {
        var normalizedLane = NormalizeLane(lane);
        _statusProcessClocks[normalizedLane] =
            new StatusProcessClock(rfidNumber, Stopwatch.StartNew());
    }

    public TimeSpan? GetStatusProcessElapsed(string lane)
    {
        var normalizedLane = NormalizeLane(lane);
        return _statusProcessClocks.TryGetValue(normalizedLane, out var clock)
            ? clock.Stopwatch.Elapsed
            : null;
    }

    public TimeSpan? StopStatusProcessTimer(string lane)
    {
        var normalizedLane = NormalizeLane(lane);
        if (!_statusProcessClocks.TryRemove(normalizedLane, out var clock))
        {
            return null;
        }

        clock.Stopwatch.Stop();
        return clock.Stopwatch.Elapsed;
    }

    private async Task WriteAsync(
        LogChannel channel,
        string message,
        CancellationToken cancellationToken)
    {
        var entry = new AppLogEntry(DateTimeOffset.Now, channel, message);
        _eventHub.PublishLog(entry);

        var filePath = BuildDatedLogPath(channel, entry.Timestamp.LocalDateTime);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                filePath,
                entry.DisplayText + Environment.NewLine,
                Encoding.UTF8,
                cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private string DecorateStatusMessage(string message)
    {
        var lane = GetLanePrefix(message);
        if (lane is null || !_statusProcessClocks.TryGetValue(lane, out var clock))
        {
            return message;
        }

        return $"[{lane} PROCESS {FormatElapsed(clock.Stopwatch.Elapsed)} | RFID {clock.RfidNumber}] {message}";
    }

    private static string? GetLanePrefix(string message)
    {
        if (message.StartsWith("IN ", StringComparison.OrdinalIgnoreCase))
        {
            return "IN";
        }

        if (message.StartsWith("OUT ", StringComparison.OrdinalIgnoreCase))
        {
            return "OUT";
        }

        return null;
    }

    private static string NormalizeLane(string lane) =>
        string.Equals(lane, "OUT", StringComparison.OrdinalIgnoreCase) ? "OUT" : "IN";

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);

    private string BuildDatedLogPath(LogChannel channel, DateTime timestamp)
    {
        var configuredPath = channel == LogChannel.Status
            ? _options.Storage.StatusLogFile
            : _options.Storage.ServerLogFile;
        var configuredFullPath = PathResolver.ResolveFromAppBase(configuredPath);
        var configuredDirectory = Path.GetDirectoryName(configuredFullPath)
            ?? throw new InvalidOperationException(
                $"The configured {channel} log path has no parent directory.");
        var configuredFileName = Path.GetFileName(configuredFullPath);

        if (string.IsNullOrWhiteSpace(configuredFileName))
        {
            configuredFileName = channel == LogChannel.Status
                ? "status.log"
                : "server.log";
        }

        var channelFolderName = channel == LogChannel.Status ? "Status" : "Server";
        var channelRoot = string.Equals(
            Path.GetFileName(configuredDirectory),
            channelFolderName,
            StringComparison.OrdinalIgnoreCase)
            ? configuredDirectory
            : Path.Combine(configuredDirectory, channelFolderName);

        var dateFolder = Path.Combine(
            channelRoot,
            timestamp.ToString("yyyy", CultureInfo.InvariantCulture),
            timestamp.ToString("MM", CultureInfo.InvariantCulture),
            timestamp.ToString("dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dateFolder);

        return Path.Combine(dateFolder, configuredFileName);
    }
}
