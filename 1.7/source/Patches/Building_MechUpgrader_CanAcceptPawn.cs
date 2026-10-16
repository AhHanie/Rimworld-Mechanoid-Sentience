using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Building_MechUpgrader_CanAcceptPawn
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn != null && MechanoidUpgradesCompat.OverseerInfixesSafe;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn;
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

    [HarmonyPatch]
    public static class Building_MechUpgrader_CanAcceptPawn_Guard
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn != null && !MechanoidUpgradesCompat.OverseerInfixesSafe;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn;
        }

        [HarmonyPrefix]
        public static bool Prefix(Pawn selPawn, ref AcceptanceReport __result)
        {
            if (!SentientMechRegistry.IsSentient(selPawn))
            {
                return true;
            }
            __result = "MechanoidSentience.MuUpgrader.Unsupported".Translate();
            return false;
        }
    }

    public static class OverseerSubjectInfixes
    {
        public static bool AnswerState(CompOverseerSubject subject, ref OverseerSubjectState result)
        {
            if (!MechanoidUpgradesCompat.IsSentientSubject(subject))
            {
                return true;
            }
            result = OverseerSubjectState.Overseen;
            return false;
        }
    }
}
