using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_DropWeapon), "TryGiveJob")]
    public static class JobGiver_DropWeapon_TryGiveJob
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && SentientMechUtility.IsLockedWeapon(pawn, __result.targetA.Thing))
            {
                __result = null;
            }
        }
    }
}
