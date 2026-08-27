using System.Diagnostics;
using System.IO;
using System.Net.Http;
using GCam.Windows.Infrastructure;
using GCam.Windows.Models;

namespace GCam.Windows.Services;

public sealed class CloudflareTunnelService : IAsyncDisposable, IDisposable
{
    private readonly object _lock = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private CancellationTokenSource? _cts;
    private Task? _watchdog;
    private Process? _process;
    private CloudflareSettings? _settings;
    private volatile bool _wantRunning;
    private string _status = "Stopped";
    private string _lastLog = "";

    public string Status => string.IsNullOrWhiteSpace(_lastLog) ? _status : $"{_status} | {_lastLog}";
    public bool IsRunning => _process is { HasExited: false };

    public void Start(CloudflareSettings settings)
    {
        if (!settings.TunnelEnabled) throw new InvalidOperationException("Enable Cloudflare Tunnel first.");
        if (!CloudflareSecretStore.HasToken)
            throw new InvalidOperationException("No Cloudflare tunnel token is saved. Paste the Add-a-replica token in the Cloudflare tab and Save Settings.");

        Stop();
        _settings = settings;
        _wantRunning = true;
        _cts = new CancellationTokenSource();
        StartProcess();
        _watchdog = Task.Run(() => WatchdogLoop(_cts.Token));
    }

    private void StartProcess()
    {
        lock (_lock)
        {
            if (!_wantRunning || _settings is null) return;
            if (_process is { HasExited: false }) return;

            string exe = CloudflaredLocator.Resolve();
            string protocol = NormalizeProtocol(_settings.Protocol);
            int metricsPort = Math.Clamp(_settings.MetricsPort, 1024, 65535);
            Directory.CreateDirectory(CloudflareSecretStore.DirectoryPath);
            string logPath = Path.Combine(CloudflareSecretStore.DirectoryPath, "cloudflared.log");

            string args = "tunnel --no-autoupdate " +
                          $"--protocol {protocol} --metrics 127.0.0.1:{metricsPort} " +
                          $"--loglevel info --logfile {Q(logPath)} run --token-file {Q(CloudflareSecretStore.TokenPath)}";

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) => CaptureLog(e.Data);
            _process.ErrorDataReceived += (_, e) => CaptureLog(e.Data);
            _process.Exited += (_, _) =>
            {
                if (_wantRunning) _status = "Connector exited; watchdog will restart";
            };
            if (!_process.Start()) throw new InvalidOperationException("Unable to start cloudflared.exe.");
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _status = "Connector starting";
        }
    }

    private async Task WatchdogLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _wantRunning)
        {
            try
            {
                if (_process is null || _process.HasExited)
                {
                    _status = "Restarting connector";
                    StartProcess();
                }
                else if (_settings is not null)
                {
                    string localHealth = $"http://127.0.0.1:{_settings.DashboardPort}/health";
                    bool localOk = await IsHealthy(localHealth, ct);
                    if (!localOk)
                    {
                        _status = "Tunnel running; local dashboard health failed";
                    }
                    else if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
                    {
                        string publicHealth = _settings.PublicBaseUrl.TrimEnd('/') + "/health";
                        bool publicOk = await IsHealthy(publicHealth, ct);
                        _status = publicOk ? "Connected - public URL healthy" : "Connector running - public URL not confirmed";
                    }
                    else
                    {
                        _status = "Connector running - configure Public Base URL for public health check";
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _status = "Tunnel watchdog: " + ex.Message; }

            try { await Task.Delay(TimeSpan.FromSeconds(20), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<bool> IsHealthy(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct);
            return (int)response.StatusCode is >= 200 and < 500;
        }
        catch { return false; }
    }

    private void CaptureLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        line = line.Trim();
        if (line.Length > 220) line = line[^220..];
        _lastLog = line;
        if (line.Contains("Registered tunnel connection", StringComparison.OrdinalIgnoreCase)) _status = "Connected";
    }

    public void Stop()
    {
        _wantRunning = false;
        try { _cts?.Cancel(); } catch { }
        lock (_lock)
        {
            if (_process is { HasExited: false })
            {
                try { _process.Kill(entireProcessTree: true); _process.WaitForExit(1500); } catch { }
            }
            _process?.Dispose();
            _process = null;
        }
        _cts?.Dispose();
        _cts = null;
        _watchdog = null;
        _status = "Stopped";
        _lastLog = "";
    }

    public static string NormalizeProtocol(string? value)
    {
        value = (value ?? "auto").Trim().ToLowerInvariant();
        return value is "auto" or "http2" or "quic" ? value : "auto";
    }

    private static string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        _http.Dispose();
        await Task.CompletedTask;
    }
}
