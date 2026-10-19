using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompCoreCarrier_CompGetGizmosExtra
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return GestatableApocritonCompat.CarrierGizmos != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return GestatableApocritonCompat.CarrierGizmos;
        }

        [HarmonyInfix(typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer))]
        public static Pawn Postfix(Pawn __result, Pawn pawn)
        {
            return SentientMechUtility.CarrierGizmoOverseer(__result, pawn);
        }
    }
}
