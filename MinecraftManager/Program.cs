using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MinecraftManager;
internal class Program
{
    private static readonly string _DefaultServerDirectory = Path.Combine("C:", "Program Files", "minecraft_servers", "lee_mindcrap_server");
    async static Task Main(string[] args)
    {
        Process serverProcess = StartMinecraftServer(_DefaultServerDirectory);
        await ProcessServerOutputAsync(serverProcess);
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
        return serverProcess;
    }

    private static async Task ProcessServerOutputAsync(Process serverProcess)
    {
        while (!serverProcess.StandardOutput.EndOfStream)
        {
            string? input = await serverProcess.StandardOutput.ReadLineAsync();
            if (Line.TryParse(input, out Line line))
            {
            }
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