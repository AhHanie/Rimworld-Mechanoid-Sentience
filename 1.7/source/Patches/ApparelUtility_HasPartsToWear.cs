using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear))]
    public static class ApparelUtility_HasPartsToWear
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (__result && SentientMechRegistry.IsSentient(p) && !DeadMansSwitchCompat.CanWearApparel(p))
            {
                __result = false;
            }
        }
    }
}
