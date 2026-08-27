using System.IO;
using System.Reflection;

namespace GCam.Windows.Infrastructure;

public static class CloudflaredLocator
{
    private const string ResourceName = "GCam.Windows.Resources.cloudflared.exe";

    public static string Resolve()
    {
        string besideExe = Path.Combine(AppContext.BaseDirectory, "tools", "cloudflared.exe");
        if (File.Exists(besideExe)) return besideExe;

        string localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GCam", "tools");
        string extracted = Path.Combine(localRoot, "cloudflared.exe");
        if (File.Exists(extracted) && new FileInfo(extracted).Length > 1_000_000) return extracted;

        Directory.CreateDirectory(localRoot);
        var asm = Assembly.GetExecutingAssembly();
        using Stream? source = asm.GetManifestResourceStream(ResourceName);
        if (source is null)
            throw new FileNotFoundException("cloudflared.exe is not available. Rebuild with build-win-x64.ps1 so Cloudflare Tunnel support is packaged.", besideExe);

        string temp = extracted + ".tmp";
        using (var output = File.Create(temp)) source.CopyTo(output);
        File.Move(temp, extracted, true);
        return extracted;
    }
}
