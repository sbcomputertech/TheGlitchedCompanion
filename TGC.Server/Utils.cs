using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGC.Server
{
    public static class Utils
    {
        public static string? GetWebUrl(string url)
        {
            using var client = new HttpClient();
            try
            {
                var res = client.GetStringAsync(url).Result;
                return res;
            } catch (Exception ex)
            {
                Log.Warning("While trying to GET the url {Url}, an exception {Ex} occured. {Message}", url, ex.GetType().FullName, ex.Message);
                return null;
            }
        }

        public static string? GetPublicIp()
        {
            var ip = GetWebUrl("https://ifconfig.me/ip");
            if (ip == null)
            {
                Log.Error("Unable to get the public IP of the server, check the above logs for error");
                return null;
            }
            return ip;
        }
    }
}
