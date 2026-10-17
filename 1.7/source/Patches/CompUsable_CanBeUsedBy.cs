using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompUsable), nameof(CompUsable.CanBeUsedBy))]
    public static class CompUsable_CanBeUsedBy
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return DeadMansSwitchCompat.CompUsableIsFleshReplacement == null;
        }

        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.IsFlesh), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.FleshOrSentient(__result, __instance);
        }
    }
}
