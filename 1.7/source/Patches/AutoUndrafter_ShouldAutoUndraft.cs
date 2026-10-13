using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(AutoUndrafter), "ShouldAutoUndraft")]
    public static class AutoUndrafter_ShouldAutoUndraft
    {
        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMech), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsRealColonyMech(__result, __instance);
        }
    }
}
