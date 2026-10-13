using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.CreateInitialComponents))]
    public static class PawnComponentsUtility_CreateInitialComponents
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            pawn.genes = null;
            if (pawn.relations == null)
            {
                pawn.relations = new Pawn_RelationsTracker(pawn);
            }
            if (ModsConfig.RoyaltyActive && pawn.psychicEntropy == null)
            {
                pawn.psychicEntropy = new Pawn_PsychicEntropyTracker(pawn);
            }
        }
    }
}
