using OpenCvSharp;

namespace GCam.Windows.Services;

public sealed class CameraService : IAsyncDisposable
{
    private readonly object _frameLock = new();
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Mat? _latest;
    private string _lastError = "Not started";

    public event Action<Mat>? FrameReady;
    public string LastError => _lastError;
    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start(string rtspUrl)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => Loop(rtspUrl, _cts.Token));
    }

    private async Task Loop(string rtspUrl, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _capture?.Dispose();
                _capture = new VideoCapture(rtspUrl, VideoCaptureAPIs.FFMPEG);
                _capture.Set(VideoCaptureProperties.BufferSize, 1);
                if (!_capture.IsOpened()) throw new InvalidOperationException("Unable to open RTSP stream.");
                _lastError = "Connected";

                using var frame = new Mat();
                while (!ct.IsCancellationRequested)
                {
                    if (!_capture.Read(frame) || frame.Empty())
                    {
                        _lastError = "RTSP read failed; reconnecting.";
                        break;
                    }

                    lock (_frameLock)
                    {
                        _latest?.Dispose();
                        _latest = frame.Clone();
                    }
                    FrameReady?.Invoke(frame.Clone());
                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                await Task.Delay(1500, ct).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    public Mat? GetLatestFrame()
    {
        lock (_frameLock) return _latest?.Clone();
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _loop?.Wait(1200); } catch { }
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
        _loop = null;
        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        lock (_frameLock) { _latest?.Dispose(); _latest = null; }
        await Task.CompletedTask;
    }
}
