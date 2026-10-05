using System;
using System.Collections.Generic;
using System.Net;
using OriCoop;

namespace OriCoopDedicatedServer.Net.Reliability
{
    /// <summary>
    /// Confiabilidade minima do novo core (D-10): instancia por host, sem
    /// estatico global, BCL apenas. Pendencias chaveadas por (destino + seq),
    /// guardando os bytes originais do datagrama para reenvio identico.
    /// Retry a cada <see cref="NetProtocol.AckRetryMs"/> ms ate
    /// <see cref="NetProtocol.MaxRetries"/> tentativas; depois disso a
    /// pendencia expira (o host loga Warning) sem derrubar a sessao.
    /// PLAYER_STATE, Ping/Pong e mensagens de sistema nunca entram aqui.
    /// </summary>
    public sealed class AckTracker
    {
        public sealed class Pending
        {
            public IPEndPoint Remote = new IPEndPoint(IPAddress.Loopback, 0);
            public uint Seq;
            public byte[] Datagram = Array.Empty<byte>();
            public int SessionId;
            public int Attempts;
            public DateTime DeadlineUtc;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>(StringComparer.Ordinal);

        private static string Key(IPEndPoint remote, uint seq)
        {
            return remote.Address + ":" + remote.Port + "#" + seq;
        }

        /// <summary>
        /// Registra (ou substitui) a pendencia de um envio confiavel.
        /// Attempts = reenvios ja feitos; deadline = proximo retry.
        /// </summary>
        public void Track(IPEndPoint remote, uint seq, byte[] datagram, int sessionId)
        {
            if (remote == null || datagram == null)
            {
                return;
            }
            var entry = new Pending
            {
                Remote = remote,
                Seq = seq,
                Datagram = datagram,
                SessionId = sessionId,
                Attempts = 0,
                DeadlineUtc = DateTime.UtcNow.AddMilliseconds(NetProtocol.AckRetryMs),
            };
            lock (_sync)
            {
                _pending[Key(remote, seq)] = entry;
            }
        }

        /// <summary>
        /// Remove a pendencia confirmada pelo ACK. Retorna true se existia.
        /// </summary>
        public bool Complete(IPEndPoint remote, uint ackedSeq)
        {
            if (remote == null)
            {
                return false;
            }
            lock (_sync)
            {
                return _pending.Remove(Key(remote, ackedSeq));
            }
        }

        /// <summary>
        /// Coleta pendencias vencidas: as com tentativas restantes voltam com
        /// novo deadline (e Attempts incrementado) para reenvio; as esgotadas
        /// saem em <paramref name="expired"/> para log e descarte.
        /// </summary>
        public List<Pending> CollectDue(DateTime now, out List<Pending> expired)
        {
            var due = new List<Pending>();
            expired = new List<Pending>();
            lock (_sync)
            {
                foreach (Pending entry in _pending.Values)
                {
                    if (now < entry.DeadlineUtc)
                    {
                        continue;
                    }
                    if (entry.Attempts >= NetProtocol.MaxRetries)
                    {
                        expired.Add(entry);
                    }
                    else
                    {
                        entry.Attempts++;
                        entry.DeadlineUtc = now.AddMilliseconds(NetProtocol.AckRetryMs);
                        due.Add(entry);
                    }
                }
                for (int i = 0; i < expired.Count; i++)
                {
                    _pending.Remove(Key(expired[i].Remote, expired[i].Seq));
                }
            }
            return due;
        }

        /// <summary>
        /// Descarta pendencias destinadas a um endpoint (ex. sessao removida
        /// pelo sweeper). Retorna quantas foram descartadas.
        /// </summary>
        public int PurgeFor(IPEndPoint remote)
        {
            if (remote == null)
            {
                return 0;
            }
            string prefix = remote.Address + ":" + remote.Port + "#";
            int removed = 0;
            lock (_sync)
            {
                var keys = new List<string>(_pending.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        _pending.Remove(keys[i]);
                        removed++;
                    }
                }
            }
            return removed;
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _pending.Count;
                }
            }
        }
    }
}
