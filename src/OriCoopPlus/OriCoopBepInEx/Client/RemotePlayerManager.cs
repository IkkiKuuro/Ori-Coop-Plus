using System;
using System.Collections.Generic;
using OriCoop;
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

            RemotePlayerPuppet puppet;
            if (!_puppets.TryGetValue(snapshot.PlayerId, out puppet) || puppet == null)
            {
                Vector3 initialPos = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
                puppet = RemotePuppetFactory.CreatePuppet(snapshot.PlayerId, snapshot.Nick, initialPos);
                if (puppet != null)
                {
                    _puppets[snapshot.PlayerId] = puppet;
                }
            }

            if (puppet != null)
            {
                puppet.UpdateNickname(snapshot.Nick);
                Vector3 pos = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
                Vector3 vel = new Vector3(snapshot.Velocity.X, snapshot.Velocity.Y, 0f);
                uint hash = AnimationSyncData.ComputeFnv1aHash(snapshot.Animation.Name);
                puppet.ApplySnapshot(pos, vel, snapshot.Animation.FacingLeft, snapshot.Animation.Name, hash);
            }
        }

        public void RemovePlayer(int playerId)
        {
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
