using BepInEx.Configuration;

namespace TGC.Client
{
    public static class ConfigManager
    {
        public static ConfigEntry<string> ServerIp;
        public static ConfigEntry<int> ServerPort;

        public static ConfigEntry<string> PlayerName;

        public static void Init(ConfigFile file)
        {
            ServerIp = file.Bind("ServerConnection", "IP", "127.0.0.1", "The IP of the server to connect to");
            ServerPort = file.Bind("ServerConnection", "Port", 9943, "The base HTTP port set in the server config");
            
            PlayerName = file.Bind("Player", "Name", "Idiot     ", "The name sent to the server and other players");
        }
    }
}
