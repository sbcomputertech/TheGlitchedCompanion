using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGC.Server
{
    // adapted from https://dusted.codes/dotenv-in-dotnet
    public static class DotEnv
    {
        public const string BasePort = "TGC_BasePort";
        public const string RemoteAdminAuthKey = "TGC_RemoteAdminAuthKey";
        public const string AllowedClientVersion = "TGC_AllowedClientVersion";
        public const string ServerPagesDir = "TGC_ServerPagesDir";

        public static void Load()
        {
            var filePath = "./.env";
            if (!File.Exists(filePath))
            {
                Log.Error("No .env file was found in the current working directory");
                return;
            }

            var envCount = 0;
            foreach (var line in File.ReadAllLines(filePath))
            {
                if (line.StartsWith("#")) continue;

                var parts = line.Split(
                    '=',
                    StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2)
                    continue;

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
                envCount++;
            }

            Log.Information("Loaded {Count} variables from .env file", envCount);
        }

        public static bool IsSet(string varName)
        {
            var data = Environment.GetEnvironmentVariable(varName);
            return !string.IsNullOrWhiteSpace(data);
        }

        public static string? Get(string varName)
        {
            return Environment.GetEnvironmentVariable(varName);
        }

        public static int? GetInt(string varName)
        {
            if (!IsSet(varName)) return null;
            var isInt = int.TryParse(Get(varName), out var value);
            if(!isInt) return null;
            return value;
        }
    }
}
