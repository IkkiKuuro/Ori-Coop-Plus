using System.Collections.Generic;
using OriCoopDedicatedServer.Net.Session;

namespace OriCoopDedicatedServer.Net.Game
{
    /// <summary>
    /// Relay imediato de PLAYER_STATE (D-09/D-11): unreliable sequenciado,
    /// drop-old wrap-safe por remetente, sem retry e sem fila. A perda se
    /// resolve no proximo snapshot. Reemite os bytes originais do datagrama,
    /// sem reconstrucao — clientId + seq do remetente sao preservados.
    /// </summary>
    public sealed class PlayerStateRelay
    {
        /// <summary>
        /// True quando seq e mais novo que o ultimo repassado deste remetente
        /// (diferenca uint32 interpretada como int maior que zero: wrap-safe).
        /// Atualiza LastRecvSeq do remetente ao aceitar.
        /// </summary>
        public bool ShouldRelay(Session.Session sender, uint seq)
        {
            unchecked
            {
                uint diff = seq - sender.LastRecvSeq;
                if ((int)diff > 0)
                {
                    sender.LastRecvSeq = seq;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Alvos: todas as sessoes prontas exceto o remetente (sem eco).
        /// </summary>
        public IEnumerable<Session.Session> Targets(Session.Session sender, IEnumerable<Session.Session> sessions)
        {
            foreach (Session.Session candidate in sessions)
            {
                if (candidate.IsReady && candidate.Id != sender.Id)
                {
                    yield return candidate;
                }
            }
        }
    }
}
