using System;
using System.Net;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TGC.Client;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ModMain : BaseUnityPlugin
{
	public static ManualLogSource LogSource { get; private set; }

	private ServerConnectionManager _connectionManager;

	private void Awake()
	{
		LogSource = Logger;
		Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} ver {PluginInfo.PLUGIN_VERSION} loaded!");

		var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
		harmony.PatchAll();

		ConfigManager.Init(Config);
		_connectionManager = new ServerConnectionManager(ConfigManager.ServerIp.Value, ConfigManager.ServerPort.Value);
	}

	private void Start()
	{
		RegisterDevConsoleCommands();
	}

	private void Update()
	{
		// --Process incoming messages-- //
		var queueLen = _connectionManager.WsMessageQueue.Count;
		var amountToProcess = Math.Min(10, queueLen); // Process a max of 10 messages each frame
		for (var i = 0; i < amountToProcess; i++)
		{
			var msg = _connectionManager.WsMessageQueue.Dequeue();
			var obj = JsonConvert.DeserializeObject<JObject>(msg);
			ProcessWebsocketMessage(obj);
		}
	}

	private void ProcessWebsocketMessage(JObject messageData)
	{
		var type = messageData.Value<WsMessageType>("t");
		switch (type)
		{
			case WsMessageType.RequestError_S2C:
				LogSource.LogWarning("A websocket request error was sent! ");
				break;
			case WsMessageType.PingResponse_S2C:
				break;
			case WsMessageType.PeerPositions_S2C:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private void RegisterDevConsoleCommands()
	{
		DevConsoleInterop.RegisterCustomCommand("tgc.test", "Tests TGC DevConsoleInterop", args =>
		{
			DevConsoleInterop.WriteMessage("Test! Args: " + string.Join(", ", args));
		});
		
		DevConsoleInterop.RegisterCustomCommand("tgc.set-conn-info", "Edit the connection info", args =>
		{
			if (args.Length < 2)
			{
				DevConsoleInterop.WriteMessage("Please supply 2 args: <ip> <port>", "red");
				return;
			}

			if (!IPAddress.TryParse(args[0], out _))
			{
				DevConsoleInterop.WriteMessage("The provided IP is not a valid IP format!", "red");
				return;
			}

			if (!int.TryParse(args[1], out var portInt) || portInt < 0 || portInt > 65535)
			{
				DevConsoleInterop.WriteMessage("The port argument is an invalid port number!", "red");
				return;
			}
			
			ConfigManager.ServerIp.SetSerializedValue(args[0]);
			ConfigManager.ServerPort.SetSerializedValue(args[1]);
		});
		
		DevConsoleInterop.RegisterCustomCommand("tgc.set-name", "Set the player name to use", args =>
		{
			if (args.Length < 1)
			{
				DevConsoleInterop.WriteMessage("Please supply a player name!", "red");
				return;
			}
			
			ConfigManager.PlayerName.SetSerializedValue(args[0]);
		});
		
		DevConsoleInterop.RegisterCustomCommand("tgc.ping", "Pings the TGC server", _ =>
		{
			if (_connectionManager.TryPing())
			{
				DevConsoleInterop.WriteMessage("Pinging the TGC server succeeded!", "green");
			}
			else
			{
				DevConsoleInterop.WriteMessage("Pinging the TGC server failed!", "red");
			}
		});
		
		DevConsoleInterop.RegisterCustomCommand("tgc.join", "Attempts to join the game running on the TGC server", _ =>
		{
			var t = Task.Run(_connectionManager.JoinGame);
			var joinRes = t.Result;
			if (joinRes.Item1)
			{
				DevConsoleInterop.WriteMessage("Successfully joined game!", "green");

				_connectionManager.SendWs(WsMessageType.Ping_C2S);
			}
			else
			{
				DevConsoleInterop.WriteMessage("Failed to join game! " + joinRes.Item2, "red");
			}
		});
	}
}