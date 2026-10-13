using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class ThoughtWorker_AgeReversalDemanded_ShouldHaveThought
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.IdeologyActive;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ThoughtWorker_AgeReversalDemanded), "ShouldHaveThought");
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            if (__result.Active && SentientMechRegistry.IsSentient(p))
            {
                __result = ThoughtState.Inactive;
            }
        }
    }
}
