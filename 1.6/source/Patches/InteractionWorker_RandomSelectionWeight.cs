using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class InteractionWorker_RandomSelectionWeight
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight));
            yield return AccessTools.Method(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.RandomSelectionWeight));
        }

        [HarmonyPrefix]
        public static bool Prefix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (!SentientMechRegistry.IsSentient(initiator) && !SentientMechRegistry.IsSentient(recipient))
            {
                return true;
            }
            __result = 0f;
            return false;
        }
    }
}
