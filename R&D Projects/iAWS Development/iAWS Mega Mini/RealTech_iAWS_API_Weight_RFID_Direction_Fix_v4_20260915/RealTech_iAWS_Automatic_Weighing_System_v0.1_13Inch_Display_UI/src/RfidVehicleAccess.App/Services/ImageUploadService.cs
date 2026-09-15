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

    public Task<ImageUploadResult?> UploadAsync(
        TripRecord trip,
        CancellationToken cancellationToken = default) =>
        UploadFileAsync(trip.ImagePath, cancellationToken);

    public async Task<ImageUploadResult?> UploadFileAsync(
        string localImagePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(localImagePath) || !File.Exists(localImagePath))
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
                ? UploadFtpCoreAsync(localImagePath, upload, timeoutSource.Token)
                : Task.Run(
                    () => UploadSftpCore(localImagePath, upload, timeoutSource.Token),
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
        // Keep the protocol-specific outer message. The inner WinSCP/SSH.NET
        // exception is often less useful (for example only "Peer certificate rejected").
        _lastFailureMessage = CollapseExceptionChain(exception);
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
        var phase = "connect/authenticate";

        try
        {
            var (client, reusedConnection) = GetConnectedSftpClient(upload);
            cancellationToken.ThrowIfCancellationRequested();

            phase = "prepare remote directory";
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

            phase = "upload";
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
                reusedConnection,
                reusedConnection
                    ? $"Existing SFTP session reused; server connection is authenticated; upload completed successfully; remote path={remotePath}."
                    : $"SFTP server connection established and authenticated; upload completed successfully; remote path={remotePath}.");
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            var diagnostics =
                $"phase={phase} | host={upload.Host}:{upload.Port} | " +
                $"SSH.NET response={CollapseExceptionChain(ex)}";
            throw new ImageTransferException(
                "SFTP",
                "No FTP-style numeric reply exists for SFTP/SSH; the SSH/SFTP diagnostic is shown separately.",
                diagnostics,
                $"SFTP connection/upload failed during {phase}.",
                ex);
        }
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
        var sessionLogPath = Path.Combine(
            Path.GetTempPath(),
            $"RealTech_iAWS_WinSCP_{Guid.NewGuid():N}.log");
        var session = new WinSCP.Session
        {
            SessionLogPath = sessionLogPath
        };
        var sessionDisposed = false;
        SetActiveFtpSession(session);

        void DisposeSessionForLogRead()
        {
            if (sessionDisposed)
            {
                return;
            }

            ClearActiveFtpSession(session);
            try
            {
                session.Dispose();
            }
            catch
            {
                // The transfer result/exception is more useful than a secondary
                // dispose error. WinSCP has already released or is releasing resources.
            }

            sessionDisposed = true;
        }

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

            // WinSCP keeps SessionLogPath open until Session.Dispose(). Dispose first,
            // then read the raw server replies so successful uploads do not report a
            // misleading "file is being used by another process" diagnostic.
            DisposeSessionForLogRead();
            var rawServerResponse = ReadWinScpServerResponses(sessionLogPath);

            return new ImageUploadResult(
                upload.UseTls ? "FTPS" : "FTP",
                remotePath,
                fileInfo.Length,
                stopwatch.Elapsed,
                ReusedConnection: false,
                (upload.UseTls
                    ? $"FTPS session connected and authenticated; upload accepted; remote size verified={remoteInfo.Length} bytes; remote path={remotePath}."
                    : $"FTP session connected and authenticated; upload accepted; remote size verified={remoteInfo.Length} bytes; remote path={remotePath}.") +
                $" Raw server reply: {rawServerResponse}");
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            // Dispose before reading SessionLogPath. This both releases the WinSCP
            // log-file handle and guarantees that the final server/TLS lines are flushed.
            DisposeSessionForLogRead();
            var serverReply = ReadWinScpServerResponses(sessionLogPath);
            var diagnostics = ReadWinScpDiagnostics(sessionLogPath);
            var exceptionText = CollapseExceptionChain(ex);

            if (upload.UseTls &&
                LooksLikeTlsCertificateFailure($"{exceptionText} {diagnostics}"))
            {
                var presentedFingerprint = TryScanFtpsSha256Fingerprint(upload);
                if (!string.IsNullOrWhiteSpace(presentedFingerprint))
                {
                    diagnostics =
                        $"presented SHA-256 fingerprint={presentedFingerprint} | {diagnostics}";
                }

                throw BuildFtpsCertificateException(
                    upload,
                    ex,
                    serverReply,
                    diagnostics,
                    presentedFingerprint);
            }

            var protocol = GetProtocolName(upload.Mode, upload.UseTls);
            throw new ImageTransferException(
                protocol,
                serverReply,
                diagnostics,
                $"{protocol} server rejected or terminated the operation.",
                ex);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            DisposeSessionForLogRead();
            SafeDeleteFile(sessionLogPath);
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

        // Do not classify every FTPS/TLS error as a certificate problem. WinSCP
        // session diagnostics mention TLS and may mention a client certificate even
        // during otherwise unrelated authentication failures. Require an actual
        // certificate rejection/validation/trust signal.
        return message.Contains("Peer certificate rejected", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("certificate rejected", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("certificate validation", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("certificate verification", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("certificate is not known", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("untrusted certificate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("certificate fingerprint", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }


    public static string TryScanFtpsSha256Fingerprint(ImageUploadOptions upload)
    {
        try
        {
            var scanOptions = new WinSCP.SessionOptions
            {
                Protocol = WinSCP.Protocol.Ftp,
                HostName = upload.Host.Trim(),
                PortNumber = upload.Port,
                FtpMode = WinSCP.FtpMode.Passive,
                FtpSecure = WinSCP.FtpSecure.Explicit,
                Timeout = TimeSpan.FromSeconds(
                    Math.Max(1, Math.Min(upload.ConnectionTimeoutSeconds, 10)))
            };

            using var scanSession = new WinSCP.Session();
            return CollapseForLog(scanSession.ScanFingerprint(scanOptions, "SHA-256"));
        }
        catch
        {
            // The original WinSCP session log still contains the connection/TLS
            // diagnostic. Fingerprint scanning is best-effort only.
            return string.Empty;
        }
    }

    private static ImageTransferException BuildFtpsCertificateException(
        ImageUploadOptions upload,
        Exception exception,
        string serverReply,
        string diagnostics,
        string presentedFingerprint)
    {
        var fingerprint = NormalizeSha256CertificateFingerprint(
            upload.TlsCertificateSha256Fingerprint);

        var presentedText = string.IsNullOrWhiteSpace(presentedFingerprint)
            ? string.Empty
            : $" Presented SHA-256 fingerprint: {presentedFingerprint}.";

        string guidance;
        if (fingerprint.Length == 64)
        {
            guidance =
                "The configured FTPS SHA-256 certificate fingerprint was rejected. " +
                $"Configured fingerprint: {FormatCertificateFingerprint(fingerprint)}." +
                presentedText +
                " Verify the host/port and update the configured fingerprint only after confirming the presented certificate belongs to the expected server.";
        }
        else if (upload.AllowInvalidTlsCertificate)
        {
            guidance =
                "The FTPS TLS handshake failed even though invalid server certificates are currently allowed. " +
                "Verify the FTPS host, port, explicit-TLS mode, username/password, and server TLS configuration.";
        }
        else
        {
            guidance =
                "FTPS server certificate validation failed." +
                presentedText +
                " After verifying that fingerprint belongs to the expected server, copy it into " +
                "Server Panel > Server > Image Upload > FTPS Certificate SHA-256 Fingerprint, or correct the server certificate/chain.";
        }

        return new ImageTransferException(
            "FTPS",
            serverReply,
            diagnostics,
            guidance,
            exception);
    }

    private static IReadOnlyList<string> ReadWinScpLogLines(string sessionLogPath)
    {
        IOException? lastIoException = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    sessionLogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var lines = new List<string>();
                while (reader.ReadLine() is { } line)
                {
                    lines.Add(line);
                }

                return lines;
            }
            catch (IOException ex) when (attempt < 5)
            {
                lastIoException = ex;
                System.Threading.Thread.Sleep(50 * attempt);
            }
        }

        throw lastIoException ?? new IOException("Unable to read WinSCP session log.");
    }

    private static string ReadWinScpServerResponses(string sessionLogPath)
    {
        try
        {
            if (!File.Exists(sessionLogPath))
            {
                return "No textual FTP/FTPS reply was captured.";
            }

            var serverLines = ReadWinScpLogLines(sessionLogPath)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
                .Select(RemoveWinScpLogTimestamp)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .TakeLast(12)
                .ToArray();

            if (serverLines.Length == 0)
            {
                return "No textual FTP/FTPS reply was captured.";
            }

            var response = string.Join(" | ", serverLines);
            return response.Length <= 1800
                ? response
                : response[^1800..] + " [tail]";
        }
        catch (Exception ex)
        {
            return $"Unable to read WinSCP reply log: {CollapseForLog(ex.Message)}";
        }
    }

    private static string ReadWinScpDiagnostics(string sessionLogPath)
    {
        try
        {
            if (!File.Exists(sessionLogPath))
            {
                return "No WinSCP TLS/connection diagnostic was captured.";
            }

            // Only include safe diagnostic lines. Do not include client command lines
            // (which can contain credentials) from the WinSCP session log.
            var diagnostics = ReadWinScpLogLines(sessionLogPath)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(". ", StringComparison.Ordinal))
                .Select(line => line[2..].TrimStart())
                .Where(line =>
                    line.Contains("Connecting to", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Connected with", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Peer certificate", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Disconnected", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Connection failed", StringComparison.OrdinalIgnoreCase))
                .Select(RemoveDiagnosticTimestamp)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                // Put the actual failure and certificate fingerprint first so the
                // important response remains visible even in the narrow on-screen log.
                .OrderBy(GetDiagnosticVisibilityPriority)
                .ThenBy(line => line, StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();

            if (diagnostics.Length == 0)
            {
                return "No WinSCP TLS/connection diagnostic was captured.";
            }

            var response = string.Join(" | ", diagnostics);
            return response.Length <= 2200
                ? response
                : response[^2200..] + " [tail]";
        }
        catch (Exception ex)
        {
            return $"Unable to read WinSCP diagnostic log: {CollapseForLog(ex.Message)}";
        }
    }

    private static int GetDiagnosticVisibilityPriority(string line)
    {
        if (line.Contains("Peer certificate rejected", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Connection failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (line.Contains("fingerprint", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (line.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (line.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (line.Contains("Connected with", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (line.Contains("Connecting to", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        return 6;
    }

    private static string RemoveDiagnosticTimestamp(string line)
    {
        // WinSCP diagnostic lines usually begin with a date and time after the ". "
        // marker. Remove those two tokens while preserving the actual message.
        if (string.IsNullOrWhiteSpace(line) || !char.IsDigit(line[0]))
        {
            return line;
        }

        var firstSpace = line.IndexOf(' ');
        if (firstSpace < 0)
        {
            return line;
        }

        var secondSpace = line.IndexOf(' ', firstSpace + 1);
        return secondSpace >= 0 && secondSpace + 1 < line.Length
            ? line[(secondSpace + 1)..].Trim()
            : line;
    }

    private static string CollapseExceptionChain(Exception exception)
    {
        var parts = new List<string>();
        Exception? current = exception;
        while (current is not null)
        {
            var message = string.IsNullOrWhiteSpace(current.Message)
                ? current.GetType().Name
                : CollapseForLog(current.Message);

            if (!parts.Any(existing =>
                    string.Equals(existing, message, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add(message);
            }

            current = current.InnerException;
        }

        var combined = string.Join(" | caused-by: ", parts);
        return combined.Length <= 1800
            ? combined
            : combined[..1800] + "...[truncated]";
    }

    private static string RemoveWinScpLogTimestamp(string line)
    {
        if (!line.StartsWith("< ", StringComparison.Ordinal))
        {
            return line;
        }

        var payload = line[2..].TrimStart();
        var firstSpace = payload.IndexOf(' ');
        if (firstSpace < 0)
        {
            return payload;
        }

        // WinSCP normally prefixes response lines with date/time. Keep only the actual
        // reply when the first token begins with a digit; otherwise keep the full payload.
        if (payload.Length > 0 && char.IsDigit(payload[0]))
        {
            var secondSpace = payload.IndexOf(' ', firstSpace + 1);
            if (secondSpace >= 0 && secondSpace + 1 < payload.Length)
            {
                return payload[(secondSpace + 1)..].Trim();
            }
        }

        return payload;
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Diagnostic temp logs are best-effort cleanup only.
        }
    }

    private static string CollapseForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No response text was returned.";
        }

        return string.Join(
            " ",
            value.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

    public static bool CertificateFingerprintsMatch(string? expected, string? presented)
    {
        var normalizedExpected = NormalizeSha256CertificateFingerprint(expected);
        var normalizedPresented = NormalizeSha256CertificateFingerprint(presented);
        return normalizedExpected.Length == 64 &&
               normalizedPresented.Length == 64 &&
               string.Equals(
                   normalizedExpected,
                   normalizedPresented,
                   StringComparison.OrdinalIgnoreCase);
    }

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

public sealed class ImageTransferException : IOException
{
    public ImageTransferException(
        string protocol,
        string serverReply,
        string diagnostics,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Protocol = protocol;
        ServerReply = serverReply;
        Diagnostics = diagnostics;
    }

    public string Protocol { get; }

    public string ServerReply { get; }

    public string Diagnostics { get; }
}

public sealed record ImageUploadResult(
    string Protocol,
    string RemotePath,
    long BytesUploaded,
    TimeSpan Duration,
    bool ReusedConnection,
    string ServerResponse);
