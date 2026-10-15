using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.RandomizePawn))]
    public static class StartingPawnUtility_RandomizePawn
    {
        private const int MaxWorkTypeAttempts = 20;

        [HarmonyPrefix]
        public static bool Prefix(int pawnIndex)
        {
            if (!SentientStartScenario.TryGetMechSlot(pawnIndex, out PawnKindDef currentKind))
            {
                return true;
            }
            List<Pawn> roster = Find.GameInitData.startingAndOptionalPawns;
            if (pawnIndex >= roster.Count)
            {
                return true;
            }
            if (!TutorSystem.AllowAction("RandomizePawn"))
            {
                return false;
            }

            int attempts = 0;
            do
            {
                PawnKindDef chosenKind = SentientMechStartingPawnFactory.ChooseKind(currentKind);
                if (chosenKind == null || !SentientMechStartingPawnFactory.TryCreateWithFallback(chosenKind, out Pawn candidate, out PawnKindDef newKind))
                {
                    Messages.Message("MechanoidSentience.Scenario.RerollFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                Replace(pawnIndex, candidate, newKind);
                currentKind = newKind;
                attempts++;
            }
            while (attempts <= MaxWorkTypeAttempts && !StartingPawnUtility.WorkTypeRequirementsSatisfied());
            TutorSystem.Notify_Event("RandomizePawn");
            return false;
        }

        private static void Replace(int index, Pawn replacement, PawnKindDef newKind)
        {
            GameInitData data = Find.GameInitData;
            Pawn old = data.startingAndOptionalPawns[index];

            SpouseRelationUtility.Notify_PawnRegenerated(old);
            if (Current.ProgramState == ProgramState.Entry)
            {
                PawnUtility.TryDestroyStartingColonistFamily(old);
            }
            old.relations.ClearAllRelations();
            PawnComponentsUtility.RemoveComponentsOnDespawned(old);
            Find.WorldPawns.PassToWorld(old, PawnDiscardDecideMode.Discard);
            data.startingPossessions.Remove(old);
            PortraitsCache.SetDirty(old);

            data.startingAndOptionalPawns[index] = replacement;
            PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(index);
            request.KindDef = newKind;
            StartingPawnUtility.SetGenerationRequest(index, request);
        }
    }
}
