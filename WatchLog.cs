using System.Text;

internal sealed class WatchLog : IDisposable
{
    private const long MaximumLogSize = 2 * 1024 * 1024;
    private readonly StreamWriter _writer;
    private readonly TextWriter _originalOutput;
    private readonly TextWriter _originalError;
    private readonly object _sync = new();

    private WatchLog(string path)
    {
        Path = path;
        _originalOutput = Console.Out;
        _originalError = Console.Error;
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
        Console.SetOut(new TeeTextWriter(_originalOutput, _writer, _sync));
        Console.SetError(new TeeTextWriter(_originalError, _writer, _sync));
    }

    public string Path { get; }

    public static WatchLog Start()
    {
        string directory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DPIDisplayScaleFix");
        Directory.CreateDirectory(directory);

        string path = System.IO.Path.Combine(directory, "watch.log");
        if (File.Exists(path) && new FileInfo(path).Length > MaximumLogSize)
            File.Move(path, path + ".1", overwrite: true);

        return new WatchLog(path);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        Console.SetError(_originalError);
        _writer.Dispose();
    }

    private sealed class TeeTextWriter(TextWriter console, TextWriter log, object sync) : TextWriter
    {
        public override Encoding Encoding => console.Encoding;

        public override void Write(char value)
        {
            lock (sync)
            {
                console.Write(value);
                log.Write(value);
            }
        }

        public override void Write(string? value)
        {
            if (value is null)
                return;

            lock (sync)
            {
                console.Write(value);
                log.Write(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (sync)
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {value}";
                console.WriteLine(line);
                log.WriteLine(line);
                log.Flush();
            }
        }

        public override void WriteLine()
        {
            lock (sync)
            {
                console.WriteLine();
                log.WriteLine();
                log.Flush();
            }
        }

        public override void Flush()
        {
            lock (sync)
            {
                console.Flush();
                log.Flush();
            }
        }
    }
}