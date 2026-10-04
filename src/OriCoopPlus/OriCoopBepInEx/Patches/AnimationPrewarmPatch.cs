using System;
using HarmonyLib;
using OriCoopBepInEx.Client;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch(typeof(CharacterAnimationSystem), "Start")]
    internal static class AnimationPrewarmPatch
    {
        private static void Postfix()
        {
            AnimationRegistry.Prewarm();
        }
    }
}
