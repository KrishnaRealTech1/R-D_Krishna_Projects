using System.Text.Json;

namespace RfidVehicleAccess.Services;

public sealed class AppConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly AppOptions _options;

    public AppConfigurationService(AppOptions options)
    {
        _options = options;
    }

    public string ConfigurationPath => ApplicationPaths.ConfigurationFile;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var configurationDirectory = Path.GetDirectoryName(ConfigurationPath);
        if (!string.IsNullOrWhiteSpace(configurationDirectory))
        {
            Directory.CreateDirectory(configurationDirectory);
        }

        var temporaryPath = ConfigurationPath + ".tmp";
        var json = JsonSerializer.Serialize(_options, SerializerOptions);

        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, ConfigurationPath, true);
    }
}
