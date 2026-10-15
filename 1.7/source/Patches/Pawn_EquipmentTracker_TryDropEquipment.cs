using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    public static class Pawn_EquipmentTracker_TryDropEquipment
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps resultingEq, ref bool __result)
        {
            if (!SentientMechUtility.IsLockedWeapon(__instance.pawn, eq))
            {
                return true;
            }
            resultingEq = null;
            __result = false;
            return false;
        }
    }
}
