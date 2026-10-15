using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechStartingPawnFactory
    {
        private const int MaxAttempts = 5;
        private const int FallbackAttempts = 2;
        private const int MaxFallbackKinds = 4;

#if DEBUG
        public static bool ForceFailureForTesting;
#endif

        public static List<PawnKindDef> StarterKinds()
        {
            var result = new List<PawnKindDef>();
            foreach (PawnKindDef original in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (original.race == null
                    || DeadMansSwitchCompat.KindOfOriginal(original.race) == DmsRaceKind.PlatformDrone
                    || !SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentient))
                {
                    continue;
                }
                result.Add(sentient);
            }
            result.Sort((x, y) => string.CompareOrdinal(x.defName, y.defName));
            return result;
        }

        public static bool TryCreateWithFallback(PawnKindDef preferredKind, out Pawn pawn, out PawnKindDef usedKind)
        {
            usedKind = preferredKind;
            if (preferredKind != null && TryCreate(preferredKind, out pawn, MaxAttempts, logFailure: false))
            {
                return true;
            }
            var failed = new List<string>();
            if (preferredKind != null)
            {
                failed.Add(preferredKind.defName);
            }
            List<PawnKindDef> others = StarterKinds();
            others.Remove(preferredKind);
            for (int i = 0; i < MaxFallbackKinds && others.Count > 0; i++)
            {
                PawnKindDef next = others.RandomElement();
                others.Remove(next);
                if (TryCreate(next, out pawn, FallbackAttempts, logFailure: false))
                {
                    usedKind = next;
                    Log.Warning("[Mechanoid Sentience] Could not create a starting sentient " + string.Join(", ", failed) + "; made a " + next.defName + " instead.");
                    return true;
                }
                failed.Add(next.defName);
            }
            pawn = null;
            usedKind = null;
            Log.Error("[Mechanoid Sentience] Could not create a starting sentient mechanoid. Tried: " + string.Join(", ", failed) + ". See the earlier messages for the cause.");
            return false;
        }

        public static PawnKindDef ChooseKind(PawnKindDef current)
        {
            List<PawnKindDef> kinds = StarterKinds();
            if (kinds.Count == 0)
            {
                return null;
            }
            if (kinds.Count > 1 && current != null)
            {
                kinds.Remove(current);
            }
            return kinds.RandomElement();
        }

        public static bool TryCreate(PawnKindDef sentientKind, out Pawn pawn, int maxAttempts = MaxAttempts, bool logFailure = true)
        {
            pawn = null;
            if (!TryResolve(sentientKind, out ThingDef originalRace, out PawnKindDef originalKind, out ThingDef sentientRace))
            {
                Log.Error("[Mechanoid Sentience] Cannot create a starting sentient mechanoid of kind " + (sentientKind?.defName ?? "null") + ": it is not a generated sentient kind.");
                return false;
            }

            Exception firstError = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Pawn candidate = null;
                try
                {
#if DEBUG
                    if (ForceFailureForTesting)
                    {
                        throw new InvalidOperationException("Forced failure (SentientMechStartingPawnFactory.ForceFailureForTesting).");
                    }
#endif
                    candidate = GenerateOriginal(originalKind);
                    Validate(candidate, originalRace);
                    SentientMechConverter.PrepareUnspawned(candidate, sentientRace, sentientKind);

                    candidate.relations.everSeenByPlayer = true;
                    PawnComponentsUtility.AddComponentsForSpawn(candidate);
                    VerifyStarter(candidate, sentientKind);
                    StartingPawnUtility.GeneratePossessions(candidate);
                    pawn = candidate;
                    return true;
                }
                catch (Exception ex)
                {
                    firstError ??= ex;
                    Discard(candidate);
                }
            }
            string failure = "[Mechanoid Sentience] Could not create a starting sentient " + sentientKind.defName + " after " + maxAttempts + " attempts. First error: " + firstError;
            if (logFailure)
            {
                Log.Error(failure);
            }
            else
            {
                Log.Warning(failure);
            }
            return false;
        }

        private static bool TryResolve(PawnKindDef sentientKind, out ThingDef originalRace, out PawnKindDef originalKind, out ThingDef sentientRace)
        {
            originalRace = null;
            originalKind = null;
            sentientRace = null;
            if (sentientKind == null || !SentientMechRegistry.IsSentientKind(sentientKind))
            {
                return false;
            }
            originalKind = SentientMechRegistry.OriginalOf(sentientKind);
            originalRace = originalKind?.race;
            if (originalRace == null || !SentientMechRegistry.TryGetSentient(originalRace, originalKind, out sentientRace, out PawnKindDef resolved))
            {
                return false;
            }
            return resolved == sentientKind;
        }

        private static Pawn GenerateOriginal(PawnKindDef originalKind)
        {
            var request = new PawnGenerationRequest(originalKind, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, developmentalStages: DevelopmentalStage.Adult);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            if (pawn == null)
            {
                throw new InvalidOperationException("The pawn generator returned no mechanoid for " + originalKind.defName + ".");
            }
            return pawn;
        }

        private static void Validate(Pawn pawn, ThingDef originalRace)
        {
            if (pawn.def != originalRace || !pawn.RaceProps.IsMechanoid)
            {
                throw new InvalidOperationException("Generated " + pawn.ToStringSafe() + " is not a " + originalRace.defName + ".");
            }
            if (DeadMansSwitchCompat.IsPlatformDrone(pawn))
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " is a platform drone.");
            }
            if (pawn.health?.hediffSet?.GetBrain() == null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has no brain.");
            }
        }

        private static void VerifyStarter(Pawn pawn, PawnKindDef sentientKind)
        {
            if (pawn.kindDef != sentientKind || !SentientMechRegistry.IsSentient(pawn))
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " did not become " + sentientKind.defName + ".");
            }
            if (pawn.Faction != Faction.OfPlayer)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " is not in the player faction.");
            }
            if (pawn.Name == null || !pawn.Name.IsValid)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has no valid name.");
            }
            if (pawn.story == null || pawn.skills == null || pawn.workSettings == null || pawn.relations == null || pawn.timetable == null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " lacks a humanlike tracker.");
            }
            if (pawn.genes != null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has genes.");
            }
            Pawn_NeedsTracker needs = pawn.needs;
            if (needs == null || needs.mood == null || needs.joy == null || needs.beauty == null || needs.energy == null || needs.food != null || needs.rest != null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has the wrong needs.");
            }
            if (pawn.GetComp<CompOverseerSubject>() != null || pawn.OverseerSubject != null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " still has an overseer comp.");
            }
        }

        private static void Discard(Pawn candidate)
        {
            if (candidate == null)
            {
                return;
            }
            try
            {
                if (Find.WorldPawns.Contains(candidate))
                {
                    Find.WorldPawns.RemovePawn(candidate);
                }
                Find.WorldPawns.PassToWorld(candidate, PawnDiscardDecideMode.Discard);
            }
            catch (Exception ex)
            {
                Log.Warning("[Mechanoid Sentience] Could not discard a failed starting mechanoid candidate: " + ex.Message);
            }
        }
    }
}
