using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Pawn_SexualityTracker_Suppressed
    {
        [HarmonyPrepare]
        public static bool Prepare() => RimpsycheCompat.IsAvailable;

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return RimpsycheCompat.CheckSuppressedMethod;
            if (RimpsycheCompat.SuppressedGetter != null)
            {
                yield return RimpsycheCompat.SuppressedGetter;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance, ref bool __result)
        {
            if (!__result && RimpsycheCompat.IsSentientTracker(__instance))
            {
                __result = true;
            }
        }
    }
}
