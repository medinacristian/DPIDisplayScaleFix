using DPIDisplayScaleFix.Core;

public sealed class CommandLineParserTests
{
    [Fact]
    public void No_arguments_select_diagnostic_mode()
    {
        Assert.Equal(AppMode.Diagnostic, CommandLineParser.Parse([]).Mode);
    }

    [Theory]
    [InlineData("--cycle", AppMode.Cycle)]
    [InlineData("--watch", AppMode.Watch)]
    [InlineData("--WATCH", AppMode.Watch)]
    public void Accepts_each_supported_mode(string argument, AppMode expected)
    {
        Assert.Equal(expected, CommandLineParser.Parse([argument]).Mode);
    }

    [Theory]
    [InlineData("--unknown")]
    public void Rejects_unknown_options(string argument)
    {
        Assert.False(CommandLineParser.Parse([argument]).IsValid);
    }

    [Fact]
    public void Rejects_conflicting_or_repeated_options()
    {
        Assert.False(CommandLineParser.Parse(["--watch", "--cycle"]).IsValid);
        Assert.False(CommandLineParser.Parse(["--cycle", "--cycle"]).IsValid);
    }
}