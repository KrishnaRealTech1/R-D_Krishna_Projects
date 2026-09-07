using System.IO;
using GCam.Windows.Infrastructure;
using GCam.Windows.Models;
using NAudio.Wave;

namespace GCam.Windows.Services;

public sealed class WarningAudioService : IDisposable
{
    private readonly object _lock = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;

    public void Play(EventKind kind, AppSettings settings)
    {
        if (!settings.Warning.MasterEnabled) return;
        string path = kind switch
        {
            EventKind.Person => settings.Warning.PersonWav,
            EventKind.Vehicle => settings.Warning.VehicleWav,
            EventKind.LicensePlate => settings.Warning.LicensePlateWav,
            EventKind.Garbage => settings.Warning.GarbageWav,
            _ => ""
        };
        path = PathHelper.ResolveAppPath(path);
        if (!File.Exists(path)) return;

        lock (_lock)
        {
            try
            {
                _output?.Stop();
                _output?.Dispose();
                _reader?.Dispose();
                _reader = new AudioFileReader(path);
                _output = new WaveOutEvent();
                _output.Init(_reader);
                _output.Play();
            }
            catch { }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _output?.Stop(); _output?.Dispose(); _reader?.Dispose();
            _output = null; _reader = null;
        }
    }
}
