using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Graphic_PawnBodySilhouette), nameof(Graphic_PawnBodySilhouette.DrawWorker))]
    public static class Graphic_PawnBodySilhouette_DrawWorker
    {
        private static readonly MethodInfo Humanlike = AccessTools.PropertyGetter(typeof(RaceProperties), nameof(RaceProperties.Humanlike));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.IsHumanlikeVisuals));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SentientMechUtility.ReplaceCalls(instructions, Humanlike, Replacement, 2, nameof(Graphic_PawnBodySilhouette_DrawWorker));
        }
    }
}
