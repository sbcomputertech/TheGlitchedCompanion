using System;

namespace TGC.Client;

public class EmptyPacketWithType
{
    public int t;

    public WsMessageType Type => (WsMessageType)t;
}