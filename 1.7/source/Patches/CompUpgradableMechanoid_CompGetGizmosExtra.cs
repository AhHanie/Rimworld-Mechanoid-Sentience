using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompUpgradableMechanoid_CompGetGizmosExtra
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgradableGizmos != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgradableGizmos;
        }

        [HarmonyInfix(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, Pawn __instance)
        {
            return SentientMechUtility.IsPlayerControlledOrSentient(__result, __instance);
        }
    }
}
