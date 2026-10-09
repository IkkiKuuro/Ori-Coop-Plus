using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

namespace OriCoopDedicatedServer.Net.Session
{
    /// <summary>
    /// Estado de uma sessao de jogador (D-05/D-06): instancia, sem estatico global.
    /// Token aleatorio por sessao (D-08 base); IsReady so apos Confirm.
    /// </summary>
    public sealed class Session
    {
        private readonly object _relSync = new object();
        private readonly HashSet<uint> _seenReliable = new HashSet<uint>();

        public int Id { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public uint Token { get; set; }

        public bool IsReady { get; set; }

        public DateTime LastSeenUtc { get; set; }

        public uint LastRecvSeq { get; set; }

        /// <summary>
        /// Ultimo Confirm processado (dedup de retry: mesmo seq nao
        /// re-dispara o join). Separado do LastRecvSeq, que avancacom snapshots.
        /// </summary>
        public uint LastConfirmSeq { get; set; }

        public bool ConfirmSeen { get; set; }

        public string Nickname { get; set; }

        public Session()
        {
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            Nickname = string.Empty;
            LastSeenUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Dedup de pacotes confiaveis (retry do cliente reenvia a mesma
        /// seq): true = duplicata ja processada (descartar sem re-executar;
        /// o SysAck ja foi enviado no dispatch). ACKs sao enviados antes do
        /// dispatch, entao o retry sempre recebe ACK mesmo descartado aqui.
        /// Janela de ~256 seqs: retry valido chega em &lt;1 s, bem antes de
        /// 256 pacotes novos; limpeza total e segura.
        /// </summary>
        public bool IsDuplicateReliable(uint seq)
        {
            lock (_relSync)
            {
                return _seenReliable.Contains(seq);
            }
        }

        public void MarkReliableProcessed(uint seq)
        {
            lock (_relSync)
            {
                _seenReliable.Add(seq);
                if (_seenReliable.Count > 256)
                {
                    _seenReliable.Clear();
                    _seenReliable.Add(seq);
                }
            }
        }

        public static uint NewToken()
        {
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }
    }
}
