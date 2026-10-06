namespace OriCoop
{
    public enum PacketType
    {
        // Core packets (IDs 1-2 removidos no rework de anims; nunca reutilizar)
        POSITION = 1, // LEGACY_REMOVED
        ANIM = 2, // LEGACY_REMOVED
        // ID 3 REMOVED (D-15): identificacao do handshake legado; nunca reutilizar.
        DISCONNECT = 4,
        // ID 5 REMOVED (D-15): REQUEST_PLAYERS, listagem de jogadores; nunca reutilizar.
        COLOR = 6,
        SKILL = 7,

        // Pacotes mortos do core antigo (D-15, quebra one-way ja aprovada):
        // nunca existiram como membros deste enum e nunca serao recriados:
        // -2 NBMessage (repasse cego) -> removido, relay agora e por pacote
        // -6 RPC (repasse cego) -> removido, sem substituto
        // -3 NetworkVar (ES/cc/Coop_*) -> absorvido pelo CONFIG_SYNC de 8 bools
        // -7 ping legado -> substituido por MsgPing 104 / MsgPong 105
        // -1 handshake legado -> substituido por Hello 100 / Welcome 101 / Confirm 102
        // Cliente e servidor precisam ser sempre do mesmo build; sem fallback.

        // Ori Coop Plus extended packets
        SYNC_ABILITY = 10,
        SYNC_LEVER = 11,
        SYNC_DOOR = 12,
        SYNC_BREAKABLE = 13,
        SYNC_WORLDEVENT = 14,
        TELEPORT_REQUEST = 15,
        CONFIG_SYNC = 16,
        DUMMY_ACTION = 17,
        PLAYER_STATE = 18,
        // PlayerEventCore piloto (fase 3, D-09/D-10): unreliable sequenciado,
        // mesma classe do PLAYER_STATE 18. Quebra one-way: exige cliente e
        // servidor do mesmo build, docs/protocol.md atualizado na mesma mudanca.
        PLAYER_EVENT = 19
    }

    public enum CoopSkillType
    {
        NONE = 0,
        Spirit = 1,
        Stomp = 2
    }
}
