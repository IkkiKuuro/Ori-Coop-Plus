#nullable disable
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using OriCoop;
using OriCoopDedicatedServer.Net.Diagnostics;

namespace OriCoopDedicatedServer.Net.Session
{
    /// <summary>
    /// Sessoes dinamicas por ID incremental nunca reutilizado no run (D-05),
    /// handshake em 3 vias Hello/Welcome/Confirm (D-06). Limite por Count vs MaxPlayers.
    /// IDs 0 (servidor) e 999 (dummy) nunca sao alocados.
    /// </summary>
    public sealed class SessionManager
    {
        private readonly ConcurrentDictionary<int, Session> _sessions = new ConcurrentDictionary<int, Session>();
        private readonly ILogger _log;
        private readonly int _maxPlayers;
        private int _nextId;

        public SessionManager(int maxPlayers, ILogger log)
        {
            _maxPlayers = maxPlayers < 1 ? 1 : (maxPlayers > 10 ? 10 : maxPlayers);
            _log = log ?? throw new ArgumentNullException("log");
        }

        public int Count
        {
            get { return _sessions.Count; }
        }

        public IEnumerable<Session> All
        {
            get { return _sessions.Values; }
        }

        public bool TryGet(int clientId, out Session session)
        {
            Session candidate;
            bool found = _sessions.TryGetValue(clientId, out candidate);
            session = found ? candidate : null;
            return found && session != null;
        }

        public void Touch(Session session)
        {
            session.LastSeenUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Validacao pos-handshake (D-08): sessao existe, token do header
        /// confere e endpoint e identico ao fixado no Hello. Divergencia de
        /// token ou endpoint descarta o datagrama sem atualizar LastSeen;
        /// troca de IP/porta (NAT) exige novo Hello com clientId -1.
        /// </summary>
        public bool ValidatePacket(int clientId, uint token, IPEndPoint remote, out Session session, out string reason)
        {
            session = null;
            reason = string.Empty;
            Session found;
            if (!_sessions.TryGetValue(clientId, out found) || found == null)
            {
                reason = "sessao desconhecida (" + clientId + ")";
                return false;
            }
            if (found.Token != token)
            {
                reason = "token divergente na sessao " + clientId;
                return false;
            }
            if (remote == null || !found.EndPoint.Address.Equals(remote.Address) || found.EndPoint.Port != remote.Port)
            {
                reason = "endpoint divergente na sessao " + clientId + " (fixo " + found.EndPoint + ", recebido " + remote + ")";
                return false;
            }
            session = found;
            return true;
        }

        /// <summary>
        /// Remove a sessao sem broadcast (o host decide o aviso aos demais).
        /// </summary>
        public bool Remove(int clientId, out Session removed)
        {
            return _sessions.TryRemove(clientId, out removed);
        }

        /// <summary>
        /// Sweeper de timeout (D-07): remove sessoes com mais de
        /// <paramref name="timeoutMs"/> ms sem datagrama valido e as devolve
        /// para o host transmitir DISCONNECT. Reconexao e sempre novo Hello
        /// com novo ID (IDs nunca reutilizados no run).
        /// </summary>
        public List<Session> SweepExpired(int timeoutMs)
        {
            var removed = new List<Session>();
            DateTime now = DateTime.UtcNow;
            foreach (KeyValuePair<int, Session> pair in _sessions)
            {
                Session candidate = pair.Value;
                if (candidate == null)
                {
                    continue;
                }
                if ((now - candidate.LastSeenUtc).TotalMilliseconds > timeoutMs)
                {
                    Session taken;
                    if (_sessions.TryRemove(pair.Key, out taken) && taken != null)
                    {
                        removed.Add(taken);
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// Hello: clientId deve ser -1 e token 0; payload = protoVer byte + nick
        /// (int32-length + ASCII, fallback Player_ID quando vazio). Retorna a
        /// sessao criada (ou existente para o mesmo endpoint) e o payload do
        /// Welcome (assignedId int + token uint + serverVer byte).
        /// </summary>
        public bool HandleHello(IPEndPoint remote, byte[] payload, out Session session, out byte[] welcomePayload, out string rejectReason)
        {
            session = null;
            welcomePayload = Array.Empty<byte>();
            rejectReason = string.Empty;

            if (!TryParseHello(payload, out byte protoVer, out string nick, out rejectReason))
            {
                return false;
            }
            if (protoVer != NetProtocol.Version)
            {
                rejectReason = "versao de protocolo " + protoVer + " recusada; servidor exige " + NetProtocol.Version + " (mesmo build)";
                return false;
            }

            Session existing = FindByEndPoint(remote);
            if (existing != null)
            {
                session = existing;
                welcomePayload = BuildWelcome(existing);
                Touch(existing);
                return true;
            }

            if (_sessions.Count >= _maxPlayers)
            {
                rejectReason = "SERVER_FULL: servidor cheio (" + _sessions.Count + "/" + _maxPlayers + ")";
                _log.Log(ServerLogLevel.Warning, "SESSAO", "Hello de " + remote + " recusado: " + rejectReason);
                return false;
            }

            int id = AllocateId();
            string finalNick = string.IsNullOrEmpty(nick) ? "Player_" + id : nick;
            var created = new Session
            {
                Id = id,
                EndPoint = remote,
                Token = Session.NewToken(),
                IsReady = false,
                LastSeenUtc = DateTime.UtcNow,
                Nickname = finalNick,
            };
            _sessions[id] = created;
            session = created;
            welcomePayload = BuildWelcome(created);
            _log.Log(ServerLogLevel.Info, "SESSAO", "Hello de " + remote + " -> ID " + id + " (" + finalNick + ")");
            return true;
        }

        /// <summary>
        /// Confirm: marca IsReady=true somente com token correto e mesmo
        /// endpoint fixado no Hello (D-06/D-08). Endpoint divergente exige
        /// novo handshake e nao toca na sessao.
        /// </summary>
        public bool HandleConfirm(int clientId, uint token, IPEndPoint remote, out Session session, out string rejectReason)
        {
            session = null;
            rejectReason = string.Empty;
            Session found;
            if (!_sessions.TryGetValue(clientId, out found))
            {
                rejectReason = "sessao desconhecida (" + clientId + "); refaca o handshake";
                return false;
            }
            if (found.Token != token)
            {
                rejectReason = "token invalido para sessao " + clientId + "; refaca o handshake";
                return false;
            }
            if (remote == null || !found.EndPoint.Address.Equals(remote.Address) || found.EndPoint.Port != remote.Port)
            {
                rejectReason = "endpoint divergente na sessao " + clientId + "; refaca o handshake do novo endpoint";
                return false;
            }
            found.IsReady = true;
            found.LastSeenUtc = DateTime.UtcNow;
            session = found;
            _log.Log(ServerLogLevel.Info, "SESSAO", "ID " + clientId + " (" + found.Nickname + ") pronto");
            return true;
        }

        private int AllocateId()
        {
            while (true)
            {
                int id = Interlocked.Increment(ref _nextId);
                if (id == NetProtocol.ServerId || id == NetProtocol.DummyId)
                {
                    continue;
                }
                return id;
            }
        }

        private Session FindByEndPoint(IPEndPoint remote)
        {
            foreach (Session candidate in _sessions.Values)
            {
                if (candidate.EndPoint.Address.Equals(remote.Address) && candidate.EndPoint.Port == remote.Port)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static byte[] BuildWelcome(Session session)
        {
            byte[] payload = new byte[9];
            Span<byte> span = payload.AsSpan();
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), session.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), session.Token);
            span[8] = NetProtocol.Version;
            return payload;
        }

        private static bool TryParseHello(byte[] payload, out byte protoVer, out string nick, out string rejectReason)
        {
            protoVer = 0;
            nick = string.Empty;
            rejectReason = string.Empty;
            if (payload == null || payload.Length < 5)
            {
                rejectReason = "Hello truncado (sem protoVer + nick)";
                return false;
            }
            protoVer = payload[0];
            int length = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, 4));
            if (length < 0 || length > payload.Length - 5 || length > 64)
            {
                rejectReason = "nick com tamanho invalido no Hello";
                return false;
            }
            if (length == 0)
            {
                return true;
            }
            try
            {
                nick = Encoding.ASCII.GetString(payload, 5, length).Trim();
                return true;
            }
            catch (Exception)
            {
                rejectReason = "nick ilegivel no Hello";
                return false;
            }
        }
    }
}
