using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.IsRequirementActive))]
    public static class ApparelUtility_IsRequirementActive
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result, ref string disabledByLabel)
        {
            if (__result && SentientMechRegistry.IsSentient(pawn) && !DeadMansSwitchCompat.CanWearApparel(pawn))
            {
                __result = false;
                disabledByLabel = "MechanoidSentience.CannotWearApparel".Translate();
            }
        }
    }
}
