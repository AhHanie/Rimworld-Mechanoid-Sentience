using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
    public static class Pawn_ApparelTracker_Wear
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, Apparel newApparel)
        {
            if (!SentientMechRegistry.IsSentient(___pawn) || DeadMansSwitchCompat.CanWearApparel(___pawn))
            {
                return true;
            }
            Logger.Warning(___pawn + " is a sentient mechanoid and cannot wear " + newApparel);
            return false;
        }
    }
}
