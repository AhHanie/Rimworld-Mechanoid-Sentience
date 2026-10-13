using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(VerbTracker), "CreateVerbTargetCommand")]
    public static class VerbTracker_CreateVerbTargetCommand
    {
        [HarmonyInfix(typeof(MechanitorUtility), nameof(MechanitorUtility.EverControllable))]
        public static bool Postfix(bool __result, Pawn mech)
        {
            return SentientMechUtility.EverControllableOrSentient(__result, mech);
        }
    }
}
