using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechWeaponRules
    {
        private static HashSet<ThingDef> weaponlessSentientRaces;

        public static bool CannotEquipWeapons(Pawn pawn)
        {
            return pawn != null && IsWeaponlessRace(pawn.def);
        }

        public static bool IsWeaponlessRace(ThingDef sentientRace)
        {
            HashSet<ThingDef> set = weaponlessSentientRaces ?? Rebuild();
            return set.Count > 0 && sentientRace != null && set.Contains(sentientRace);
        }

        public static string CannotEquipReason(Pawn pawn)
        {
            return "MechanoidSentience.CannotEquipWeaponWithChassis".Translate(pawn.LabelShort);
        }

        internal static HashSet<ThingDef> Rebuild(bool logProblems = false)
        {
            var set = new HashSet<ThingDef>();
            foreach (SentientWeaponBlacklistDef blacklist in DefDatabase<SentientWeaponBlacklistDef>.AllDefsListForReading)
            {
                foreach (ThingDef original in blacklist.races)
                {
                    if (original == null)
                    {
                        continue;
                    }
                    ThingDef sentient = SentientMechRegistry.SentientOf(original);
                    if (sentient != null)
                    {
                        set.Add(sentient);
                    }
                    else if (logProblems)
                    {
                        Log.Warning("[Mechanoid Sentience] " + blacklist.defName + " lists " + original.defName + (original.race == null || !original.race.IsMechanoid ? ", which is not a mechanoid race" : ", which has no generated sentient counterpart") + ". The entry is ignored.");
                    }
                }
            }
            weaponlessSentientRaces = set;
            return set;
        }

        public static bool Disarm(Pawn pawn, bool allowDrop)
        {
            if (!CannotEquipWeapons(pawn) || pawn.equipment == null)
            {
                return true;
            }
            List<ThingWithComps> equipped = pawn.equipment.AllEquipmentListForReading;
            if (equipped.Count == 0)
            {
                ClearDroppedWeapon(pawn);
                return true;
            }
            bool allMoved = true;
            foreach (ThingWithComps item in new List<ThingWithComps>(equipped))
            {
                if (MoveOut(pawn, item, allowDrop))
                {
                    Logger.Message(pawn.ToStringSafe() + " cannot hold weapons, " + item + " was taken off it.");
                }
                else
                {
                    allMoved = false;
                    Logger.Warning("Could not take " + item + " off " + pawn.ToStringSafe() + ", which cannot hold weapons. It will be tried again later.");
                }
            }
            ClearDroppedWeapon(pawn);
            if (pawn.Spawned)
            {
                pawn.stances?.CancelBusyStanceSoft();
            }
            return allMoved;
        }

        private static bool MoveOut(Pawn pawn, ThingWithComps item, bool allowDrop)
        {
            if (allowDrop && pawn.Spawned && !item.def.destroyOnDrop && !SentientMechUtility.IsLockedWeapon(pawn, item)
                && pawn.equipment.TryDropEquipment(item, out _, pawn.Position, forbid: false))
            {
                return true;
            }
            if (pawn.inventory?.innerContainer == null)
            {
                return false;
            }
            pawn.inventory.innerContainer.TryAddOrTransfer(item);
            return !pawn.equipment.Contains(item);
        }

        private static void ClearDroppedWeapon(Pawn pawn)
        {
            if (pawn.mindState != null)
            {
                pawn.mindState.droppedWeapon = null;
            }
        }

        internal static void ForgetDroppedWeapon(Pawn pawn)
        {
            ClearDroppedWeapon(pawn);
        }
    }
}
