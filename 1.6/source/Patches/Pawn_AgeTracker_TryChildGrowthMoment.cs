using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.TryChildGrowthMoment))]
    public static class Pawn_AgeTracker_TryChildGrowthMoment
    {
        private static readonly AccessTools.FieldRef<Pawn_AgeTracker, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_AgeTracker, Pawn>("pawn");

        [HarmonyPrefix]
        public static bool Prefix(Pawn_AgeTracker __instance, out int newPassionOptions, out int newTraitOptions, out int passionGainsCount)
        {
            newPassionOptions = 0;
            newTraitOptions = 0;
            passionGainsCount = 0;
            return !SentientMechRegistry.IsSentient(PawnOf(__instance));
        }
    }
}
