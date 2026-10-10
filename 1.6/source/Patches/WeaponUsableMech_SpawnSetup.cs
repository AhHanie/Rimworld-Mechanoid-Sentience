using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class WeaponUsableMech_SpawnSetup
    {
        public sealed class SkillState
        {
            public Pawn_SkillTracker Tracker;
            public int[] Levels;
        }

        [HarmonyPrepare]
        public static bool Prepare()
        {
            return DeadMansSwitchCompat.WeaponUsableMechSpawnSetup != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return DeadMansSwitchCompat.WeaponUsableMechSpawnSetup;
        }

        [HarmonyPrefix]
        public static void Prefix(Pawn __instance, out SkillState __state)
        {
            __state = null;
            if (!SentientMechRegistry.IsSentient(__instance))
            {
                return;
            }
            Pawn_SkillTracker tracker = __instance.skills;
            int[] levels = SkillLevelGuard.Capture(tracker?.skills);
            if (levels != null)
            {
                __state = new SkillState { Tracker = tracker, Levels = levels };
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, SkillState __state)
        {
            if (__state != null && ReferenceEquals(__instance.skills, __state.Tracker))
            {
                SkillLevelGuard.Restore(__state.Tracker.skills, __state.Levels);
            }
        }
    }
}
