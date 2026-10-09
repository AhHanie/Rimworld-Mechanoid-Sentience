using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) })]
    public static class QualityUtility_GenerateQualityCreatedByPawn
    {
        private static readonly MethodInfo IsMechanoid = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.IsMechanoid));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsOrdinaryMechanoid));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, IsMechanoid, Replacement, 1, nameof(QualityUtility_GenerateQualityCreatedByPawn));
        }
    }
}
