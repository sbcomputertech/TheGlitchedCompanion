using System;
using System.Collections.Generic;
using System.Text;

namespace TGC.Shared
{
    public enum WsMessageType
    {
        StartHandshake_S2C,
        RespondToHandshake_C2S,
        ConfirmHandshake_S2C,

        RequestError_S2C,

        Ping_C2S,
        PingResponse_S2C,

        SyncPosition_C2S,
        PeerPositions_S2C,
    }
}
