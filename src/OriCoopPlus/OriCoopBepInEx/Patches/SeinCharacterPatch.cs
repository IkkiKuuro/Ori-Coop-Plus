using System;
using HarmonyLib;
using OriCoopBepInEx.Plugin;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch(typeof(SeinCharacter), "FixedUpdate")]
    internal static class SeinCharacterPatch
    {
        private static void Postfix(SeinCharacter __instance)
        {
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.Publish(PlayerStateReader.Read(__instance));
            }
        }
    }
}
