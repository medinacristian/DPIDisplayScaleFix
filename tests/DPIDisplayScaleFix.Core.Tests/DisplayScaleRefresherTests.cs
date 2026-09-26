using DPIDisplayScaleFix.Core;

public sealed class DisplayScaleRefresherTests
{
    [Fact]
    public void Cycle_temporarily_uses_100_percent_then_restores_the_initial_scale()
    {
        var display = TestDisplays.Internal("NB140B9M-T01", 175, sourceId: 1);
        var configuration = new FakeDisplayConfiguration(display);
        var waits = new List<TimeSpan>();
        var refresher = new DisplayScaleRefresher(configuration, waits.Add);

        DisplayRefreshResult result = refresher.Run(runCycle: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { 100, 175 }, configuration.WritesFor(display.Source));
        Assert.Equal(175, configuration.Current(display.Source).ScalePercent);
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(750), TimeSpan.FromMilliseconds(250) },
            waits);
    }

    [Fact]
    public void Does_not_change_a_screen_already_at_100_percent()
    {
        var display = TestDisplays.Internal("NB140B9M-T01", 100, sourceId: 1);
        var configuration = new FakeDisplayConfiguration(display);

        DisplayRefreshResult result = new DisplayScaleRefresher(configuration, _ => { }).Run(runCycle: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(configuration.Writes);
    }

    [Fact]
    public void Changes_only_internal_screens_when_external_displays_are_active()
    {
        var firstInternal = TestDisplays.Internal("NB140B9M-T01", 175, sourceId: 1);
        var secondInternal = TestDisplays.Internal("NB140B9M-T02", 200, sourceId: 2);
        var external = TestDisplays.External("LG ULTRAWIDE", 150, sourceId: 3);
        var configuration = new FakeDisplayConfiguration(firstInternal, secondInternal, external);

        DisplayRefreshResult result = new DisplayScaleRefresher(configuration, _ => { }).Run(runCycle: true);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(configuration.Writes, write => write.Source == external.Source);
        Assert.Equal(175, configuration.Current(firstInternal.Source).ScalePercent);
        Assert.Equal(200, configuration.Current(secondInternal.Source).ScalePercent);
        Assert.Equal(150, configuration.Current(external.Source).ScalePercent);
    }

    [Fact]
    public void Attempts_to_restore_other_screens_when_one_restore_fails()
    {
        var firstInternal = TestDisplays.Internal("NB140B9M-T01", 175, sourceId: 1);
        var secondInternal = TestDisplays.Internal("NB140B9M-T02", 200, sourceId: 2);
        var configuration = new FakeDisplayConfiguration(firstInternal, secondInternal)
        {
            BeforeSet = (display, percentage) =>
                display.Source == firstInternal.Source && percentage == 175
                    ? new InvalidOperationException("Simulated restore failure.")
                    : null
        };

        DisplayRefreshResult result = new DisplayScaleRefresher(configuration, _ => { }).Run(runCycle: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(200, configuration.Current(secondInternal.Source).ScalePercent);
        Assert.Contains(configuration.Writes, write =>
            write.Source == secondInternal.Source && write.Percentage == 200);
        Assert.True(configuration.Writes.Count(write =>
            write.Source == firstInternal.Source && write.Percentage == 175) >= 2);
    }

    [Fact]
    public void Does_not_change_any_scale_when_an_internal_scale_cannot_be_read()
    {
        var display = TestDisplays.Internal("NB140B9M-T01", 175, sourceId: 1) with { DpiReadError = 5 };
        var configuration = new FakeDisplayConfiguration(display);

        DisplayRefreshResult result = new DisplayScaleRefresher(configuration, _ => { }).Run(runCycle: true);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(configuration.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Refuses_to_operate_unless_one_or_two_internal_screens_are_active(int internalCount)
    {
        DisplayInfo[] displays = Enumerable.Range(1, internalCount)
            .Select(index => TestDisplays.Internal($"NB140B9M-T0{index}", 175, (uint)index))
            .ToArray();
        var configuration = new FakeDisplayConfiguration(displays);

        DisplayRefreshResult result = new DisplayScaleRefresher(configuration, _ => { }).Run(runCycle: true);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(configuration.Writes);
    }
}