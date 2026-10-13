using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompMechCarrier_CompGetGizmosExtra
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.StateMachineMoveNext(AccessTools.Method(typeof(CompMechCarrier), nameof(CompMechCarrier.CompGetGizmosExtra)));
        }

        [HarmonyInfix(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer))]
        public static Pawn Postfix(Pawn __result, Pawn pawn)
        {
            return SentientMechUtility.CarrierGizmoOverseer(__result, pawn);
        }
    }
}
