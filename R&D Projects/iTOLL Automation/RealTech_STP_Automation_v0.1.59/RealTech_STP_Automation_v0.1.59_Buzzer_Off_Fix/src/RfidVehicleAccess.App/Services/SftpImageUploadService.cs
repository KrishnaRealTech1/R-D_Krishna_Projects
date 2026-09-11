using System.Diagnostics;
using Renci.SshNet;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class SftpImageUploadService(AppOptions options) : IDisposable
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _clientLock = new();
    private SftpClient? _client;
    private string _connectionFingerprint = string.Empty;
    private string _preparedRemoteDirectory = string.Empty;
    private DateTimeOffset? _cooldownUntil;
    private string _lastFailureMessage = string.Empty;
    private bool _disposed;

    public bool IsEnabled => IsSftpMode(options.Server.ImageUpload.Mode);

    public async Task<SftpUploadResult?> UploadAsync(
        TripRecord trip,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(trip.ImagePath) || !File.Exists(trip.ImagePath))
        {
            return null;
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SftpImageUploadService));
        }
        var upload = options.Server.ImageUpload;
        ValidateConfiguration(upload);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfCoolingDown();

            var totalTimeout = TimeSpan.FromSeconds(
                Math.Max(5, upload.TotalTimeoutSeconds));

            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(totalTimeout);

            var operationTask = Task.Run(
                () => UploadCore(trip.ImagePath, upload, timeoutSource.Token),
                CancellationToken.None);

            try
            {
                var result = await operationTask.WaitAsync(timeoutSource.Token);
                ClearCooldown();
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                AbortClient();
                await ObserveStoppedOperationAsync(operationTask);
                var timeoutException = new TimeoutException(
                    $"SFTP upload exceeded the total timeout of {totalTimeout.TotalSeconds:0} second(s).");
                StartCooldown(timeoutException);
                throw timeoutException;
            }
            catch (OperationCanceledException)
            {
                AbortClient();
                await ObserveStoppedOperationAsync(operationTask);
                throw;
            }
            catch (Exception ex)
            {
                AbortClient();
                StartCooldown(ex);
                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AbortClient();
        _operationLock.Dispose();
    }


    private void ThrowIfCoolingDown()
    {
        if (!_cooldownUntil.HasValue || _cooldownUntil.Value <= DateTimeOffset.Now)
        {
            return;
        }

        throw new IOException(
            $"SFTP retry cooldown is active until {_cooldownUntil.Value:HH:mm:ss}: " +
            _lastFailureMessage);
    }

    private void StartCooldown(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        _lastFailureMessage = current.Message;
        _cooldownUntil = DateTimeOffset.Now.AddSeconds(
            Math.Max(2, options.Server.SyncIntervalSeconds));
    }

    private void ClearCooldown()
    {
        _cooldownUntil = null;
        _lastFailureMessage = string.Empty;
    }

    private SftpUploadResult UploadCore(
        string localImagePath,
        ImageUploadOptions upload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var (client, reusedConnection) = GetConnectedClient(upload);
        cancellationToken.ThrowIfCancellationRequested();

        var remoteDirectory = NormalizeRemoteDirectory(upload.RemoteDirectory);
        if (!string.Equals(
                _preparedRemoteDirectory,
                remoteDirectory,
                StringComparison.Ordinal))
        {
            EnsureRemoteDirectory(client, remoteDirectory, cancellationToken);
            _preparedRemoteDirectory = remoteDirectory;
        }

        var fileInfo = new FileInfo(localImagePath);
        var remotePath = CombineRemotePath(remoteDirectory, fileInfo.Name);

        using var input = new FileStream(
            localImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.SequentialScan);

        client.UploadFile(
            input,
            remotePath,
            canOverride: true,
            uploadedBytes =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (uploadedBytes > (ulong)fileInfo.Length)
                {
                    throw new IOException(
                        $"SFTP reported {uploadedBytes} uploaded bytes for a {fileInfo.Length}-byte file.");
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();
        return new SftpUploadResult(
            remotePath,
            fileInfo.Length,
            stopwatch.Elapsed,
            reusedConnection);
    }

    private (SftpClient Client, bool ReusedConnection) GetConnectedClient(
        ImageUploadOptions upload)
    {
        var fingerprint = BuildConnectionFingerprint(upload);
        var existing = GetClient();

        if (existing is not null &&
            existing.IsConnected &&
            string.Equals(_connectionFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return (existing, true);
        }

        AbortClient();

        var connectionInfo = new PasswordConnectionInfo(
            upload.Host,
            upload.Port,
            upload.Username,
            upload.Password)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, upload.ConnectionTimeoutSeconds))
        };

        var client = new SftpClient(connectionInfo)
        {
            OperationTimeout = TimeSpan.FromSeconds(Math.Max(1, upload.OperationTimeoutSeconds)),
            BufferSize = 128u * 1024u
        };

        try
        {
            client.Connect();
        }
        catch
        {
            client.Dispose();
            throw;
        }

        lock (_clientLock)
        {
            _client = client;
            _connectionFingerprint = fingerprint;
            _preparedRemoteDirectory = string.Empty;
        }

        return (client, false);
    }

    private SftpClient? GetClient()
    {
        lock (_clientLock)
        {
            return _client;
        }
    }

    private void AbortClient()
    {
        SftpClient? client;
        lock (_clientLock)
        {
            client = _client;
            _client = null;
            _connectionFingerprint = string.Empty;
            _preparedRemoteDirectory = string.Empty;
        }

        if (client is null)
        {
            return;
        }

        try
        {
            // Dispose the session directly. Calling Disconnect first can itself block when
            // the network connection is already stalled, which would defeat the total timeout.
            client.Dispose();
        }
        catch
        {
            // The connection is already faulted or is being aborted because of timeout.
        }
    }

    private static async Task ObserveStoppedOperationAsync(Task operationTask)
    {
        try
        {
            await operationTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // The original timeout/cancellation is more useful than a secondary abort error.
            // If the SSH.NET call is still unwinding, observe a later fault so it cannot become
            // an unobserved task exception after the worker has continued.
            if (!operationTask.IsCompleted)
            {
                _ = operationTask.ContinueWith(
                    completedTask => _ = completedTask.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted |
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private static bool IsSftpMode(string mode) =>
        string.Equals(mode, "Sftp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase);

    private static void ValidateConfiguration(ImageUploadOptions upload)
    {
        if (string.IsNullOrWhiteSpace(upload.Host))
        {
            throw new InvalidOperationException("SFTP Host is empty.");
        }

        if (upload.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("SFTP Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(upload.Username))
        {
            throw new InvalidOperationException("SFTP Username is empty.");
        }
    }

    private static void EnsureRemoteDirectory(
        SftpClient client,
        string remoteDirectory,
        CancellationToken cancellationToken)
    {
        if (remoteDirectory == "/")
        {
            return;
        }

        var current = remoteDirectory.StartsWith('/') ? "/" : string.Empty;
        foreach (var segment in remoteDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = CombineRemotePath(current, segment);
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }

    private static string NormalizeRemoteDirectory(string remoteDirectory)
    {
        var normalized = string.IsNullOrWhiteSpace(remoteDirectory)
            ? "/"
            : remoteDirectory.Trim().Replace('\\', '/');

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static string CombineRemotePath(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || left == "/")
        {
            return "/" + right.Trim('/');
        }

        return left.TrimEnd('/') + "/" + right.Trim('/');
    }

    private static string BuildConnectionFingerprint(ImageUploadOptions upload) =>
        $"{upload.Host.Trim().ToUpperInvariant()}|{upload.Port}|{upload.Username}|{upload.Password}";
}

public sealed record SftpUploadResult(
    string RemotePath,
    long BytesUploaded,
    TimeSpan Duration,
    bool ReusedConnection);
