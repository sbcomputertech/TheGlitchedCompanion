namespace TGC.Client.StrongTypedPackets;

public class HandshakeStartIncoming
{
    public int t { get; set; }
    public Token token { get; set; }
}