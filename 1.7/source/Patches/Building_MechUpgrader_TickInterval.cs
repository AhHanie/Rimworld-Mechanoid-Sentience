using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Building_MechUpgrader_TickInterval
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgraderTickInterval != null && MechanoidUpgradesCompat.OverseerInfixesSafe;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderTickInterval;
        }

        [HarmonyPostfix]
        [HarmonyInfix(typeof(Pawn), nameof(Pawn.OverseerSubject), InnerTargetKind.Getter)]
        public static CompOverseerSubject SubjectPostfix(CompOverseerSubject __result, Pawn __instance)
        {
            return MechanoidUpgradesCompat.SubjectFor(__result, __instance);
        }

        [HarmonyPrefix]
        [HarmonyInfix(typeof(CompOverseerSubject), nameof(CompOverseerSubject.State), InnerTargetKind.Getter)]
        public static bool StatePrefix(CompOverseerSubject __instance, ref OverseerSubjectState __result)
        {
            return OverseerSubjectInfixes.AnswerState(__instance, ref __result);
        }
    }
}
