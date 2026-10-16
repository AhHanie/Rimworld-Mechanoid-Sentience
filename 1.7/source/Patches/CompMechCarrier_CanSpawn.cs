using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompMechCarrier), nameof(CompMechCarrier.CanSpawn), MethodType.Getter)]
    public static class CompMechCarrier_CanSpawn
    {
        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }
}
