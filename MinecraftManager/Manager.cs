using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MinecraftManager;
internal class Manager
{
    public const string _ShutdownCommand = "!shutdown";
    public const string _CancelCommand = "!cancel";
    public const string _EnableShutdownCommand = "enable shutdown";
    public const string _DisableShutdownCommand = "disable shutdown";

    private string _serverPath;
    private bool _isShutdownEnabled;
    private TimeSpan _shutdownDelay;
    private Process? _serverProcess;

    public Manager(string serverPath, bool isShutdownEnabled, TimeSpan shutdownDelay)
    {
        _serverPath = serverPath;
        _isShutdownEnabled = isShutdownEnabled;
        _shutdownDelay = shutdownDelay;
    }

    public async Task Run()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        _serverProcess = StartMinecraftServer();
        if (_serverProcess != null)
        {
            _ = Task.Run(() => ProcessServerInputAsync(_serverProcess));
            bool playerShutdown = await ProcessServerOutputAsync(_serverProcess);

            if (playerShutdown && _isShutdownEnabled)
            {
                ShutdownComputer();
            }
        }
    }

    private void ProcessServerInputAsync(Process serverProcess)
    {
        try
        {
            while (!serverProcess.HasExited)
            {
                string? input = Console.ReadLine();
                if (input != null)
                {
                    if (input.Equals(_EnableShutdownCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        _isShutdownEnabled = true;
                        WriteMessage("Shutdown has been enabled");
                        WriteMessage($"Type '{_ShutdownCommand}' or '{_CancelCommand}' to control the server");
                    }
                    else if (input.Equals(_DisableShutdownCommand, StringComparison.OrdinalIgnoreCase))
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

    private void OnProcessExit(object? sender, EventArgs e)
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

    private Process StartMinecraftServer()
    {
        string serverDirectory = Path.GetDirectoryName(_serverPath) ?? string.Empty;
        string serverFileName = Path.GetFileName(_serverPath);

        Process serverProcess = new()
        {
            StartInfo = new ProcessStartInfo("java", $"-jar {serverFileName} --nogui")
            {
                WorkingDirectory = serverDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            }
        };
        serverProcess.Start();
        return serverProcess;
    }

    private async Task<bool> ProcessServerOutputAsync(Process serverProcess)
    {
        CancellationTokenSource? shutdownCancel = null;

        while (!serverProcess.StandardOutput.EndOfStream)
        {
            string? input = await serverProcess.StandardOutput.ReadLineAsync();
            Console.WriteLine(input);
            if (Line.TryParse(input, out Line line))
            {
                if (line.Command.Equals(_ShutdownCommand, StringComparison.OrdinalIgnoreCase))
                {
                    TryStartServerShutdown(serverProcess, _isShutdownEnabled, ref shutdownCancel);
                }
                else if (line.Command.Equals(_CancelCommand, StringComparison.OrdinalIgnoreCase))
                {
                    TryAbortShutdown(_isShutdownEnabled, ref shutdownCancel);
                }
            }
        }
        return shutdownCancel != null;
    }

    private bool TryStartServerShutdown(Process serverProcess, bool isShutdownEnabled, ref CancellationTokenSource? shutdownCancel)
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
            Task.Run(() => ShutdownServer(serverProcess, token));
        }
        catch (Exception e)
        {
            WriteError("Error shutting down server", e);
            WriteMessage(e.ToString());
        }
        return true;
    }

    private async Task ShutdownServer(Process serverProcess, CancellationToken cancel)
    {
        WriteMessage($"Shutting down in {_shutdownDelay.TotalSeconds} seconds");
        WriteMessage($"Type '{_CancelCommand}' to abort shutdown");
        await WriteCountdown(_shutdownDelay, cancel);
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        WriteMessage("Server stopping...");
        WriteStop();
        await serverProcess.WaitForExitAsync();
        Console.WriteLine("Server stopped successfully");
    }

    private void ShutdownComputer()
    {
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

    private async Task WriteCountdown(TimeSpan totalTime, CancellationToken cancel)
    {
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

    private bool TryAbortShutdown(bool isShutdownEnabled, ref CancellationTokenSource? shutdownCancel)
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

    private void WriteStop() => WriteToServer("stop");
    private void WriteMessage(string message) => WriteToServer($"/say {message}");
    private void WriteError(string error, Exception e) => WriteMessage($"{error}: {e}");
    private void WriteToServer(string command)
    {
        Console.WriteLine($"Write: '{command}'");
        _serverProcess?.StandardInput?.WriteLine(command);
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
