using DPIDisplayScaleFix.Core;

public sealed class DisplayTopologyTests
{
    [Fact]
    public void Fingerprint_does_not_depend_on_enumeration_order()
    {
        DisplayInfo first = TestDisplays.Internal("NB140B9M-T01", sourceId: 1);
        DisplayInfo second = TestDisplays.External("LG ULTRAWIDE", sourceId: 2);

        string forward = DisplayTopology.CreateFingerprint([first, second]);
        string reverse = DisplayTopology.CreateFingerprint([second, first]);

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void Fingerprint_changes_when_a_target_is_added_or_removed()
    {
        DisplayInfo internalDisplay = TestDisplays.Internal("NB140B9M-T01", sourceId: 1);
        DisplayInfo externalDisplay = TestDisplays.External("LG ULTRAWIDE", sourceId: 2);

        Assert.NotEqual(
            DisplayTopology.CreateFingerprint([internalDisplay]),
            DisplayTopology.CreateFingerprint([internalDisplay, externalDisplay]));
    }

    [Fact]
    public void Internal_display_selection_deduplicates_shared_sources_and_ignores_externals()
    {
        DisplayInfo internalDisplay = TestDisplays.Internal("NB140B9M-T01", sourceId: 1);
        DisplayInfo cloneTarget = internalDisplay with { TargetId = 99 };
        DisplayInfo externalDisplay = TestDisplays.External("LG ULTRAWIDE", sourceId: 2);

        IReadOnlyList<DisplayInfo> result =
            DisplayTopology.GetInternalDisplays([internalDisplay, cloneTarget, externalDisplay]);

        Assert.Single(result);
        Assert.Equal(internalDisplay.Source, result[0].Source);
    }
}