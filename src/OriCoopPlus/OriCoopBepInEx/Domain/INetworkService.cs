using System;

namespace OriCoopBepInEx.Domain
{
    // Delegate proprio em vez de Action<...> com 8 parametros: o mscorlib do
    // Unity 5.3 (perfil .NET 3.5) nao garante Action com mais de 4 parametros.
    // Ordem canonica do CONFIG_SYNC 16: AllowTeleport, ShareAbilities,
    // ShareStoryOnly, ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames,
    // ClientColors, EntitySync.
    public delegate void ConfigSyncHandler(bool allowTeleport, bool shareAbilities, bool shareStoryOnly, bool shareWorldEvents, bool shareDoorsAndLevers, bool showNicknames, bool clientColors, bool entitySync);

    public interface INetworkService : IDisposable
    {
        event Action<PlayerSnapshot> PlayerSnapshotReceived;
        event Action<Vector3Data, string> TeleportRequested;
        event Action<string, string> ChatMessageReceived;
        event Action<bool> EntitySyncChanged;
        event Action<int> PingUpdated;
        event Action<string, int> IdentityAssigned;
        event Action<int> PlayerDisconnected;
        event ConfigSyncHandler ConfigSyncReceived;

        bool IsConnected { get; }

        void Start();
        void SendPlayerSnapshot(PlayerSnapshot snapshot);
        void SendTeleportRequest(int targetPlayerId);
        void SendChatMessage(string text);
        void SendSkill(int skillId);
        void SendColor(byte r, byte g, byte b);
        void SendDisconnect();
        void SendSyncAbility(int abilityId);
        void SendSyncLever(int v0, int v1, int v2, int v3, int v4);
        void SendSyncDoor(int v0, int v1, int v2, int v3);
        void SendSyncWorldEvent(int v0, int v1, int v2, int v3, int v4);
        void SendDummyAction(int action);
        void SendDummyAction(int action, int abilityId);
        void SendNicknameUpdate(string newNick);
    }
}
