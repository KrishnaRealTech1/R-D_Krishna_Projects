using System.Globalization;
using System.Text;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed record CameraCaptureResult(
    int CameraNumber,
    string Path,
    string Status);

public sealed class CameraSnapshotService(
    AppOptions options,
    CameraStreamService cameraStreams,
    AppLogger logger)
{
    private const string DefaultTimestampFormat = "yyyy-MM-dd_HH-mm-ss-fff";

    private static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    public async Task<IReadOnlyList<CameraCaptureResult>> CaptureAllAsync(
        LaneDirection direction,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var tasks = Enumerable.Range(1, 4)
            .Select(cameraNumber => CaptureCameraAsync(
                cameraNumber,
                direction,
                transactionId,
                cancellationToken))
            .ToArray();

        return await Task.WhenAll(tasks);
    }

    public async Task<CameraCaptureResult> CaptureCameraAsync(
        int cameraNumber,
        LaneDirection direction,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var lane = direction == LaneDirection.In ? "IN" : "OUT";
        var root = PathResolver.EnsureDirectory(options.Cameras.LocalImageFolder);
        var now = DateTime.Now;
        var dayFolder = Path.Combine(
            root,
            now.ToString("yyyy", CultureInfo.InvariantCulture),
            now.ToString("MM", CultureInfo.InvariantCulture),
            now.ToString("dd", CultureInfo.InvariantCulture),
            lane,
            transactionId.ToString("N"));
        Directory.CreateDirectory(dayFolder);

        var fileName = BuildImageFileName(lane, cameraNumber, now);
        var path = Path.Combine(dayFolder, fileName);
        var camera = options.Cameras.Get(cameraNumber);

        if (!camera.Enabled)
        {
            await SavePlaceholderAsync(path, cancellationToken);
            await logger.StatusAsync(
                $"Camera {cameraNumber} is disabled; saved placeholder {fileName}.",
                cancellationToken);
            return new CameraCaptureResult(
                cameraNumber,
                path,
                "CameraDisabled-PlaceholderSaved");
        }

        try
        {
            var captured = await cameraStreams.CaptureSnapshotAsync(
                cameraNumber,
                path,
                camera.SnapshotWidth,
                camera.SnapshotHeight,
                cancellationToken);

            if (captured)
            {
                await logger.StatusAsync(
                    $"Camera {cameraNumber} snapshot saved as {fileName}.",
                    cancellationToken);
                return new CameraCaptureResult(cameraNumber, path, "Captured");
            }

            await SavePlaceholderAsync(path, cancellationToken);
            await logger.StatusAsync(
                $"Camera {cameraNumber} was not streaming; saved placeholder {fileName}.",
                cancellationToken);
            return new CameraCaptureResult(
                cameraNumber,
                path,
                "CameraUnavailable-PlaceholderSaved");
        }
        catch (Exception ex)
        {
            await SavePlaceholderAsync(path, cancellationToken);
            await logger.StatusAsync(
                $"Camera {cameraNumber} snapshot failed ({ex.Message}); saved placeholder {fileName}.",
                cancellationToken);
            return new CameraCaptureResult(
                cameraNumber,
                path,
                "CaptureFailed-PlaceholderSaved");
        }
    }

    private string BuildImageFileName(
        string lane,
        int cameraNumber,
        DateTime capturedAt)
    {
        var configuredPrefix = SanitizeFileNamePart(options.Cameras.ImageFilePrefix);
        var timestamp = FormatTimestamp(capturedAt, options.Cameras.ImageTimestampFormat);

        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(configuredPrefix))
        {
            parts.Add(configuredPrefix);
        }

        parts.Add(timestamp);
        parts.Add(lane);
        parts.Add($"CAM{cameraNumber}");
        return string.Join(" - ", parts) + ".png";
    }

    private static string FormatTimestamp(DateTime value, string configuredFormat)
    {
        var format = string.IsNullOrWhiteSpace(configuredFormat)
            ? DefaultTimestampFormat
            : configuredFormat.Trim();

        string formatted;
        try
        {
            formatted = value.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            formatted = value.ToString(DefaultTimestampFormat, CultureInfo.InvariantCulture);
        }

        return SanitizeFileNamePart(formatted);
    }

    private static string SanitizeFileNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim())
        {
            var replacementRequired = invalidCharacters.Contains(character) || char.IsControl(character);
            var output = replacementRequired ? '-' : character;
            var isSeparator = output is '-' or ' ';
            if (isSeparator && previousWasSeparator)
            {
                continue;
            }

            builder.Append(output);
            previousWasSeparator = isSeparator;
        }

        return builder.ToString().Trim(' ', '-', '.');
    }

    private static Task SavePlaceholderAsync(
        string path,
        CancellationToken cancellationToken) =>
        File.WriteAllBytesAsync(path, PlaceholderPng, cancellationToken);
}
