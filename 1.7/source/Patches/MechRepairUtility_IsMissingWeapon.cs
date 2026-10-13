using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MechRepairUtility), nameof(MechRepairUtility.IsMissingWeapon))]
    public static class MechRepairUtility_IsMissingWeapon
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn mech, ref bool __result)
        {
            if (__result && SentientMechRegistry.IsSentient(mech))
            {
                __result = false;
            }
        }
    }
}
