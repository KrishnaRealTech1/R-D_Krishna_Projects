using System.Net.Http;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed record InternetConnectivityStatus(bool IsOnline, string Status);

public sealed class InternetConnectivityWorker(
    AppOptions options,
    AppLogger logger,
    SystemEventHub eventHub) : BackgroundService
{
    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true
    });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InternetConnectivityStatus? previousStatus = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            InternetConnectivityStatus currentStatus;
            try
            {
                currentStatus = await CheckConnectivityAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                currentStatus = NetworkInterface.GetIsNetworkAvailable()
                    ? new InternetConnectivityStatus(
                        true,
                        "Network connected (internet verification unavailable)")
                    : new InternetConnectivityStatus(false, "Offline");

                await logger.StatusAsync(
                    $"Internet connectivity check failed: {ex.Message}",
                    stoppingToken);
            }

            eventHub.PublishInternetConnectivity(currentStatus);

            if (previousStatus != currentStatus)
            {
                await logger.StatusAsync(
                    $"Internet status: {currentStatus.Status}.",
                    stoppingToken);
                previousStatus = currentStatus;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(5, options.Connectivity.CheckIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task<InternetConnectivityStatus> CheckConnectivityAsync(
        CancellationToken cancellationToken)
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return new InternetConnectivityStatus(false, "Offline");
        }

        var endpoints = options.Connectivity.CheckEndpoints ?? [];
        foreach (var endpoint in endpoints
                     .Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(
                Math.Max(1, options.Connectivity.RequestTimeoutSeconds)));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

                if ((int)response.StatusCode is >= 200 and < 400)
                {
                    return new InternetConnectivityStatus(true, "Online");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Try the next endpoint. Some networks block public connectivity probes.
            }
            catch (HttpRequestException)
            {
                // Try the next endpoint.
            }
        }

        // The machine has an active network interface, but public probes may be blocked by
        // a firewall or proxy. Do not incorrectly show Offline in that situation.
        return new InternetConnectivityStatus(
            true,
            "Network connected (internet check blocked)");
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
