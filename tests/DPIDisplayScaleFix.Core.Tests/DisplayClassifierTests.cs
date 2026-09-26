using DPIDisplayScaleFix.Core;

public sealed class DisplayClassifierTests
{
    [Theory]
    [InlineData(DisplayClassifier.InternalOutputTechnology, "Generic panel")]
    [InlineData(DisplayClassifier.LvdsOutputTechnology, "Generic panel")]
    [InlineData(DisplayClassifier.EmbeddedDisplayPortOutputTechnology, "Generic panel")]
    [InlineData(DisplayClassifier.EmbeddedUdiOutputTechnology, "Generic panel")]
    [InlineData(10, "NB140B9M-T01")]
    [InlineData(10, "nb140b9m-t02")]
    public void Recognizes_internal_panel_signatures(int technology, string name)
    {
        Assert.True(DisplayClassifier.IsInternal(technology, name));
    }

    [Theory]
    [InlineData(5, "LG ULTRAWIDE")]
    [InlineData(10, "ARZOPA")]
    public void Does_not_classify_external_monitors_as_internal(int technology, string name)
    {
        Assert.False(DisplayClassifier.IsInternal(technology, name));
    }
}