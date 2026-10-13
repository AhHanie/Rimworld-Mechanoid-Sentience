using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.CurKindLifeStage), MethodType.Getter)]
    public static class Pawn_AgeTracker_CurKindLifeStage
    {
        private static readonly AccessTools.FieldRef<Pawn_AgeTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_AgeTracker, Pawn>("pawn");

        [HarmonyPrefix]
        public static bool Prefix(Pawn_AgeTracker __instance, ref PawnKindLifeStage __result)
        {
            Pawn pawn = PawnOf(__instance);
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            __result = pawn.kindDef.lifeStages[__instance.CurLifeStageIndex];
            return false;
        }
    }
}
