using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class TrueRpgInventoryCompat
    {
        private const string PackageId = "astryl.TrueRPGInventory";
        private const string GearCommandsTypeName = "TrueRPGInventory.GearCommands";
        private const string HoradricTypeName = "TrueRPGInventory.CompatHoradric";

        private static bool bound;

        public static MethodBase EquipMethod { get { Bind(); return equipMethod; } }
        public static MethodBase UnequipMethod { get { Bind(); return unequipMethod; } }
        public static MethodBase MakeSidearmMethod { get { Bind(); return makeSidearmMethod; } }
        public static MethodBase CycleWeaponMethod { get { Bind(); return cycleWeaponMethod; } }
        public static MethodBase MoveToCubeMethod { get { Bind(); return moveToCubeMethod; } }

        private static MethodInfo equipMethod;
        private static MethodInfo unequipMethod;
        private static MethodInfo makeSidearmMethod;
        private static MethodInfo cycleWeaponMethod;
        private static MethodInfo moveToCubeMethod;

        private static void Bind()
        {
            if (bound || !ModsConfig.IsActive(PackageId))
            {
                return;
            }
            bound = true;

            Type commands = AccessTools.TypeByName(GearCommandsTypeName);
            Type horadric = AccessTools.TypeByName(HoradricTypeName);
            equipMethod = FindStatic(commands, "Equip", typeof(void), typeof(Pawn), typeof(ThingWithComps));
            unequipMethod = FindStatic(commands, "UnequipToInventory", typeof(void), typeof(Pawn), typeof(Thing));
            makeSidearmMethod = FindStatic(commands, "MakeSidearm", typeof(void), typeof(Pawn), typeof(ThingWithComps));
            cycleWeaponMethod = FindStatic(commands, "CycleWeapon", typeof(void), typeof(Pawn));
            moveToCubeMethod = FindStatic(horadric, "TryMoveToCube", typeof(bool), typeof(Pawn), typeof(Thing));

            var missing = new List<string>();
            if (equipMethod == null) missing.Add("GearCommands.Equip");
            if (unequipMethod == null) missing.Add("GearCommands.UnequipToInventory");
            if (makeSidearmMethod == null) missing.Add("GearCommands.MakeSidearm");
            if (cycleWeaponMethod == null) missing.Add("GearCommands.CycleWeapon");
            if (moveToCubeMethod == null) missing.Add("CompatHoradric.TryMoveToCube");
            if (missing.Count > 0)
            {
                Log.Warning("[Mechanoid Sentience] True RPG Inventory is active, but these compatibility targets changed or are missing: "
                    + string.Join(", ", missing) + ". Their matching weapon guards were skipped.");
            }
        }

        private static MethodInfo FindStatic(Type type, string name, Type returnType, params Type[] arguments)
        {
            MethodInfo method = type == null ? null : AccessTools.Method(type, name, arguments);
            return method != null && method.IsStatic && method.ReturnType == returnType ? method : null;
        }

        public static void RejectDrop(Pawn pawn, ThingWithComps locked)
        {
            Messages.Message(SentientMechUtility.CannotDropReason(pawn, locked), pawn, MessageTypeDefOf.RejectInput, historical: false);
        }

        public static void RejectSwap(Pawn pawn, ThingWithComps locked)
        {
            Messages.Message(SentientMechUtility.CannotSwapReason(pawn, locked), pawn, MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
