using System.Reflection;
using System.Runtime.InteropServices;

public sealed class DisplayConfigAbiTests
{
    [Theory]
    [InlineData("Luid", 8)]
    [InlineData("Header", 20)]
    [InlineData("SourceInfo", 20)]
    [InlineData("TargetInfo", 48)]
    [InlineData("PathInfo", 72)]
    [InlineData("TargetName", 420)]
    [InlineData("DpiGet", 32)]
    [InlineData("DpiSet", 24)]
    public void Native_structure_sizes_match_the_expected_x64_layout(string structureName, int expectedSize)
    {
        Type structure = GetNativeStructure(structureName);

        Assert.Equal(expectedSize, Marshal.SizeOf(structure));
    }

    [Theory]
    [InlineData("TargetInfo", "OutputTechnology", 16)]
    [InlineData("TargetInfo", "RefreshRateNumerator", 28)]
    [InlineData("PathInfo", "Target", 20)]
    [InlineData("PathInfo", "Flags", 68)]
    [InlineData("TargetName", "FriendlyName", 36)]
    [InlineData("TargetName", "MonitorDevicePath", 164)]
    [InlineData("DpiGet", "CurrentRelativeScale", 24)]
    public void Critical_native_field_offsets_match_the_expected_x64_layout(
        string structureName,
        string fieldName,
        int expectedOffset)
    {
        Type structure = GetNativeStructure(structureName);

        Assert.Equal(expectedOffset, Marshal.OffsetOf(structure, fieldName).ToInt32());
    }

    [Fact]
    public void Display_config_mode_info_buffer_entry_size_is_64_bytes()
    {
        Assert.Equal(64, DisplayConfigurationService.DisplayConfigModeInfoSize);
    }

    private static Type GetNativeStructure(string name) =>
        typeof(DisplayConfigurationService).GetNestedType(name, BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Native structure {name} was not found.");
}