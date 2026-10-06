namespace OriCoop
{
    // Contrato compartilhado do corpo do PLAYER_EVENT 19 (fase 3, D-10/D-11).
    // Apenas consts: compila em C# 5 (.NET 3.5, cliente Unity) e em .NET 8
    // (servidor). Ordem de campos congelada, total 37 bytes:
    //   Off 0 : int marker 19 (convecao legado-identica: o corpo carrega o proprio ID)
    //   Off 4 : byte kind (PlayerEventKind; o servidor trata como byte opaco)
    //   Off 5 : float dirX, dirY, dirZ (direcao do disparo)
    //   Off 17: float originX, originY, originZ (posicao de origem)
    //   Off 29: long timestampTicks (DateTime.UtcNow.Ticks)
    // A identidade do remetente viaja no clientId do header do envelope
    // (NetProtocol.OffClientId), nunca no corpo; o relay reemite os bytes
    // sem reconstrucao. Unreliable sequenciado (flags 0, sem retry, D-09).
    public static class PlayerEventProtocol
    {
        public const int BodySize = 37;
        public const int MinBodyLength = 37;
        public const int OffKind = 4;
        public const int OffDirection = 5;
        public const int OffOrigin = 17;
        public const int OffTimestamp = 29;
    }
}
