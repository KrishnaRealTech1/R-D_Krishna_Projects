using System.Globalization;
using System.Text;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed record IawsCaptureResult(
    string FrontPath,
    string BackPath,
    string LeftPath,
    string RightPath,
    bool FrontCaptured,
    bool BackCaptured,
    bool LeftCaptured,
    bool RightCaptured)
{
    public bool AllCaptured => FrontCaptured && BackCaptured && LeftCaptured && RightCaptured;
    public string FrontFileName => Path.GetFileName(FrontPath);
    public string BackFileName => Path.GetFileName(BackPath);
    public string LeftFileName => Path.GetFileName(LeftPath);
    public string RightFileName => Path.GetFileName(RightPath);
}

public sealed class IawsCameraCaptureService(
    AppOptions options,
    IawsCameraStreamService streams,
    AppLogger logger)
{
    public async Task<IawsCaptureResult> CaptureAllAsync(
        DateTime capturedAt,
        CancellationToken cancellationToken = default)
    {
        var root = PathResolver.EnsureDirectory(options.Cameras.LocalImageFolder);
        var dayFolder = Path.Combine(
            root,
            capturedAt.ToString("yyyy", CultureInfo.InvariantCulture),
            capturedAt.ToString("MM", CultureInfo.InvariantCulture),
            capturedAt.ToString("dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dayFolder);

        var prefix = SanitizeFileNamePart(
            string.IsNullOrWhiteSpace(options.Iaws.SitePrefix)
                ? "IAWS_SITE"
                : options.Iaws.SitePrefix);
        var timePart = capturedAt.ToString("HHmmss_ddMMyyyy", CultureInfo.InvariantCulture);

        var frontPath = Path.Combine(dayFolder, $"{prefix}_CAM_1_{timePart}.jpg");
        var backPath = Path.Combine(dayFolder, $"{prefix}_CAM_2_{timePart}.jpg");
        var leftPath = Path.Combine(dayFolder, $"{prefix}_CAM_3_{timePart}.jpg");
        var rightPath = Path.Combine(dayFolder, $"{prefix}_CAM_4_{timePart}.jpg");

        var frontTask = streams.CaptureSnapshotAsync(IawsCameraPosition.Front, frontPath, cancellationToken);
        var backTask = streams.CaptureSnapshotAsync(IawsCameraPosition.Back, backPath, cancellationToken);
        var leftTask = streams.CaptureSnapshotAsync(IawsCameraPosition.Left, leftPath, cancellationToken);
        var rightTask = streams.CaptureSnapshotAsync(IawsCameraPosition.Right, rightPath, cancellationToken);

        await Task.WhenAll(frontTask, backTask, leftTask, rightTask);

        var result = new IawsCaptureResult(
            frontPath,
            backPath,
            leftPath,
            rightPath,
            frontTask.Result,
            backTask.Result,
            leftTask.Result,
            rightTask.Result);

        await logger.StatusAsync(
            $"4-camera capture: Front={ToText(result.FrontCaptured)}, Back={ToText(result.BackCaptured)}, Left={ToText(result.LeftCaptured)}, Right={ToText(result.RightCaptured)}.",
            cancellationToken);

        return result;
    }

    private static string ToText(bool value) => value ? "OK" : "FAILED";

    private static string SanitizeFileNamePart(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "IAWS_SITE" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(invalid.Contains(character) || char.IsControl(character) ? '_' : character);
        }

        return builder.ToString().Trim(' ', '_', '.');
    }
}
