namespace DPIDisplayScaleFix.Core;

public readonly record struct DisplaySourceKey(uint AdapterLow, int AdapterHigh, uint SourceId);

public sealed record DisplayInfo(
    DisplaySourceKey Source,
    uint TargetId,
    int OutputTechnology,
    string Name,
    string DevicePath,
    int MinimumRelativeScale,
    int CurrentRelativeScale,
    int DpiReadError)
{
    public bool IsInternal => DisplayClassifier.IsInternal(OutputTechnology, Name);

    public int? ScalePercent => DpiReadError == 0
        ? DisplayScale.FromRelativeValue(MinimumRelativeScale, CurrentRelativeScale)
        : null;
}