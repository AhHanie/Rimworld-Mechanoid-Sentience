using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_IdeoTracker), "CertaintyChangeFactor", MethodType.Getter)]
    public static class Pawn_IdeoTracker_CertaintyChangeFactor
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.IdeologyActive;

        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, ref float __result)
        {
            if (!SentientMechRegistry.IsSentient(___pawn))
            {
                return true;
            }
            __result = 1f;
            return false;
        }
    }
}
