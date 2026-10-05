namespace OriCoop
{
    public enum PacketType
    {
        // Core packets (IDs 1-2 removidos no rework de anims; nunca reutilizar)
        POSITION = 1, // LEGACY_REMOVED
        ANIM = 2, // LEGACY_REMOVED
        ID = 3,
        DISCONNECT = 4,
        REQUEST_PLAYERS = 5,
        COLOR = 6,
        SKILL = 7,

        // Ori Coop Plus extended packets
        SYNC_ABILITY = 10,
        SYNC_LEVER = 11,
        SYNC_DOOR = 12,
        SYNC_BREAKABLE = 13,
        SYNC_WORLDEVENT = 14,
        TELEPORT_REQUEST = 15,
        CONFIG_SYNC = 16,
        DUMMY_ACTION = 17,
        PLAYER_STATE = 18
    }

    public enum CoopSkillType
    {
        NONE = 0,
        Spirit = 1,
        Stomp = 2
    }
}
