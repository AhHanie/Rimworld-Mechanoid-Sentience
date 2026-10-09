using HarmonyLib;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MentalBreakWorker_RunWild), nameof(MentalBreakWorker_RunWild.BreakCanOccur))]
    public static class MentalBreakWorker_RunWild_BreakCanOccur
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
