using System.Reflection;
using HarmonyLib;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class RimHud_RowLayer_Prepare
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return RimHudEnergyRow.IsAvailable;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return RimHudEnergyRow.RowPrepareMethod;
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance, out int __state)
        {
            __state = RimHudEnergyRow.WidenRow(__instance);
        }

        [HarmonyFinalizer]
        public static void Finalizer(object __instance, int __state)
        {
            RimHudEnergyRow.RestoreRow(__instance, __state);
        }
    }
}
