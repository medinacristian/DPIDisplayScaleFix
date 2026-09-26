namespace DPIDisplayScaleFix.Core;

public static class DisplayScale
{
    private static readonly int[] SupportedPercentages = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    public static IReadOnlyList<int> SupportedValues => SupportedPercentages;

    public static int? FromRelativeValue(int minimumRelativeValue, int currentRelativeValue)
    {
        int index = -minimumRelativeValue + currentRelativeValue;
        return index >= 0 && index < SupportedPercentages.Length
            ? SupportedPercentages[index]
            : null;
    }

    public static int ToRelativeValue(int minimumRelativeValue, int percentage)
    {
        int recommendedIndex = -minimumRelativeValue;
        int targetIndex = Array.IndexOf(SupportedPercentages, percentage);
        if (targetIndex < 0 || recommendedIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(percentage), percentage,
                "La escala no está representada por la tabla admitida por el protocolo DPI.");

        return targetIndex - recommendedIndex;
    }
}