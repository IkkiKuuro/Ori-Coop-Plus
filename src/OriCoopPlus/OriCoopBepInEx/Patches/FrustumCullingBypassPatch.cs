using System;
using HarmonyLib;
using OriCoopBepInEx.Client;
using UnityEngine;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch(typeof(CameraFrustumOptimizer), "ProcessFrustumOptimizable")]
    internal static class FrustumCullingBypassPatch
    {
        private static bool Prefix(IFrustumOptimizable o)
        {
            Component comp = o as Component;
            if (comp != null)
            {
                RemotePlayerPuppet puppet = comp.GetComponentInParent<RemotePlayerPuppet>();
                if (puppet != null)
                {
                    // Never cull remote players via CameraFrustumOptimizer
                    return false;
                }
            }
            return true;
        }
    }
}
