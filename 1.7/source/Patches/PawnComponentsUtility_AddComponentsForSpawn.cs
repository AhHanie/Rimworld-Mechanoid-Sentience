using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddComponentsForSpawn))]
    public static class PawnComponentsUtility_AddComponentsForSpawn
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            if (pawn.interactions == null)
            {
                pawn.interactions = new Pawn_InteractionsTracker(pawn);
            }
            if (pawn.relations == null)
            {
                pawn.relations = new Pawn_RelationsTracker(pawn);
            }
            if (ModsConfig.RoyaltyActive && pawn.psychicEntropy == null)
            {
                pawn.psychicEntropy = new Pawn_PsychicEntropyTracker(pawn);
            }
            pawn.genes = null;
        }
    }
}
