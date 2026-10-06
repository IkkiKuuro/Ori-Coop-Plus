namespace OriCoop
{
    // Catalogo de eventos do PlayerEventCore (fase 3, piloto Spirit Flame).
    // Namespace OriCoop por convencao do contrato; no fio viaja como byte
    // opaco (o servidor nunca interpreta o kind, so repassa).
    // Append-only: Stomp/Bash/... ganham 2, 3, ... em fases futuras; nunca
    // reordenar nem reutilizar valores. 0 reservado (desconhecido: o receptor
    // mantem a ultima anim valida — fail-closed, D-15).
    public enum PlayerEventKind : byte
    {
        Unknown = 0,
        SpiritFlame = 1
    }
}
