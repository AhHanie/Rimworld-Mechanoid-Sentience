using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(AutoUndrafter), "ShouldAutoUndraft")]
    public static class AutoUndrafter_ShouldAutoUndraft
    {
        private static readonly MethodInfo IsColonyMech = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonyMech));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsRealColonyMech));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, IsColonyMech, Replacement, 1, nameof(AutoUndrafter_ShouldAutoUndraft));
        }
    }
}
