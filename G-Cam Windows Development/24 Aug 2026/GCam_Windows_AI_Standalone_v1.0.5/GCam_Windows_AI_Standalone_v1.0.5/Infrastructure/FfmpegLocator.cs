using System.IO;
using System.Reflection;

namespace GCam.Windows.Infrastructure;

public static class FfmpegLocator
{
    private const string ResourceName = "GCam.Windows.Resources.ffmpeg.exe";

    public static string Resolve()
    {
        // Normal portable deployment: Publish\win-x64\tools\ffmpeg.exe
        string appTools = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        if (File.Exists(appTools)) return appTools;

        // Also accept ffmpeg.exe directly beside GCam.Windows.exe.
        string appRoot = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(appRoot)) return appRoot;

        // Accept an FFmpeg installation available through PATH.
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (string rawDir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string dir = rawDir.Trim().Trim('"');
                    if (dir.Length == 0) continue;
                    string candidate = Path.Combine(dir, "ffmpeg.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
        }

        // Single-EXE deployment: build-win-x64.ps1 embeds ffmpeg.exe as an assembly resource.
        return ExtractEmbeddedFfmpeg();
    }

    private static string ExtractEmbeddedFfmpeg()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using Stream? resource = asm.GetManifestResourceStream(ResourceName);
        if (resource is null)
        {
            throw new FileNotFoundException(
                "FFmpeg was not found beside the application, under tools\\ffmpeg.exe, on PATH, " +
                "or inside the application package. Rebuild with build-win-x64.ps1 so FFmpeg is downloaded and embedded.");
        }

        string toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GCam", "tools");
        Directory.CreateDirectory(toolsDir);

        string target = Path.Combine(toolsDir, "ffmpeg.exe");
        long expectedLength = resource.CanSeek ? resource.Length : -1;

        try
        {
            if (File.Exists(target) && expectedLength > 0 && new FileInfo(target).Length == expectedLength)
                return target;
        }
        catch { }

        string temp = target + ".tmp";
        try
        {
            if (File.Exists(temp)) File.Delete(temp);
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                resource.CopyTo(output);

            File.Move(temp, target, overwrite: true);
            return target;
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            throw;
        }
    }
}
