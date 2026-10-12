using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompMechCarrier_CompGetGizmosExtra
    {
        private static readonly MethodInfo GetOverseer = AccessTools.Method(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.CarrierGizmoOverseer));

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(CompMechCarrier), nameof(CompMechCarrier.CompGetGizmosExtra)));
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, GetOverseer, Replacement, 1, nameof(CompMechCarrier_CompGetGizmosExtra));
        }
    }
}
