using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(GasUtility), nameof(GasUtility.IsAffectedByExposure))]
    public static class GasUtility_IsAffectedByExposure
    {
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
