namespace OriCoop
{
    public enum PacketType
    {
        // Core packets (matching original WW protocol)
        POSITION = 1,
        ANIM = 2,
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
        DUMMY_ACTION = 17
    }

    public enum CoopSkillType
    {
        NONE = 0,
        Spirit = 1,
        Stomp = 2
    }
}
