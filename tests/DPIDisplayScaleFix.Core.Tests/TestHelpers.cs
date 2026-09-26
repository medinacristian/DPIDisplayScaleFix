using DPIDisplayScaleFix.Core;

internal sealed class FakeDisplayConfiguration(params DisplayInfo[] initialDisplays) : IDisplayConfiguration
{
    private readonly List<DisplayInfo> _displays = [.. initialDisplays];

    public List<(DisplaySourceKey Source, int Percentage)> Writes { get; } = [];

    public Func<DisplayInfo, int, Exception?>? BeforeSet { get; init; }

    public IReadOnlyList<DisplayInfo> GetActiveDisplays() => _displays.ToArray();

    public void SetScale(DisplayInfo display, int percentage)
    {
        Writes.Add((display.Source, percentage));
        Exception? failure = BeforeSet?.Invoke(display, percentage);
        if (failure is not null)
            throw failure;

        int relative = DisplayScale.ToRelativeValue(display.MinimumRelativeScale, percentage);
        int index = _displays.FindIndex(candidate => candidate.Source == display.Source);
        _displays[index] = _displays[index] with
        {
            CurrentRelativeScale = relative,
            DpiReadError = 0
        };
    }

    public int[] WritesFor(DisplaySourceKey source) =>
        Writes.Where(write => write.Source == source).Select(write => write.Percentage).ToArray();

    public DisplayInfo Current(DisplaySourceKey source) =>
        _displays.Single(display => display.Source == source);
}

internal static class TestDisplays
{
    public static DisplayInfo Internal(string name, int percentage = 175, uint sourceId = 1) =>
        Create(name, DisplayClassifier.InternalOutputTechnology, percentage, sourceId);

    public static DisplayInfo External(string name, int percentage = 100, uint sourceId = 2) =>
        Create(name, 10, percentage, sourceId);

    private static DisplayInfo Create(string name, int technology, int percentage, uint sourceId)
    {
        const int minimumRelativeScale = -2;
        return new DisplayInfo(
            new DisplaySourceKey(1, 0, sourceId),
            sourceId,
            technology,
            name,
            $@"\\?\DISPLAY\{name}",
            minimumRelativeScale,
            DisplayScale.ToRelativeValue(minimumRelativeScale, percentage),
            0);
    }
}