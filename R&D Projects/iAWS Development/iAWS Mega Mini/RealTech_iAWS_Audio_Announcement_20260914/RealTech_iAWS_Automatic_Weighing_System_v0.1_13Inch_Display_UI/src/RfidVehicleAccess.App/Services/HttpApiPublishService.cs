using System.Net.Http;
using System.Text;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Sends completed iAWS weighing transactions to the configurable HTTP API endpoint.
/// The API contract is JSON over HTTP POST.
/// </summary>
public sealed class HttpApiPublishService(AppOptions options) : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private bool _disposed;

    public async Task<string> PublishAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var api = options.Server.Api;
        if (!api.Enabled)
        {
            throw new InvalidOperationException("HTTP API Method is disabled.");
        }

        if (!Uri.TryCreate(api.Endpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("HTTP API Endpoint is invalid.");
        }

        if (!string.Equals(api.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTP API Method must be POST.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(api.RequestTimeoutSeconds, 1, 300)));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);

        var responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
        if (!response.IsSuccessStatusCode)
        {
            var preview = responseBody.Length <= 512
                ? responseBody
                : responseBody[..512];
            throw new HttpRequestException(
                $"HTTP API returned {(int)response.StatusCode} {response.ReasonPhrase}. Response: {preview}");
        }

        return responseBody;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}
