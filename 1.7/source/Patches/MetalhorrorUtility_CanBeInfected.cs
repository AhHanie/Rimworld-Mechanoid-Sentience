using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MetalhorrorUtility), nameof(MetalhorrorUtility.CanBeInfected))]
    public static class MetalhorrorUtility_CanBeInfected
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.AnomalyActive;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && SentientMechRegistry.IsSentient(pawn))
            {
                __result = false;
            }
        }
    }
}
