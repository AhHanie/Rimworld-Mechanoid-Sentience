using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompCoreCarrier_PostSpawnSetup
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return GestatableApocritonCompat.CarrierSpawnSetup != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return GestatableApocritonCompat.CarrierSpawnSetup;
        }

        public static void Postfix(ThingComp __instance)
        {
            GestatableApocritonCompat.EnsureStorage(__instance);
        }
    }
}
