using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class UpgradeComp_LaserDefence_CompGetGizmosExtra
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.LaserDefenceGizmos != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.LaserDefenceGizmos;
        }

        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }
}
