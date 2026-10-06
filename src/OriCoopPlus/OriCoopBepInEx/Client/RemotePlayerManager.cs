using System;
using System.Collections.Generic;
using OriCoop;
using OriCoopBepInEx.Domain;
using OriCoopBepInEx.Events;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public sealed class RemotePlayerManager
    {
        private readonly Dictionary<int, RemotePlayerPuppet> _puppets = new Dictionary<int, RemotePlayerPuppet>();

        public RemotePlayerManager()
        {
            PlayerEventCore.RegisterHandler((byte)PlayerEventKind.SpiritFlame, HandleSpiritFlame);
        }

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

        // Rota de eventos do personagem (fase 3): mesma guarda de
        // HandleDirectState (sender invalido -> return), lazy-puppet na
        // origem do disparo via factory + SnapTo quando o remetente ainda
        // nao tem puppet, e delegacao ao PlayerEventCore. Disciplina
        // explicita de nunca-publicar (D-07): nenhum Publish/Core.Publish
        // e alcançavel deste caminho — o receptor remoto jamais republica
        // (sem eco). O corpo do evento nao carrega nick (D-11: identidade so
        // no header); nicks chegam pelos snapshots. Roda na main thread.
        public void HandlePlayerEvent(int senderId, byte kind, SpiritFlameEventData data)
        {
            if (senderId < 0)
            {
                return;
            }

            Vector3 origin = new Vector3(data.Origin.X, data.Origin.Y, data.Origin.Z);

            RemotePlayerPuppet puppet;
            if (!_puppets.TryGetValue(senderId, out puppet) || puppet == null)
            {
                puppet = RemotePuppetFactory.CreatePuppet(senderId, null, origin);
                if (puppet != null)
                {
                    _puppets[senderId] = puppet;
                    puppet.SnapTo(origin);
                }
            }

            PlayerEventCore.DispatchLocal(senderId, kind, data);
        }

        private void HandleSpiritFlame(int senderId, SpiritFlameEventData data)
        {
            RemotePlayerPuppet puppet;
            if (!_puppets.TryGetValue(senderId, out puppet) || puppet == null)
            {
                return;
            }
            Vector3 origin = new Vector3(data.Origin.X, data.Origin.Y, data.Origin.Z);
            Vector3 direction = new Vector3(data.Direction.X, data.Direction.Y, data.Direction.Z);
            puppet.PlaySpiritFlameVisual(origin, direction);
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
