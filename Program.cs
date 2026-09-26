using System.Runtime.InteropServices;

internal static class Program
{
    private const string AsusId = "B0E0D7B0_09_07E9_38^AE5C4BA78855794E6C44D214F3C38A80";
    private static readonly int[] ScaleValues = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];
    private const uint QdcOnlyActivePaths = 0x2;
    private const int OutputTechnologyInternal = unchecked((int)0x80000000);

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public int Type; public uint Size; public Luid AdapterId; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] private struct SourceInfo { public Luid AdapterId; public uint Id; public uint ModeInfoIdx; public uint StatusFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct TargetInfo { public Luid AdapterId; public uint Id; public uint ModeInfoIdx; public int OutputTechnology; public int Rotation; public int Scaling; public long RefreshRate; public int ScanLineOrdering; public int TargetAvailable; public uint StatusFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct PathInfo { public SourceInfo Source; public TargetInfo Target; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName
    {
        public Header Header; public uint Flags; public int OutputTechnology; public ushort Manufacturer; public ushort Product; public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DpiGet { public Header Header; public int MinRel; public int CurrentRel; public int MaxRel; }
    [StructLayout(LayoutKind.Sequential)] private struct DpiSet { public Header Header; public int ScaleRel; }

    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths, ref uint modeCount, IntPtr modes, IntPtr topologyId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName request);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DpiGet request);
    [DllImport("user32.dll")] private static extern int DisplayConfigSetDeviceInfo(ref DpiSet request);

    private sealed record Display(PathInfo Path, string Name, string DevicePath, DpiGet Dpi)
    {
        public bool IsInternal => Path.Target.OutputTechnology == OutputTechnologyInternal;
        public bool MatchesAsus => Normalize(DevicePath).Contains(Normalize(AsusId), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());

    private static List<Display> Enumerate()
    {
        int rc = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
        if (rc != 0) throw new InvalidOperationException($"GetDisplayConfigBufferSizes falló: {rc}");
        var paths = new PathInfo[pathCount];
        IntPtr modeBuffer = Marshal.AllocHGlobal(checked((int)Math.Max(1, modeCount) * 48));
        try { rc = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modeBuffer, IntPtr.Zero); }
        finally { Marshal.FreeHGlobal(modeBuffer); }
        if (rc != 0) throw new InvalidOperationException($"QueryDisplayConfig falló: {rc}");

        var result = new List<Display>();
        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var name = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), AdapterId = path.Target.AdapterId, Id = path.Target.Id }, FriendlyName = "", MonitorDevicePath = "" };
            if (DisplayConfigGetDeviceInfo(ref name) != 0) continue;
            var dpi = new DpiGet { Header = new Header { Type = -3, Size = (uint)Marshal.SizeOf<DpiGet>(), AdapterId = path.Source.AdapterId, Id = path.Source.Id } };
            if (DisplayConfigGetDeviceInfo(ref dpi) != 0) dpi = new DpiGet();
            result.Add(new Display(path, name.FriendlyName ?? "", name.MonitorDevicePath ?? "", dpi));
        }
        return result;
    }

    private static int Percent(DpiGet dpi)
    {
        int index = -dpi.MinRel + dpi.CurrentRel;
        return index >= 0 && index < ScaleValues.Length ? ScaleValues[index] : -1;
    }

    private static void SetScale(Display display, int percent)
    {
        int recommendedIndex = -display.Dpi.MinRel;
        int targetIndex = Array.IndexOf(ScaleValues, percent);
        if (targetIndex < 0 || recommendedIndex < 0) throw new InvalidOperationException("Escala no representable por la tabla del protocolo DPI.");
        var set = new DpiSet { Header = new Header { Type = -4, Size = (uint)Marshal.SizeOf<DpiSet>(), AdapterId = display.Path.Source.AdapterId, Id = display.Path.Source.Id }, ScaleRel = targetIndex - recommendedIndex };
        int rc = DisplayConfigSetDeviceInfo(ref set);
        if (rc != 0) throw new InvalidOperationException($"DisplayConfigSetDeviceInfo({percent}%) falló: {rc}");
    }

    public static int Main(string[] args)
    {
        try
        {
            var displays = Enumerate();
            Console.WriteLine("Pantallas activas (el modo predeterminado solo diagnostica):");
            foreach (var display in displays)
                Console.WriteLine($"{(display.IsInternal ? "INTERNA" : "externa  ")} | {display.Name} | escala={Percent(display.Dpi)}% | ruta={display.DevicePath}");

            var matches = displays.Where(display => display.IsInternal && display.MatchesAsus).ToArray();
            if (matches.Length != 1)
            {
                Console.Error.WriteLine($"No se actuó: el ID ASUS debe coincidir con exactamente un destino interno; coincidencias={matches.Length}.");
                return 2;
            }
            if (!args.Contains("--cycle", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("Coincidencia única del ASUS confirmada. Para ejecutar 100% → 200%: --cycle");
                return 0;
            }

            var asus = matches[0];
            if (Percent(asus.Dpi) != 200) throw new InvalidOperationException($"El ASUS no está a 200% (lectura={Percent(asus.Dpi)}%); se aborta.");
            SetScale(asus, 100);
            Thread.Sleep(750);
            var refreshed = Enumerate().Where(display => display.IsInternal && display.MatchesAsus).ToArray();
            if (refreshed.Length != 1) throw new InvalidOperationException("El ASUS dejó de ser una coincidencia única tras el cambio a 100%; no se envió el segundo cambio.");
            SetScale(refreshed[0], 200);
            Thread.Sleep(250);
            var verify = Enumerate().Where(display => display.IsInternal && display.MatchesAsus).ToArray();
            bool ok = verify.Length == 1 && Percent(verify[0].Dpi) == 200;
            Console.WriteLine(ok ? "Verificado: ASUS nuevamente a 200%." : "ATENCIÓN: no se pudo confirmar 200%.");
            return ok ? 0 : 3;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
