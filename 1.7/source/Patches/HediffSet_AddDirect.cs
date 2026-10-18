using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    public static class HediffSet_AddDirect
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.AnomalyActive;

        [HarmonyPrefix]
        public static bool Prefix(HediffSet __instance, Hediff hediff)
        {
            return !(SentientMetalhorror.IsImplant(hediff) && SentientMechRegistry.IsSentient(__instance.pawn));
        }
    }
}
