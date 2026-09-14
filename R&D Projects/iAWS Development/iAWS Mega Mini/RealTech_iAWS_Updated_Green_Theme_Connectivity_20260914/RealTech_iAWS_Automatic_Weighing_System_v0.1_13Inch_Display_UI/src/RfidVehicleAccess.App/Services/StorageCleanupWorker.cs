using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed class StorageCleanupWorker(
    AppOptions options,
    AppLogger logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _cleanupRequest = new(0, 1);

    public void RequestCleanup()
    {
        try
        {
            _cleanupRequest.Release();
        }
        catch (SemaphoreFullException)
        {
            // A cleanup is already queued.
        }
        catch (ObjectDisposedException)
        {
            // The application is shutting down.
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cleanupRequest.WaitAsync(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunCleanupSafelyAsync(stoppingToken);
        }
    }

    private async Task RunCleanupSafelyAsync(CancellationToken cancellationToken)
    {
        if (!options.Storage.AutoDeleteEnabled)
        {
            return;
        }

        try
        {
            var imageRoot = PathResolver.ResolveFromAppBase(options.Cameras.LocalImageFolder);
            var statusLogRoot = ResolveLogRoot(options.Storage.StatusLogFile, "Status");
            var serverLogRoot = ResolveLogRoot(options.Storage.ServerLogFile, "Server");

            var imageResult = CleanupDateFolders(
                imageRoot,
                options.Storage.ImageRetentionDays,
                cancellationToken);
            var statusResult = CleanupDateFolders(
                statusLogRoot,
                options.Storage.LogRetentionDays,
                cancellationToken);
            var serverResult = CleanupDateFolders(
                serverLogRoot,
                options.Storage.LogRetentionDays,
                cancellationToken);

            var deletedFiles = imageResult.DeletedFiles +
                               statusResult.DeletedFiles +
                               serverResult.DeletedFiles;
            var deletedBytes = imageResult.DeletedBytes +
                               statusResult.DeletedBytes +
                               serverResult.DeletedBytes;
            var failedFiles = imageResult.FailedFiles +
                              statusResult.FailedFiles +
                              serverResult.FailedFiles;

            if (deletedFiles > 0 || failedFiles > 0)
            {
                await logger.StatusAsync(
                    $"Storage auto-delete completed. Deleted {deletedFiles} old image/log file(s) " +
                    $"({FormatBytes(deletedBytes)}); failed files: {failedFiles}. " +
                    $"Retention: images {options.Storage.ImageRetentionDays} day(s), " +
                    $"logs {options.Storage.LogRetentionDays} day(s).",
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            try
            {
                await logger.StatusAsync(
                    $"Storage auto-delete failed: {ex.Message}",
                    cancellationToken);
            }
            catch
            {
                // Storage cleanup must never stop the application when logging is unavailable.
            }
        }
    }

    private static CleanupResult CleanupDateFolders(
        string root,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        if (!IsSafeCleanupRoot(root) || !Directory.Exists(root))
        {
            return CleanupResult.Empty;
        }

        var effectiveRetentionDays = Math.Max(1, retentionDays);
        var oldestDateToKeep = DateTime.Today.AddDays(-(effectiveRetentionDays - 1));
        var deletedFiles = 0;
        var failedFiles = 0;
        long deletedBytes = 0;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var filePath in Directory.EnumerateFiles(root, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetDateFolder(root, filePath, out var fileDate) ||
                fileDate >= oldestDateToKeep)
            {
                continue;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var length = fileInfo.Exists ? fileInfo.Length : 0;
                fileInfo.IsReadOnly = false;
                fileInfo.Delete();
                deletedFiles++;
                deletedBytes += length;
            }
            catch
            {
                failedFiles++;
            }
        }

        DeleteEmptyDirectories(root, cancellationToken);
        return new CleanupResult(deletedFiles, deletedBytes, failedFiles);
    }

    private static bool TryGetDateFolder(
        string root,
        string filePath,
        out DateTime date)
    {
        date = default;
        var relativePath = Path.GetRelativePath(root, filePath);
        var parts = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4 ||
            !int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var day))
        {
            return false;
        }

        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void DeleteEmptyDirectories(
        string root,
        CancellationToken cancellationToken)
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var directories = Directory.EnumerateDirectories(root, "*", enumerationOptions)
            .OrderByDescending(path => path.Length)
            .ToList();

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, recursive: false);
                }
            }
            catch
            {
                // A new file may have been created after the empty check; retry next cycle.
            }
        }
    }

    private static string ResolveLogRoot(string configuredPath, string channelFolderName)
    {
        var configuredFullPath = PathResolver.ResolveFromAppBase(configuredPath);
        var configuredDirectory = Path.GetDirectoryName(configuredFullPath)
            ?? throw new InvalidOperationException(
                $"The configured {channelFolderName} log path has no parent directory.");

        return string.Equals(
            Path.GetFileName(configuredDirectory),
            channelFolderName,
            StringComparison.OrdinalIgnoreCase)
            ? configuredDirectory
            : Path.Combine(configuredDirectory, channelFolderName);
    }

    private static bool IsSafeCleanupRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var driveRoot = Path.GetPathRoot(fullPath)?
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return !string.IsNullOrWhiteSpace(fullPath) &&
               !string.Equals(fullPath, driveRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var displayValue = (double)value;

        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return $"{displayValue:0.##} {units[unitIndex]}";
    }

    public override void Dispose()
    {
        _cleanupRequest.Dispose();
        base.Dispose();
    }

    private sealed record CleanupResult(
        int DeletedFiles,
        long DeletedBytes,
        int FailedFiles)
    {
        public static CleanupResult Empty { get; } = new(0, 0, 0);
    }
}
