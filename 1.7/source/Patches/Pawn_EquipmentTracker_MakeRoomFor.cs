using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.MakeRoomFor), new[] { typeof(ThingWithComps), typeof(ThingWithComps) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
    public static class Pawn_EquipmentTracker_MakeRoomFor
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps dropped)
        {
            if (eq.def.equipmentType != EquipmentType.Primary || SentientMechUtility.LockedPrimary(__instance.pawn) == null)
            {
                return true;
            }
            dropped = null;
            return false;
        }
    }
}
