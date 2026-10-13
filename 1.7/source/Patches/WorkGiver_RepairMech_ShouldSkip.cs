using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(WorkGiver_RepairMech), nameof(WorkGiver_RepairMech.ShouldSkip))]
    public static class WorkGiver_RepairMech_ShouldSkip
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

    [HarmonyPatch(typeof(WorkGiver_RepairMech), nameof(WorkGiver_RepairMech.HasJobOnThing))]
    public static class WorkGiver_RepairMech_HasJobOnThing
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (__result && pawn == t)
            {
                __result = false;
            }
        }
    }
}
