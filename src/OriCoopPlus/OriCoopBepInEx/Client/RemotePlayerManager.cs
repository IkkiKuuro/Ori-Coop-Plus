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

        private sealed class MergedState
        {
            public Vector3 Position;
            public bool HasPosition;
            public Vector3 Velocity;
            public bool FacingLeft;
            public string AnimName = string.Empty;
            public uint AnimHash;
            public string Nick = string.Empty;
            public float LastTime;
            public Vector3 LastPosForVelocity;
            public bool HasLastPos;
        }

        private readonly Dictionary<int, MergedState> _states = new Dictionary<int, MergedState>();

        public void HandleSnapshot(PlayerSnapshot snapshot)
        {
            if (snapshot == null || snapshot.PlayerId < 0)
            {
                return;
            }

            // BUG #2: o protocolo envia POSITION e ANIM em pacotes separados.
            // O codigo antigo criava/aplicava um snapshot cru a cada pacote:
            // um pacote ANIM (sem posicao) zerava _targetPosition para (0,0,0),
            // arremessando o puppet para a origem e sumindo do mapa, e um pacote
            // POSITION (sem AnimName/velocidade) congelava a animacao no Idle.
            // Aqui fundimos os dois fluxos por jogador e inferimos velocidade
            // pelo delta de posicao (o servidor nunca envia velocidade).
            MergedState st;
            if (!_states.TryGetValue(snapshot.PlayerId, out st) || st == null)
            {
                st = new MergedState();
                _states[snapshot.PlayerId] = st;
            }

            bool hasPos = snapshot.Position.X != 0f || snapshot.Position.Y != 0f || snapshot.Position.Z != 0f;
            // (0,0,0) pode ser posicao real no menu; considera que POSITION sempre
            // chega com Nick preenchido pelo servidor, enquanto ANIM puro vem sem Nick.
            bool looksLikePositionPacket = !string.IsNullOrEmpty(snapshot.Nick) || hasPos;
            bool looksLikeAnimPacket = !string.IsNullOrEmpty(snapshot.Animation.Name) && string.IsNullOrEmpty(snapshot.Nick) && !hasPos;

            if (looksLikeAnimPacket)
            {
                st.AnimName = snapshot.Animation.Name ?? string.Empty;
                st.AnimHash = AnimationSyncData.ComputeFnv1aHash(st.AnimName);
            }
            else
            {
                Vector3 newPos = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
                float now = Time.time;

                if (st.HasPosition && st.HasLastPos)
                {
                    float dt = Mathf.Max(now - st.LastTime, 0.001f);
                    if (dt < 2.0f)
                    {
                        Vector3 inferred = (newPos - st.LastPosForVelocity) / dt;
                        // Clampa spikes de rede para nao quebrar a heuristica de animacao.
                        inferred.x = Mathf.Clamp(inferred.x, -30f, 30f);
                        inferred.y = Mathf.Clamp(inferred.y, -30f, 30f);
                        inferred.z = 0f;
                        // Suaviza para evitar jitter.
                        st.Velocity = Vector3.Lerp(st.Velocity, inferred, 0.5f);
                    }
                    else
                    {
                        st.Velocity = Vector3.zero;
                    }
                }

                st.Position = newPos;
                st.HasPosition = true;
                st.LastPosForVelocity = newPos;
                st.HasLastPos = true;
                st.LastTime = now;
                st.FacingLeft = snapshot.Animation.FacingLeft;

                if (!string.IsNullOrEmpty(snapshot.Animation.Name))
                {
                    st.AnimName = snapshot.Animation.Name;
                    st.AnimHash = AnimationSyncData.ComputeFnv1aHash(st.AnimName);
                }

                if (!string.IsNullOrEmpty(snapshot.Nick))
                {
                    st.Nick = snapshot.Nick;
                }
            }

            if (!st.HasPosition)
            {
                // Anim chegou antes da primeira posicao: guarda para aplicar no spawn.
                return;
            }

            RemotePlayerPuppet puppet;
            if (!_puppets.TryGetValue(snapshot.PlayerId, out puppet) || puppet == null)
            {
                string nick = !string.IsNullOrEmpty(st.Nick) ? st.Nick : snapshot.Nick;
                puppet = RemotePuppetFactory.CreatePuppet(snapshot.PlayerId, nick, st.Position);
                if (puppet != null)
                {
                    _puppets[snapshot.PlayerId] = puppet;
                    // Spawn ja no lugar certo, sem Lerp vindo da origem.
                    puppet.SnapTo(st.Position);
                }
            }

            if (puppet != null)
            {
                if (!string.IsNullOrEmpty(st.Nick))
                {
                    puppet.UpdateNickname(st.Nick);
                }
                puppet.ApplySnapshot(st.Position, st.Velocity, st.FacingLeft, st.AnimName, st.AnimHash, st.Nick);
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
            _states.Remove(playerId);
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
            _states.Clear();
        }
    }
}
