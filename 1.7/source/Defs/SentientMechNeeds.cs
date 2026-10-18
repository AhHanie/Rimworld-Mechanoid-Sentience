using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechNeeds
    {
        private static readonly string[] StaleHediffDefNames = { "BadHygiene", "DBHDehydration" };

        public static bool IsAllowed(NeedDef need)
        {
            return need != null && (need == MS_NeedDefOf.Mood || need == MS_NeedDefOf.Joy || need == MS_NeedDefOf.Beauty || need == MS_NeedDefOf.MechEnergy);
        }

        public static bool HasExactlyAllowedNeeds(Pawn_NeedsTracker needs)
        {
            return needs != null && needs.mood != null && needs.joy != null && needs.beauty != null && needs.energy != null
                && needs.food == null && needs.rest == null && needs.AllNeeds.Count == 4;
        }

        public static void MigrateLoadedPawn(Pawn pawn)
        {
            if (pawn?.needs == null || !SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            if (HasDisallowedNeed(pawn.needs.AllNeeds))
            {
                Logger.Message("Removing needs a sentient mechanoid may not have from " + pawn.ToStringSafe() + ".");
                pawn.needs.AddOrRemoveNeedsAsAppropriate();
            }
            RemoveStaleHediffs(pawn);
        }

        private static bool HasDisallowedNeed(List<Need> needs)
        {
            for (int i = 0; i < needs.Count; i++)
            {
                if (!IsAllowed(needs[i].def))
                {
                    return true;
                }
            }
            return false;
        }

        private static void RemoveStaleHediffs(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i].def != null && System.Array.IndexOf(StaleHediffDefNames, hediffs[i].def.defName) >= 0)
                {
                    pawn.health.RemoveHediff(hediffs[i]);
                }
            }
        }
    }
}
