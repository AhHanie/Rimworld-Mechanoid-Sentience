using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.IsTeetotaler))]
    public static class PawnUtility_IsTeetotaler
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && SentientMechRegistry.IsSentient(pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MentalStateWorker_BingingDrug), nameof(MentalStateWorker_BingingDrug.StateCanOccur))]
    public static class MentalStateWorker_BingingDrug_StateCanOccur
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
