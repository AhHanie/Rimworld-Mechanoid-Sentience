using HarmonyLib;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_PickupDroppedWeapon), "TryGiveJob")]
    public static class JobGiver_PickupDroppedWeapon_TryGiveJob
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!SentientMechWeaponRules.CannotEquipWeapons(pawn))
            {
                return true;
            }
            SentientMechWeaponRules.ForgetDroppedWeapon(pawn);
            __result = null;
            return false;
        }
    }
}
