using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Pawn_SexualityTracker_GetRelationshipWith
    {
        [HarmonyPrepare]
        public static bool Prepare() => RimpsycheCompat.IsAvailable;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => RimpsycheCompat.RelationshipWithMethod;

        [HarmonyPostfix]
        public static void Postfix(object __instance, Pawn target, ref float __result)
        {
            if (__result != 0f && (SentientMechRegistry.IsSentient(target) || RimpsycheCompat.IsSentientTracker(__instance)))
            {
                __result = 0f;
            }
        }
    }
}
