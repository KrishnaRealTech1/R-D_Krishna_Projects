using System.IO;
namespace GCam.Windows.Infrastructure;

public static class PathHelper
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string ResolveAppPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(BaseDirectory, path));
    }
}
