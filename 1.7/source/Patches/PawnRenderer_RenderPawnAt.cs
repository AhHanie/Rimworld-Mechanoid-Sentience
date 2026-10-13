using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class PawnRenderer_RenderPawnAt
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.Humanlike), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsHumanlikeVisuals(__result, __instance);
        }
    }
}
