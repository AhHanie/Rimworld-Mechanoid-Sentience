using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnWoundDrawer), "WriteCache")]
    public static class PawnWoundDrawer_WriteCache
    {
        private static readonly MethodInfo Humanlike = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.Humanlike));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsHumanlikeVisuals));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, Humanlike, Replacement, 2, nameof(PawnWoundDrawer_WriteCache));
        }
    }
}
