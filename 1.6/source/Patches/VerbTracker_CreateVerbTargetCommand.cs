using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(VerbTracker), "CreateVerbTargetCommand")]
    public static class VerbTracker_CreateVerbTargetCommand
    {
        private static readonly MethodInfo EverControllable = AccessTools.Method(typeof(MechanitorUtility), nameof(MechanitorUtility.EverControllable));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.EverControllableOrSentient));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, EverControllable, Replacement, 1, nameof(VerbTracker_CreateVerbTargetCommand));
        }
    }
}
