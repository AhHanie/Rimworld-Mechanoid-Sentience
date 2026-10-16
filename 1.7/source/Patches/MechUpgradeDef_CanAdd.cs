using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class MechUpgradeDef_CanAdd
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgradeDefCanAdd != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgradeDefCanAdd;
        }

        [HarmonyPrefix]
        public static void Prefix(ref ThingDef t)
        {
            t = MechanoidUpgradesCompat.RestrictionRace(t);
        }
    }
}
