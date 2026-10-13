using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Pawn_SetFaction
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn __instance, out PawnKindDef __state)
        {
            __state = SentientMechRegistry.IsSentient(__instance) ? __instance.kindDef : null;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, PawnKindDef __state)
        {
            if (__state != null && __instance.kindDef != __state)
            {
                __instance.kindDef = __state;
            }
        }
    }
}
