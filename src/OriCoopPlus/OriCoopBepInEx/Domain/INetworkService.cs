using System;

namespace OriCoopBepInEx.Domain
{
    // Delegate proprio em vez de Action<...> com 6 parametros: o mscorlib do
    // Unity 5.3 (perfil .NET 3.5) nao garante Action com mais de 4 parametros.
    public delegate void ConfigSyncHandler(bool allowTeleport, bool shareAbilities, bool shareStoryOnly, bool shareWorldEvents, bool shareDoorsAndLevers, bool showNicknames);

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
        void SendNicknameUpdate(string newNick);
    }
}
