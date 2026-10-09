using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompUsable), nameof(CompUsable.CanBeUsedBy))]
    public static class CompUsable_CanBeUsedBy
    {
        private static readonly MethodInfo IsFlesh = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.IsFlesh));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.FleshOrSentient));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, IsFlesh, Replacement, 1, nameof(CompUsable_CanBeUsedBy));
        }
    }
}
