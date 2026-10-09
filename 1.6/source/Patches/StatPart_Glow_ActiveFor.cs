using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(StatPart_Glow), "ActiveFor")]
    public static class StatPart_Glow_ActiveFor
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing t, ref bool __result)
        {
            if (!(t is Pawn pawn) || !SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
