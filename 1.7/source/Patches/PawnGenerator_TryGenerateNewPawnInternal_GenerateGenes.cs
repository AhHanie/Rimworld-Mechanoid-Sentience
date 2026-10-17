using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class PawnGenerator_TryGenerateNewPawnInternal_GenerateGenes
    {
        [HarmonyInfix(typeof(PawnGenerator), "GenerateGenes", typeof(Pawn), typeof(XenotypeDef), typeof(PawnGenerationRequest))]
        public static bool Prefix(Pawn pawn)
        {
            return pawn.genes != null || !SentientMechRegistry.IsSentient(pawn);
        }
    }
}
