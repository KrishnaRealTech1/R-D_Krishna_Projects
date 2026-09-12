using System.Globalization;
using System.Text;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class AppLogger
{
    private readonly AppOptions _options;
    private readonly SystemEventHub _eventHub;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public AppLogger(AppOptions options, SystemEventHub eventHub)
    {
        _options = options;
        _eventHub = eventHub;
    }

    public Task StatusAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync(LogChannel.Status, message, cancellationToken);

    public Task ApiAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync(LogChannel.Api, message, cancellationToken);

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

    private string BuildDatedLogPath(LogChannel channel, DateTime timestamp)
    {
        var configuredPath = channel == LogChannel.Status
            ? _options.Storage.StatusLogFile
            : _options.Storage.ApiLogFile;
        var configuredFullPath = PathResolver.ResolveFromAppBase(configuredPath);
        var configuredDirectory = Path.GetDirectoryName(configuredFullPath)
            ?? throw new InvalidOperationException($"The configured {channel} log path has no parent directory.");
        var configuredFileName = Path.GetFileName(configuredFullPath);

        var dateFolder = Path.Combine(
            configuredDirectory,
            timestamp.ToString("yyyy", CultureInfo.InvariantCulture),
            timestamp.ToString("MM", CultureInfo.InvariantCulture),
            timestamp.ToString("dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dateFolder);

        return Path.Combine(
            dateFolder,
            string.IsNullOrWhiteSpace(configuredFileName)
                ? (channel == LogChannel.Status ? "status.log" : "api.log")
                : configuredFileName);
    }
}
