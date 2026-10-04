using System;

namespace OriCoopBepInEx.Domain
{
    public interface INetworkService : IDisposable
    {
        event Action<PlayerSnapshot> PlayerSnapshotReceived;
        event Action<Vector3Data, string> TeleportRequested;
        event Action<string, string> ChatMessageReceived;
        event Action<bool> EntitySyncChanged;
        event Action<int> PingUpdated;
        event Action<string, int> IdentityAssigned;

        void Start();
        void SendPlayerSnapshot(PlayerSnapshot snapshot);
        void SendTeleportRequest(int targetPlayerId);
    }
}
