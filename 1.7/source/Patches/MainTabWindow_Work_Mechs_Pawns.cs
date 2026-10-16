using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class MainTabWindow_Work_Mechs_Pawns
    {
        private static MethodBase target;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            target = WorkModesCompat.WorkMechsPawnsGetter();
            return target != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return target ?? WorkModesCompat.WorkMechsPawnsGetter();
        }

        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<Pawn> __result)
        {
            if (__result != null)
            {
                __result = __result.Where(p => !SentientMechRegistry.IsSentient(p));
            }
        }
    }
}
