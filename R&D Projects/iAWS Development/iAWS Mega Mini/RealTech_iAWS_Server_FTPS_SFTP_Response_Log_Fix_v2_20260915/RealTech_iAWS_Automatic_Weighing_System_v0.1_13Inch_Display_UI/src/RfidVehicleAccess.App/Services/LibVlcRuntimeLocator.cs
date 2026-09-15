namespace RfidVehicleAccess.Services;

/// <summary>
/// Locates the native LibVLC runtime in both normal folder deployments and
/// .NET self-contained single-file deployments. The single-file bundle is
/// configured to extract native/content files before managed startup.
/// </summary>
public static class LibVlcRuntimeLocator
{
    private const string LibVlcFileName = "libvlc.dll";
    private const string LibVlcCoreFileName = "libvlccore.dll";
    private const string NativeSearchDirectoriesKey = "NATIVE_DLL_SEARCH_DIRECTORIES";
    private const string TrustedPlatformAssembliesKey = "TRUSTED_PLATFORM_ASSEMBLIES";

    public static string Locate()
    {
        var searchedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetCandidateRoots())
        {
            foreach (var candidate in ExpandCandidateRoot(root))
            {
                if (!searchedDirectories.Add(candidate))
                {
                    continue;
                }

                if (ContainsLibVlc(candidate))
                {
                    return candidate;
                }
            }
        }

        var extractedDirectory = FindInDefaultBundleExtractionDirectory();
        if (extractedDirectory is not null)
        {
            return extractedDirectory;
        }

        throw new DirectoryNotFoundException(
            "The bundled LibVLC runtime could not be located. " +
            "Rebuild the application with IncludeNativeLibrariesForSelfExtract and " +
            "IncludeAllContentForSelfExtract enabled. Searched: " +
            string.Join("; ", searchedDirectories));
    }

    private static IEnumerable<string> GetCandidateRoots()
    {
        yield return AppContext.BaseDirectory;

        var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(processDirectory))
        {
            yield return processDirectory;
        }

        foreach (var path in ReadAppContextPathList(NativeSearchDirectoriesKey))
        {
            yield return path;
        }

        foreach (var assemblyPath in ReadAppContextPathList(TrustedPlatformAssembliesKey))
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                yield return assemblyDirectory;
            }
        }
    }

    private static IEnumerable<string> ExpandCandidateRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(root);
        }
        catch (Exception) when (
            root.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            yield break;
        }

        yield return fullRoot;
        yield return Path.Combine(fullRoot, "libvlc", "win-x64");
        yield return Path.Combine(fullRoot, "runtimes", "win-x64", "native");

        var parent = Directory.GetParent(fullRoot)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            yield return Path.Combine(parent, "libvlc", "win-x64");
            yield return Path.Combine(parent, "runtimes", "win-x64", "native");
        }
    }

    private static IEnumerable<string> ReadAppContextPathList(string key)
    {
        if (AppContext.GetData(key) is not string pathList ||
            string.IsNullOrWhiteSpace(pathList))
        {
            yield break;
        }

        foreach (var path in pathList.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return path;
        }
    }

    private static string? FindInDefaultBundleExtractionDirectory()
    {
        var executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(executableName))
        {
            executableName = "RealTechiAWS";
        }

        var extractionBase = Environment.GetEnvironmentVariable(
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR");
        var extractionRoot = string.IsNullOrWhiteSpace(extractionBase)
            ? Path.Combine(Path.GetTempPath(), ".net", executableName)
            : Path.Combine(Environment.ExpandEnvironmentVariables(extractionBase), executableName);

        if (!Directory.Exists(extractionRoot))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    extractionRoot,
                    LibVlcFileName,
                    SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(static directory => !string.IsNullOrWhiteSpace(directory))
                .Select(static directory => directory!)
                .Where(ContainsLibVlc)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool ContainsLibVlc(string directory) =>
        File.Exists(Path.Combine(directory, LibVlcFileName)) &&
        File.Exists(Path.Combine(directory, LibVlcCoreFileName));
}
