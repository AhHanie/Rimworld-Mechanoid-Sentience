using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryTransferEquipmentToContainer))]
    public static class Pawn_EquipmentTracker_TryTransferEquipmentToContainer
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref bool __result)
        {
            if (!SentientMechUtility.IsLockedWeapon(__instance.pawn, eq))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
