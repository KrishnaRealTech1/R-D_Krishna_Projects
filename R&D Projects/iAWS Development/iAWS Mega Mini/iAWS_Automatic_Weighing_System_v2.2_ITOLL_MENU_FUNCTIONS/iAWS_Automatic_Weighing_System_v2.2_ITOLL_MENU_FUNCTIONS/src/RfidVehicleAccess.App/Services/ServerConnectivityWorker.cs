using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed record ServerConnectivityStatus(
    bool IsConnected,
    bool IsPartiallyConnected,
    string Status);

/// <summary>
/// Performs a lightweight reachability check for the configured MQTT and image-upload endpoint.
/// The check confirms that the server ports can be reached; normal MQTT/SFTP/FTP operations
/// continue to perform their own authentication and protocol validation.
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
            }

            eventHub.PublishServerConnectivity(currentStatus);

            if (previousStatus != currentStatus)
            {
                await logger.StatusAsync(
                    $"Server status: {currentStatus.Status}.",
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

        var mqtt = options.Server.Mqtt;
        var mqttConfigured = IsEndpointConfigured(mqtt.BrokerHost, mqtt.Port);
        var mqttReachable = mqttConfigured && await CanConnectAsync(
            mqtt.BrokerHost,
            mqtt.Port,
            cancellationToken);

        var upload = options.Server.ImageUpload;
        var uploadRequired = ImageUploadService.IsSftpMode(upload.Mode) ||
                             ImageUploadService.IsFtpMode(upload.Mode);
        var uploadProtocol = ImageUploadService.GetProtocolName(upload.Mode, upload.UseTls);
        var uploadConfigured = !uploadRequired || IsEndpointConfigured(upload.Host, upload.Port);
        var uploadReachable = !uploadRequired || (uploadConfigured && await CanConnectAsync(
            upload.Host,
            upload.Port,
            cancellationToken));

        if (!mqttConfigured)
        {
            return new ServerConnectivityStatus(false, false, "MQTT not configured");
        }

        if (uploadRequired && !uploadConfigured)
        {
            return mqttReachable
                ? new ServerConnectivityStatus(
                    false,
                    true,
                    $"MQTT online; {uploadProtocol} not configured")
                : new ServerConnectivityStatus(false, false, "Server not configured");
        }

        if (mqttReachable && uploadReachable)
        {
            return new ServerConnectivityStatus(
                true,
                false,
                uploadRequired
                    ? $"Connected (MQTT + {uploadProtocol})"
                    : "Connected (MQTT)");
        }

        if (mqttReachable)
        {
            return new ServerConnectivityStatus(
                false,
                true,
                $"MQTT online; {uploadProtocol} offline");
        }

        if (uploadRequired && uploadReachable)
        {
            return new ServerConnectivityStatus(
                false,
                true,
                $"{uploadProtocol} online; MQTT offline");
        }

        return new ServerConnectivityStatus(false, false, "Server offline");
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

    private static bool IsEndpointConfigured(string? host, int port) =>
        !string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535;
}
