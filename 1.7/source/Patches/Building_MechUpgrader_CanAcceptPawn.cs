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
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderCanAcceptPawn;
        }

        [HarmonyPrefix]
        public static bool Prefix(Building_Enterable __instance, Pawn selPawn, ref AcceptanceReport __result)
        {
            if (!SentientMechRegistry.IsSentient(selPawn))
            {
                return true;
            }
            __result = MechanoidUpgradesCompat.CanAcceptSentient(__instance, selPawn);
            return false;
        }
    }
}
