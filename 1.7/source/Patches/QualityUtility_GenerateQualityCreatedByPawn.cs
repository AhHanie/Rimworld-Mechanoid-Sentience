using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) })]
    public static class QualityUtility_GenerateQualityCreatedByPawn
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.IsMechanoid), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsOrdinaryMechanoid(__result, __instance);
        }
    }
}
