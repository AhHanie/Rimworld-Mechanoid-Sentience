using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought))]
    public static class ThoughtUtility_CanGetThought
    {
        private static readonly HashSet<string> BlockedWorkerClassNames = new HashSet<string>
        {
            "ThoughtWorker_Greedy", "ThoughtWorker_BedroomJealous", "ThoughtWorker_Precept_HighLife", "ThoughtWorker_Precept_Scarification",
            "ThoughtWorker_Precept_HasNoProsthetic", "ThoughtWorker_NoPersonalBedroom", "ThoughtWorker_BedroomRequirementsNotMet",
            "ThoughtWorker_NoThroneroom", "ThoughtWorker_ThroneRoomRequirementsNotMet", "ThoughtWorker_RoyalTitleApparelRequirementNotMet",
            "ThoughtWorker_Cold", "ThoughtWorker_Hot"
        };

        private const string RoyalApparelWorkerName = "ThoughtWorker_RoyalTitleApparelRequirementNotMet";

        private static HashSet<ThoughtDef> blockedThoughts;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ThoughtDef def, ref bool __result)
        {
            if (!__result || !SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            if (blockedThoughts == null)
            {
                blockedThoughts = new HashSet<ThoughtDef>();
                foreach (ThoughtDef thought in DefDatabase<ThoughtDef>.AllDefsListForReading)
                {
                    Type worker = thought.workerClass;
                    if (worker != null && BlockedWorkerClassNames.Contains(worker.Name))
                    {
                        blockedThoughts.Add(thought);
                    }
                }
            }
            if (blockedThoughts.Contains(def) && !(def.workerClass?.Name == RoyalApparelWorkerName && DeadMansSwitchCompat.CanWearApparel(pawn)))
            {
                __result = false;
            }
        }
    }
}
