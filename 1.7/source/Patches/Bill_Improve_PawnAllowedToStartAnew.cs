using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Bill_Improve_PawnAllowedToStartAnew
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.ImproveBillAllowed != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.ImproveBillAllowed;
        }

        [HarmonyInfix(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer))]
        public static Pawn Postfix(Pawn __result, Pawn pawn)
        {
            return SentientMechUtility.CarrierGizmoOverseer(__result, pawn);
        }
    }
}
