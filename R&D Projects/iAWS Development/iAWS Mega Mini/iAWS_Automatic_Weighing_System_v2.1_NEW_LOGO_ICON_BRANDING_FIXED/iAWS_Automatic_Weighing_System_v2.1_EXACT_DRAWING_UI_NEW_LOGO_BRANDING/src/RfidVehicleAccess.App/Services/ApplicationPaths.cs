using System.Reflection;

namespace RfidVehicleAccess.Services;

public static class ApplicationPaths
{
    private const string DefaultSettingsResourceName =
        "RfidVehicleAccess.DefaultAppSettings.json";

    public static string ConfigurationDirectory => Path.Combine(
        PathResolver.IawsSystemRoot,
        "Config");

    public static string ConfigurationFile => Path.Combine(
        ConfigurationDirectory,
        "appsettings.json");

    public static string LegacyConfigurationFile => Path.Combine(
        AppContext.BaseDirectory,
        "appsettings.json");

    public static string EnsureConfigurationFile()
    {
        Directory.CreateDirectory(ConfigurationDirectory);

        if (File.Exists(ConfigurationFile))
        {
            return ConfigurationFile;
        }

        if (!PathsEqual(ConfigurationFile, LegacyConfigurationFile) &&
            File.Exists(LegacyConfigurationFile))
        {
            File.Copy(LegacyConfigurationFile, ConfigurationFile, overwrite: false);
            return ConfigurationFile;
        }

        ExtractDefaultConfiguration();
        return ConfigurationFile;
    }

    private static void ExtractDefaultConfiguration()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var source = assembly.GetManifestResourceStream(DefaultSettingsResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded configuration resource '{DefaultSettingsResourceName}' was not found.");

        var temporaryFile = ConfigurationFile + ".tmp";
        try
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }

            using (var destination = new FileStream(
                       temporaryFile,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }

            File.Move(temporaryFile, ConfigurationFile, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static bool PathsEqual(string firstPath, string secondPath) =>
        string.Equals(
            Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
