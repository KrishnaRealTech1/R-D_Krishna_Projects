using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RfidVehicleAccess.Services;

public sealed class IawsApiPayload
{
    [JsonPropertyName("rf_id")]
    public string Rfid { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; init; } = "WEIGH";

    [JsonPropertyName("weight")]
    public string Weight { get; init; } = string.Empty;

    [JsonPropertyName("front")]
    public string Front { get; init; } = string.Empty;

    [JsonPropertyName("back")]
    public string Back { get; init; } = string.Empty;

    [JsonPropertyName("left")]
    public string Left { get; init; } = string.Empty;

    [JsonPropertyName("right")]
    public string Right { get; init; } = string.Empty;

    [JsonPropertyName("dates")]
    public string Dates { get; init; } = string.Empty;

    [JsonPropertyName("material_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MaterialType { get; init; }
}

public sealed record IawsApiResult(
    bool Success,
    int? StatusCode,
    string ResponseBody,
    string Message);

public sealed class IawsApiClient : IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly HttpClient _httpClient = new();

    public IawsApiClient(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IawsApiResult> SendAsync(
        IawsApiPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_options.Iaws.ApiEndpoint, UriKind.Absolute, out var endpoint))
        {
            return new IawsApiResult(
                false,
                null,
                string.Empty,
                "Configure a full API URL ending in /iaws_raw/mega/insert.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.Iaws.ApiTimeoutSeconds, 2, 300)));

        try
        {
            await _logger.ApiAsync(
                $"POST {endpoint.AbsolutePath} | RFID={payload.Rfid} | Weight={payload.Weight} kg",
                timeout.Token);

            using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var apiSuccess = response.IsSuccessStatusCode;
            var apiMessage = response.IsSuccessStatusCode
                ? "API accepted the weighing event."
                : $"API returned HTTP {(int)response.StatusCode}.";

            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("success", out var successElement) &&
                            successElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            apiSuccess = successElement.GetBoolean();
                        }

                        if (root.TryGetProperty("message", out var messageElement) &&
                            messageElement.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(messageElement.GetString()))
                        {
                            apiMessage = messageElement.GetString()!;
                        }
                    }
                }
                catch (JsonException)
                {
                    // A non-JSON 2xx body is still a valid HTTP success response.
                }
            }

            await _logger.ApiAsync(
                $"HTTP {(int)response.StatusCode} | {TrimForLog(body)}",
                timeout.Token);

            return new IawsApiResult(
                apiSuccess,
                (int)response.StatusCode,
                body,
                apiMessage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            const string message = "API request timed out.";
            await _logger.ApiAsync(message, cancellationToken);
            return new IawsApiResult(false, null, string.Empty, message);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            var message = $"API request failed: {ex.Message}";
            await _logger.ApiAsync(message, cancellationToken);
            return new IawsApiResult(false, null, string.Empty, message);
        }
    }

    private static string TrimForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty response)";
        }

        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500] + "...";
    }

    public void Dispose() => _httpClient.Dispose();
}
