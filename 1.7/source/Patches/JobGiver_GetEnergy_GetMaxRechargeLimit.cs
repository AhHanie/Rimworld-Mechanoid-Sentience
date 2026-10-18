using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMaxRechargeLimit))]
    public static class JobGiver_GetEnergy_GetMaxRechargeLimit
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (SentientMechRegistry.IsSentient(pawn))
            {
                __result = SentientRechargeUtility.MaxLimit(pawn.RaceProps.maxMechEnergy, SentientRechargeUtility.RangeOf(pawn));
            }
        }
    }
}
