using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_PawnJoined))]
    public static class Faction_Notify_PawnJoined
    {
        private static readonly MethodInfo Humanlike = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.Humanlike));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsHumanlikeVisuals));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, Humanlike, Replacement, 1, nameof(Faction_Notify_PawnJoined));
        }
    }
}
