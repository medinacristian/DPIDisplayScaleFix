namespace DPIDisplayScaleFix.Core;

public interface IDisplayConfiguration
{
    IReadOnlyList<DisplayInfo> GetActiveDisplays();
    void SetScale(DisplayInfo display, int percentage);
}