using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class UpgradeCompTurretGun_CanShoot
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.TurretCanShoot != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.TurretCanShoot;
        }

        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }

    [HarmonyPatch]
    public static class UpgradeCompTurretGun_CompGetGizmosExtra
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.TurretGizmos != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.TurretGizmos;
        }

        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }
}
