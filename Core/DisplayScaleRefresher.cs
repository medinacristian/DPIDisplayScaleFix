namespace DPIDisplayScaleFix.Core;

public sealed record DisplayRefreshResult(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> Errors);

public sealed class DisplayScaleRefresher
{
    private static readonly TimeSpan TemporaryScaleDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan VerificationDelay = TimeSpan.FromMilliseconds(250);
    private readonly IDisplayConfiguration _displayConfiguration;
    private readonly Action<TimeSpan> _wait;

    public DisplayScaleRefresher(IDisplayConfiguration displayConfiguration, Action<TimeSpan>? wait = null)
    {
        _displayConfiguration = displayConfiguration;
        _wait = wait ?? Thread.Sleep;
    }

    public DisplayRefreshResult Run(bool runCycle)
    {
        var output = new List<string>();
        var errors = new List<string>();

        try
        {
            IReadOnlyList<DisplayInfo> displays = _displayConfiguration.GetActiveDisplays();
            AddDisplayDiagnostics(displays, output);

            IReadOnlyList<DisplayInfo> internalDisplays = DisplayTopology.GetInternalDisplays(displays);
            if (internalDisplays.Count is < 1 or > 2)
            {
                errors.Add($"No se actuó: se esperaban una o dos pantallas internas ASUS activas; encontradas={internalDisplays.Count}.");
                return Result(2, output, errors);
            }

            if (!ValidateInitialScales(internalDisplays, output, errors))
                return Result(1, output, errors);

            var originalScales = internalDisplays.ToDictionary(display => display.Source, display => display.ScalePercent!.Value);
            if (!runCycle)
            {
                output.Add("Modo de diagnóstico. Para cada pantalla interna distinta de 100%, --cycle la cambia temporalmente a 100% y restaura su escala inicial.");
                return Result(0, output, errors);
            }

            int cycleResult = RunCycle(originalScales, internalDisplays, output, errors);
            return Result(cycleResult, output, errors);
        }
        catch (Exception exception)
        {
            errors.Add(exception.ToString());
            return Result(1, output, errors);
        }
    }

    private static void AddDisplayDiagnostics(IEnumerable<DisplayInfo> displays, ICollection<string> output)
    {
        output.Add("Pantallas activas (el modo predeterminado solo diagnostica):");
        foreach (DisplayInfo display in displays)
        {
            string category = display.IsInternal ? "INTERNA" : "externa  ";
            string scale = display.ScalePercent is int percentage
                ? $"{percentage}%"
                : $"no disponible (error {display.DpiReadError})";
            output.Add($"{category} | {display.Name} | salida={display.OutputTechnology} | escala={scale} | ruta={display.DevicePath}");
        }
    }

    private static bool ValidateInitialScales(
        IEnumerable<DisplayInfo> displays,
        ICollection<string> output,
        ICollection<string> errors)
    {
        foreach (DisplayInfo display in displays)
        {
            if (display.DpiReadError != 0)
            {
                errors.Add($"No se pudo consultar la escala de {display.Name}; error={display.DpiReadError}.");
                return false;
            }

            if (display.ScalePercent is not int percentage)
            {
                errors.Add($"No se pudo interpretar la escala de {display.Name}.");
                return false;
            }

            output.Add($"Pantalla interna: {display.Name}; escala inicial={percentage}%.");
        }

        return true;
    }

    private int RunCycle(
        IReadOnlyDictionary<DisplaySourceKey, int> originalScales,
        IReadOnlyList<DisplayInfo> initialDisplays,
        ICollection<string> output,
        ICollection<string> errors)
    {
        KeyValuePair<DisplaySourceKey, int>[] scalesToRefresh = originalScales
            .Where(scale => scale.Value != 100)
            .ToArray();
        if (scalesToRefresh.Length == 0)
        {
            output.Add("Todas las pantallas internas ya están a 100%; no se necesita el ciclo.");
            return 0;
        }

        bool restorationRequired = false;
        try
        {
            restorationRequired = true;
            SetScales(initialDisplays, scalesToRefresh, 100);
            _wait(TemporaryScaleDelay);

            List<string> restorationErrors = RestoreAfterTemporaryScale(originalScales, scalesToRefresh);
            if (restorationErrors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, restorationErrors));

            restorationRequired = false;
        }
        finally
        {
            if (restorationRequired)
            {
                foreach (string error in TryRestoreEveryOriginalScale(scalesToRefresh))
                    errors.Add(error);
            }
        }

        _wait(VerificationDelay);
        return VerifyOriginalScales(originalScales, output, errors) ? 0 : 3;
    }

    private void SetScales(
        IEnumerable<DisplayInfo> displays,
        IEnumerable<KeyValuePair<DisplaySourceKey, int>> scales,
        int percentage)
    {
        Dictionary<DisplaySourceKey, DisplayInfo> bySource = displays.ToDictionary(display => display.Source);
        foreach (KeyValuePair<DisplaySourceKey, int> scale in scales)
            _displayConfiguration.SetScale(bySource[scale.Key], percentage);
    }

    private List<string> RestoreAfterTemporaryScale(
        IReadOnlyDictionary<DisplaySourceKey, int> originalScales,
        IEnumerable<KeyValuePair<DisplaySourceKey, int>> scalesToRefresh)
    {
        Dictionary<DisplaySourceKey, DisplayInfo> refreshed = DisplayTopology
            .GetInternalDisplays(_displayConfiguration.GetActiveDisplays())
            .ToDictionary(display => display.Source);
        var errors = new List<string>();

        foreach (KeyValuePair<DisplaySourceKey, int> scale in scalesToRefresh)
        {
            if (!refreshed.TryGetValue(scale.Key, out DisplayInfo? display))
            {
                errors.Add("No se pudo volver a identificar una pantalla interna después del cambio a 100%.");
                continue;
            }

            if (display.DpiReadError != 0 || display.ScalePercent != 100)
            {
                errors.Add($"Windows no confirmó 100% temporalmente en {display.Name}; lectura={display.ScalePercent?.ToString() ?? "no disponible"}%.");
                continue;
            }

            try
            {
                _displayConfiguration.SetScale(display, originalScales[scale.Key]);
            }
            catch (Exception exception)
            {
                errors.Add($"No se pudo restaurar {display.Name} a {originalScales[scale.Key]}%: {exception.Message}");
            }
        }

        return errors;
    }

    private List<string> TryRestoreEveryOriginalScale(
        IEnumerable<KeyValuePair<DisplaySourceKey, int>> scalesToRefresh)
    {
        var errors = new List<string>();
        foreach (KeyValuePair<DisplaySourceKey, int> scale in scalesToRefresh)
        {
            try
            {
                Dictionary<DisplaySourceKey, DisplayInfo> currentDisplays = DisplayTopology
                    .GetInternalDisplays(_displayConfiguration.GetActiveDisplays())
                    .ToDictionary(display => display.Source);

                if (!currentDisplays.TryGetValue(scale.Key, out DisplayInfo? display))
                {
                    errors.Add($"No se pudo volver a identificar la pantalla con origen {scale.Key} para restaurarla.");
                    continue;
                }

                if (display.DpiReadError != 0 || display.ScalePercent is null)
                {
                    errors.Add($"No se pudo leer la escala de {display.Name} durante la recuperación.");
                    continue;
                }

                if (display.ScalePercent != scale.Value)
                    _displayConfiguration.SetScale(display, scale.Value);
            }
            catch (Exception exception)
            {
                errors.Add($"Falló la recuperación de la pantalla con origen {scale.Key}: {exception.Message}");
            }
        }

        return errors;
    }

    private bool VerifyOriginalScales(
        IReadOnlyDictionary<DisplaySourceKey, int> originalScales,
        ICollection<string> output,
        ICollection<string> errors)
    {
        Dictionary<DisplaySourceKey, DisplayInfo> currentDisplays = DisplayTopology
            .GetInternalDisplays(_displayConfiguration.GetActiveDisplays())
            .ToDictionary(display => display.Source);
        bool restored = originalScales.All(scale =>
            currentDisplays.TryGetValue(scale.Key, out DisplayInfo? display) &&
            display.DpiReadError == 0 && display.ScalePercent == scale.Value);

        if (restored)
            output.Add("Verificado: se restauraron las escalas iniciales de las pantallas internas.");
        else
            errors.Add("ATENCIÓN: no se pudieron confirmar todas las escalas iniciales.");

        return restored;
    }

    private static DisplayRefreshResult Result(int code, IReadOnlyList<string> output, IReadOnlyList<string> errors) =>
        new(code, output, errors);
}