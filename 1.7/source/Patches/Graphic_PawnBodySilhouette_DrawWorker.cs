using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Graphic_PawnBodySilhouette), nameof(Graphic_PawnBodySilhouette.DrawWorker))]
    public static class Graphic_PawnBodySilhouette_DrawWorker
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.Humanlike), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsHumanlikeVisuals(__result, __instance);
        }
    }
}
