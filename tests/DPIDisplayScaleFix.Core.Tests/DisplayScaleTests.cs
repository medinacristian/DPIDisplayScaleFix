using DPIDisplayScaleFix.Core;

public sealed class DisplayScaleTests
{
    [Theory]
    [InlineData(100, -2)]
    [InlineData(125, -1)]
    [InlineData(150, 0)]
    [InlineData(175, 1)]
    [InlineData(200, 2)]
    [InlineData(225, 3)]
    [InlineData(250, 4)]
    [InlineData(300, 5)]
    [InlineData(350, 6)]
    [InlineData(400, 7)]
    [InlineData(450, 8)]
    [InlineData(500, 9)]
    public void Relative_scale_round_trips_to_supported_percentage(int percentage, int relativeValue)
    {
        int relative = DisplayScale.ToRelativeValue(minimumRelativeValue: -2, percentage);

        Assert.Equal(relativeValue, relative);
        Assert.Equal(percentage, DisplayScale.FromRelativeValue(-2, relative));
    }

    [Fact]
    public void Relative_values_outside_the_supported_table_return_null()
    {
        Assert.Null(DisplayScale.FromRelativeValue(-2, 100));
    }

    [Fact]
    public void Unsupported_percentage_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DisplayScale.ToRelativeValue(minimumRelativeValue: -2, percentage: 133));
    }
}