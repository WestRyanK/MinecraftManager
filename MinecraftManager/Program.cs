using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MinecraftManager;
internal class Program
{
    private static readonly string _ServerDirectoryDefault = Path.Combine("C:", "Program Files", "minecraft_servers", "lee_mindcrap_server");
    private static readonly TimeSpan _ShutdownCountdownDefault = TimeSpan.FromSeconds(10);

    private static StreamWriter? _serverInput;

    async static Task Main(string[] args)
    {
        Process serverProcess = StartMinecraftServer(_ServerDirectoryDefault);
        await ProcessServerOutputAsync(serverProcess, _ShutdownCountdownDefault);
    }

    private static Process StartMinecraftServer(string serverDirectory)
    {
        Process serverProcess = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "java",
                Arguments = "-jar server.jar --nogui",
                WorkingDirectory = serverDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            }
        };
        serverProcess.Start();
        _serverInput = serverProcess.StandardInput;
        return serverProcess;
    }

    private static async Task ProcessServerOutputAsync(Process serverProcess, TimeSpan shutdownCountdown)
    {
        CancellationTokenSource? shutdownCancel = null;

        while (!serverProcess.StandardOutput.EndOfStream)
        {
            string? input = await serverProcess.StandardOutput.ReadLineAsync();
            Console.WriteLine(input);
            if (Line.TryParse(input, out Line line))
            {
                if (line.Command.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    TryStartShutdownCountdown(shutdownCountdown, ref shutdownCancel);
                }
                else if (line.Command.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    TryAbortShutdown(ref shutdownCancel);
                }
            }
        }
    }

    private static bool TryStartShutdownCountdown(TimeSpan shutdownCountdown, ref CancellationTokenSource? shutdownCancel)
    {
        try
        {
            if (shutdownCancel != null)
            {
                WriteMessage("Already shutting down");
                return false;
            }
            shutdownCancel = new CancellationTokenSource();
            CancellationToken token = shutdownCancel.Token;
            Task.Run(() => CountdownShutdown(shutdownCountdown, token));
        }
        catch (Exception e)
        {
            WriteMessage(e.ToString());
        }
        return true;
    }

    private static async Task CountdownShutdown(TimeSpan shutdownCountdown, CancellationToken cancel)
    {
        WriteMessage($"Shutting down in {shutdownCountdown.TotalSeconds} seconds");
        WriteMessage($"Type 'cancel' to abort shutdown");
        await WriteCountdown(shutdownCountdown, cancel);
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        WriteMessage("Shut down started");
    }

    private static async Task WriteCountdown(TimeSpan totalTime, CancellationToken cancel) {
        int seconds = (int)Math.Ceiling(totalTime.TotalSeconds);
        for (int i = seconds; i > 0; i--)
        {
            WriteMessage($"{i}");
            await Task.Delay(TimeSpan.FromSeconds(1), cancel);
            if (cancel.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static bool TryAbortShutdown(ref CancellationTokenSource? shutdownCancel)
    {
        if (shutdownCancel == null)
        {
            WriteMessage("Nothing to abort");
            return false;
        }

        WriteMessage("Aborting shutdown");
        shutdownCancel.Cancel();
        shutdownCancel = null;
        return true;
    }

    private static void WriteMessage(string message)
    {
        WriteToServer($"/say {message}");
    }

    private static void WriteToServer(string command)
    {
        if (_serverInput != null)
        {
            _serverInput.WriteLine(command);
        }
        else
        {
            Console.WriteLine($"Error writing: '{command}'");
        }
    }
}

internal struct Line
{
    private const string _TimeGroup = "Time";
    private const string _TimeExpression = $"\\[(?<{_TimeGroup}>\\d+:\\d+:\\d+)\\]";
    private const string _LevelGroup = "Level";
    private const string _LevelExpression = $"\\[Server thread/(?<{_LevelGroup}>.*)\\]:";
    private const string _PlayerGroup = "Player";
    private const string _PlayerExpression = $"<(?<{_PlayerGroup}>.+)>";
    private const string _CommandGroup = "Command";
    private const string _CommandExpression = $"(?<{_CommandGroup}>.+)";
    private const string _Expression = $"^{_TimeExpression} {_LevelExpression} {_PlayerExpression} {_CommandExpression}$";
    private static readonly Regex _Regex = new Regex(_Expression, RegexOptions.Compiled);

    public DateTime Time;
    public LogLevel Level;
    public string Player;
    public string Command;

    public static bool TryParse(string? input, out Line line)
    {
        if (input == null)
        {
            line = default;
            return false;
        }

        Match match = _Regex.Match(input);
        if (!match.Success ||
            !DateTime.TryParse(match.Groups[_TimeGroup].Value, out DateTime time) ||
            !Enum.TryParse(match.Groups[_LevelGroup].Value?.ToUpper(), true, out LogLevel level) ||
            match.Groups[_PlayerGroup].Value is not string player ||
            match.Groups[_CommandGroup].Value is not string command)
        {
            line = default;
            return false;
        }

        line = new Line()
        {
            Time = time,
            Level = level,
            Player = player,
            Command = command,
        };
        return true;
    }
}

internal enum LogLevel
{
    Trace,
    Debug,
    Info,
    Error,
    Fatal,
}