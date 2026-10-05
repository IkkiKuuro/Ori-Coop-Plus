namespace OriCoop
{
    // Contrato unico do envelope versionado (fase 2, D-02/D-15).
    // Apenas consts: compila em C# 5 (.NET 3.5, cliente Unity) e em .NET 8 (servidor).
    // Header fixo de 24 bytes, little-endian, layout:
    //   OffMagic 0  : ushort magic 0x4F43 ("OC")
    //   OffVersion 2: byte versao do envelope (2)
    //   OffFlags 3  : byte flags (Reliable 0x01, AckPresent 0x02)
    //   OffSeq 4    : uint32 seq por remetente (wrap-safe)
    //   OffClientId 8 : int32 clientId (-1 = pre-handshake, 0 = servidor)
    //   OffToken 12 : uint32 token da sessao (0 = pre-handshake)
    //   OffPacketId 16: int32 packetId (100-106 sistema, demais = jogo legado)
    //   OffAckSeq 20: uint32 ack (usado a partir do plano 02-02)
    public static class NetProtocol
    {
        public const ushort Magic = 0x4F43;
        public const byte Version = 2;
        public const int HeaderSize = 24;

        public const byte FlagReliable = 0x01;
        public const byte FlagAckPresent = 0x02;

        public const int OffMagic = 0;
        public const int OffVersion = 2;
        public const int OffFlags = 3;
        public const int OffSeq = 4;
        public const int OffClientId = 8;
        public const int OffToken = 12;
        public const int OffPacketId = 16;
        public const int OffAckSeq = 20;

        public const int MsgHello = 100;
        public const int MsgWelcome = 101;
        public const int MsgConfirm = 102;
        public const int MsgAck = 103;
        public const int MsgPing = 104;
        public const int MsgPong = 105;
        public const int MsgReject = 106;

        public const int ServerId = 0;
        public const int DummyId = 999;
        public const int PreHandshakeId = -1;

        public const int HeartbeatIntervalMs = 2000;
        public const int SessionTimeoutMs = 10000;
        public const int AckRetryMs = 250;
        public const int MaxRetries = 3;
        public const int ChatMaxChars = 350;
    }
}
