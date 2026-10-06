using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OriCoopBepInEx.Diagnostics
{
    public static class ReplicationObservability
    {
        private static float s_lastLogTime;
        private static int s_packetsReceivedCounter;
        private static int s_packetsAppliedCounter;
        private static int s_packetsDroppedCounter;

        // Contadores do dominio de eventos (fase 3, D-12): espelham o
        // vocabulario de pacotes acima — received conta cada transicao
        // rastreada (envio + despacho), applied os entregues ao transporte
        // ou ao handler, dropped os fail-closed (tipo desconhecido,
        // handler com excecao). Calibragem futura de throttle le estes
        // numeros, nao impressoes.
        private static int s_eventsReceivedCounter;
        private static int s_eventsAppliedCounter;
        private static int s_eventsDroppedCounter;
        private static byte s_lastEventKind;

        // Ultimo contexto de pacote para o resumo parâmetro-less: atualizado
        // a cada TrackPacket para que o disparo do resumo pelo dominio de
        // eventos (TrackPlayerEvent) ainda imprima o estado de anim.
        private static int s_lastPacketPlayerId;
        private static uint s_lastAnimHash;
        private static string s_lastActionState;

        private static readonly Queue<string> s_ring = new Queue<string>();
        private const int RingCapacity = 200;
        private static readonly object s_ringSync = new object();

        public static void Record(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            lock (s_ringSync)
            {
                while (s_ring.Count >= RingCapacity)
                {
                    s_ring.Dequeue();
                }
                s_ring.Enqueue(line);
            }
        }

        public static string[] Snapshot()
        {
            lock (s_ringSync)
            {
                return s_ring.ToArray();
            }
        }

        public static void LogVisibilityEvent(int playerId, string reason, bool newState)
        {
            string status = newState ? "VISIBLE" : "HIDDEN";
            Debug.Log(string.Format("[OBSERVABILITY][VISIBILITY] Player {0} -> {1} | Reason: {2} | Frame: {3}",
                playerId, status, reason, Time.frameCount));
        }

        public static void TrackPacket(int playerId, uint animHash, string actionState, bool applied)
        {
            s_packetsReceivedCounter++;
            if (applied)
            {
                s_packetsAppliedCounter++;
            }
            else
            {
                s_packetsDroppedCounter++;
            }
            s_lastPacketPlayerId = playerId;
            s_lastAnimHash = animHash;
            s_lastActionState = actionState;

            if (Time.time - s_lastLogTime >= 3.0f)
            {
                s_lastLogTime = Time.time;
                EmitMetricsSummary();
            }
        }

        // Telemetria de eventos (fase 3): mesma forma do TrackPacket —
        // received sempre incrementa, applied/dropped pelo flag, mesma
        // cadencia de 3 s do resumo compartilhado. Chamadas verboso-gated
        // nos pontos de publish/dispatch (PlayerEventCore), como os logs
        // de transicao de anim.
        public static void TrackPlayerEvent(byte kind, bool applied)
        {
            s_eventsReceivedCounter++;
            if (applied)
            {
                s_eventsAppliedCounter++;
            }
            else
            {
                s_eventsDroppedCounter++;
            }
            s_lastEventKind = kind;

            if (Time.time - s_lastLogTime >= 3.0f)
            {
                s_lastLogTime = Time.time;
                EmitMetricsSummary();
            }
        }

        private static void EmitMetricsSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[OBSERVABILITY][NET-METRICS] ");
            sb.AppendFormat("P{0} | Recv: {1} | Applied: {2} | Dropped: {3} | ",
                s_lastPacketPlayerId, s_packetsReceivedCounter, s_packetsAppliedCounter, s_packetsDroppedCounter);
            sb.AppendFormat("LastState: {0} | Hash: 0x{1:X8}", s_lastActionState, s_lastAnimHash);
            sb.AppendFormat(" | EvRecv: {0} | EvApplied: {1} | EvDropped: {2} | EvKind: {3}",
                s_eventsReceivedCounter, s_eventsAppliedCounter, s_eventsDroppedCounter, s_lastEventKind);

            Debug.Log(sb.ToString());

            s_packetsReceivedCounter = 0;
            s_packetsAppliedCounter = 0;
            s_packetsDroppedCounter = 0;
            s_eventsReceivedCounter = 0;
            s_eventsAppliedCounter = 0;
            s_eventsDroppedCounter = 0;
        }
    }
}
