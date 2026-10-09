using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer))]
    public static class MechanitorUtility_GetOverseer
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref Pawn __result)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            __result = null;
            return false;
        }
    }
}
