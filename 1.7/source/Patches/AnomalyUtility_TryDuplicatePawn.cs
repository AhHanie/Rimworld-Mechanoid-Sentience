using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.TryDuplicatePawn))]
    public static class AnomalyUtility_TryDuplicatePawn
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.AnomalyActive;

        [HarmonyPrefix]
        public static bool Prefix(Pawn originalPawn, out Pawn duplicatePawn, ref bool __result)
        {
            if (!SentientMechRegistry.IsSentient(originalPawn))
            {
                duplicatePawn = null;
                return true;
            }
            duplicatePawn = null;
            __result = false;
            return false;
        }
    }
}
