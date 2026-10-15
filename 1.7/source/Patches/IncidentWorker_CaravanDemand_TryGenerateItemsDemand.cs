using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class IncidentWorker_CaravanDemand_TryGenerateItemsDemand
    {
        private static readonly MethodBase Target = AccessTools.Method(typeof(IncidentWorker_CaravanDemand), "TryGenerateItemsDemand", new[] { typeof(Caravan) });

        [HarmonyPrepare]
        public static bool Prepare()
        {
            if (Target == null)
            {
                Logger.Warning("IncidentWorker_CaravanDemand.TryGenerateItemsDemand was not found, locked weapons stay demandable.");
            }
            return Target != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => Target;

        [HarmonyPostfix]
        public static void Postfix(ref List<ThingCount> __result)
        {
            __result?.RemoveAll(IsLockedWeaponOfSentientMech);
        }

        private static bool IsLockedWeaponOfSentientMech(ThingCount demanded)
        {
            Thing thing = demanded.Thing;
            return thing?.ParentHolder is Pawn_EquipmentTracker tracker && SentientMechUtility.IsLockedWeapon(tracker.pawn, thing);
        }
    }
}
