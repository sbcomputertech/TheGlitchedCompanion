using Serilog;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TGC.Server;

public class ServerManager
{
    private Thread _thread;
    private List<PlayerInfo> _players;
    private HttpListener _listener;
    private int _basePort;
    private int _requestIndex = -1;

    private CancellationToken _cancellationToken;

    public ServerManager(int maxPlayers, int basePort, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        _players = new List<PlayerInfo>(maxPlayers);
        _basePort = basePort;

        var prefix = "http://+:" + _basePort + "/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        Log.Information("Started HTTP listen server using prefix {Prefix} (bp: {BasePort})", prefix, _basePort);

        _thread = new Thread(ThreadProc);
        _thread.Start();
        Log.Information("Started listener processing thread (mtid: {Id})", _thread.ManagedThreadId);
    }

    private void ThreadProc()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            var ctxTask = _listener.GetContextAsync();
            try
            {
                ctxTask.Wait(_cancellationToken);
            } 
            catch(OperationCanceledException)
            {
                Log.Information("ServerManager recieved cancellation while waiting for request context. Shutting down");
                return;
            }
            var ctx = ctxTask.Result;

            _requestIndex++;
            var req = ctx.Request;
            var res = ctx.Response;

            var reqBodyStream = req.InputStream;
            var reqBuffer = new byte[req.ContentLength64];
            reqBodyStream.Read(reqBuffer);
            reqBodyStream.Close();
            var reqBody = Encoding.UTF8.GetString(reqBuffer);

            Log.Verbose("Recieved request #{Index} from {Client}. Payload: {Data}", _requestIndex, req.RemoteEndPoint, reqBody);

            JsonObject deserialisedRequest;
            try
            {
                deserialisedRequest = JsonSerializer.Deserialize<JsonObject>(reqBody)
                    ?? throw new Exception("Object was null after deserializing json body");
            } 
            catch (Exception ex)
            {
                Log.Error("An exception {ExType} occured while parsing HTTP request #{Index}. Message: {ExMessage}", ex.GetType().FullName, _requestIndex, ex.Message);
                goto FinishRequest;
            }

            var type = deserialisedRequest["t"].ToString();
            var ok = HandlePacket(type, deserialisedRequest, req.RemoteEndPoint, out var responseString);
            var response = new Dictionary<string, object>
            {
                ["k"] = ok,
                ["r"] = responseString,
                ["t"] = DateTimeOffset.Now
            };
            var responseJson = JsonSerializer.Serialize(response);
            var responseBuffer = Encoding.UTF8.GetBytes(responseJson);
            res.OutputStream.Write(responseBuffer);

            Log.Verbose("Request #{Index} type: {Type} was processed {Status}", _requestIndex, type, ok ? "Successfully" : "Unsuccessfully");

        FinishRequest:
            res.OutputStream.Close();
        }

        _listener.Close();
    }

    private bool HandlePacket(string type, JsonObject requestData, IPEndPoint remote, out object response)
    {

        switch(type)
        {
            case "ping":
                response = "ack";
                return true;
            case "status":
                response = RespondToStatusRequest();
                return true;
            case "admin":
                return HandleAdminCommand(requestData["a"].ToString(), requestData["c"].ToString(), requestData["p"].AsArray(), remote, out response);
            case "player":
                return HandlePlayerCommand(requestData, remote, out response);
            default:
                response = "Unknown packet type '" + type + "'!";
                return false;
        }
    }

    private bool HandleAdminCommand(string auth, string command, JsonArray args, IPEndPoint client, out object responseMessage)
    {
        if(!DotEnv.IsSet(DotEnv.RemoteAdminAuthKey))
        {
            Log.Error("Attempted to run a server admin command while no auth key was specified in .env");
            responseMessage = "No authentication has been set up for remote server admin";
            return false;
        }

        if(DotEnv.Get(DotEnv.RemoteAdminAuthKey) != auth)
        {
            Log.Warning("Client at {ClientAddress} attempted to run a server admin command, but failed authentication", client);
            responseMessage = "Failed to validate authentication key. This request has been logged";
            return false;
        }

        switch(command)
        {
            case "panic":
                {
                    var time = 5;
                    responseMessage = string.Format("The server will perform a panic shutdown in {0} seconds", time);
                    Log.Fatal("A panic quit has been triggered in {Seconds} seconds by client {Client}", time, client);

                    var timer = new Timer((object? _) => Environment.Exit(1));
                    timer.Change(time * 1000, Timeout.Infinite);

                    return true;
                }
            case "shutdown":
                {
                    var time = 10;
                    if(args.Count > 0)
                    {
                        var timeArgValid = int.TryParse(args[0].ToString(), out var timeArg);
                        if(!timeArgValid)
                        {
                            responseMessage = string.Format("'{0}' is not a valid integer for parameter 'time'!", timeArg);
                            return false;
                        }
                        time = timeArg;
                    }

                    responseMessage = string.Format("The server will perform a controlled shutdown in {0} seconds", time);
                    Log.Fatal("A controlled shutdown has been triggered in {Seconds} seconds by client {Client}", time, client);

                    var timer = new Timer((object? _) => Program.Stop());
                    timer.Change(time * 1000, Timeout.Infinite);

                    return true;
                }
            default:
                responseMessage = string.Format("Unknown command '{0} [{1}]'!", command, string.Join(", ", args));
                return false;
        }
    }

    private bool HandlePlayerCommand(JsonObject data, IPEndPoint client, out object response)
    {
        var action = data["action"].ToString();
        switch (action)
        {
            case "join":
                return HandlePlayerJoin(data, client, out response);
            case "list":
                response = _players;
                return true;
            case "leave":
            default:
                response = "Unrecognised action: " + action;
                return false;
        }
    }

    private bool HandlePlayerJoin(JsonObject data, IPEndPoint client, out object response)
    {
        if(_players.Any(p => p.Name == data["name"].ToString()))
        {
            response = "A player already exists with that name";
            return false;
        }

        var info = new PlayerInfo
        {
            Name = data["name"].ToString(),
            JoinedAt = DateTimeOffset.Now,
            Guid = Guid.NewGuid(),
            IP = client.ToString(),
            ClientVersion = data["clientVersion"].ToString(),
            SocketPort = _basePort + 1 + _players.Count
        };
        info.Connection = new PlayerConnection(info, _cancellationToken);

        _players.Add(info);
        response = info;
        Log.Information("Player #{Index} '{Name}' ({IP}) has joined from client v{Version}. Guid: {Guid}", _players.Count, info.Name, info.IP, info.ClientVersion, info.Guid);
        return true;
    }

    private Dictionary<string,object> RespondToStatusRequest()
    {
        var statusResponse = new Dictionary<string, object>
        {
            ["inGame"] = Program.InGame,
            ["uptime"] = Program.Uptime
        };

        if(Program.InGame)
        {

        } else
        {
            statusResponse["currentPlayers"] = _players.Count;
        }

        return statusResponse;
    }
}