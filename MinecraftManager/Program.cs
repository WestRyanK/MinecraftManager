using Microsoft.Extensions.Configuration;


namespace MinecraftManager;
internal class Program
{
    private static readonly string _ServerPathDefault = Path.Combine("C:", "Program Files", "minecraft_servers", "lee_mindcrap_server", "server.jar");
    private static readonly TimeSpan _ShutdownDelayDefault = TimeSpan.FromSeconds(30);
    private const bool _IsShutdownEnabledDefault = false;

    private const string _ServerPathArg = "ServerPath";
    private const string _IsShutdownEnabledArg = "IsShutdownEnabled";
    private const string _ShutdownDelayArg = "ShutdownDelay";

    async static Task Main(string[] args)
    {
        ConfigurationBuilder builder = new();
        builder.AddCommandLine(args);
        IConfigurationRoot? config = null;
        try
        {
            config = builder.Build();
        }
        catch (FormatException e)
        {
            Console.WriteLine($"Error parsing commandline arguments: {e}");
            Console.WriteLine($"Valid arguments are: {_ServerPathArg}=\"path_to_server.jar\" {_ShutdownDelayArg}=seconds_number {_IsShutdownEnabledArg}=true_or_false");
            return;
        }

        string serverPath = config[_ServerPathArg] ?? _ServerPathDefault;
        bool isShutdownEnabled = bool.TryParse(config[_IsShutdownEnabledArg], out bool isEnabled) ? isEnabled : _IsShutdownEnabledDefault;
        TimeSpan shutdownDelay = double.TryParse(config[_ShutdownDelayArg], out double delaySeconds) ? TimeSpan.FromSeconds(delaySeconds) : _ShutdownDelayDefault;
        Console.WriteLine($"{_ServerPathArg}: '{serverPath}'");
        Console.WriteLine($"{_IsShutdownEnabledArg}: '{isShutdownEnabled}'");
        Console.WriteLine($"{_ShutdownDelayArg}: {shutdownDelay.TotalSeconds} seconds");
        Console.WriteLine();
        Console.WriteLine($"Type '{Manager._EnableShutdownCommand}' or '{Manager._DisableShutdownCommand}' to allow/disallow players shutting down server");
        Console.WriteLine("Type 'stop' to shutdown the server");
        Console.WriteLine("You can also issue any normal server commands from this window");
        Console.WriteLine();

        Manager minecraftManager = new(serverPath, isShutdownEnabled, shutdownDelay);
        await minecraftManager.Run();
    }
}
