using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompMechCarrier), nameof(CompMechCarrier.CanSpawn), MethodType.Getter)]
    public static class CompMechCarrier_CanSpawn
    {
        private static readonly MethodInfo PlayerControlled = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsCarrierPlayerControlled));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, PlayerControlled, Replacement, 1, nameof(CompMechCarrier_CanSpawn));
        }
    }
}
