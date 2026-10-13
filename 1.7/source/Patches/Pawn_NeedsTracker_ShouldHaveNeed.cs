using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Pawn_NeedsTracker_ShouldHaveNeed
    {
        [HarmonyPrefix]
        public static bool Prefix(NeedDef nd, Pawn ___pawn, ref bool __result)
        {
            if (!SentientMechRegistry.IsSentient(___pawn))
            {
                return true;
            }
            if (nd == MS_NeedDefOf.MechEnergy)
            {
                __result = ___pawn.Faction != null && ___pawn.Faction.IsPlayer;
                return false;
            }
            if (nd == MS_NeedDefOf.Mood || nd == MS_NeedDefOf.Joy || nd == MS_NeedDefOf.Beauty)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
