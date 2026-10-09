using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanDraftMech))]
    public static class MechanitorUtility_CanDraftMech
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn mech, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !SentientMechRegistry.IsSentient(mech))
            {
                return;
            }
            if (mech.needs?.energy != null && mech.needs.energy.IsLowEnergySelfShutdown)
            {
                return;
            }
            __result = true;
        }
    }
}
