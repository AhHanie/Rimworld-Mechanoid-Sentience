using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.EverControllable))]
    public static class MechanitorUtility_EverControllable
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn mech, ref bool __result)
        {
            __result = SentientMechUtility.EverControllableOrSentient(__result, mech);
        }
    }
}
