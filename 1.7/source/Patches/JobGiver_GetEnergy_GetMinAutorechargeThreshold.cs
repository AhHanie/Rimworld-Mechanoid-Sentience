using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMinAutorechargeThreshold))]
    public static class JobGiver_GetEnergy_GetMinAutorechargeThreshold
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref int __result)
        {
            if (SentientMechRegistry.IsSentient(pawn))
            {
                __result = SentientRechargeUtility.MinThreshold(pawn.RaceProps.maxMechEnergy, SentientRechargeUtility.RangeOf(pawn));
            }
        }
    }
}
