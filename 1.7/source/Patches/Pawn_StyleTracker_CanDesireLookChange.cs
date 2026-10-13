using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_StyleTracker), nameof(Pawn_StyleTracker.CanDesireLookChange), MethodType.Getter)]
    public static class Pawn_StyleTracker_CanDesireLookChange
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.IdeologyActive;

        [HarmonyPostfix]
        public static void Postfix(Pawn_StyleTracker __instance, ref bool __result)
        {
            if (__result && SentientMechRegistry.IsSentient(__instance.pawn))
            {
                __result = false;
            }
        }
    }
}
