using System.IO;
using GCam.Windows.Infrastructure;
using GCam.Windows.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace GCam.Windows.Services;

public sealed class YoloOnnxDetector : IDisposable
{
    private readonly DetectorSettings _settings;
    private readonly Func<string, EventKind?> _labelMapper;
    private readonly string[] _labels;
    private InferenceSession? _session;
    private string? _inputName;

    public string Status { get; private set; } = "Not loaded";
    public bool IsLoaded => _session is not null;

    public YoloOnnxDetector(DetectorSettings settings, Func<string, EventKind?> labelMapper)
    {
        _settings = settings;
        _labelMapper = labelMapper;
        var labelsPath = PathHelper.ResolveAppPath(settings.LabelsPath);
        _labels = File.Exists(labelsPath)
            ? File.ReadAllLines(labelsPath).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray()
            : Array.Empty<string>();
        TryLoad();
    }

    private void TryLoad()
    {
        if (!_settings.Enabled) { Status = "Disabled"; return; }
        var modelPath = PathHelper.ResolveAppPath(_settings.ModelPath);
        if (!File.Exists(modelPath)) { Status = $"Missing model: {modelPath}"; return; }
        try
        {
            var options = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                EnableMemoryPattern = false
            };
            if (_settings.UseDirectMl)
            {
                try { options.AppendExecutionProvider_DML(0); }
                catch { /* CPU fallback is automatic */ }
            }
            _session = new InferenceSession(modelPath, options);
            _inputName = _session.InputMetadata.Keys.First();
            Status = "Loaded";
        }
        catch (Exception ex) { Status = "Model error: " + ex.Message; }
    }

    public IReadOnlyList<Detection> Detect(Mat bgr)
    {
        if (_session is null || _inputName is null || bgr.Empty()) return Array.Empty<Detection>();
        try
        {
            int size = Math.Max(320, _settings.InputSize);
            float scale = Math.Min((float)size / bgr.Width, (float)size / bgr.Height);
            int newW = (int)Math.Round(bgr.Width * scale);
            int newH = (int)Math.Round(bgr.Height * scale);
            int padX = (size - newW) / 2;
            int padY = (size - newH) / 2;

            using var resized = new Mat();
            Cv2.Resize(bgr, resized, new Size(newW, newH));
            using var letterbox = new Mat(new Size(size, size), MatType.CV_8UC3, Scalar.All(114));
            resized.CopyTo(new Mat(letterbox, new Rect(padX, padY, newW, newH)));
            Cv2.CvtColor(letterbox, letterbox, ColorConversionCodes.BGR2RGB);

            var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = letterbox.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = p.Item0 / 255f;
                tensor[0, 1, y, x] = p.Item1 / 255f;
                tensor[0, 2, y, x] = p.Item2 / 255f;
            }

            using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
            var output = results.First().AsTensor<float>();
            return ParseOutput(output, bgr.Size(), scale, padX, padY);
        }
        catch (Exception ex)
        {
            Status = "Inference error: " + ex.Message;
            return Array.Empty<Detection>();
        }
    }

    private IReadOnlyList<Detection> ParseOutput(Tensor<float> output, Size original, float scale, int padX, int padY)
    {
        var dims = output.Dimensions.ToArray();
        var candidates = new List<(EventKind kind, string label, float conf, Rect box)>();

        // Common end-to-end format: [1,N,6] => x1,y1,x2,y2,score,classId.
        if (dims.Length == 3 && dims[2] == 6)
        {
            for (int i = 0; i < dims[1]; i++)
            {
                float conf = output[0, i, 4];
                if (conf < _settings.Confidence) continue;
                int cls = (int)output[0, i, 5];
                AddCandidate(candidates, cls, conf, output[0, i, 0], output[0, i, 1], output[0, i, 2], output[0, i, 3], original, scale, padX, padY, xyxy: true);
            }
            return ApplyNms(candidates);
        }

        // YOLOv8/v11 export is usually [1,4+C,N] or [1,N,4+C].
        if (dims.Length != 3) return Array.Empty<Detection>();
        bool channelsFirst = dims[1] < dims[2];
        int features = channelsFirst ? dims[1] : dims[2];
        int count = channelsFirst ? dims[2] : dims[1];
        int classes = Math.Max(0, features - 4);

        for (int i = 0; i < count; i++)
        {
            float cx = Get(output, channelsFirst, 0, i);
            float cy = Get(output, channelsFirst, 1, i);
            float w = Get(output, channelsFirst, 2, i);
            float h = Get(output, channelsFirst, 3, i);
            int bestClass = -1;
            float best = 0f;
            for (int c = 0; c < classes; c++)
            {
                float score = Get(output, channelsFirst, 4 + c, i);
                if (score > best) { best = score; bestClass = c; }
            }
            if (bestClass < 0 || best < _settings.Confidence) continue;
            AddCandidate(candidates, bestClass, best, cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f, original, scale, padX, padY, xyxy: true);
        }
        return ApplyNms(candidates);
    }

    private static float Get(Tensor<float> t, bool channelsFirst, int feature, int index)
        => channelsFirst ? t[0, feature, index] : t[0, index, feature];

    private void AddCandidate(List<(EventKind kind, string label, float conf, Rect box)> list, int classId, float conf,
        float x1, float y1, float x2, float y2, Size original, float scale, int padX, int padY, bool xyxy)
    {
        string label = classId >= 0 && classId < _labels.Length ? _labels[classId] : classId.ToString();
        var kind = _labelMapper(label);
        if (kind is null) return;
        int left = (int)Math.Round((x1 - padX) / scale);
        int top = (int)Math.Round((y1 - padY) / scale);
        int right = (int)Math.Round((x2 - padX) / scale);
        int bottom = (int)Math.Round((y2 - padY) / scale);
        left = Math.Clamp(left, 0, Math.Max(0, original.Width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, original.Height - 1));
        right = Math.Clamp(right, left + 1, original.Width);
        bottom = Math.Clamp(bottom, top + 1, original.Height);
        list.Add((kind.Value, label, conf, new Rect(left, top, right - left, bottom - top)));
    }

    private IReadOnlyList<Detection> ApplyNms(List<(EventKind kind, string label, float conf, Rect box)> input)
    {
        var output = new List<Detection>();
        foreach (var group in input.GroupBy(x => x.kind))
        {
            var boxes = group.Select(x => x.box).ToArray();
            var scores = group.Select(x => x.conf).ToArray();
            CvDnn.NMSBoxes(boxes, scores, _settings.Confidence, _settings.NmsThreshold, out int[] indices);
            var arr = group.ToArray();
            output.AddRange(indices.Select(i => new Detection(arr[i].kind, arr[i].label, arr[i].conf, arr[i].box)));
        }
        return output;
    }

    public void Dispose() => _session?.Dispose();
}
