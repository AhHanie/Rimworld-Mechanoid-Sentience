using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    public static class PawnRenderer_ParallelGetPreRenderResults
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.Humanlike), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsHumanlikeVisuals(__result, __instance);
        }
    }
}
