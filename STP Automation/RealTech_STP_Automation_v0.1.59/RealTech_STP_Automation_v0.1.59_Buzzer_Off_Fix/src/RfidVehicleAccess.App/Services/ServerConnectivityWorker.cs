using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed record ServerConnectivityStatus(
    bool IsConnected,
    bool IsPartiallyConnected,
    string Status);

/// <summary>
/// Performs a lightweight reachability check for the configured MQTT and SFTP endpoints.
/// The check confirms that the server ports can be reached; normal MQTT/SFTP operations
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

        var sftpRequired = IsSftpEnabled(options.Server.ImageUpload.Mode);
        var sftp = options.Server.ImageUpload;
        var sftpConfigured = !sftpRequired || IsEndpointConfigured(sftp.Host, sftp.Port);
        var sftpReachable = !sftpRequired || (sftpConfigured && await CanConnectAsync(
            sftp.Host,
            sftp.Port,
            cancellationToken));

        if (!mqttConfigured)
        {
            return new ServerConnectivityStatus(false, false, "MQTT not configured");
        }

        if (sftpRequired && !sftpConfigured)
        {
            return mqttReachable
                ? new ServerConnectivityStatus(false, true, "MQTT online; SFTP not configured")
                : new ServerConnectivityStatus(false, false, "Server not configured");
        }

        if (mqttReachable && sftpReachable)
        {
            return new ServerConnectivityStatus(
                true,
                false,
                sftpRequired ? "Connected (MQTT + SFTP)" : "Connected (MQTT)");
        }

        if (mqttReachable)
        {
            return new ServerConnectivityStatus(false, true, "MQTT online; SFTP offline");
        }

        if (sftpRequired && sftpReachable)
        {
            return new ServerConnectivityStatus(false, true, "SFTP online; MQTT offline");
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

    private static bool IsSftpEnabled(string? mode) =>
        string.Equals(mode, "Sftp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enable", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase);
}
