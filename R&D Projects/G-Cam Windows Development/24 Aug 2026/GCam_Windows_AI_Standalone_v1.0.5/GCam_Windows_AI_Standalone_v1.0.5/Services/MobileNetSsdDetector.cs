using GCam.Windows.Infrastructure;
using GCam.Windows.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace GCam.Windows.Services;

/// <summary>
/// CPU-friendly person/vehicle fallback using the MobileNet-SSD model already used by the
/// original G-Cam Linux implementation. This makes Person/Vehicle event capture work without
/// requiring a separate YOLO ONNX file.
/// </summary>
public sealed class MobileNetSsdDetector : IDisposable
{
    private static readonly string[] Classes =
    {
        "background", "aeroplane", "bicycle", "bird", "boat", "bottle", "bus", "car", "cat",
        "chair", "cow", "diningtable", "dog", "horse", "motorbike", "person", "pottedplant",
        "sheep", "sofa", "train", "tvmonitor"
    };

    private readonly float _confidence;
    private Net? _net;
    public string Status { get; private set; } = "Not loaded";
    public bool IsLoaded => _net is not null;

    public MobileNetSsdDetector(float confidence)
    {
        _confidence = Math.Clamp(confidence, 0.05f, 0.99f);
        TryLoad();
    }

    private void TryLoad()
    {
        try
        {
            var resolved = BundledAiModelLocator.ResolveMobileNetSsd();
            if (resolved.Prototxt is null || resolved.Model is null)
            {
                Status = resolved.Status;
                return;
            }

            _net = CvDnn.ReadNetFromCaffe(resolved.Prototxt, resolved.Model);
            Status = $"Loaded MobileNetSSD ({resolved.Status})";
        }
        catch (Exception ex)
        {
            _net?.Dispose();
            _net = null;
            Status = "MobileNetSSD error: " + ex.Message;
        }
    }

    public IReadOnlyList<Detection> Detect(Mat frame)
    {
        if (_net is null || frame.Empty()) return Array.Empty<Detection>();
        try
        {
            using var blob = CvDnn.BlobFromImage(
                frame,
                0.007843,
                new Size(300, 300),
                new Scalar(127.5, 127.5, 127.5),
                swapRB: false,
                crop: false);

            _net.SetInput(blob);
            using var output = _net.Forward();
            long total = output.Total();
            if (total < 7 || total % 7 != 0) return Array.Empty<Detection>();

            int rows = checked((int)(total / 7));
            using var detections = output.Reshape(1, rows);
            var result = new List<Detection>();

            for (int i = 0; i < rows; i++)
            {
                int classId = (int)detections.At<float>(i, 1);
                float confidence = detections.At<float>(i, 2);
                if (confidence < _confidence || classId < 0 || classId >= Classes.Length) continue;

                string label = Classes[classId];
                EventKind? kind = label switch
                {
                    "person" => EventKind.Person,
                    "car" or "bus" or "motorbike" or "bicycle" => EventKind.Vehicle,
                    _ => null
                };
                if (kind is null) continue;

                int left = (int)Math.Round(detections.At<float>(i, 3) * frame.Width);
                int top = (int)Math.Round(detections.At<float>(i, 4) * frame.Height);
                int right = (int)Math.Round(detections.At<float>(i, 5) * frame.Width);
                int bottom = (int)Math.Round(detections.At<float>(i, 6) * frame.Height);
                left = Math.Clamp(left, 0, Math.Max(0, frame.Width - 1));
                top = Math.Clamp(top, 0, Math.Max(0, frame.Height - 1));
                right = Math.Clamp(right, left + 1, frame.Width);
                bottom = Math.Clamp(bottom, top + 1, frame.Height);

                result.Add(new Detection(kind.Value, label, confidence,
                    new Rect(left, top, right - left, bottom - top)));
            }

            return result;
        }
        catch (Exception ex)
        {
            Status = "MobileNetSSD inference error: " + ex.Message;
            return Array.Empty<Detection>();
        }
    }

    public void Dispose() => _net?.Dispose();
}
