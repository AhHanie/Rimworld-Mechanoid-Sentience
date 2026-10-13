using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class JobGiver_Work_PawnCanUseWorkGiver
    {
        [HarmonyInfix(typeof(WorkGiverDef), nameof(WorkGiverDef.canBeDoneByMechs), InnerTargetKind.FieldRead)]
        public static bool Postfix(bool __result, [HarmonyOuter] Pawn pawn)
        {
            return SentientMechUtility.CanBeDoneByMech(__result, pawn);
        }
    }
}
