using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class UpgradeComp_MechCarrier_CanSpawn
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.CarrierCanSpawn != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.CarrierCanSpawn;
        }

        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }

    [HarmonyPatch]
    public static class UpgradeComp_MechCarrier_CompGetGizmosExtra
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.CarrierGizmos != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.CarrierGizmos;
        }

        [HarmonyInfix(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer))]
        public static Pawn Postfix(Pawn __result, Pawn pawn)
        {
            return SentientMechUtility.CarrierGizmoOverseer(__result, pawn);
        }
    }
}
