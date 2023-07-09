using Serilog;
using Serilog.Core;
using System.Text.Json;

namespace TGC.Server;

public class Program
{
    private int _basePort = 9943;
    private ServerManager _server;
    private DateTimeOffset _startedAt;

    private static bool _keepRunning = true;

    private CancellationTokenSource _serverCancellation;

    public static TimeSpan Uptime { get; private set; }
    public static bool InGame { get; private set; }

    public static void Main(string[] args) => new Program(args).Run();

    private Program(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();

        DotEnv.Load(".env");
        if(DotEnv.Get(DotEnv.RemoteAdminAuthKey) == "ExamplePassword_CHANGE_ME")
        {
            Log.Fatal("For security reasons, change the remote admin auth password in the .env file, or remove the entry to disable the feature");
            Environment.Exit(1);
        }

        Log.Information("Initializing TGC server");
        Log.Information("Working directory: {Cwd}", Directory.GetCurrentDirectory());

        if(DotEnv.IsSet(DotEnv.BasePort))
        {
            _basePort = DotEnv.GetInt(DotEnv.BasePort) ?? 0;
            Log.Information("Using port {BasePort} from .env", _basePort);
        }

        _startedAt = DateTimeOffset.Now;

        _serverCancellation = new CancellationTokenSource();
        _server = new ServerManager(2, _basePort, _serverCancellation.Token);
    }

    public static void Stop()
    {
        _keepRunning = false;
    }

    private void Run()
    {
        while(_keepRunning)
        {
            var uptimeMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _startedAt.ToUnixTimeMilliseconds();
            Uptime = TimeSpan.FromMilliseconds(uptimeMillis);
        }
        _serverCancellation.Cancel();
    }
}