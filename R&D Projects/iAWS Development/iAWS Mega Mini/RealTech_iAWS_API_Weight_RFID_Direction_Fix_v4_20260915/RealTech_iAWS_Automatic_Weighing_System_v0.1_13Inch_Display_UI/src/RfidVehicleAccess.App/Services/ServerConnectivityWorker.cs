using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed record ServerConnectivityStatus(
    bool IsConnected,
    bool IsPartiallyConnected,
    string Status);

/// <summary>
/// Performs lightweight reachability checks for whichever iAWS server methods are enabled.
/// MQTT, HTTP API and image upload are checked independently and combined into one dashboard status.
/// </summary>
public sealed class ServerConnectivityWorker(
    AppOptions options,
    AppLogger logger,
    SystemEventHub eventHub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerConnectivityStatus? previousStatus = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            ServerConnectivityStatus currentStatus;
            try
            {
                currentStatus = await CheckServerConnectivityAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                currentStatus = new ServerConnectivityStatus(
                    false,
                    false,
                    "Server check unavailable");

                await logger.StatusAsync(
                    $"Server connectivity check failed: {ex.Message}",
                    stoppingToken);
                await logger.ServerAsync(
                    $"SERVER CONNECTIVITY CHECK FAILED | {ex.Message}",
                    stoppingToken);
            }

            eventHub.PublishServerConnectivity(currentStatus);

            if (previousStatus != currentStatus)
            {
                await logger.StatusAsync(
                    $"Server status: {currentStatus.Status}.",
                    stoppingToken);
                await logger.ServerAsync(
                    $"SERVER CONNECTIVITY | {currentStatus.Status}.",
                    stoppingToken);
                previousStatus = currentStatus;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(5, options.Connectivity.CheckIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task<ServerConnectivityStatus> CheckServerConnectivityAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Server.Enabled)
        {
            return new ServerConnectivityStatus(false, false, "Disabled");
        }

        var checks = new List<EndpointCheck>();

        var mqtt = options.Server.Mqtt;
        if (mqtt.Enabled)
        {
            var configured = IsEndpointConfigured(mqtt.BrokerHost, mqtt.Port);
            var reachable = configured && await CanConnectAsync(
                mqtt.BrokerHost,
                mqtt.Port,
                cancellationToken);
            checks.Add(new EndpointCheck("MQTT", configured, reachable));
        }

        var api = options.Server.Api;
        if (api.Enabled)
        {
            var configured = TryResolveApiEndpoint(api.Endpoint, out var host, out var port);
            var reachable = configured && await CanConnectAsync(host, port, cancellationToken);
            checks.Add(new EndpointCheck("API", configured, reachable));
        }

        var upload = options.Server.ImageUpload;
        var uploadRequired = ImageUploadService.IsSftpMode(upload.Mode) ||
                             ImageUploadService.IsFtpMode(upload.Mode);
        if (uploadRequired)
        {
            var protocol = ImageUploadService.GetProtocolName(upload.Mode, upload.UseTls);
            var configured = IsEndpointConfigured(upload.Host, upload.Port);
            var reachable = configured && await CanConnectAsync(
                upload.Host,
                upload.Port,
                cancellationToken);
            string? failureStatus = null;

            // A successful TCP connection to port 21 does not prove that an FTPS
            // session can be trusted. When a pinned SHA-256 certificate is configured,
            // scan the certificate presented by the server and compare it before the
            // dashboard reports the upload endpoint as connected.
            if (reachable &&
                ImageUploadService.IsFtpMode(upload.Mode) &&
                upload.UseTls &&
                ImageUploadService.IsValidSha256CertificateFingerprint(
                    upload.TlsCertificateSha256Fingerprint))
            {
                var presentedFingerprint = await Task.Run(
                    () => ImageUploadService.TryScanFtpsSha256Fingerprint(upload),
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(presentedFingerprint) &&
                    !ImageUploadService.CertificateFingerprintsMatch(
                        upload.TlsCertificateSha256Fingerprint,
                        presentedFingerprint))
                {
                    reachable = false;
                    failureStatus = $"{protocol} certificate mismatch";
                }
            }

            checks.Add(new EndpointCheck(protocol, configured, reachable, failureStatus));
        }

        if (checks.Count == 0)
        {
            return new ServerConnectivityStatus(false, false, "No server method enabled");
        }

        var notConfigured = checks.Where(check => !check.Configured).ToList();
        if (notConfigured.Count > 0)
        {
            var configuredOnline = checks.Any(check => check.Configured && check.Reachable);
            var names = string.Join(" + ", notConfigured.Select(check => check.Name));
            return configuredOnline
                ? new ServerConnectivityStatus(false, true, $"Online; {names} not configured")
                : new ServerConnectivityStatus(false, false, $"{names} not configured");
        }

        if (checks.All(check => check.Reachable))
        {
            return new ServerConnectivityStatus(
                true,
                false,
                $"Connected ({string.Join(" + ", checks.Select(check => check.Name))})");
        }

        var online = checks
            .Where(check => check.Reachable)
            .Select(check => check.Name)
            .ToList();
        var offline = checks
            .Where(check => !check.Reachable)
            .Select(check => check.FailureStatus ?? $"{check.Name} offline")
            .ToList();
        if (online.Count > 0)
        {
            return new ServerConnectivityStatus(
                false,
                true,
                $"{string.Join(" + ", online)} online; {string.Join(" + ", offline)}");
        }

        return new ServerConnectivityStatus(
            false,
            false,
            offline.Count > 0 ? string.Join(" + ", offline) : "Server offline");
    }

    private async Task<bool> CanConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(1, options.Connectivity.RequestTimeoutSeconds)));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool TryResolveApiEndpoint(
        string? endpoint,
        out string host,
        out int port)
    {
        host = string.Empty;
        port = 0;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        host = uri.Host;
        port = uri.IsDefaultPort
            ? uri.Scheme == Uri.UriSchemeHttps ? 443 : 80
            : uri.Port;
        return IsEndpointConfigured(host, port);
    }

    private static bool IsEndpointConfigured(string? host, int port) =>
        !string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535;

    private sealed record EndpointCheck(string Name, bool Configured, bool Reachable, string? FailureStatus = null);
}
