using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompUseEffect_InstallImplant_BrainPart
    {
        private static readonly MethodInfo GetPartsWithDef = AccessTools.Method(typeof(BodyDef), nameof(BodyDef.GetPartsWithDef));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.GetPartsWithDefOrArtificialBrain));

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.DoEffect));
            yield return AccessTools.Method(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.CanBeUsedBy));
            yield return AccessTools.Method(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.GetExistingImplant));
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, GetPartsWithDef, Replacement, 1, nameof(CompUseEffect_InstallImplant_BrainPart));
        }
    }
}
