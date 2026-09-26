using DPIDisplayScaleFix.Core;

internal static class Program
{
    private const int InvalidArgumentsExitCode = 64;
    private const string Usage = "Uso: AsusDpiProbe [--cycle | --watch]";

    public static int Main(string[] args)
    {
        CommandLineResult command = CommandLineParser.Parse(args);
        if (!command.IsValid)
        {
            Console.Error.WriteLine(command.Error);
            Console.Error.WriteLine(Usage);
            return InvalidArgumentsExitCode;
        }

        var displayConfiguration = new DisplayConfigurationService();
        var refresher = new DisplayScaleRefresher(displayConfiguration);

        try
        {
            if (command.Mode == AppMode.Watch)
                return new DisplayWatcher(displayConfiguration, refresher).Run();

            DisplayRefreshResult result = refresher.Run(command.Mode == AppMode.Cycle);
            Print(result);
            return result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Print(DisplayRefreshResult result)
    {
        foreach (string message in result.Output)
            Console.WriteLine(message);

        foreach (string error in result.Errors)
            Console.Error.WriteLine(error);
    }
}