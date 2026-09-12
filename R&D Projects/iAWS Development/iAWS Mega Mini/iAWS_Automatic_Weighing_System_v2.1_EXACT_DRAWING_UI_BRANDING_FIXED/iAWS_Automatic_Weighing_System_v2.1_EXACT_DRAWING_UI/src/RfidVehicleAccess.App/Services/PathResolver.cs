namespace RfidVehicleAccess.Services;

public static class PathResolver
{
    private const string DocumentsToken = "%DOCUMENTS%";
    private const string IawsSystemToken = "%IAWS_SYSTEM%";

    public static string IawsSystemRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "iAWS");

    public static string ResolveFromAppBase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A storage path is required.", nameof(path));
        }

        var normalizedPath = path.Trim();
        normalizedPath = ReplacePathToken(normalizedPath, IawsSystemToken, IawsSystemRoot);
        normalizedPath = ReplacePathToken(
            normalizedPath,
            DocumentsToken,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        normalizedPath = Environment.ExpandEnvironmentVariables(normalizedPath);

        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedPath));
    }

    public static string EnsureParentDirectory(string path)
    {
        var fullPath = ResolveFromAppBase(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return fullPath;
    }

    public static string EnsureDirectory(string path)
    {
        var fullPath = ResolveFromAppBase(path);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public static string EnsureCaptureStorage()
    {
        var root = IawsSystemRoot;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        Directory.CreateDirectory(Path.Combine(root, "Images"));
        Directory.CreateDirectory(Path.Combine(root, "Logs", "Status"));
        Directory.CreateDirectory(Path.Combine(root, "Logs", "Api"));
        Directory.CreateDirectory(Path.Combine(root, "Config"));
        return root;
    }

    private static string ReplacePathToken(
        string path,
        string token,
        string replacement)
    {
        if (!path.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var remainingPath = path[token.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\', '/');
        return string.IsNullOrWhiteSpace(remainingPath)
            ? replacement
            : Path.Combine(replacement, remainingPath);
    }
}
