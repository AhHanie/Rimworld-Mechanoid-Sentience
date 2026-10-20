using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class TrueRpgInventory_Equip
    {
        [HarmonyPrepare]
        public static bool Prepare() => TrueRpgInventoryCompat.EquipMethod != null;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TrueRpgInventoryCompat.EquipMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn __0)
        {
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__0);
            if (locked == null)
            {
                return true;
            }
            TrueRpgInventoryCompat.RejectSwap(__0, locked);
            return false;
        }
    }

    [HarmonyPatch]
    public static class TrueRpgInventory_UnequipToInventory
    {
        [HarmonyPrepare]
        public static bool Prepare() => TrueRpgInventoryCompat.UnequipMethod != null;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TrueRpgInventoryCompat.UnequipMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn __0, Thing __1)
        {
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__0);
            if (locked == null || locked != __1)
            {
                return true;
            }
            TrueRpgInventoryCompat.RejectDrop(__0, locked);
            return false;
        }
    }

    [HarmonyPatch]
    public static class TrueRpgInventory_MakeSidearm
    {
        [HarmonyPrepare]
        public static bool Prepare() => TrueRpgInventoryCompat.MakeSidearmMethod != null;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TrueRpgInventoryCompat.MakeSidearmMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn __0, ThingWithComps __1)
        {
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__0);
            if (locked == null || locked != __1)
            {
                return true;
            }
            TrueRpgInventoryCompat.RejectDrop(__0, locked);
            return false;
        }
    }

    [HarmonyPatch]
    public static class TrueRpgInventory_CycleWeapon
    {
        [HarmonyPrepare]
        public static bool Prepare() => TrueRpgInventoryCompat.CycleWeaponMethod != null;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TrueRpgInventoryCompat.CycleWeaponMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn __0)
        {
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__0);
            if (locked == null)
            {
                return true;
            }
            TrueRpgInventoryCompat.RejectSwap(__0, locked);
            return false;
        }
    }

    [HarmonyPatch]
    public static class TrueRpgInventory_MoveToCube
    {
        [HarmonyPrepare]
        public static bool Prepare() => TrueRpgInventoryCompat.MoveToCubeMethod != null;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TrueRpgInventoryCompat.MoveToCubeMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn __0, Thing __1, ref bool __result)
        {
            ThingWithComps locked = SentientMechUtility.LockedPrimary(__0);
            if (locked == null || locked != __1)
            {
                return true;
            }
            TrueRpgInventoryCompat.RejectDrop(__0, locked);
            __result = false;
            return false;
        }
    }
}
