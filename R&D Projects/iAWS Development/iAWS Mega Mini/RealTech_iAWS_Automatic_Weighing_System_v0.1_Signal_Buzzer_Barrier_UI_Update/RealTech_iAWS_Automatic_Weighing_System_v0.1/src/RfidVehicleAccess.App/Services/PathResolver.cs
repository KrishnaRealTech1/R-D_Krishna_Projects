namespace RfidVehicleAccess.Services;

public static class PathResolver
{
    private const string DocumentsToken = "%DOCUMENTS%";
    private const string RealtechSystemsToken = "%REALTECH_SYSTEMS%";

    public static string RealtechSystemsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Realtech_systems");

    public static string ResolveFromAppBase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A storage path is required.", nameof(path));
        }

        var normalizedPath = path.Trim();
        normalizedPath = ReplacePathToken(
            normalizedPath,
            RealtechSystemsToken,
            RealtechSystemsRoot);
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
        var root = RealtechSystemsRoot;
        var dataFolder = Path.Combine(root, "Data");
        var imagesFolder = Path.Combine(root, "Images");
        var logsFolder = Path.Combine(root, "Logs");
        var statusLogsFolder = Path.Combine(logsFolder, "Status");
        var serverLogsFolder = Path.Combine(logsFolder, "Server");
        var configurationFolder = Path.Combine(root, "Config");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(logsFolder);
        Directory.CreateDirectory(statusLogsFolder);
        Directory.CreateDirectory(serverLogsFolder);
        Directory.CreateDirectory(configurationFolder);

        CopyMissingFile(
            Path.Combine(AppContext.BaseDirectory, "Data", "vehicle-access.db"),
            Path.Combine(dataFolder, "vehicle-access.db"));
        CopyMissingFiles(
            Path.Combine(AppContext.BaseDirectory, "Data", "Images"),
            imagesFolder);
        CopyMissingFiles(
            Path.Combine(AppContext.BaseDirectory, "Logs"),
            logsFolder);

        return root;
    }


    private static void CopyMissingFile(string sourceFile, string destinationFile)
    {
        if (!File.Exists(sourceFile) || File.Exists(destinationFile))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourceFile, destinationFile, overwrite: false);
    }

    private static void CopyMissingFiles(string sourceFolder, string destinationFolder)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourceFolder,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
            var destinationFile = Path.Combine(destinationFolder, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (!File.Exists(destinationFile))
            {
                File.Copy(sourceFile, destinationFile);
            }
        }
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
