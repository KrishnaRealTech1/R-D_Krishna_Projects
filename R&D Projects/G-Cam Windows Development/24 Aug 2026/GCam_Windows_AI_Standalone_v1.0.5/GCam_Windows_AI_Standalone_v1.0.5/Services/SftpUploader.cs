using System.IO;
using GCam.Windows.Models;
using Renci.SshNet;

namespace GCam.Windows.Services;

public sealed class SftpUploader
{
    public async Task UploadEvidenceAsync(AppSettings settings, EventKind kind, params string?[] paths)
    {
        if (!settings.Evidence.UploadEnabled || string.IsNullOrWhiteSpace(settings.Sftp.Host)) return;
        await Task.Run(() =>
        {
            using var client = new SftpClient(settings.Sftp.Host, settings.Sftp.Port, settings.Sftp.Username, settings.Sftp.Password);
            client.Connect();
            string remoteDir = CombineRemote(settings.Sftp.BaseDirectory, kind.ToString());
            EnsureDirectory(client, remoteDir);
            foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)))
            {
                using var fs = File.OpenRead(path!);
                client.UploadFile(fs, CombineRemote(remoteDir, Path.GetFileName(path)), true);
            }
            client.Disconnect();
        });
    }

    private static void EnsureDirectory(SftpClient client, string path)
    {
        string current = path.StartsWith('/') ? "/" : "";
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = CombineRemote(current, part);
            if (!client.Exists(current)) client.CreateDirectory(current);
        }
    }

    private static string CombineRemote(string a, string b)
        => (a.TrimEnd('/') + "/" + b.TrimStart('/')).Replace("//", "/");
}
