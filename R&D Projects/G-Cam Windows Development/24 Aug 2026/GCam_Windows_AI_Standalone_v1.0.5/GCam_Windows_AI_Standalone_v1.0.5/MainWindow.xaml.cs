using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GCam.Windows.Infrastructure;
using GCam.Windows.Models;
using GCam.Windows.Services;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace GCam.Windows;

public partial class MainWindow : System.Windows.Window
{
    private readonly SettingsService _settingsService = new();
    private readonly object _settingsLock = new();
    private readonly object _eventHistoryLock = new();
    private readonly List<EventRecord> _eventHistory = new();
    private readonly ObservableCollection<EventRecord> _eventRows = new();
    private readonly DispatcherTimer _statusTimer;

    private AppSettings _settings;
    private CameraService? _camera;
    private AiCoordinator? _ai;
    private RollingBufferService? _buffer;
    private WarningAudioService? _audio;
    private EventCoordinator? _events;
    private DashboardService? _dashboard;
    private CloudflareTunnelService? _cloudflareTunnel;

    private int _processing;
    private long _detectionsSeen;
    private long _eventsTriggered;
    private string _lastAiError = "";

    public sealed class EventSettingsRow
    {
        public string Name { get; init; } = "";
        public EventTypeSettings Settings { get; init; } = new();
    }

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        LoadSettingsToUi();
        EventsGrid.ItemsSource = _eventRows;
        EventSettingsGrid.ItemsSource = new[]
        {
            new EventSettingsRow { Name = "Person", Settings = _settings.Ai.Person },
            new EventSettingsRow { Name = "Vehicle", Settings = _settings.Ai.Vehicle },
            new EventSettingsRow { Name = "LPD", Settings = _settings.Ai.LicensePlateEvent },
            new EventSettingsRow { Name = "Garbage", Settings = _settings.Ai.GarbageEvent },
        };

        _cloudflareTunnel = new CloudflareTunnelService();
        _dashboard = new DashboardService(CreateDashboardBridge());
        TryStartDashboardOnLaunch();
        TryAutoStartTunnel();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += (_, _) => RefreshCloudflareStatusUi();
        _statusTimer.Start();
        RefreshCloudflareStatusUi();
    }

    private DashboardBridge CreateDashboardBridge() => new()
    {
        GetState = GetDashboardState,
        GetControls = GetDashboardControls,
        GetEvents = GetEventHistorySnapshot,
        GetSnapshotJpeg = GetDashboardSnapshotJpeg,
        UpdateEventControl = ApplyEventControlUpdate,
        SetFeatureFlag = ApplyFeatureFlag,
        SetWarningMaster = SetWarningMasterFromDashboard,
        TriggerTestEvent = kind => Dispatcher.Invoke(() => TriggerManualEvent(kind, false)),
        StartRuntime = () => Dispatcher.BeginInvoke(StartRuntime),
        StopRuntime = () => Dispatcher.BeginInvoke(StopRuntime)
    };

    private void Start_Click(object sender, RoutedEventArgs e) => StartRuntime();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRuntime();

    private void StartRuntime()
    {
        try
        {
            SaveUiToSettings();
            StopRuntime();
            _audio = new WarningAudioService();
            _buffer = new RollingBufferService();
            _buffer.Start(_settings);
            _ai = new AiCoordinator(_settings);
            _events = new EventCoordinator(_settings, _buffer, _audio, new SftpUploader());
            _events.EventUpdated += OnEventUpdated;
            _camera = new CameraService();
            _camera.FrameReady += OnFrameReady;
            _camera.Start(_settings.Camera.LiveRtspUrl);
            ModelStatusText.Text = "Models: " + _ai.ModelStatus;
            BufferStatusText.Text = "Buffer: " + _buffer.Status;
            _detectionsSeen = 0;
            _eventsTriggered = 0;
            _lastAiError = "";
            DetectionStatusText.Text = "Detections: 0 | Events: 0";
            FooterText.Text = "Runtime started";
        }
        catch (Exception ex)
        {
            StopRuntime();
            MessageBox.Show(ex.Message, "Start failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnFrameReady(Mat frame)
    {
        if (Interlocked.Exchange(ref _processing, 1) != 0)
        {
            frame.Dispose();
            return;
        }

        try
        {
            if (_camera is null || _ai is null || _events is null) return;
            IReadOnlyList<Detection> detections;
            try
            {
                detections = await Task.Run(() => _ai.Detect(frame));
                _lastAiError = "";
            }
            catch (Exception ex)
            {
                _lastAiError = ex.Message;
                detections = Array.Empty<Detection>();
            }

            if (detections.Count > 0) Interlocked.Add(ref _detectionsSeen, detections.Count);
            using var display = frame.Clone();
            foreach (var d in detections)
            {
                var color = d.Kind switch
                {
                    EventKind.Person => Scalar.LimeGreen,
                    EventKind.Vehicle => Scalar.DeepSkyBlue,
                    EventKind.LicensePlate => Scalar.Yellow,
                    EventKind.Garbage => Scalar.OrangeRed,
                    _ => Scalar.White
                };
                Cv2.Rectangle(display, d.Box, color, 2);
                Cv2.PutText(display, $"{d.Kind}: {d.Confidence:P0}",
                    new OpenCvSharp.Point(d.Box.X, Math.Max(20, d.Box.Y - 6)),
                    HersheyFonts.HersheySimplex, 0.55, color, 2);
            }

            var triggered = _ai.TriggeredDetections(detections);
            foreach (var t in triggered)
            {
                Interlocked.Increment(ref _eventsTriggered);
                _events.Trigger(t.Kind, t, frame);
            }

            BitmapSource bmp = display.ToBitmapSource();
            bmp.Freeze();
            await Dispatcher.InvokeAsync(() =>
            {
                LiveImage.Source = bmp;
                CameraStatusText.Text = "Camera: " + (_camera?.LastError ?? "unknown");
                BufferStatusText.Text = "Buffer: " + (_buffer?.Status ?? "stopped");
                DetectionStatusText.Text = $"Detections: {Interlocked.Read(ref _detectionsSeen)} | Events: {Interlocked.Read(ref _eventsTriggered)}" +
                    (string.IsNullOrWhiteSpace(_lastAiError) ? "" : $" | AI error: {_lastAiError}");
                ModelStatusText.Text = "Models: " + (_ai?.ModelStatus ?? "not loaded");
            });
        }
        catch (Exception ex)
        {
            _lastAiError = ex.Message;
            try
            {
                await Dispatcher.InvokeAsync(() =>
                    DetectionStatusText.Text = $"Detections: {Interlocked.Read(ref _detectionsSeen)} | Events: {Interlocked.Read(ref _eventsTriggered)} | Frame error: {ex.Message}");
            }
            catch { }
        }
        finally
        {
            frame.Dispose();
            Interlocked.Exchange(ref _processing, 0);
        }
    }

    private void TestPerson_Click(object sender, RoutedEventArgs e) => TriggerManualEvent(EventKind.Person, true);
    private void TestVehicle_Click(object sender, RoutedEventArgs e) => TriggerManualEvent(EventKind.Vehicle, true);
    private void TestLpd_Click(object sender, RoutedEventArgs e) => TriggerManualEvent(EventKind.LicensePlate, true);
    private void TestGarbage_Click(object sender, RoutedEventArgs e) => TriggerManualEvent(EventKind.Garbage, true);

    private bool TriggerManualEvent(EventKind kind, bool showMessages)
    {
        if (_camera is null || _events is null)
        {
            if (showMessages)
                MessageBox.Show("Start the runtime before testing an event.", "Runtime not started", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        using var frame = _camera.GetLatestFrame();
        if (frame is null || frame.Empty())
        {
            if (showMessages)
                MessageBox.Show("No live frame is available yet. Wait until Camera shows Connected, then try again.", "No live frame", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var box = new OpenCvSharp.Rect(2, 2, Math.Max(1, frame.Width - 4), Math.Max(1, frame.Height - 4));
        var detection = new Detection(kind, "manual-test", 1.0f, box);
        Interlocked.Increment(ref _eventsTriggered);
        _events.Trigger(kind, detection, frame);
        FooterText.Text = $"Manual {kind} event triggered. Image should appear immediately; video finishes after {_settings.Evidence.PostEventSeconds}s.";
        return true;
    }

    private void OnEventUpdated(EventRecord row)
    {
        lock (_eventHistoryLock)
        {
            _eventHistory.RemoveAll(x => x.EventId == row.EventId);
            _eventHistory.Insert(0, row);
            if (_eventHistory.Count > 500) _eventHistory.RemoveRange(500, _eventHistory.Count - 500);
        }

        Dispatcher.Invoke(() =>
        {
            var existing = _eventRows.FirstOrDefault(x => x.EventId == row.EventId);
            if (existing is not null) _eventRows.Remove(existing);
            _eventRows.Insert(0, row);
            while (_eventRows.Count > 500) _eventRows.RemoveAt(_eventRows.Count - 1);
            LatestEventText.Text = $"{row.Timestamp:yyyy-MM-dd HH:mm:ss} | {row.Kind} | {row.Confidence:P0} | {row.Status}\nImage: {row.ImagePath ?? "-"}\nVideo: {row.VideoPath ?? "-"}";
        });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();
            _settingsService.Save(_settings);
            FooterText.Text = "Settings saved. Restart runtime for model/camera changes; restart dashboard for bind/port changes.";
            RefreshCloudflareStatusUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenEvidence_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();
        string root = RollingBufferService.EvidenceRoot(_settings);
        Process.Start(new ProcessStartInfo("explorer.exe", root) { UseShellExecute = true });
    }

    private void LoadSettingsToUi()
    {
        LiveRtspText.Text = _settings.Camera.LiveRtspUrl;
        EvidenceRtspText.Text = _settings.Camera.EvidenceRtspUrl;
        BufferSecondsText.Text = _settings.Evidence.RollingBufferSeconds.ToString();
        SegmentSecondsText.Text = _settings.Evidence.BufferSegmentSeconds.ToString();
        PreSecondsText.Text = _settings.Evidence.PreEventSeconds.ToString();
        PostSecondsText.Text = _settings.Evidence.PostEventSeconds.ToString();
        EvidenceRootText.Text = _settings.Evidence.RootFolder;
        WarningMasterCheck.IsChecked = _settings.Warning.MasterEnabled;
        PvModelText.Text = _settings.Ai.PersonVehicle.ModelPath;
        PvLabelsText.Text = _settings.Ai.PersonVehicle.LabelsPath;
        PlateModelText.Text = _settings.Ai.LicensePlate.ModelPath;
        PlateLabelsText.Text = _settings.Ai.LicensePlate.LabelsPath;
        GarbageModelText.Text = _settings.Ai.Garbage.ModelPath;
        GarbageLabelsText.Text = _settings.Ai.Garbage.LabelsPath;
        UploadEnabledCheck.IsChecked = _settings.Evidence.UploadEnabled;
        SftpHostText.Text = _settings.Sftp.Host;
        SftpPortText.Text = _settings.Sftp.Port.ToString();
        SftpUserText.Text = _settings.Sftp.Username;
        SftpPasswordText.Password = _settings.Sftp.Password;
        SftpBaseText.Text = _settings.Sftp.BaseDirectory;

        DashboardEnabledCheck.IsChecked = _settings.Cloudflare.DashboardEnabled;
        DashboardBindText.Text = _settings.Cloudflare.BindAddress;
        DashboardPortText.Text = _settings.Cloudflare.DashboardPort.ToString();
        DashboardApiKeyText.Text = _settings.Cloudflare.ControlApiKey;
        TunnelEnabledCheck.IsChecked = _settings.Cloudflare.TunnelEnabled;
        TunnelAutoStartCheck.IsChecked = _settings.Cloudflare.TunnelAutoStart;
        PublicBaseUrlText.Text = _settings.Cloudflare.PublicBaseUrl;
        TunnelProtocolText.Text = _settings.Cloudflare.Protocol;
        TunnelMetricsPortText.Text = _settings.Cloudflare.MetricsPort.ToString();
        TunnelTokenPassword.Password = "";
        TunnelTokenStatusText.Text = CloudflareSecretStore.HasToken
            ? $"Tunnel token saved in {CloudflareSecretStore.TokenPath}. Leave token box blank to keep it."
            : "No tunnel token saved. Paste the token or the complete Cloudflare 'service install ...' command.";
    }

    private void SaveUiToSettings()
    {
        lock (_settingsLock)
        {
            _settings.Camera.LiveRtspUrl = LiveRtspText.Text.Trim();
            _settings.Camera.EvidenceRtspUrl = EvidenceRtspText.Text.Trim();
            if (string.IsNullOrWhiteSpace(_settings.Camera.EvidenceRtspUrl) ||
                _settings.Camera.EvidenceRtspUrl.Contains("camera-ip", StringComparison.OrdinalIgnoreCase))
            {
                _settings.Camera.EvidenceRtspUrl = _settings.Camera.LiveRtspUrl;
                EvidenceRtspText.Text = _settings.Camera.EvidenceRtspUrl;
            }

            _settings.Evidence.RollingBufferSeconds = ParseInt(BufferSecondsText.Text, 30, 10, 300);
            _settings.Evidence.BufferSegmentSeconds = ParseInt(SegmentSecondsText.Text, 2, 1, 10);
            _settings.Evidence.PreEventSeconds = ParseInt(PreSecondsText.Text, 10, 1, _settings.Evidence.RollingBufferSeconds);
            _settings.Evidence.PostEventSeconds = ParseInt(PostSecondsText.Text, 20, 1, 300);
            _settings.Evidence.RootFolder = EvidenceRootText.Text.Trim();
            _settings.Warning.MasterEnabled = WarningMasterCheck.IsChecked == true;
            _settings.Ai.PersonVehicle.ModelPath = PvModelText.Text.Trim();
            _settings.Ai.PersonVehicle.LabelsPath = PvLabelsText.Text.Trim();
            _settings.Ai.LicensePlate.ModelPath = PlateModelText.Text.Trim();
            _settings.Ai.LicensePlate.LabelsPath = PlateLabelsText.Text.Trim();
            _settings.Ai.Garbage.ModelPath = GarbageModelText.Text.Trim();
            _settings.Ai.Garbage.LabelsPath = GarbageLabelsText.Text.Trim();
            _settings.Evidence.UploadEnabled = UploadEnabledCheck.IsChecked == true;
            _settings.Sftp.Host = SftpHostText.Text.Trim();
            _settings.Sftp.Port = ParseInt(SftpPortText.Text, 22, 1, 65535);
            _settings.Sftp.Username = SftpUserText.Text.Trim();
            _settings.Sftp.Password = SftpPasswordText.Password;
            _settings.Sftp.BaseDirectory = SftpBaseText.Text.Trim();

            _settings.Cloudflare.DashboardEnabled = DashboardEnabledCheck.IsChecked == true;
            _settings.Cloudflare.BindAddress = NormalizeDashboardBind(DashboardBindText.Text);
            _settings.Cloudflare.DashboardPort = ParseInt(DashboardPortText.Text, 8080, 1024, 65535);
            _settings.Cloudflare.ControlApiKey = string.IsNullOrWhiteSpace(DashboardApiKeyText.Text)
                ? Guid.NewGuid().ToString("N")
                : DashboardApiKeyText.Text.Trim();
            DashboardApiKeyText.Text = _settings.Cloudflare.ControlApiKey;
            _settings.Cloudflare.TunnelEnabled = TunnelEnabledCheck.IsChecked == true;
            _settings.Cloudflare.TunnelAutoStart = TunnelAutoStartCheck.IsChecked == true;
            _settings.Cloudflare.PublicBaseUrl = PublicBaseUrlText.Text.Trim().TrimEnd('/');
            _settings.Cloudflare.Protocol = CloudflareTunnelService.NormalizeProtocol(TunnelProtocolText.Text);
            _settings.Cloudflare.MetricsPort = ParseInt(TunnelMetricsPortText.Text, 20241, 1024, 65535);

            if (!string.IsNullOrWhiteSpace(TunnelTokenPassword.Password))
            {
                CloudflareSecretStore.SaveToken(TunnelTokenPassword.Password);
                TunnelTokenPassword.Password = "";
                TunnelTokenStatusText.Text = $"Tunnel token saved in {CloudflareSecretStore.TokenPath}.";
            }
        }
    }

    private static int ParseInt(string text, int fallback, int min, int max)
        => int.TryParse(text, out int v) ? Math.Clamp(v, min, max) : fallback;

    private static string NormalizeDashboardBind(string? value)
    {
        value = (value ?? "0.0.0.0").Trim();
        return value is "0.0.0.0" or "127.0.0.1" or "localhost" ? value : "0.0.0.0";
    }

    private void StopRuntime()
    {
        try
        {
            if (_camera is not null)
            {
                _camera.FrameReady -= OnFrameReady;
                _camera.Stop();
                _camera.DisposeAsync().AsTask().Wait(500);
            }
        }
        catch { }
        _camera = null;
        if (_events is not null) _events.EventUpdated -= OnEventUpdated;
        _events = null;
        _ai?.Dispose();
        _ai = null;
        _buffer?.Dispose();
        _buffer = null;
        _audio?.Dispose();
        _audio = null;
        CameraStatusText.Text = "Camera: stopped";
        BufferStatusText.Text = "Buffer: stopped";
        DetectionStatusText.Text = $"Detections: {Interlocked.Read(ref _detectionsSeen)} | Events: {Interlocked.Read(ref _eventsTriggered)}";
        FooterText.Text = "Stopped";
    }

    // ---------------- Cloudflare / Web Dashboard ----------------

    private void TryStartDashboardOnLaunch()
    {
        if (_dashboard is null || !_settings.Cloudflare.DashboardEnabled) return;
        try { _dashboard.Start(_settings.Cloudflare); }
        catch (Exception ex) { FooterText.Text = "Dashboard start failed: " + ex.Message; }
    }

    private void TryAutoStartTunnel()
    {
        if (_cloudflareTunnel is null || !_settings.Cloudflare.TunnelEnabled || !_settings.Cloudflare.TunnelAutoStart) return;
        try
        {
            if (!_settings.Cloudflare.DashboardEnabled)
            {
                _settings.Cloudflare.DashboardEnabled = true;
                DashboardEnabledCheck.IsChecked = true;
                _settingsService.Save(_settings);
            }
            if (_dashboard is { IsRunning: false }) _dashboard.Start(_settings.Cloudflare);
            _cloudflareTunnel.Start(_settings.Cloudflare);
        }
        catch (Exception ex) { FooterText.Text = "Cloudflare auto-start failed: " + ex.Message; }
    }

    private void StartDashboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();
            _settingsService.Save(_settings);
            if (_dashboard is null) _dashboard = new DashboardService(CreateDashboardBridge());
            if (_settings.Cloudflare.DashboardEnabled) _dashboard.Start(_settings.Cloudflare);
            else _dashboard.Stop();
            FooterText.Text = _settings.Cloudflare.DashboardEnabled ? "Web dashboard started." : "Web dashboard disabled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Dashboard start failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        RefreshCloudflareStatusUi();
    }

    private void OpenLocalDashboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();
            if (_dashboard is null) _dashboard = new DashboardService(CreateDashboardBridge());
            if (!_dashboard.IsRunning)
            {
                _settings.Cloudflare.DashboardEnabled = true;
                DashboardEnabledCheck.IsChecked = true;
                _dashboard.Start(_settings.Cloudflare);
                _settingsService.Save(_settings);
            }
            string url = $"http://127.0.0.1:{_settings.Cloudflare.DashboardPort}/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open dashboard failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartTunnel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();
            _settings.Cloudflare.TunnelEnabled = true;
            TunnelEnabledCheck.IsChecked = true;
            _settingsService.Save(_settings);
            if (_dashboard is null) _dashboard = new DashboardService(CreateDashboardBridge());
            if (!_dashboard.IsRunning)
            {
                _settings.Cloudflare.DashboardEnabled = true;
                DashboardEnabledCheck.IsChecked = true;
                _dashboard.Start(_settings.Cloudflare);
                _settingsService.Save(_settings);
            }
            _cloudflareTunnel ??= new CloudflareTunnelService();
            _cloudflareTunnel.Start(_settings.Cloudflare);
            FooterText.Text = "Cloudflare connector starting.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cloudflare start failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        RefreshCloudflareStatusUi();
    }

    private void StopTunnel_Click(object sender, RoutedEventArgs e)
    {
        _cloudflareTunnel?.Stop();
        FooterText.Text = "Cloudflare tunnel stopped.";
        RefreshCloudflareStatusUi();
    }

    private void OpenPublicDashboard_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();
        if (string.IsNullOrWhiteSpace(_settings.Cloudflare.PublicBaseUrl))
        {
            MessageBox.Show("Set Public Base URL first, for example https://rtgcamx.example.com", "Public URL missing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(_settings.Cloudflare.PublicBaseUrl) { UseShellExecute = true });
    }

    private void ForgetTunnelToken_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Delete the locally saved Cloudflare tunnel token?", "Forget tunnel token", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _cloudflareTunnel?.Stop();
        CloudflareSecretStore.DeleteToken();
        TunnelTokenPassword.Password = "";
        RefreshCloudflareStatusUi();
    }

    private void RefreshCloudflareStatusUi()
    {
        string dashboardStatus = _dashboard?.Status ?? "Stopped";
        string tunnelStatus = _cloudflareTunnel?.Status ?? "Stopped";
        string localUrl = $"http://127.0.0.1:{_settings.Cloudflare.DashboardPort}";
        string publicUrl = string.IsNullOrWhiteSpace(_settings.Cloudflare.PublicBaseUrl) ? "not configured" : _settings.Cloudflare.PublicBaseUrl;
        string text = $"Dashboard: {dashboardStatus} | Tunnel: {tunnelStatus}\nLocal: {localUrl} | Public: {publicUrl}";
        CloudflareStatusText.Text = text;
        CloudflareRuntimeStatusText.Text = $"Dashboard: {dashboardStatus} | Tunnel: {tunnelStatus}";
        TunnelTokenStatusText.Text = CloudflareSecretStore.HasToken
            ? $"Tunnel token saved securely under your Windows profile: {CloudflareSecretStore.TokenPath}. Leave token box blank to keep it."
            : "No tunnel token saved. Paste the Add-a-replica token or complete service-install command.";
    }

    // ---------------- Dashboard bridge ----------------

    private DashboardState GetDashboardState()
    {
        EventRecord? latest = GetEventHistorySnapshot().FirstOrDefault();
        return new DashboardState(
            DateTimeOffset.Now,
            _camera?.LastError ?? "Stopped",
            _buffer?.Status ?? "Stopped",
            _ai?.ModelStatus ?? "Not loaded",
            string.IsNullOrWhiteSpace(_lastAiError) ? "OK" : _lastAiError,
            Interlocked.Read(ref _detectionsSeen),
            Interlocked.Read(ref _eventsTriggered),
            _camera?.IsRunning == true ? "Running" : "Stopped",
            _dashboard?.Status ?? "Stopped",
            _cloudflareTunnel?.Status ?? "Stopped",
            $"http://127.0.0.1:{_settings.Cloudflare.DashboardPort}",
            _settings.Cloudflare.PublicBaseUrl,
            latest);
    }

    private DashboardControlsSnapshot GetDashboardControls()
    {
        lock (_settingsLock)
        {
            return new DashboardControlsSnapshot(
                _settings.Warning.MasterEnabled,
                new[]
                {
                    Snapshot("Person", _settings.Ai.Person),
                    Snapshot("Vehicle", _settings.Ai.Vehicle),
                    Snapshot("LicensePlate", _settings.Ai.LicensePlateEvent),
                    Snapshot("Garbage", _settings.Ai.GarbageEvent)
                },
                _settings.Evidence.RollingBufferSeconds,
                _settings.Evidence.PreEventSeconds,
                _settings.Evidence.PostEventSeconds,
                _settings.Evidence.UploadEnabled);
        }
    }

    private static EventControlSnapshot Snapshot(string name, EventTypeSettings s) => new(
        name, s.DetectionEnabled, s.DetectionDelaySeconds, s.CooldownSeconds,
        s.WarningAudioEnabled, s.ImageCaptureEnabled, s.VideoCaptureEnabled);

    private IReadOnlyList<EventRecord> GetEventHistorySnapshot()
    {
        lock (_eventHistoryLock) return _eventHistory.ToArray();
    }

    private byte[]? GetDashboardSnapshotJpeg()
    {
        using var frame = _camera?.GetLatestFrame();
        if (frame is null || frame.Empty()) return null;
        using var output = new Mat();
        if (frame.Width > 800)
            Cv2.Resize(frame, output, new OpenCvSharp.Size(800, Math.Max(1, frame.Height * 800 / frame.Width)));
        else
            frame.CopyTo(output);
        return Cv2.ImEncode(".jpg", output, out byte[] jpeg, new[] { (int)ImwriteFlags.JpegQuality, 55 }) ? jpeg : null;
    }

    private void ApplyEventControlUpdate(EventControlUpdate update)
    {
        if (!DashboardService.TryParseEventKind(update.Event, out EventKind kind)) throw new ArgumentException("Unknown event type.");
        lock (_settingsLock)
        {
            EventTypeSettings cfg = GetEventSettings(kind);
            if (update.DetectionEnabled.HasValue) cfg.DetectionEnabled = update.DetectionEnabled.Value;
            if (update.DetectionDelaySeconds.HasValue) cfg.DetectionDelaySeconds = Math.Clamp(update.DetectionDelaySeconds.Value, 0, 300);
            if (update.CooldownSeconds.HasValue) cfg.CooldownSeconds = Math.Clamp(update.CooldownSeconds.Value, 0, 3600);
            if (update.WarningAudioEnabled.HasValue) cfg.WarningAudioEnabled = update.WarningAudioEnabled.Value;
            if (update.ImageCaptureEnabled.HasValue) cfg.ImageCaptureEnabled = update.ImageCaptureEnabled.Value;
            if (update.VideoCaptureEnabled.HasValue) cfg.VideoCaptureEnabled = update.VideoCaptureEnabled.Value;
            _settingsService.Save(_settings);
        }
        Dispatcher.BeginInvoke(() => EventSettingsGrid.Items.Refresh());
    }

    private void ApplyFeatureFlag(string flag, bool enabled)
    {
        string f = (flag ?? "").Trim().ToLowerInvariant();
        lock (_settingsLock)
        {
            switch (f)
            {
                case "person_detection_enabled": _settings.Ai.Person.DetectionEnabled = enabled; break;
                case "vehicle_detection_enabled": _settings.Ai.Vehicle.DetectionEnabled = enabled; break;
                case "lpd_detection_enabled":
                case "license_plate_detection_enabled": _settings.Ai.LicensePlateEvent.DetectionEnabled = enabled; break;
                case "garbage_detection_enabled": _settings.Ai.GarbageEvent.DetectionEnabled = enabled; break;

                case "person_video_recording_enabled": _settings.Ai.Person.VideoCaptureEnabled = enabled; break;
                case "vehicle_video_recording_enabled": _settings.Ai.Vehicle.VideoCaptureEnabled = enabled; break;
                case "lpd_video_recording_enabled":
                case "license_plate_video_recording_enabled": _settings.Ai.LicensePlateEvent.VideoCaptureEnabled = enabled; break;
                case "garbage_video_recording_enabled": _settings.Ai.GarbageEvent.VideoCaptureEnabled = enabled; break;

                case "person_image_capture_enabled": _settings.Ai.Person.ImageCaptureEnabled = enabled; break;
                case "vehicle_image_capture_enabled": _settings.Ai.Vehicle.ImageCaptureEnabled = enabled; break;
                case "lpd_image_capture_enabled":
                case "license_plate_image_capture_enabled": _settings.Ai.LicensePlateEvent.ImageCaptureEnabled = enabled; break;
                case "garbage_image_capture_enabled": _settings.Ai.GarbageEvent.ImageCaptureEnabled = enabled; break;

                case "person_warning_audio_enabled": _settings.Ai.Person.WarningAudioEnabled = enabled; break;
                case "vehicle_warning_audio_enabled": _settings.Ai.Vehicle.WarningAudioEnabled = enabled; break;
                case "lpd_warning_audio_enabled":
                case "license_plate_warning_audio_enabled": _settings.Ai.LicensePlateEvent.WarningAudioEnabled = enabled; break;
                case "garbage_warning_audio_enabled": _settings.Ai.GarbageEvent.WarningAudioEnabled = enabled; break;
                case "warning_audio_enabled": _settings.Warning.MasterEnabled = enabled; break;
                default: throw new ArgumentException("Unknown feature flag: " + flag);
            }
            _settingsService.Save(_settings);
        }
        Dispatcher.BeginInvoke(() =>
        {
            WarningMasterCheck.IsChecked = _settings.Warning.MasterEnabled;
            EventSettingsGrid.Items.Refresh();
        });
    }

    private void SetWarningMasterFromDashboard(bool enabled)
    {
        lock (_settingsLock)
        {
            _settings.Warning.MasterEnabled = enabled;
            _settingsService.Save(_settings);
        }
        Dispatcher.BeginInvoke(() => WarningMasterCheck.IsChecked = enabled);
    }

    private EventTypeSettings GetEventSettings(EventKind kind) => kind switch
    {
        EventKind.Person => _settings.Ai.Person,
        EventKind.Vehicle => _settings.Ai.Vehicle,
        EventKind.LicensePlate => _settings.Ai.LicensePlateEvent,
        EventKind.Garbage => _settings.Ai.GarbageEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _statusTimer.Stop();
        try
        {
            SaveUiToSettings();
            _settingsService.Save(_settings);
        }
        catch { }
        StopRuntime();
        try { _cloudflareTunnel?.Dispose(); } catch { }
        try { _dashboard?.Dispose(); } catch { }
    }
}
