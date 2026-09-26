namespace DPIDisplayScaleFix.Core;

public enum AppMode
{
    Diagnostic,
    Cycle,
    Watch,
    Invalid
}

public readonly record struct CommandLineResult(AppMode Mode, string? Error)
{
    public bool IsValid => Mode != AppMode.Invalid;
}

public static class CommandLineParser
{
    public static CommandLineResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return new CommandLineResult(AppMode.Diagnostic, null);
        if (args.Count != 1)
            return Invalid("Usa solo una opción: --cycle o --watch.");

        return args[0].ToLowerInvariant() switch
        {
            "--cycle" => new CommandLineResult(AppMode.Cycle, null),
            "--watch" => new CommandLineResult(AppMode.Watch, null),
            _ => Invalid($"Opción desconocida: {args[0]}.")
        };
    }

    private static CommandLineResult Invalid(string error) => new(AppMode.Invalid, error);
}