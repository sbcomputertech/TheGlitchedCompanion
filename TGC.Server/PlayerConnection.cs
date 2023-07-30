using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TGC.Client;

namespace TGC.Server;

public class PlayerConnection
{
    private PlayerInfo _info;
    private HttpListener _listener;
    private HttpListenerContext _httpContext;
    private WebSocketContext _wsContext;
    private WebSocket _ws;
    private Thread _establishConnectionThread;
    private CancellationToken _cancellationToken;
    private Token _playerToken;
    private bool _closeSent;
    private ConcurrentQueue<JsonObject> _sendToSocketQueue;
    private int _socketPort => _info.SocketPort;

    public PlayerConnection(PlayerInfo info, CancellationToken cancellationToken)
    {
        _info = info;
        _cancellationToken = cancellationToken;
        _playerToken = Token.Generate(32);
        _sendToSocketQueue = new ConcurrentQueue<JsonObject>();

        var prefix = "http://+:" + _socketPort + "/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        Log.Information("Created HttpListener for player {Name} with prefix {Prefix}", info.Name, prefix);

        _establishConnectionThread = new Thread(EstablishConnectionThreadProc);
        _establishConnectionThread.Start();
    }

    private void ConnectionEstablished()
    {
        Log.Verbose("WS connection on port {Port} established", _socketPort);
        Task.Run(RecieverProc);
    }

    private void EstablishConnectionThreadProc()
    {
        while (true)
        {
            _httpContext = _listener.GetContext();
            if (!_httpContext.Request.IsWebSocketRequest)
            {
                Log.Verbose("A connection was made to the socket on port {Port} that was not WS! Ignoring", _socketPort);
                _httpContext.Response.Close();
                continue;
            }

            string? proto = null; //"tgc.ws.gamesync";
            _wsContext = _httpContext.AcceptWebSocketAsync(proto).Result;
            _ws = _wsContext.WebSocket;
            ConnectionEstablished();
            break;
        }
    }

    private async Task SenderProc()
    {
        while (!_cancellationToken.IsCancellationRequested && !_closeSent)
        {
            if(!_sendToSocketQueue.TryDequeue(out var obj))
            {
                continue;
            }
            var json = JsonSerializer.Serialize(obj);
            await WsSend(json);
        }
    }

    public void QueueForSend(WsMessageType type, JsonObject data)
    {
        var obj = new JsonObject
        {
            ["t"] = (int)type,
            ["data"] = data
        };
        _sendToSocketQueue.Enqueue(obj);
    }

    private async Task RecieverProc()
    {
        // HANDSHAKE
        bool handshakeComplete = false;
        var handshakeFails = 0;
        while(!handshakeComplete)
        {
            if(handshakeFails >= 5)
            {
                Log.Error("Player {Name} experienced 5 or more handshake fails! Disconnecting them", _info.Name);
                await  _ws.CloseAsync(WebSocketCloseStatus.ProtocolError, "Too many handshake fails!", _cancellationToken);
                return;
            }
            
            Log.Verbose("Starting handshake attempt {AttemptNum} for client {ClientName}", handshakeFails + 1, _info.Name);

            var handshakeObj = new Dictionary<string, object>();
            handshakeObj["t"] = WsMessageType.StartHandshake_S2C;
            handshakeObj["token"] = _playerToken;
            await WsSend(JsonSerializer.Serialize(handshakeObj));
            
            Log.Verbose("Sent token packet to client {Client}. Token: {Token}", _info.Name, _playerToken.Value);

            var msg = await WsRecieve();
            JsonObject msgObject;
            try
            {
                msgObject = JsonSerializer.Deserialize<JsonObject>(msg) ?? throw new Exception("Object was null after json deserialise");
            } catch (Exception ex)
            {
                Log.Warning("An exception {ExType} occured while processing message from player {Player}. Bad json? Exception message: {ExMsg}", ex.GetType().FullName, _info.Name, ex.Message);
                handshakeFails++;
                continue;
            }

            var type = msgObject["t"].Deserialize<WsMessageType>();
            if(type == WsMessageType.RespondToHandshake_C2S)
            {
                var sig = msgObject["s"]?.ToString();
                var expectedSig = GetSignature();
                if(sig != expectedSig)
                {
                    Log.Verbose("Player {Name} sent handshake response with invalid signature! Expected '{Sig}' Restarting handshake", _info.Name, expectedSig);
                    handshakeFails++;
                    continue;
                }
                Log.Verbose("Player {Name} finished handshake successfully! Signature is '{Sig}'", _info.Name, sig);
                await WsSendEmpty(WsMessageType.ConfirmHandshake_S2C);
                handshakeComplete = true;
            }
        }

        Task.Run(SenderProc); // run the sending loop in paralell

        // MAIN HANDLER
        while (!_cancellationToken.IsCancellationRequested && !_closeSent)
        {
            var msgString = await WsRecieve();
            JsonObject msg;
            try
            {
                msg = JsonSerializer.Deserialize<JsonObject>(msgString) ?? throw new Exception("Object was null after json deserialise");
            }
            catch (Exception ex)
            {
                Log.Warning("An exception {ExType} occured while processing message from player {Player}. Bad json? Exception message: {ExMsg}", ex.GetType().FullName, _info.Name, ex.Message);
                handshakeFails++;
                continue;
            }
            var type = msg["t"].Deserialize<WsMessageType>();
            var responseObject = new JsonObject();
            var ok = HandleWsPacket(type, msg, _httpContext.Request.RemoteEndPoint, out var responseType, responseObject);
            var responseData = new JsonObject
            {
                ["ok"] = ok,
                ["t"] = (int)responseType, // cast to get index in enum
                ["data"] = responseObject
            };
            var responseJson = JsonSerializer.Serialize(responseData);
            await WsSend(responseJson);
        }
    }

    private bool HandleWsPacket(WsMessageType type, JsonObject obj, IPEndPoint client, out WsMessageType responseType, JsonObject response)
    {
        Log.Verbose("Handling packet of type {Type} from client {Client}", type, client);
        switch(type)
        {
            // All S2C packets
            case WsMessageType.StartHandshake_S2C:
            case WsMessageType.ConfirmHandshake_S2C:
            case WsMessageType.PingResponse_S2C:
            case WsMessageType.RequestError_S2C:
                responseType = WsMessageType.RequestError_S2C;
                response["msg"] = "Client attempted to send S2C packet";
                return false;
            case WsMessageType.RespondToHandshake_C2S:
                responseType = WsMessageType.RequestError_S2C;
                response["msg"] = "Attempted to send handshake response after handshake has been finished";
                return false;
            case WsMessageType.Ping_C2S:
                responseType = WsMessageType.PingResponse_S2C;
                response["acknowledged"] = true;
                return true;
        }

        responseType = WsMessageType.RequestError_S2C;
        response["msg"] = "The server was unable to process the packet";
        return false;
    }

    private async Task<string> WsRecieve(int bufferSize = 128)
    {
        var message = "";
        while(true)
        {
            var buffer = new byte[bufferSize];
            var res = await _ws.ReceiveAsync(buffer, _cancellationToken);
            if(res.MessageType == WebSocketMessageType.Binary)
            {
                Log.Error("Binary message was recieved from player {Player} (p:{Port}). This is unsupported!", _info.Name, _socketPort);
                return "";
            }
            if(res.MessageType == WebSocketMessageType.Close)
            {
                Log.Warning("Player {Name} sent a WS close request! Disconnecting", _info.Name);
                _closeSent = true;
                return "";
            }
            var str = Encoding.UTF8.GetString(buffer);
            message += str;
            Log.Verbose("Player {Player} (p:{Port}) sent WS message fragment: {Fragment}", _info.Name, _socketPort, str);
            if(res.EndOfMessage)
            {
                break;
            }
        }

        var unpaddedMessage = "";
        for(var i = message.Length - 1; i >= 0; i--)
        {
            if (message[i] != 0x00)
            {
                unpaddedMessage = message.Substring(0, i + 1);
                break;
            }
        }
        return unpaddedMessage;
    }

    private async Task WsSend(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, _cancellationToken);
    }

    private async Task WsSendEmpty(WsMessageType type)
    {
        var json = new Dictionary<string, object>();
        json["t"] = type;
        var str = JsonSerializer.Serialize(json);
        await WsSend(str);
    }

    private string GetSignature()
    {
        var str = _info.Guid + _playerToken.Value;
        var bytes = Encoding.UTF8.GetBytes(str);
        var hashed = MD5.HashData(bytes);
        return Convert.ToBase64String(hashed);
    }
}