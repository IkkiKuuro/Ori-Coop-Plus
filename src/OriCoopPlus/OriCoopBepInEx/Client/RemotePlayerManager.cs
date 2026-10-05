using System;
using System.Collections.Generic;
using OriCoopBepInEx.Domain;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public sealed class RemotePlayerManager
    {
        private readonly Dictionary<int, RemotePlayerPuppet> _puppets = new Dictionary<int, RemotePlayerPuppet>();

        public void HandleSnapshot(PlayerSnapshot snapshot)
        {
            if (snapshot == null || snapshot.PlayerId < 0)
            {
                return;
            }

            if (!snapshot.IsPlayerStatePacket)
            {
                return;
            }

            HandleDirectState(snapshot);
        }

        public void HandleDirectState(PlayerSnapshot snapshot)
        {
            if (snapshot == null || snapshot.PlayerId < 0)
            {
                return;
            }

            Vector3 pos = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
            Vector3 vel = new Vector3(snapshot.Velocity.X, snapshot.Velocity.Y, 0f);

            RemotePlayerPuppet puppet;
            if (!_puppets.TryGetValue(snapshot.PlayerId, out puppet) || puppet == null)
            {
                puppet = RemotePuppetFactory.CreatePuppet(snapshot.PlayerId, snapshot.Nick, pos);
                if (puppet != null)
                {
                    _puppets[snapshot.PlayerId] = puppet;
                    puppet.SnapTo(pos);
                }
            }

            if (puppet != null)
            {
                if (!string.IsNullOrEmpty(snapshot.Nick))
                {
                    puppet.UpdateNickname(snapshot.Nick);
                }
                puppet.ApplySnapshotDirect(pos, vel, snapshot.Animation.FacingLeft,
                    snapshot.Animation.State, snapshot.Animation.Name,
                    snapshot.Animation.AnimNameHash, snapshot.Nick);
            }
        }

        public void RemovePlayer(int playerId)        {
            RemotePlayerPuppet puppet;
            if (_puppets.TryGetValue(playerId, out puppet))
            {
                if (puppet != null && puppet.gameObject != null)
                {
                    UnityEngine.Object.Destroy(puppet.gameObject);
                }
                _puppets.Remove(playerId);
            }
        }

        public void ClearAll()
        {
            foreach (KeyValuePair<int, RemotePlayerPuppet> entry in _puppets)
            {
                if (entry.Value != null && entry.Value.gameObject != null)
                {
                    UnityEngine.Object.Destroy(entry.Value.gameObject);
                }
            }
            _puppets.Clear();
        }
    }
}
