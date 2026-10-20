using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_HackingExpansionDisabled
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return HackingExpansionCompat.IsActive;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad && SentientMechRegistry.IsSentient(__instance))
            {
                HackingExpansionCompat.RemoveDisabledHediff(__instance);
            }
        }
    }
}
