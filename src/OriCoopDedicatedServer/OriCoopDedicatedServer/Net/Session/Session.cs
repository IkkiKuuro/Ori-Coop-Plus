using System;
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
        public int Id { get; set; }

        public IPEndPoint EndPoint { get; set; }

        public uint Token { get; set; }

        public bool IsReady { get; set; }

        public DateTime LastSeenUtc { get; set; }

        public uint LastRecvSeq { get; set; }

        public uint LastSentSeq { get; set; }

        /// <summary>
        /// Contador de varreduras do sweeper sem datagrama (D-07). Com o
        /// sweeper atual baseado em tempo (LastSeenUtc vs 10 s), permanece
        /// em zero; preservado para diagnostico futuro.
        /// </summary>
        public int MissedSweeps { get; set; }

        public string Nickname { get; set; }

        public Session()
        {
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            Nickname = string.Empty;
            LastSeenUtc = DateTime.UtcNow;
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
