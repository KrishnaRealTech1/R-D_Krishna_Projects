using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class VehicleApiImportService : IDisposable
{
    private const long MaximumDownloadBytes = 50L * 1024L * 1024L;
    private const int MaximumErrorPreviewCharacters = 500;

    private static readonly HashSet<string> PreferredLinkPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "csv_link",
            "csv_url",
            "file_url",
            "download_url",
            "download_link",
            "url",
            "link",
            "file",
            "path"
        };

    private static readonly Regex HtmlLinkRegex = new(
        "href\\s*=\\s*[\\\"'](?<url>[^\\\"']+)[\\\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly AppOptions _options;
    private readonly VehicleImportService _importService;
    private readonly AppLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _synchronizationLock = new(1, 1);

    public VehicleApiImportService(
        AppOptions options,
        VehicleImportService importService,
        AppLogger logger)
    {
        _options = options;
        _importService = importService;
        _logger = logger;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public IReadOnlyList<VehicleApiSourceOptions> GetEnabledSources() =>
        SnapshotEnabledSources()
            .OrderBy(source => source.Priority)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<VehicleApiImportResult> ImportAsync(
        CancellationToken cancellationToken = default)
    {
        await _synchronizationLock.WaitAsync(cancellationToken);
        try
        {
            var source = SnapshotEnabledSources()
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No enabled Vehicle CSV API source is configured in " +
                    "Server Panel > Storage & Import.");

            return await ImportSourceCoreAsync(source, cancellationToken);
        }
        finally
        {
            _synchronizationLock.Release();
        }
    }

    public async Task<VehicleApiBatchImportResult> ImportAllAsync(
        CancellationToken cancellationToken = default)
    {
        await _synchronizationLock.WaitAsync(cancellationToken);
        try
        {
            var sources = SnapshotEnabledSources();
            if (sources.Count == 0)
            {
                throw new InvalidOperationException(
                    "No enabled Vehicle CSV API source is configured in " +
                    "Server Panel > Storage & Import.");
            }

            // Priority 1 is the highest priority. Lower-priority sources are imported first,
            // so the highest-priority source is applied last and wins conflicting fields.
            var orderedSources = sources
                .OrderByDescending(source => source.Priority)
                .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var results = new List<VehicleApiSourceImportResult>(orderedSources.Count);
            foreach (var source in orderedSources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await ImportSourceCoreAsync(source, cancellationToken);
                    results.Add(new VehicleApiSourceImportResult(
                        source.Name,
                        source.Priority,
                        true,
                        result.ImportResult,
                        result.SourceDescription,
                        string.Empty));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var error =
                        $"Timed out after {source.RequestTimeoutSeconds} second(s).";
                    await _logger.StatusAsync(
                        $"Vehicle API source '{source.Name}' failed: {error}",
                        cancellationToken);
                    results.Add(new VehicleApiSourceImportResult(
                        source.Name,
                        source.Priority,
                        false,
                        null,
                        string.Empty,
                        error));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await _logger.StatusAsync(
                        $"Vehicle API source '{source.Name}' failed: {ex.Message}",
                        cancellationToken);
                    results.Add(new VehicleApiSourceImportResult(
                        source.Name,
                        source.Priority,
                        false,
                        null,
                        string.Empty,
                        ex.Message));
                }
            }

            return new VehicleApiBatchImportResult(results);
        }
        finally
        {
            _synchronizationLock.Release();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _synchronizationLock.Dispose();
    }

    private async Task<VehicleApiImportResult> ImportSourceCoreAsync(
        VehicleApiSourceOptions source,
        CancellationToken cancellationToken)
    {
        var endpoint = ParseEndpoint(source.Endpoint, source.Name);
        var username = source.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                $"Vehicle CSV API Username is required for source '{source.Name}'.");
        }

        var timeoutSeconds = Math.Clamp(source.RequestTimeoutSeconds, 1, 300);

        using var networkTimeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        networkTimeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var networkToken = networkTimeoutCts.Token;

        await _logger.StatusAsync(
            $"Requesting registered-vehicle CSV from source '{source.Name}' " +
            $"({endpoint.Host}) for API user {username}.",
            cancellationToken);

        var tempFile = CreateTemporaryCsvPath(source.Name);
        string sourceDescription;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new Dictionary<string, string>
                {
                    ["username"] = username
                })
            };
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/csv"));
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/plain"));

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                networkToken);

            var responseBytes = await ReadContentWithLimitAsync(
                response.Content,
                networkToken);
            var responseText = DecodeText(responseBytes);

            EnsureSuccess(response, responseText, $"Vehicle CSV API '{source.Name}' request");

            if (IsCsvResponse(response.Content.Headers.ContentType, responseText))
            {
                await File.WriteAllBytesAsync(tempFile, responseBytes, networkToken);
                sourceDescription =
                    $"direct API response from '{source.Name}' on {endpoint.Host}";
            }
            else if (TryExtractCsvText(responseText, out var embeddedCsv))
            {
                await File.WriteAllTextAsync(
                    tempFile,
                    embeddedCsv,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    networkToken);
                sourceDescription =
                    $"embedded API response from '{source.Name}' on {endpoint.Host}";
            }
            else
            {
                var csvUri = ResolveCsvUri(endpoint, responseText);
                await DownloadCsvAsync(csvUri, tempFile, networkToken);
                sourceDescription =
                    $"download URL from '{source.Name}' on {csvUri.Host}";
            }

            await ValidateCsvFileAsync(tempFile, networkToken);

            // The request timeout protects network operations only. Local database import
            // remains cancellable by the application token but is not cut off by the API timeout.
            var importResult = await _importService.ImportAsync(
                tempFile,
                cancellationToken);
            await _logger.StatusAsync(
                $"Vehicle API import completed from {sourceDescription}. " +
                importResult.ToSummary(),
                cancellationToken);

            return new VehicleApiImportResult(
                source.Name,
                source.Priority,
                importResult,
                sourceDescription);
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private List<VehicleApiSourceOptions> SnapshotEnabledSources() =>
        (_options.Import.ApiSources ?? [])
        .Where(source => source is not null && source.Enabled)
        .Select(source => new VehicleApiSourceOptions
        {
            Name = source.Name?.Trim() ?? string.Empty,
            Enabled = true,
            Priority = Math.Max(1, source.Priority),
            Endpoint = source.Endpoint?.Trim() ?? string.Empty,
            Username = source.Username?.Trim() ?? string.Empty,
            RequestTimeoutSeconds = Math.Clamp(source.RequestTimeoutSeconds, 1, 300)
        })
        .ToList();

    private static Uri ParseEndpoint(string? configuredEndpoint, string sourceName)
    {
        var endpoint = configuredEndpoint?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Vehicle CSV API Endpoint for source '{sourceName}' must be a valid " +
                "HTTP or HTTPS URL.");
        }

        return uri;
    }

    private static async Task<byte[]> ReadContentWithLimitAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumDownloadBytes)
        {
            throw new InvalidOperationException(
                $"The server response exceeds the {MaximumDownloadBytes / 1024 / 1024} MB limit.");
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        await CopyWithLimitAsync(source, destination, MaximumDownloadBytes, cancellationToken);
        return destination.ToArray();
    }

    private async Task DownloadCsvAsync(
        Uri csvUri,
        string destinationFile,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, csvUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var errorPreview = string.Empty;
        if (!response.IsSuccessStatusCode)
        {
            var errorBytes = await ReadContentWithLimitAsync(response.Content, cancellationToken);
            errorPreview = DecodeText(errorBytes);
        }

        EnsureSuccess(response, errorPreview, "Vehicle CSV download");

        if (response.Content.Headers.ContentLength is > MaximumDownloadBytes)
        {
            throw new InvalidOperationException(
                $"The downloaded CSV exceeds the {MaximumDownloadBytes / 1024 / 1024} MB limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await CopyWithLimitAsync(
            source,
            destination,
            MaximumDownloadBytes,
            cancellationToken);
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                throw new InvalidOperationException(
                    $"The downloaded data exceeds the {maximumBytes / 1024 / 1024} MB limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static void EnsureSuccess(
        HttpResponseMessage response,
        string responseText,
        string operationName)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var preview = CreateSafePreview(responseText);
        var details = string.IsNullOrWhiteSpace(preview)
            ? string.Empty
            : $" Server response: {preview}";

        throw new InvalidOperationException(
            $"{operationName} failed with HTTP {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}).{details}");
    }

    private static bool IsCsvResponse(
        MediaTypeHeaderValue? contentType,
        string responseText)
    {
        _ = contentType;
        return LooksLikeCsv(responseText);
    }

    private static bool TryExtractCsvText(string responseText, out string csvText)
    {
        csvText = string.Empty;

        if (!TryParseJson(responseText, out var document))
        {
            return false;
        }

        using (document)
        {
            foreach (var value in EnumerateStringValues(document.RootElement))
            {
                if (!LooksLikeCsv(value.Value))
                {
                    continue;
                }

                csvText = value.Value;
                return true;
            }
        }

        return false;
    }

    private static Uri ResolveCsvUri(Uri endpoint, string responseText)
    {
        var candidates = new List<string>();

        if (TryParseJson(responseText, out var document))
        {
            using (document)
            {
                var strings = EnumerateStringValues(document.RootElement).ToList();

                candidates.AddRange(strings
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value.PropertyName) &&
                        PreferredLinkPropertyNames.Contains(value.PropertyName))
                    .Select(value => value.Value));

                candidates.AddRange(strings
                    .Where(value => LooksLikeLinkCandidate(value.Value))
                    .Select(value => value.Value));
            }
        }

        var trimmedResponse = responseText.Trim();
        var plainValue = trimmedResponse.Trim('"', '\'', ' ', '\r', '\n', '\t');
        if (!string.IsNullOrWhiteSpace(plainValue) &&
            !trimmedResponse.StartsWith("{", StringComparison.Ordinal) &&
            !trimmedResponse.StartsWith("[", StringComparison.Ordinal) &&
            !trimmedResponse.StartsWith("<", StringComparison.Ordinal))
        {
            candidates.Add(plainValue);
        }

        foreach (Match match in HtmlLinkRegex.Matches(responseText))
        {
            candidates.Add(WebUtility.HtmlDecode(match.Groups["url"].Value));
        }

        foreach (var candidate in candidates)
        {
            if (TryCreateDownloadUri(endpoint, candidate, out var csvUri))
            {
                return csvUri;
            }
        }

        throw new InvalidOperationException(
            "The vehicle API responded successfully but did not provide a usable CSV link " +
            "or CSV file. Expected a plain URL or a JSON field such as csv_link, csv_url, " +
            "download_url, url or link.");
    }

    private static bool TryCreateDownloadUri(
        Uri endpoint,
        string candidate,
        out Uri csvUri)
    {
        csvUri = null!;
        var value = WebUtility.HtmlDecode(candidate).Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(value) || LooksLikeCsv(value))
        {
            return false;
        }

        Uri? resolved;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            resolved = absolute;
        }
        else if (Uri.TryCreate(endpoint, value, out var relative))
        {
            resolved = relative;
        }
        else
        {
            return false;
        }

        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        csvUri = resolved;
        return true;
    }

    private static bool LooksLikeLinkCandidate(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("/", StringComparison.Ordinal) ||
               trimmed.Contains(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCsv(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var firstLine = value
            .TrimStart('\uFEFF', ' ', '\r', '\n', '\t')
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine) || !firstLine.Contains(','))
        {
            return false;
        }

        var header = firstLine.ToLowerInvariant();
        var hasRfid =
            header.Contains("rf_id", StringComparison.Ordinal) ||
            header.Contains("rfid", StringComparison.Ordinal) ||
            header.Contains("tag_number", StringComparison.Ordinal);
        var hasVehicle =
            header.Contains("vehicle_no", StringComparison.Ordinal) ||
            header.Contains("vehicle_number", StringComparison.Ordinal) ||
            header.Contains("vehicleno", StringComparison.Ordinal);

        return hasRfid && hasVehicle;
    }

    private static async Task ValidateCsvFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);

        var firstLine = await reader.ReadLineAsync(cancellationToken);
        if (!LooksLikeCsv(firstLine ?? string.Empty))
        {
            throw new InvalidOperationException(
                "The downloaded file is not a supported vehicle CSV. Its header must contain " +
                "an RFID column and a vehicle-number column.");
        }
    }

    private static bool TryParseJson(string value, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }

    private static IEnumerable<JsonStringValue> EnumerateStringValues(
        JsonElement element,
        string? propertyName = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return new JsonStringValue(
                    propertyName,
                    element.GetString() ?? string.Empty);
                yield break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in EnumerateStringValues(
                                 property.Value,
                                 property.Name))
                    {
                        yield return nested;
                    }
                }

                yield break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in EnumerateStringValues(item, propertyName))
                    {
                        yield return nested;
                    }
                }

                yield break;
        }
    }

    private static string DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string CreateTemporaryCsvPath(string sourceName)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "RealTechSTPAutomation",
            "VehicleApiImports");
        Directory.CreateDirectory(directory);

        var safeSourceName = new string(sourceName
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(40)
            .ToArray());
        if (string.IsNullOrWhiteSpace(safeSourceName))
        {
            safeSourceName = "source";
        }

        return Path.Combine(
            directory,
            $"vehicle-api-{safeSourceName}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
    }

    private static string CreateSafePreview(string value)
    {
        var singleLine = string.Join(
            ' ',
            value.Split(
                ['\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return singleLine.Length <= MaximumErrorPreviewCharacters
            ? singleLine
            : singleLine[..MaximumErrorPreviewCharacters] + "...";
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Temporary-file cleanup must not hide the import result or the real failure.
        }
    }

    private sealed record JsonStringValue(string? PropertyName, string Value);
}

public sealed record VehicleApiImportResult(
    string SourceName,
    int Priority,
    ImportResult ImportResult,
    string SourceDescription);

public sealed record VehicleApiSourceImportResult(
    string SourceName,
    int Priority,
    bool Success,
    ImportResult? ImportResult,
    string SourceDescription,
    string Error);

public sealed record VehicleApiBatchImportResult(
    IReadOnlyList<VehicleApiSourceImportResult> Sources)
{
    public int SuccessfulSourceCount => Sources.Count(source => source.Success);
    public int FailedSourceCount => Sources.Count - SuccessfulSourceCount;

    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"API sources: {Sources.Count}; successful: {SuccessfulSourceCount}; " +
            $"failed: {FailedSourceCount}");

        foreach (var source in Sources
                     .OrderBy(item => item.Priority)
                     .ThenBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.Append($"[{source.Priority}] {source.SourceName}: ");
            if (source.Success && source.ImportResult is not null)
            {
                builder.Append(source.ImportResult.ToSummary());
            }
            else
            {
                builder.Append($"FAILED - {source.Error}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
