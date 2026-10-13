using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class MechanitorUtility_InMechanitorCommandRange
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn mech, ref bool __result)
        {
            if (!__result && SentientMechRegistry.IsSentient(mech))
            {
                __result = true;
            }
        }
    }
}
