using DPIDisplayScaleFix.Core;

internal sealed class DisplayWatcher(
    IDisplayConfiguration displayConfiguration,
    DisplayScaleRefresher scaleRefresher)
{
    private const string MutexName = @"Local\DPIDisplayScaleFix-Watch";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StableTopologyDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorLogInterval = TimeSpan.FromSeconds(10);

    public int Run()
    {
        using var mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("Ya hay una instancia de vigilancia activa; esta instancia se cerrará.");
            return 4;
        }

        using WatchLog watchLog = WatchLog.Start();
        Console.WriteLine($"Registro de actividad: {watchLog.Path}");
        Console.WriteLine(
            "Modo vigilancia: se ejecutará un ciclo al iniciar y después de cada cambio estable de pantallas. Ctrl+C para salir.");
        RunCycleAndReportFailure("El ciclo inicial");

        var tracker = new StableTopologyTracker(GetInitialTopology(), StableTopologyDelay);
        string? lastQueryError = null;
        DateTimeOffset lastErrorLogAt = DateTimeOffset.MinValue;

        while (true)
        {
            Thread.Sleep(PollInterval);

            string currentTopology;
            try
            {
                currentTopology = DisplayTopology.CreateFingerprint(displayConfiguration.GetActiveDisplays());
            }
            catch (Exception exception)
            {
                tracker.ResetCandidate();
                LogTopologyQueryError(exception, ref lastQueryError, ref lastErrorLogAt);
                continue;
            }

            if (lastQueryError is not null)
            {
                Console.WriteLine("La consulta de pantallas volvió a funcionar.");
                lastQueryError = null;
            }

            if (!tracker.Observe(currentTopology))
                continue;

            Console.WriteLine("Cambio de pantallas detectado y estable; ejecutando ciclo de refresco.");
            RunCycleAndReportFailure("El ciclo");
        }
    }

    private string GetInitialTopology()
    {
        while (true)
        {
            try
            {
                return DisplayTopology.CreateFingerprint(displayConfiguration.GetActiveDisplays());
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"No se pudo consultar la configuración inicial de pantallas; se reintentará: {exception.Message}");
                Thread.Sleep(PollInterval);
            }
        }
    }

    private void LogTopologyQueryError(
        Exception exception,
        ref string? lastError,
        ref DateTimeOffset lastLoggedAt)
    {
        string message = exception.Message;
        DateTimeOffset now = DateTimeOffset.Now;
        if (message != lastError || now - lastLoggedAt >= ErrorLogInterval)
        {
            Console.Error.WriteLine(
                $"No se pudo consultar la configuración de pantallas; se reintentará: {message}");
            lastError = message;
            lastLoggedAt = now;
        }
    }

    private void RunCycleAndReportFailure(string operation)
    {
        DisplayRefreshResult result = scaleRefresher.Run(runCycle: true);
        foreach (string message in result.Output)
            Console.WriteLine(message);

        foreach (string error in result.Errors)
            Console.Error.WriteLine(error);

        if (result.ExitCode != 0)
            Console.Error.WriteLine($"{operation} terminó con código {result.ExitCode}; la vigilancia continuará.");
    }
}