using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobDriver_Equip), nameof(JobDriver_Equip.TryMakePreToilReservations))]
    public static class JobDriver_Equip_TryMakePreToilReservations
    {
        [HarmonyPrefix]
        public static bool Prefix(JobDriver_Equip __instance, ref bool __result)
        {
            if (SentientMechWeaponRules.CannotEquipWeapons(__instance.pawn))
            {
                __result = false;
                return false;
            }
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__instance.pawn);
            if (locked == null)
            {
                return true;
            }
            Thing target = __instance.job.targetA.Thing;
            if (target == null || target == locked || target.def.equipmentType != EquipmentType.Primary)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
