using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TGC.Client
{
    public class PlayerInfo
    {
        public string Name { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
        public Guid Guid { get; set; }
        public string IP { get; set; }
        public string ClientVersion { get; set; }
        public int SocketPort { get; set; }
    }
}
