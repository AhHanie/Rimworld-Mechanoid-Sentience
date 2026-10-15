using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class EquipmentUtility_CanEquip
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result || !SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            if (!thing.def.IsWeapon)
            {
                cantReason = "MechanoidSentience.CannotEquipNonWeapon".Translate();
                __result = false;
                return;
            }
            if (thing.def.equipmentType != EquipmentType.Primary)
            {
                return;
            }
            ThingWithComps locked = SentientMechUtility.LockedPrimary(pawn);
            if (locked == null || locked == thing)
            {
                return;
            }
            cantReason = SentientMechUtility.CannotSwapReason(pawn, locked);
            __result = false;
        }
    }
}
