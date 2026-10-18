using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMetalhorror
    {
        public static bool IsImplant(Hediff hediff)
        {
            return hediff?.def != null && hediff.def == HediffDefOf.MetalhorrorImplant;
        }

        public static void RemoveMetalhorrorImplants(Pawn pawn)
        {
            if (!ModsConfig.AnomalyActive || !SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            Pawn_HealthTracker health = pawn.health;
            if (health?.hediffSet?.hediffs == null)
            {
                return;
            }
            var hediffs = health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = hediffs[i];
                if (IsImplant(hediff))
                {
                    health.RemoveHediff(hediff);
                }
            }
        }
    }
}
