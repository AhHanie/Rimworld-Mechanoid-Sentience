using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnCapacityWorker_Manipulation), nameof(PawnCapacityWorker_Manipulation.CalculateCapacityLevel))]
    public static class PawnCapacityWorker_Manipulation_CalculateCapacityLevel
    {
        [HarmonyPostfix]
        public static void Postfix(HediffSet diffSet, ref float __result)
        {
            if (__result > 0f)
            {
                return;
            }
            Pawn pawn = diffSet.pawn;
            if (!SentientMechRegistry.IsSentient(pawn) || pawn.RaceProps.body.HasPartWithTag(BodyPartTagDefOf.ManipulationLimbCore))
            {
                return;
            }
            __result = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
        }
    }
}
