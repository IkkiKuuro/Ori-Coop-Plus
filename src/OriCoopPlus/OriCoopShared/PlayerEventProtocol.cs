namespace OriCoop
{
    // Contrato compartilhado do corpo do PLAYER_EVENT 19 (fase 3, D-10/D-11).
    // Apenas consts: compila em C# 5 (.NET 3.5, cliente Unity) e em .NET 8
    // (servidor). Ordem de campos congelada, total 49 bytes (v2: +aim):
    //   Off 0 : int marker 19 (convecao legado-identica: o corpo carrega o proprio ID)
    //   Off 4 : byte kind (PlayerEventKind; o servidor trata como byte opaco)
    //   Off 5 : float dirX, dirY, dirZ (direcao inicial do disparo)
    //   Off 17: float originX, originY, originZ (posicao do ORBE no disparo)
    //   Off 29: float aimX, aimY, aimZ (ponto de mira: alvo travado ou facing; v2)
    //   Off 41: long timestampTicks (DateTime.UtcNow.Ticks)
    // v2 quebra o fio one-way (37B antigos sao descartados pelo MinBodyLength):
    // cliente e servidor sempre do mesmo build (regra D-10). O beam remoto
    // (orbe→mira, RealBeamDriver) precisa do ponto final exato — sem ele o
    // visual destacava da bola.
    // A identidade do remetente viaja no clientId do header do envelope
    // (NetProtocol.OffClientId), nunca no corpo; o relay reemite os bytes
    // sem reconstrucao. Unreliable sequenciado (flags 0, sem retry, D-09).
    public static class PlayerEventProtocol
    {
        public const int BodySize = 49;
        public const int MinBodyLength = 49;
        public const int OffKind = 4;
        public const int OffDirection = 5;
        public const int OffOrigin = 17;
        public const int OffAim = 29;
        public const int OffTimestamp = 41;
    }
}
