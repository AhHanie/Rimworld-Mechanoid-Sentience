using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
    public static class FloatMenuOptionProvider_DraftedMove_PawnGotoAction
    {
        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMech), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsRealColonyMech(__result, __instance);
        }
    }
}
