using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider), nameof(FloatMenuOptionProvider.SelectedPawnValid))]
    public static class FloatMenuOptionProvider_SelectedPawnValid
    {
        private static readonly MethodInfo IsMechanoid = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.IsMechanoid));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsOrdinaryMechanoid));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, IsMechanoid, Replacement, 1, nameof(FloatMenuOptionProvider_SelectedPawnValid));
        }
    }
}
