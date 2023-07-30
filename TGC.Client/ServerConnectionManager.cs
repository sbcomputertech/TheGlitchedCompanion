using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using TGC.Client.StrongTypedPackets;
using UnityEngine;
using Websocket.Client;

namespace TGC.Client;

public class ServerConnectionManager
{
    private string _serverIp;
    private int _serverPort;
    private HttpClient _baseClient;
    private PlayerInfo _info;
    private WebsocketClient _ws;
    public Queue<string> WsMessageQueue;

    public bool IsServerReachable { get; private set; }

    public ServerConnectionManager(string serverIp, int serverPort)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;

        _baseClient = new HttpClient();
        _baseClient.BaseAddress = new Uri($"http://{_serverIp}:{_serverPort}/");
        WsMessageQueue = new Queue<string>();
    }

    public bool TryPing()
    {
        try
        {
            var pingResponseJson = _baseClient.PostEmpty("ping");
            var pingResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(pingResponseJson);
            IsServerReachable = (bool)pingResponse["k"];
            ModMain.LogSource.LogInfo("Ping request made, response: " + IsServerReachable);
            return IsServerReachable;
        }
        catch (Exception ex)
        {
            ModMain.LogSource.LogError($"Unable to ping server: {ex.GetType().FullName} - {ex.Message}");
            return false;
        }
    }

    public async Task<(bool, string)> JoinGame()
    {
        var joinRequest = new Dictionary<string, object>
        {
            ["t"] = "player",
            ["action"] = "join",
            ["name"] = ConfigManager.PlayerName.Value,
            ["clientVersion"] = Application.version
        };
        var joinResponse = _baseClient.Post(joinRequest);
        var resp = JObject.Parse(joinResponse);

        if (!resp.Value<bool>("k"))
        {
            var msg = resp.Value<string>("r");
            ModMain.LogSource.LogError("Join request to server responded with not ok! Message: " + msg);
            return (false, msg);
        }
        ModMain.LogSource.LogInfo("Join request to server succeeded!");

        _info = resp.GetValue("r")?.ToObject<PlayerInfo>() ?? throw new Exception("Player info was null!");
        ModMain.LogSource.LogInfo("Playerinfo: " + JsonConvert.SerializeObject(_info));

        var uri = new Uri($"ws://{_serverIp}:{_info.SocketPort}/");
        ModMain.LogSource.LogInfo("Starting connection to uri: " + uri);
        _ws = new WebsocketClient(uri);
        // _ws.NativeClient.Options.AddSubProtocol("tgc.ws.gamesync"); //TODO: fix

        _ws.ReconnectTimeout = TimeSpan.FromSeconds(30);
        _ws.ReconnectionHappened.Subscribe(info => ModMain.LogSource.LogWarning($"WS Reconnection happened, type: {info.Type}"));
        _ws.MessageReceived.Subscribe(OnWsMessage);
        await _ws.StartOrFail();
        ModMain.LogSource.LogInfo("WS started!");

        var handshakeOk = DoHandshakeNegotiate();
        return (handshakeOk, handshakeOk ? "OK" : "Handshake failed!");
    }

    private bool DoHandshakeNegotiate()
    {
        ModMain.LogSource.LogInfo("Starting handshake negotiation");


        var startPacketJson = BlockForNewWsMessage();
        var startPacket = JsonConvert.DeserializeObject<HandshakeStartIncoming>(startPacketJson);
        var token = startPacket.token.Value;
        var guid = _info.Guid.ToString();
        
        ModMain.LogSource.LogInfo($"Generating sig with GUID: '{guid}' and Token: '{token}'.");

        var bytesToHash = Encoding.UTF8.GetBytes(guid + token);
        using var md5 = MD5.Create();
        var signature = Convert.ToBase64String(md5.ComputeHash(bytesToHash));
        ModMain.LogSource.LogInfo("Generated handshake signature: " + signature);

        var packet = new HandshakeSendSignaturePacket
        {
            t = 1,
            s = signature
        };
        _ws.Send(JsonConvert.SerializeObject(packet));
            
        var handshakeResponse = JsonConvert.DeserializeObject<EmptyPacketWithType>(BlockForNewWsMessage());
        if (handshakeResponse.Type == WsMessageType.ConfirmHandshake_S2C)
        {
            ModMain.LogSource.LogInfo("WS handshake succeeded!");
            return true;
        }
        ModMain.LogSource.LogError("WS handshake failed!");
        return false;
    }
    
    public string BlockForNewWsMessage() {
        while(WsMessageQueue.Count < 1) {}
        return WsMessageQueue.Dequeue();
    }

    private void OnWsMessage(ResponseMessage msg)
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (msg.MessageType)
        {
            case WebSocketMessageType.Text:
                WsMessageQueue.Enqueue(msg.Text);
                break;
            case WebSocketMessageType.Close:
                ModMain.LogSource.LogInfo("WS close was sent!");
                break;
            default:
                ModMain.LogSource.LogWarning("Recieved unsupported WS message type!");
                break;
        }
    }

    public void SendWs(WsMessageType type, [CanBeNull] object data = null)
    {
        var jo = new JObject
        {
            ["t"] = (int)type
        };
        if (data != null)
        {
            jo.Add(data);
        }
        _ws.Send(JsonConvert.SerializeObject(jo));
    }
}