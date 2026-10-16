using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Building_MechUpgrader_GetInspectString
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgraderInspectString != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderInspectString;
        }

        [HarmonyPostfix]
        public static void Postfix(Building_Enterable __instance, ref string __result)
        {
            __result = MechanoidUpgradesCompat.WithSentientWaitingText(__result, __instance);
        }
    }

    [HarmonyPatch]
    public static class ScenPart_Upgrades_MechDefs
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.ScenPartMechDefs != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.ScenPartMechDefs;
        }

        [HarmonyPostfix]
        public static void Postfix(List<PawnKindDef> __result)
        {
            MechanoidUpgradesCompat.HideSentientKinds(__result);
        }
    }
}
