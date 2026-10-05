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

            if (Time.time - s_lastLogTime >= 3.0f)
            {
                s_lastLogTime = Time.time;
                EmitMetricsSummary(playerId, animHash, actionState);
            }
        }

        private static void EmitMetricsSummary(int playerId, uint lastHash, string lastState)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[OBSERVABILITY][NET-METRICS] ");
            sb.AppendFormat("P{0} | Recv: {1} | Applied: {2} | Dropped: {3} | ",
                playerId, s_packetsReceivedCounter, s_packetsAppliedCounter, s_packetsDroppedCounter);
            sb.AppendFormat("LastState: {0} | Hash: 0x{1:X8}", lastState, lastHash);

            Debug.Log(sb.ToString());

            s_packetsReceivedCounter = 0;
            s_packetsAppliedCounter = 0;
            s_packetsDroppedCounter = 0;
        }
    }
}
