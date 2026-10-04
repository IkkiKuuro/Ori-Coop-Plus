using HarmonyLib;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch("SeinInput", "Update")]
    internal static class SeinInputPatch
    {
        private static void Postfix()
        {
            // Input capture is intentionally a separate trigger from character state.
            // The input adapter will be added once the game's concrete fields are verified.
        }
    }
}
