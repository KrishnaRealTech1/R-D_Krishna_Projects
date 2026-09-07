using System.Diagnostics;
using System.Security.Authentication;
using Renci.SshNet;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Uploads captured trip images using the configured image-transfer protocol.
/// Supported modes are Disabled, Sftp and Ftp. FTP/FTPS transfers use the
/// WinSCP transfer engine for robust TLS-session and data-channel handling.
/// </summary>
public sealed class ImageUploadService(AppOptions options) : IDisposable
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _sftpClientLock = new();
    private readonly object _ftpSessionLock = new();
    private SftpClient? _sftpClient;
    private WinSCP.Session? _activeFtpSession;
    private string _sftpConnectionFingerprint = string.Empty;
    private string _preparedSftpRemoteDirectory = string.Empty;
    private DateTimeOffset? _cooldownUntil;
    private string _lastFailureMessage = string.Empty;
    private bool _disposed;

    public bool IsEnabled => IsSftpMode(options.Server.ImageUpload.Mode) ||
                             IsFtpMode(options.Server.ImageUpload.Mode);

    public string ProtocolName => GetProtocolName(
        options.Server.ImageUpload.Mode,
        options.Server.ImageUpload.UseTls);

    public async Task<ImageUploadResult?> UploadAsync(
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
            throw new ObjectDisposedException(nameof(ImageUploadService));
        }

        var upload = options.Server.ImageUpload;
        ValidateConfiguration(upload);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfCoolingDown();

            var totalTimeout = TimeSpan.FromSeconds(Math.Max(5, upload.TotalTimeoutSeconds));
            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(totalTimeout);

            var operationTask = IsFtpMode(upload.Mode)
                ? UploadFtpCoreAsync(trip.ImagePath, upload, timeoutSource.Token)
                : Task.Run(
                    () => UploadSftpCore(trip.ImagePath, upload, timeoutSource.Token),
                    CancellationToken.None);

            try
            {
                var result = await operationTask.WaitAsync(timeoutSource.Token);
                ClearCooldown();
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                AbortCurrentTransfer();
                await ObserveStoppedOperationAsync(operationTask);
                var timeoutException = new TimeoutException(
                    $"{GetProtocolName(upload.Mode, upload.UseTls)} upload exceeded the total timeout of " +
                    $"{totalTimeout.TotalSeconds:0} second(s).");
                StartCooldown(timeoutException);
                throw timeoutException;
            }
            catch (OperationCanceledException)
            {
                AbortCurrentTransfer();
                await ObserveStoppedOperationAsync(operationTask);
                throw;
            }
            catch (Exception ex)
            {
                AbortCurrentTransfer();
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
        AbortCurrentTransfer();
        _operationLock.Dispose();
    }

    private void ThrowIfCoolingDown()
    {
        if (!_cooldownUntil.HasValue || _cooldownUntil.Value <= DateTimeOffset.Now)
        {
            return;
        }

        throw new IOException(
            $"{ProtocolName} retry cooldown is active until {_cooldownUntil.Value:HH:mm:ss}: " +
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

    private ImageUploadResult UploadSftpCore(
        string localImagePath,
        ImageUploadOptions upload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var (client, reusedConnection) = GetConnectedSftpClient(upload);
        cancellationToken.ThrowIfCancellationRequested();

        var remoteDirectory = NormalizeRemoteDirectory(upload.RemoteDirectory);
        if (!string.Equals(
                _preparedSftpRemoteDirectory,
                remoteDirectory,
                StringComparison.Ordinal))
        {
            EnsureSftpRemoteDirectory(client, remoteDirectory, cancellationToken);
            _preparedSftpRemoteDirectory = remoteDirectory;
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
                        $"SFTP reported {uploadedBytes} uploaded bytes for a " +
                        $"{fileInfo.Length}-byte file.");
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();
        return new ImageUploadResult(
            "SFTP",
            remotePath,
            fileInfo.Length,
            stopwatch.Elapsed,
            reusedConnection);
    }

    private Task<ImageUploadResult> UploadFtpCoreAsync(
        string localImagePath,
        ImageUploadOptions upload,
        CancellationToken cancellationToken)
    {
        // WinSCP's .NET API is synchronous. Run it off the caller thread and wire the
        // cancellation token to Session.Abort so total-timeout/cancellation still works.
        return Task.Run(
            () => UploadFtpWithWinScpCore(localImagePath, upload, cancellationToken),
            CancellationToken.None);
    }

    private ImageUploadResult UploadFtpWithWinScpCore(
        string localImagePath,
        ImageUploadOptions upload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var fileInfo = new FileInfo(localImagePath);
        var remoteDirectory = NormalizeRemoteDirectory(upload.RemoteDirectory);
        var remotePath = CombineRemotePath(remoteDirectory, fileInfo.Name);

        var sessionOptions = BuildWinScpSessionOptions(upload);
        using var session = new WinSCP.Session();
        SetActiveFtpSession(session);

        try
        {
            using var cancellationRegistration = cancellationToken.Register(
                static state => SafeAbortFtpSession((WinSCP.Session)state!),
                session);

            session.FileTransferProgress += (_, progress) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    progress.Cancel = true;
                }
            };

            session.Open(sessionOptions);
            cancellationToken.ThrowIfCancellationRequested();

            EnsureWinScpRemoteDirectory(session, remoteDirectory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var transferOptions = new WinSCP.TransferOptions
            {
                TransferMode = WinSCP.TransferMode.Binary,
                OverwriteMode = WinSCP.OverwriteMode.Overwrite,
                PreserveTimestamp = false
            };

            // Avoid temporary-name/resume behavior. Each transaction retry overwrites the
            // target image from byte 0, which is deterministic after a failed partial upload.
            transferOptions.ResumeSupport.State = WinSCP.TransferResumeSupportState.Off;

            session.PutFileToDirectory(
                localImagePath,
                remoteDirectory,
                false,
                transferOptions);

            cancellationToken.ThrowIfCancellationRequested();

            // Never report success for a partial transfer. Verify the final server-side size.
            var remoteInfo = session.GetFileInfo(remotePath);
            if (remoteInfo.IsDirectory || remoteInfo.Length != fileInfo.Length)
            {
                throw new IOException(
                    $"{GetProtocolName(upload.Mode, upload.UseTls)} upload size verification failed. " +
                    $"Local={fileInfo.Length} bytes, remote={remoteInfo.Length} bytes, " +
                    $"path={remotePath}.");
            }

            stopwatch.Stop();
            return new ImageUploadResult(
                upload.UseTls ? "FTPS" : "FTP",
                remotePath,
                fileInfo.Length,
                stopwatch.Elapsed,
                ReusedConnection: false);
        }
        catch (WinSCP.SessionRemoteException ex) when (
            upload.UseTls &&
            !upload.AllowInvalidTlsCertificate &&
            string.IsNullOrWhiteSpace(upload.TlsCertificateSha256Fingerprint) &&
            LooksLikeTlsCertificateFailure(ex.Message))
        {
            throw new AuthenticationException(
                "FTPS certificate validation failed. Verify the server certificate in WinSCP " +
                "and paste its SHA-256 fingerprint into Server Panel > Server > Image Upload > " +
                "FTPS Certificate SHA-256 Fingerprint, or correct the server certificate.",
                ex);
        }
        finally
        {
            ClearActiveFtpSession(session);
        }
    }

    private static WinSCP.SessionOptions BuildWinScpSessionOptions(ImageUploadOptions upload)
    {
        var sessionOptions = new WinSCP.SessionOptions
        {
            Protocol = WinSCP.Protocol.Ftp,
            HostName = upload.Host.Trim(),
            PortNumber = upload.Port,
            UserName = upload.Username,
            Password = upload.Password,
            FtpMode = WinSCP.FtpMode.Passive,
            FtpSecure = upload.UseTls ? WinSCP.FtpSecure.Explicit : WinSCP.FtpSecure.None,
            Timeout = TimeSpan.FromSeconds(
                Math.Max(1, Math.Max(upload.ConnectionTimeoutSeconds, upload.OperationTimeoutSeconds)))
        };

        if (upload.UseTls)
        {
            var fingerprint = NormalizeSha256CertificateFingerprint(
                upload.TlsCertificateSha256Fingerprint);

            if (fingerprint.Length == 64)
            {
                sessionOptions.TlsHostCertificateFingerprint =
                    FormatCertificateFingerprint(fingerprint);
            }
            else if (upload.AllowInvalidTlsCertificate)
            {
                sessionOptions.GiveUpSecurityAndAcceptAnyTlsHostCertificate = true;
            }
        }

        return sessionOptions;
    }

    private static bool LooksLikeTlsCertificateFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("SSL", StringComparison.OrdinalIgnoreCase);
    }

    private (SftpClient Client, bool ReusedConnection) GetConnectedSftpClient(
        ImageUploadOptions upload)
    {
        var fingerprint = BuildSftpConnectionFingerprint(upload);
        var existing = GetSftpClient();

        if (existing is not null &&
            existing.IsConnected &&
            string.Equals(_sftpConnectionFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return (existing, true);
        }

        AbortSftpClient();

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

        lock (_sftpClientLock)
        {
            _sftpClient = client;
            _sftpConnectionFingerprint = fingerprint;
            _preparedSftpRemoteDirectory = string.Empty;
        }

        return (client, false);
    }

    private SftpClient? GetSftpClient()
    {
        lock (_sftpClientLock)
        {
            return _sftpClient;
        }
    }

    private void AbortCurrentTransfer()
    {
        AbortFtpSession();
        AbortSftpClient();
    }

    private void AbortSftpClient()
    {
        SftpClient? client;
        lock (_sftpClientLock)
        {
            client = _sftpClient;
            _sftpClient = null;
            _sftpConnectionFingerprint = string.Empty;
            _preparedSftpRemoteDirectory = string.Empty;
        }

        if (client is null)
        {
            return;
        }

        try
        {
            // Dispose directly. Disconnect can itself block on a stalled network session.
            client.Dispose();
        }
        catch
        {
            // The connection is already faulted or is being aborted because of timeout.
        }
    }

    private void SetActiveFtpSession(WinSCP.Session session)
    {
        lock (_ftpSessionLock)
        {
            _activeFtpSession = session;
        }
    }

    private void ClearActiveFtpSession(WinSCP.Session session)
    {
        lock (_ftpSessionLock)
        {
            if (ReferenceEquals(_activeFtpSession, session))
            {
                _activeFtpSession = null;
            }
        }
    }

    private void AbortFtpSession()
    {
        WinSCP.Session? session;
        lock (_ftpSessionLock)
        {
            session = _activeFtpSession;
            _activeFtpSession = null;
        }

        if (session is not null)
        {
            SafeAbortFtpSession(session);
        }
    }

    private static void SafeAbortFtpSession(WinSCP.Session session)
    {
        try
        {
            session.Abort();
        }
        catch
        {
            // Session is already completed, closed, disposed, or faulted.
        }
    }

    private static void EnsureWinScpRemoteDirectory(
        WinSCP.Session session,
        string remoteDirectory,
        CancellationToken cancellationToken)
    {
        if (remoteDirectory == "/")
        {
            return;
        }

        var current = "/";
        foreach (var segment in remoteDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = CombineRemotePath(current, segment);
            if (!session.FileExists(current))
            {
                session.CreateDirectory(current);
            }
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

    public static bool IsSftpMode(string? mode) =>
        string.Equals(mode, "Sftp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase);

    public static bool IsFtpMode(string? mode) =>
        string.Equals(mode, "Ftp", StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedMode(string? mode) =>
        string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase) ||
        IsSftpMode(mode) ||
        IsFtpMode(mode);

    public static string GetProtocolName(string? mode, bool ftpUseTls = false)
    {
        if (IsFtpMode(mode))
        {
            return ftpUseTls ? "FTPS" : "FTP";
        }

        if (IsSftpMode(mode))
        {
            return "SFTP";
        }

        return "Image upload";
    }

    private static void ValidateConfiguration(ImageUploadOptions upload)
    {
        if (!IsSupportedMode(upload.Mode))
        {
            throw new InvalidOperationException(
                "Image upload mode must be Disabled, Sftp, or Ftp.");
        }

        if (!IsSftpMode(upload.Mode) && !IsFtpMode(upload.Mode))
        {
            return;
        }

        var protocol = GetProtocolName(upload.Mode, upload.UseTls);
        if (string.IsNullOrWhiteSpace(upload.Host))
        {
            throw new InvalidOperationException($"{protocol} Host is empty.");
        }

        if (upload.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException(
                $"{protocol} Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(upload.Username))
        {
            throw new InvalidOperationException($"{protocol} Username is empty.");
        }

        if (IsFtpMode(upload.Mode) &&
            upload.UseTls &&
            !string.IsNullOrWhiteSpace(upload.TlsCertificateSha256Fingerprint) &&
            !IsValidSha256CertificateFingerprint(upload.TlsCertificateSha256Fingerprint))
        {
            throw new InvalidOperationException(
                "FTPS Certificate SHA-256 Fingerprint must contain exactly 64 hexadecimal characters.");
        }
    }

    public static bool IsValidSha256CertificateFingerprint(string? value) =>
        NormalizeSha256CertificateFingerprint(value).Length == 64;

    private static string NormalizeSha256CertificateFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Accept common WinSCP/Windows renderings such as
        // "SHA-256: AA:BB:..." as well as a raw 64-character hex value.
        var hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (hex.Length > 64)
        {
            hex = hex[^64..];
        }

        return hex;
    }

    private static string FormatCertificateFingerprint(string normalized)
    {
        var value = NormalizeSha256CertificateFingerprint(normalized);
        if (value.Length != 64)
        {
            return string.IsNullOrWhiteSpace(normalized) ? "unavailable" : normalized;
        }

        return string.Join(
            ":",
            Enumerable.Range(0, value.Length / 2)
                .Select(index => value.Substring(index * 2, 2)));
    }

    private static void EnsureSftpRemoteDirectory(
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

    private static string BuildSftpConnectionFingerprint(ImageUploadOptions upload) =>
        $"{upload.Host.Trim().ToUpperInvariant()}|{upload.Port}|{upload.Username}|{upload.Password}";
}

public sealed record ImageUploadResult(
    string Protocol,
    string RemotePath,
    long BytesUploaded,
    TimeSpan Duration,
    bool ReusedConnection);
