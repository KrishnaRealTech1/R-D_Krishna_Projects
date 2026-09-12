namespace RfidVehicleAccess.Services;

public static class PathResolver
{
    private const string DocumentsToken = "%DOCUMENTS%";
    private const string IawsSystemToken = "%IAWS_SYSTEM%";
    private const string RealtechSystemsToken = "%REALTECH_SYSTEMS%";

    // Keep the existing iAWS storage root while exposing the iTOLL-compatible alias.
    public static string IawsSystemRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "iAWS");

    public static string RealtechSystemsRoot => IawsSystemRoot;

    public static string ResolveFromAppBase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AppContext.BaseDirectory;
        }

        var normalizedPath = path.Trim();
        normalizedPath = ReplacePathToken(normalizedPath, IawsSystemToken, IawsSystemRoot);
        normalizedPath = ReplacePathToken(normalizedPath, RealtechSystemsToken, RealtechSystemsRoot);
        normalizedPath = ReplacePathToken(
            normalizedPath,
            DocumentsToken,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        normalizedPath = Environment.ExpandEnvironmentVariables(normalizedPath);

        return Path.IsPathRooted(normalizedPath)
            ? Path.GetFullPath(normalizedPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedPath));
    }

    public static string EnsureDirectory(string path)
    {
        var fullPath = ResolveFromAppBase(path);
        Directory.CreateDirectory(fullPath);
        return fullPath;
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

    public static string EnsureCaptureStorage()
    {
        var root = IawsSystemRoot;
        var dataFolder = Path.Combine(root, "Data");
        var imagesFolder = Path.Combine(root, "Images");
        var logsFolder = Path.Combine(root, "Logs");
        var statusLogsFolder = Path.Combine(logsFolder, "Status");
        var serverLogsFolder = Path.Combine(logsFolder, "Server");
        var apiLogsFolder = Path.Combine(logsFolder, "Api");
        var configurationFolder = Path.Combine(root, "Config");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(logsFolder);
        Directory.CreateDirectory(statusLogsFolder);
        Directory.CreateDirectory(serverLogsFolder);
        Directory.CreateDirectory(apiLogsFolder);
        Directory.CreateDirectory(configurationFolder);

        CopyMissingFile(
            Path.Combine(AppContext.BaseDirectory, "Data", "vehicle-access.db"),
            Path.Combine(dataFolder, "vehicle-access.db"));
        CopyMissingFiles(Path.Combine(AppContext.BaseDirectory, "Data", "Images"), imagesFolder);
        CopyMissingFiles(Path.Combine(AppContext.BaseDirectory, "Logs"), logsFolder);

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

        foreach (var sourceFile in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
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

    private static string ReplacePathToken(string path, string token, string replacement)
    {
        if (path.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return replacement + path[token.Length..];
        }

        return path.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);
    }
}
