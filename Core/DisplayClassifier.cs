namespace DPIDisplayScaleFix.Core;

public static class DisplayClassifier
{
    public const int InternalOutputTechnology = unchecked((int)0x80000000);
    public const int LvdsOutputTechnology = 6;
    public const int EmbeddedDisplayPortOutputTechnology = 11;
    public const int EmbeddedUdiOutputTechnology = 13;
    public const string ZenbookDuoPanelPrefix = "NB140B9M-T";

    public static bool IsInternal(int outputTechnology, string friendlyName) =>
        outputTechnology is InternalOutputTechnology or LvdsOutputTechnology or
            EmbeddedDisplayPortOutputTechnology or EmbeddedUdiOutputTechnology ||
        friendlyName.StartsWith(ZenbookDuoPanelPrefix, StringComparison.OrdinalIgnoreCase);
}