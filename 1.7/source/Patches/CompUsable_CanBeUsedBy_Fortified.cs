using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompUsable_CanBeUsedBy_Fortified
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return DeadMansSwitchCompat.CompUsableIsFleshReplacement != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return DeadMansSwitchCompat.CompUsableIsFleshReplacement;
        }

        [HarmonyPostfix]
        public static void Postfix(RaceProperties race, ref bool __result)
        {
            __result = SentientMechUtility.FleshOrSentient(__result, race);
        }
    }
}
