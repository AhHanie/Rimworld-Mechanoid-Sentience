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
            if (!SentientMechRegistry.IsSentient(___pawn))
            {
                return true;
            }
            if (!newEq.def.IsWeapon)
            {
                Logger.Warning(___pawn + " is a sentient mechanoid and cannot equip the non-weapon " + newEq);
                PutBack(___pawn, newEq);
                return false;
            }
            ThingWithComps locked = SentientMechUtility.LockedPrimary(___pawn);
            if (locked == null || locked == newEq || newEq.def.equipmentType != EquipmentType.Primary)
            {
                return true;
            }
            Logger.Warning(___pawn + " has the built-in weapon " + locked + " and cannot equip " + newEq);
            PutBack(___pawn, newEq);
            return false;
        }

        private static void PutBack(Pawn pawn, ThingWithComps thing)
        {
            if (pawn.MapHeld != null)
            {
                GenPlace.TryPlaceThing(thing, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            }
            else
            {
                pawn.inventory.innerContainer.TryAdd(thing);
            }
        }
    }
}
