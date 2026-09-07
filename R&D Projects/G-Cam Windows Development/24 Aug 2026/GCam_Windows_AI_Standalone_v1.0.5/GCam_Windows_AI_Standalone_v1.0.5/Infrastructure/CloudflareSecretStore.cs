using System.IO;

namespace GCam.Windows.Infrastructure;

public static class CloudflareSecretStore
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GCam", "cloudflare");

    public static string TokenPath => Path.Combine(DirectoryPath, "tunnel.token");

    public static bool HasToken => File.Exists(TokenPath) && new FileInfo(TokenPath).Length > 20;

    public static void SaveToken(string token)
    {
        token = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Cloudflare tunnel token is empty.");
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(TokenPath, token + Environment.NewLine);
        try { File.SetAttributes(TokenPath, FileAttributes.Hidden); } catch { }
    }

    public static string? ReadToken()
    {
        if (!HasToken) return null;
        return File.ReadAllText(TokenPath).Trim();
    }

    public static void DeleteToken()
    {
        try { File.Delete(TokenPath); } catch { }
    }

    public static string NormalizeToken(string value)
    {
        value = (value ?? string.Empty).Trim();
        // Accept the complete command copied from Cloudflare Dashboard -> Add a replica.
        if (value.Contains("cloudflared", StringComparison.OrdinalIgnoreCase) && value.Contains("service install", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            value = parts.LastOrDefault(p => p.StartsWith("eyJ", StringComparison.Ordinal)) ?? parts.LastOrDefault() ?? value;
        }
        return value.Trim().Trim('"', '\'');
    }
}
