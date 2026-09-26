namespace DPIDisplayScaleFix.Core;

public static class DisplayTopology
{
    public static IReadOnlyList<DisplayInfo> GetInternalDisplays(IEnumerable<DisplayInfo> displays) =>
        displays.Where(display => display.IsInternal)
            .DistinctBy(display => display.Source)
            .ToArray();

    public static string CreateFingerprint(IEnumerable<DisplayInfo> displays) => string.Join(Environment.NewLine,
        displays
            .OrderBy(display => display.Source.AdapterHigh)
            .ThenBy(display => display.Source.AdapterLow)
            .ThenBy(display => display.Source.SourceId)
            .ThenBy(display => display.TargetId)
            .Select(display => string.Join("|",
                display.Source.AdapterHigh,
                display.Source.AdapterLow,
                display.Source.SourceId,
                display.TargetId,
                display.OutputTechnology,
                display.Name,
                display.DevicePath)));
}