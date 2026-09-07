using System.IO;
using System.Text.Json;
using GCam.Windows.Models;

namespace GCam.Windows.Services;

public sealed class SettingsService
{
    private readonly string _path;
    private readonly string _defaultsPath;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string ConfigPath => _path;

    public SettingsService(string? path = null)
    {
        var userRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GCam");
        Directory.CreateDirectory(userRoot);
        _path = path ?? Path.Combine(userRoot, "config.json");
        _defaultsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public AppSettings Load()
    {
        AppSettings settings;
        if (!File.Exists(_path))
        {
            settings = File.Exists(_defaultsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_defaultsPath), _json) ?? new AppSettings()
                : new AppSettings();
            Normalize(settings);
            Save(settings);
            return settings;
        }

        settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _json) ?? new AppSettings();
        Normalize(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, _json));
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Cloudflare ??= new CloudflareSettings();
        if (string.IsNullOrWhiteSpace(settings.Cloudflare.ControlApiKey))
            settings.Cloudflare.ControlApiKey = Guid.NewGuid().ToString("N");
        settings.Cloudflare.DashboardPort = Math.Clamp(settings.Cloudflare.DashboardPort, 1024, 65535);
        settings.Cloudflare.MetricsPort = Math.Clamp(settings.Cloudflare.MetricsPort, 1024, 65535);
        settings.Cloudflare.Protocol = CloudflareTunnelService.NormalizeProtocol(settings.Cloudflare.Protocol);
    }
}
