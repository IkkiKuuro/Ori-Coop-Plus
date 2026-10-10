namespace OriCoop
{
    // Catalogo de eventos do PlayerEventCore (fase 3, piloto Spirit Flame).
    // Namespace OriCoop por convencao do contrato; no fio viaja como byte
    // opaco (o servidor nunca interpreta o kind, so repassa).
    // Append-only: Stomp/Bash/... ganham 3, 4, ... em fases futuras; nunca
    // reordenar nem reutilizar valores. 0 reservado (desconhecido: o receptor
    // mantem a ultima anim valida — fail-closed, D-15).
    // 2 = ChargedFlame (rajada carregada via SeinChargeFlameAbility.
    // ReleaseChargeBurst, radial no orbe; fecha G-03-6).
    public enum PlayerEventKind : byte
    {
        Unknown = 0,
        SpiritFlame = 1,
        ChargedFlame = 2
    }
}
