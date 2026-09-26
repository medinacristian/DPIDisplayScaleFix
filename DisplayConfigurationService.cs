using System.Runtime.InteropServices;
using DPIDisplayScaleFix.Core;

internal sealed class DisplayConfigurationService : IDisplayConfiguration
{
    private const uint QueryOnlyActivePaths = 0x2;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaximumQueryAttempts = 3;
    internal const int DisplayConfigModeInfoSize = 64;

    // Windows' DPI scale device-info packet types are undocumented.
    private const int GetDpiScaleRequestType = -3;
    private const int SetDpiScaleRequestType = -4;
    private const int GetTargetNameRequestType = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Header
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public int OutputTechnology;
        public int Rotation;
        public int Scaling;
        public uint RefreshRateNumerator;
        public uint RefreshRateDenominator;
        public int ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathInfo
    {
        public SourceInfo Source;
        public TargetInfo Target;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct TargetName
    {
        public Header Header;
        public uint Flags;
        public int OutputTechnology;
        public ushort Manufacturer;
        public ushort Product;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string FriendlyName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DpiGet
    {
        public Header Header;
        public int MinimumRelativeScale;
        public int CurrentRelativeScale;
        public int MaximumRelativeScale;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DpiSet
    {
        public Header Header;
        public int RelativeScale;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint pathCount,
        out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] PathInfo[] paths,
        ref uint modeCount,
        IntPtr modes,
        IntPtr topologyId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref TargetName request);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DpiGet request);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DpiSet request);

    public IReadOnlyList<DisplayInfo> GetActiveDisplays() =>
        BoundedRetry.Execute(
            QueryActiveDisplaysOnce,
            ErrorInsufficientBuffer,
            MaximumQueryAttempts,
            error => $"QueryDisplayConfig falló: {error}");

    public void SetScale(DisplayInfo display, int percentage)
    {
        int relativeScale = DisplayScale.ToRelativeValue(display.MinimumRelativeScale, percentage);
        var request = new DpiSet
        {
            Header = new Header
            {
                Type = SetDpiScaleRequestType,
                Size = (uint)Marshal.SizeOf<DpiSet>(),
                AdapterId = new Luid
                {
                    LowPart = display.Source.AdapterLow,
                    HighPart = display.Source.AdapterHigh
                },
                Id = display.Source.SourceId
            },
            RelativeScale = relativeScale
        };

        int result = DisplayConfigSetDeviceInfo(ref request);
        if (result != 0)
            throw new InvalidOperationException($"DisplayConfigSetDeviceInfo({percentage}%) falló: {result}");
    }


    private static (IReadOnlyList<DisplayInfo> Value, int ErrorCode) QueryActiveDisplaysOnce()
    {
        (PathInfo[] paths, uint pathCount, uint modeCount) = GetDisplayConfigBuffers();
        IntPtr modeBuffer = AllocateModeBuffer(modeCount);

        int result;
        try
        {
            result = QueryDisplayConfig(
                QueryOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modeBuffer,
                IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(modeBuffer);
        }

        return result == 0
            ? (CreateDisplaySnapshots(paths, pathCount), 0)
            : (Array.Empty<DisplayInfo>(), result);
    }
    private static (PathInfo[] Paths, uint PathCount, uint ModeCount) GetDisplayConfigBuffers()
    {
        int result = GetDisplayConfigBufferSizes(
            QueryOnlyActivePaths,
            out uint pathCount,
            out uint modeCount);

        if (result != 0)
            throw new InvalidOperationException($"GetDisplayConfigBufferSizes falló: {result}");

        return (new PathInfo[pathCount], pathCount, modeCount);
    }

    private static IntPtr AllocateModeBuffer(uint modeCount)
    {
        int entryCount = checked((int)Math.Max(1, modeCount));
        return Marshal.AllocHGlobal(checked(entryCount * DisplayConfigModeInfoSize));
    }

    private static IReadOnlyList<DisplayInfo> CreateDisplaySnapshots(
        IReadOnlyList<PathInfo> paths,
        uint pathCount)
    {
        var displays = new List<DisplayInfo>((int)pathCount);
        for (int index = 0; index < pathCount; index++)
        {
            PathInfo path = paths[index];
            TargetName targetName = GetTargetName(path);
            (DpiGet dpi, int dpiError) = GetDpiScale(path);

            displays.Add(new DisplayInfo(
                CreateSourceKey(path.Source),
                path.Target.Id,
                path.Target.OutputTechnology,
                targetName.FriendlyName ?? string.Empty,
                targetName.MonitorDevicePath ?? string.Empty,
                dpi.MinimumRelativeScale,
                dpi.CurrentRelativeScale,
                dpiError));
        }

        return displays;
    }

    private static DisplaySourceKey CreateSourceKey(SourceInfo source) =>
        new(source.AdapterId.LowPart, source.AdapterId.HighPart, source.Id);

    private static TargetName GetTargetName(PathInfo path)
    {
        var request = new TargetName
        {
            Header = new Header
            {
                Type = GetTargetNameRequestType,
                Size = (uint)Marshal.SizeOf<TargetName>(),
                AdapterId = path.Target.AdapterId,
                Id = path.Target.Id
            },
            FriendlyName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        int result = DisplayConfigGetDeviceInfo(ref request);
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"No se pudo consultar el nombre del destino {path.Target.Id}; error={result}.");
        }

        return request;
    }

    private static (DpiGet Dpi, int Error) GetDpiScale(PathInfo path)
    {
        var request = new DpiGet
        {
            Header = new Header
            {
                Type = GetDpiScaleRequestType,
                Size = (uint)Marshal.SizeOf<DpiGet>(),
                AdapterId = path.Source.AdapterId,
                Id = path.Source.Id
            }
        };

        int error = DisplayConfigGetDeviceInfo(ref request);
        return (request, error);
    }
}