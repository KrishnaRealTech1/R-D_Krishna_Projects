using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Applies the RealTech identity to the Windows shell and every WPF window.
/// WPF's managed Icon property is retained, while WM_SETICON and the native
/// window-class icon are also set so self-contained single-file builds do not
/// fall back to the generic WPF window icon in the Windows taskbar.
/// </summary>
public static class WindowsBrandingService
{
    private const string AppUserModelId = "RealTech.iAWS.AutomaticWeighingSystem";
    private const string IconResourceUri =
        "pack://application:,,,/Assets/RealTechiAWS.ico";

    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int IconSmall2 = 2;
    private const int GclpHicon = -14;
    private const int GclpHiconSm = -34;
    private const int SmCxIcon = 11;
    private const int SmCyIcon = 12;
    private const int SmCxSmIcon = 49;
    private const int SmCySmIcon = 50;
    private const uint BiRgb = 0;
    private const uint DibRgbColors = 0;

    private static readonly Lazy<IReadOnlyList<BitmapFrame>> IconFrames =
        new(LoadIconFrames);

    private static readonly object NativeIconLock = new();
    private static IntPtr _smallNativeIcon;
    private static IntPtr _largeNativeIcon;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            }
            catch
            {
                // Branding must never prevent the application from starting.
            }
        }

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    public static void ApplyTo(Window window)
    {
        var managedIcon = GetLargestIconFrame();
        if (managedIcon is not null)
        {
            window.Icon = managedIcon;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        window.ContentRendered -= OnWindowContentRendered;
        window.ContentRendered += OnWindowContentRendered;

        if (PresentationSource.FromVisual(window) is null)
        {
            window.SourceInitialized += OnWindowSourceInitialized;
            return;
        }

        ApplyNativeIcon(window);
        QueueNativeIconRefresh(window);
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        ApplyNativeIcon(window);
        QueueNativeIconRefresh(window);
    }

    private static void OnWindowContentRendered(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.ContentRendered -= OnWindowContentRendered;
        ApplyNativeIcon(window);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            ApplyTo(window);
        }
    }

    private static void QueueNativeIconRefresh(Window window)
    {
        _ = window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => ApplyNativeIcon(window)));
    }

    private static void ApplyNativeIcon(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            EnsureNativeIcons();

            if (_smallNativeIcon != IntPtr.Zero)
            {
                _ = SendMessage(
                    handle,
                    WmSetIcon,
                    new IntPtr(IconSmall),
                    _smallNativeIcon);
                _ = SendMessage(
                    handle,
                    WmSetIcon,
                    new IntPtr(IconSmall2),
                    _smallNativeIcon);
                SetWindowClassIcon(handle, GclpHiconSm, _smallNativeIcon);
            }

            if (_largeNativeIcon != IntPtr.Zero)
            {
                _ = SendMessage(
                    handle,
                    WmSetIcon,
                    new IntPtr(IconBig),
                    _largeNativeIcon);
                SetWindowClassIcon(handle, GclpHicon, _largeNativeIcon);
            }
        }
        catch
        {
            // Branding must never interrupt the iAWS weighing workflow.
        }
    }

    private static void EnsureNativeIcons()
    {
        if (_smallNativeIcon != IntPtr.Zero && _largeNativeIcon != IntPtr.Zero)
        {
            return;
        }

        lock (NativeIconLock)
        {
            if (_smallNativeIcon == IntPtr.Zero)
            {
                var width = Math.Max(16, GetSystemMetrics(SmCxSmIcon));
                var height = Math.Max(16, GetSystemMetrics(SmCySmIcon));
                _smallNativeIcon = CreateNativeIcon(width, height);
            }

            if (_largeNativeIcon == IntPtr.Zero)
            {
                var width = Math.Max(32, GetSystemMetrics(SmCxIcon));
                var height = Math.Max(32, GetSystemMetrics(SmCyIcon));
                _largeNativeIcon = CreateNativeIcon(width, height);
            }
        }
    }

    private static IntPtr CreateNativeIcon(int width, int height)
    {
        var source = GetBestIconFrame(width, height);
        if (source is null)
        {
            return IntPtr.Zero;
        }

        BitmapSource resizedSource = source;
        if (source.PixelWidth != width || source.PixelHeight != height)
        {
            var scale = new ScaleTransform(
                width / (double)source.PixelWidth,
                height / (double)source.PixelHeight);
            scale.Freeze();

            var transformed = new TransformedBitmap(source, scale);
            transformed.Freeze();
            resizedSource = transformed;
        }

        var formatted = new FormatConvertedBitmap(
            resizedSource,
            PixelFormats.Bgra32,
            null,
            0);
        formatted.Freeze();

        var stride = width * 4;
        var pixels = new byte[stride * height];
        formatted.CopyPixels(pixels, stride, 0);

        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb,
                SizeImage = (uint)pixels.Length
            }
        };

        var colorBitmap = CreateDIBSection(
            IntPtr.Zero,
            ref bitmapInfo,
            DibRgbColors,
            out var bitmapBits,
            IntPtr.Zero,
            0);

        if (colorBitmap == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (bitmapBits == IntPtr.Zero)
        {
            _ = DeleteObject(colorBitmap);
            return IntPtr.Zero;
        }

        var maskBitmap = IntPtr.Zero;
        try
        {
            Marshal.Copy(pixels, 0, bitmapBits, pixels.Length);
            var maskStride = ((width + 15) / 16) * 2;
            var maskBits = new byte[maskStride * height];
            maskBitmap = CreateBitmap(width, height, 1, 1, maskBits);
            if (maskBitmap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var iconInfo = new IconInfo
            {
                IsIcon = true,
                ColorBitmap = colorBitmap,
                MaskBitmap = maskBitmap
            };

            return CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            if (maskBitmap != IntPtr.Zero)
            {
                _ = DeleteObject(maskBitmap);
            }

            _ = DeleteObject(colorBitmap);
        }
    }

    private static IReadOnlyList<BitmapFrame> LoadIconFrames()
    {
        try
        {
            var resource = Application.GetResourceStream(
                new Uri(IconResourceUri, UriKind.Absolute));
            if (resource?.Stream is null)
            {
                return Array.Empty<BitmapFrame>();
            }

            using var stream = resource.Stream;
            var decoder = new IconBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            var frames = decoder.Frames.ToArray();
            foreach (var frame in frames)
            {
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }
            }

            return frames;
        }
        catch
        {
            return Array.Empty<BitmapFrame>();
        }
    }

    private static BitmapFrame? GetLargestIconFrame() =>
        IconFrames.Value
            .OrderByDescending(frame => frame.PixelWidth * frame.PixelHeight)
            .FirstOrDefault();

    private static BitmapFrame? GetBestIconFrame(int width, int height) =>
        IconFrames.Value
            .OrderBy(frame =>
                Math.Abs(frame.PixelWidth - width) +
                Math.Abs(frame.PixelHeight - height))
            .FirstOrDefault();

    private static void SetWindowClassIcon(
        IntPtr windowHandle,
        int index,
        IntPtr iconHandle)
    {
        if (IntPtr.Size == 8)
        {
            _ = SetClassLongPtr64(windowHandle, index, iconHandle);
            return;
        }

        _ = SetClassLong32(
            windowHandle,
            index,
            unchecked((uint)iconHandle.ToInt32()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;

        public uint XHotspot;
        public uint YHotspot;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateBitmap(
        int width,
        int height,
        uint planes,
        uint bitsPerPixel,
        byte[] bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetClassLongPtrW",
        SetLastError = true)]
    private static extern IntPtr SetClassLongPtr64(
        IntPtr windowHandle,
        int index,
        IntPtr newValue);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetClassLongW",
        SetLastError = true)]
    private static extern uint SetClassLong32(
        IntPtr windowHandle,
        int index,
        uint newValue);
}
