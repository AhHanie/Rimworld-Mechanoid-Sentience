using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment))]
    public static class Pawn_EquipmentTracker_AddEquipment
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, ThingWithComps newEq)
        {
            if (!SentientMechRegistry.IsSentient(___pawn) || newEq.def.IsWeapon)
            {
                return true;
            }
            Logger.Warning(___pawn + " is a sentient mechanoid and cannot equip the non-weapon " + newEq);
            if (___pawn.MapHeld != null)
            {
                GenPlace.TryPlaceThing(newEq, ___pawn.PositionHeld, ___pawn.MapHeld, ThingPlaceMode.Near);
            }
            else
            {
                ___pawn.inventory.innerContainer.TryAdd(newEq);
            }
            return false;
        }
    }
}
