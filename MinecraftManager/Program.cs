using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MinecraftManager;
internal class Program
{
    private static readonly string _ServerDirectoryDefault = Path.Combine("C:", "Program Files", "minecraft_servers", "lee_mindcrap_server");
    private static readonly TimeSpan _ShutdownCountdownDefault = TimeSpan.FromSeconds(30);

    private static Process? _serverProcess;
    private static StreamWriter? _serverInput;
    private static bool _isShutdownEnabled = false;

    async static Task Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _serverProcess = StartMinecraftServer(_ServerDirectoryDefault);
        if (_serverProcess != null)
        {
            _ = Task.Run(() => ProcessServerInputAsync(_serverProcess));
            bool playerShutdown = await ProcessServerOutputAsync(_serverProcess, _ShutdownCountdownDefault);

            if (playerShutdown && _isShutdownEnabled)
            {
                ShutdownComputer();
            }
        }
    }

    private static void ProcessServerInputAsync(Process serverProcess)
    {
        try
        {
            while (!serverProcess.HasExited)
            {
                string? input = Console.ReadLine();
                if (input != null)
                {
                    if (input.Equals("enable shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        _isShutdownEnabled = true;
                        WriteMessage("Shutdown has been enabled");
                        WriteMessage("Type 'shutdown' or 'cancel' to control the server");
                    }
                    else if (input.Equals("disable shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        _isShutdownEnabled = false;
                        WriteMessage("Shutdown has been disabled");
                    }
                    else
                    {
                        WriteToServer(input);
                    }
                }
            }
        }
        catch (Exception e)
        {
            WriteError("Error processing input", e);
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Console.WriteLine("Process Exiting");
        WriteStop();
        if (_serverProcess != null)
        {
            _serverProcess.WaitForExit();
            Console.WriteLine("Server shutdown successfully");
        }
        else
        {
            Console.WriteLine("Server process doesn't exist");
        }
    }

    private static Process StartMinecraftServer(string serverDirectory)
    {
        Process serverProcess = new()
        {
            StartInfo = new ProcessStartInfo("java", "-jar server.jar --nogui")
            {
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

    private static async Task<bool> ProcessServerOutputAsync(Process serverProcess, TimeSpan shutdownCountdown)
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
                    TryStartServerShutdown(shutdownCountdown, serverProcess, _isShutdownEnabled, ref shutdownCancel);
                }
                else if (line.Command.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    TryAbortShutdown(_isShutdownEnabled, ref shutdownCancel);
                }
            }
        }
        return shutdownCancel != null;
    }

    private static bool TryStartServerShutdown(TimeSpan shutdownCountdown, Process serverProcess, bool isShutdownEnabled, ref CancellationTokenSource? shutdownCancel)
    {
        try
        {
            if (!isShutdownEnabled)
            {
                WriteMessage("Doing nothing. Shutdown is disabled");
                return false;
            }
            if (shutdownCancel != null)
            {
                WriteMessage("Already shutting down");
                return false;
            }
            shutdownCancel = new CancellationTokenSource();
            CancellationToken token = shutdownCancel.Token;
            Task.Run(() => ShutdownServer(shutdownCountdown, serverProcess, token));
        }
        catch (Exception e)
        {
            WriteError("Error shutting down server", e);
            WriteMessage(e.ToString());
        }
        return true;
    }

    private static async Task ShutdownServer(TimeSpan shutdownCountdown, Process serverProcess, CancellationToken cancel)
    {
        WriteMessage($"Shutting down in {shutdownCountdown.TotalSeconds} seconds");
        WriteMessage($"Type 'cancel' to abort shutdown");
        await WriteCountdown(shutdownCountdown, cancel);
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        WriteMessage("Server stopping...");
        WriteStop();
        await serverProcess.WaitForExitAsync();
        Console.WriteLine("Server stopped successfully");
    }

    private static void ShutdownComputer() {
        Process shutdownProcess = new()
        {
            StartInfo = new ProcessStartInfo("shutdown", "/s /t 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        shutdownProcess.Start();
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

    private static bool TryAbortShutdown(bool isShutdownEnabled, ref CancellationTokenSource? shutdownCancel)
    {
        if (!isShutdownEnabled)
        {
            WriteMessage("Doing nothing. Shutdown is disabled");
            return false;
        }
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

    private static void WriteStop() => WriteToServer("stop");
    private static void WriteMessage(string message) => WriteToServer($"/say {message}");
    private static void WriteError(string error, Exception e) {
        WriteMessage($"{error}: {e}");
    }

    private static void WriteToServer(string command)
    {
        Console.WriteLine($"Write: '{command}'");
        if (_serverInput != null)
        {
            _serverInput.WriteLine(command);
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