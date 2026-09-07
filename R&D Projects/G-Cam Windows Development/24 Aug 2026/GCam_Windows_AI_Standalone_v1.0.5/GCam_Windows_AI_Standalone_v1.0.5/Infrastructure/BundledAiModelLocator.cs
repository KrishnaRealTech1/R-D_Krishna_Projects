using System.IO;
using System.Reflection;

namespace GCam.Windows.Infrastructure;

public static class BundledAiModelLocator
{
    private const string ProtoResource = "GCam.Windows.Resources.MobileNetSSD_deploy.prototxt";
    private const string ModelResource = "GCam.Windows.Resources.MobileNetSSD_deploy.caffemodel";

    public static (string? Prototxt, string? Model, string Status) ResolveMobileNetSsd()
    {
        // Prefer normal deployment files so field technicians can replace them without rebuilding.
        string externalProto = Path.Combine(AppContext.BaseDirectory, "models", "MobileNetSSD_deploy.prototxt");
        string externalModel = Path.Combine(AppContext.BaseDirectory, "models", "MobileNetSSD_deploy.caffemodel");
        if (File.Exists(externalProto) && File.Exists(externalModel))
            return (externalProto, externalModel, "external files");

        // Single-EXE deployments extract the model once into LocalAppData.
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GCam", "models");
        Directory.CreateDirectory(root);
        string proto = Path.Combine(root, "MobileNetSSD_deploy.prototxt");
        string model = Path.Combine(root, "MobileNetSSD_deploy.caffemodel");

        bool protoOk = EnsureEmbeddedFile(ProtoResource, proto);
        bool modelOk = EnsureEmbeddedFile(ModelResource, model);
        if (protoOk && modelOk && File.Exists(proto) && File.Exists(model))
            return (proto, model, "embedded fallback");

        return (null, null,
            "MobileNetSSD fallback missing. Rebuild with build-win-x64.ps1 so the AI model is downloaded and embedded.");
    }

    private static bool EnsureEmbeddedFile(string resourceName, string destination)
    {
        try
        {
            if (File.Exists(destination) && new FileInfo(destination).Length > 0) return true;
            using Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (source is null) return false;
            string tmp = destination + ".tmp";
            using (var output = File.Create(tmp)) source.CopyTo(output);
            File.Move(tmp, destination, true);
            return File.Exists(destination) && new FileInfo(destination).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
