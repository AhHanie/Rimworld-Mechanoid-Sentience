using System;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    public static class StartingPawnUtility_NewGeneratedStartingPawn
    {
        [HarmonyPrefix]
        public static bool Prefix(int index, ref Pawn __result)
        {
            if (!SentientStartScenario.TryGetMechSlot(index, out PawnKindDef sentientKind))
            {
                return true;
            }
            if (!SentientMechStartingPawnFactory.TryCreateWithFallback(sentientKind, out Pawn pawn, out PawnKindDef usedKind))
            {
                throw new InvalidOperationException("Could not create a starting sentient mechanoid for roster slot " + index + ".");
            }
            if (usedKind != sentientKind)
            {
                PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(index);
                request.KindDef = usedKind;
                StartingPawnUtility.SetGenerationRequest(index, request);
            }
            __result = pawn;
            return false;
        }
    }
}
