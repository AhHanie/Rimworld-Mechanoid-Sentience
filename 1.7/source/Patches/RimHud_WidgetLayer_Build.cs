using System.Reflection;
using HarmonyLib;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class RimHud_WidgetLayer_Build
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return RimHudEnergyRow.IsAvailable;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return RimHudEnergyRow.WidgetBuildMethod;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            RimHudEnergyRow.RebuildBlankedWidget(__instance);
        }
    }
}
