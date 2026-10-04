using HarmonyLib;
using OriCoopBepInEx.Plugin;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch("SeinCharacter", "FixedUpdate")]
    internal static class SeinCharacterPatch
    {
        private static void Postfix(object __instance)
        {
            if (OriCoopPlugin.Instance != null)
            {
                OriCoopPlugin.Instance.Publish(PlayerStateReader.Read(__instance));
            }
        }
    }
}
