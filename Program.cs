using System.Runtime.InteropServices;

internal static class Program
{
    private static readonly int[] ScaleValues = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];
    private const uint QdcOnlyActivePaths = 0x2;
    private const int OutputTechnologyInternal = unchecked((int)0x80000000);
    private const int OutputTechnologyLvds = 6;
    private const int OutputTechnologyDisplayPortEmbedded = 11;
    private const int OutputTechnologyUdiEmbedded = 13;
    private const int DisplayConfigModeInfoSize = 64;
    private const string ZenbookDuoPanelPrefix = "NB140B9M-T";

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public int Type; public uint Size; public Luid AdapterId; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] private struct SourceInfo { public Luid AdapterId; public uint Id; public uint ModeInfoIdx; public uint StatusFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct TargetInfo { public Luid AdapterId; public uint Id; public uint ModeInfoIdx; public int OutputTechnology; public int Rotation; public int Scaling; public uint RefreshRateNumerator; public uint RefreshRateDenominator; public int ScanLineOrdering; public int TargetAvailable; public uint StatusFlags; }
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

    private readonly record struct SourceKey(uint AdapterLow, int AdapterHigh, uint SourceId);

    private sealed record Display(PathInfo Path, string Name, string DevicePath, DpiGet Dpi, int DpiError)
    {
        public bool IsInternal =>
            Path.Target.OutputTechnology is OutputTechnologyInternal or OutputTechnologyLvds or OutputTechnologyDisplayPortEmbedded or OutputTechnologyUdiEmbedded ||
            Name.StartsWith(ZenbookDuoPanelPrefix, StringComparison.OrdinalIgnoreCase);
        public SourceKey Key => new(Path.Source.AdapterId.LowPart, Path.Source.AdapterId.HighPart, Path.Source.Id);
    }

    private static List<Display> Enumerate()
    {
        int rc = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
        if (rc != 0) throw new InvalidOperationException($"GetDisplayConfigBufferSizes falló: {rc}");
        var paths = new PathInfo[pathCount];
        IntPtr modeBuffer = Marshal.AllocHGlobal(checked((int)Math.Max(1, modeCount) * DisplayConfigModeInfoSize));
        try { rc = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modeBuffer, IntPtr.Zero); }
        finally { Marshal.FreeHGlobal(modeBuffer); }
        if (rc != 0) throw new InvalidOperationException($"QueryDisplayConfig falló: {rc}");

        var result = new List<Display>();
        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var name = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), AdapterId = path.Target.AdapterId, Id = path.Target.Id }, FriendlyName = "", MonitorDevicePath = "" };
            int nameError = DisplayConfigGetDeviceInfo(ref name);
            if (nameError != 0)
            {
                Console.Error.WriteLine($"No se pudo consultar el nombre del destino (target={path.Target.Id}); error={nameError}.");
                continue;
            }
            var dpi = new DpiGet { Header = new Header { Type = -3, Size = (uint)Marshal.SizeOf<DpiGet>(), AdapterId = path.Source.AdapterId, Id = path.Source.Id } };
            int dpiError = DisplayConfigGetDeviceInfo(ref dpi);
            result.Add(new Display(path, name.FriendlyName ?? "", name.MonitorDevicePath ?? "", dpi, dpiError));
        }
        return result;
    }

    private static Display[] GetInternalSources() => Enumerate()
        .Where(display => display.IsInternal)
        .GroupBy(display => display.Key)
        .Select(group => group.First())
        .ToArray();

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
                Console.WriteLine($"{(display.IsInternal ? "INTERNA" : "externa  ")} | {display.Name} | salida={display.Path.Target.OutputTechnology} | escala={(display.DpiError == 0 ? $"{Percent(display.Dpi)}%" : $"no disponible (error {display.DpiError})")} | ruta={display.DevicePath}");

            var internalDisplays = displays.Where(display => display.IsInternal).GroupBy(display => display.Key).Select(group => group.First()).ToArray();
            if (internalDisplays.Length is < 1 or > 2)
            {
                Console.Error.WriteLine($"No se actuó: se esperaban una o dos pantallas internas ASUS activas; encontradas={internalDisplays.Length}.");
                return 2;
            }
            foreach (var display in internalDisplays)
            {
                if (display.DpiError != 0) throw new InvalidOperationException($"No se pudo consultar la escala de {display.Name}; error={display.DpiError}.");
                int scale = Percent(display.Dpi);
                if (!ScaleValues.Contains(scale)) throw new InvalidOperationException($"No se pudo interpretar la escala de {display.Name}: {scale}%.");
                Console.WriteLine($"Pantalla interna: {display.Name}; escala inicial={scale}%.");
            }
            var originalScales = internalDisplays.ToDictionary(display => display.Key, display => Percent(display.Dpi));
            if (!args.Contains("--cycle", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("Modo de diagnóstico. Para cada pantalla interna distinta de 100%, --cycle la cambia temporalmente a 100% y restaura su escala inicial.");
                return 0;
            }

            var toRefresh = originalScales.Where(pair => pair.Value != 100).ToArray();
            if (toRefresh.Length == 0)
            {
                Console.WriteLine("Todas las pantallas internas ya están a 100%; no se necesita el ciclo.");
                return 0;
            }

            bool restoreNeeded = false;
            try
            {
                restoreNeeded = true;
                foreach (var target in toRefresh)
                {
                    var display = internalDisplays.Single(candidate => candidate.Key == target.Key);
                    SetScale(display, 100);
                }
                Thread.Sleep(750);
                var refreshed = GetInternalSources().ToDictionary(display => display.Key);
                foreach (var target in toRefresh)
                {
                    if (!refreshed.TryGetValue(target.Key, out var display)) throw new InvalidOperationException("No se pudo volver a identificar una pantalla interna después del cambio a 100%.");
                    if (display.DpiError != 0) throw new InvalidOperationException($"No se pudo consultar la escala tras cambiar {display.Name} a 100%; error={display.DpiError}.");
                    if (Percent(display.Dpi) != 100) throw new InvalidOperationException($"Windows no confirmó 100% temporalmente en {display.Name}; lectura={Percent(display.Dpi)}%.");
                    SetScale(display, target.Value);
                }
                restoreNeeded = false;
            }
            finally
            {
                if (restoreNeeded)
                {
                    try
                    {
                        var recovery = GetInternalSources().ToDictionary(display => display.Key);
                        foreach (var target in toRefresh)
                        {
                            if (!recovery.TryGetValue(target.Key, out var display) || display.DpiError != 0)
                                throw new InvalidOperationException("No se pudo volver a identificar una pantalla interna para restaurarla.");
                            if (Percent(display.Dpi) != target.Value) SetScale(display, target.Value);
                        }
                    }
                    catch (Exception recoveryError)
                    {
                        Console.Error.WriteLine($"ERROR: no se pudieron restaurar todas las escalas originales: {recoveryError.Message}");
                    }
                }
            }
            Thread.Sleep(250);
            var verify = GetInternalSources().ToDictionary(display => display.Key);
            bool ok = originalScales.All(pair => verify.TryGetValue(pair.Key, out var display) && display.DpiError == 0 && Percent(display.Dpi) == pair.Value);
            Console.WriteLine(ok ? "Verificado: se restauraron las escalas iniciales de las pantallas internas." : "ATENCIÓN: no se pudieron confirmar todas las escalas iniciales.");
            return ok ? 0 : 3;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
