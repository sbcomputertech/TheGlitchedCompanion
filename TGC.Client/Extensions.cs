using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace TGC.Client
{
    public static class Extensions
    {
        public static string GetString(this HttpClient client, string url = "/")
        {
            return client.GetStringAsync(url).Result;
        }
        public static string Post(this HttpClient client, object body)
        {
            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json);
            var response = client.PostAsync("/", content).Result;
            return response.Content.ReadAsStringAsync().Result;
        }
        public static string PostEmpty(this HttpClient client, string type)
        {
            var body = new Dictionary<string, object>
            {
                ["t"] = type
            };
            return client.Post(body);
        }
    }
}
