using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(VerbTracker), "CreateVerbTargetCommand")]
    public static class VerbTracker_CreateVerbTargetCommand
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SentientMechUtility.EnterVerbCommand();
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            SentientMechUtility.ExitVerbCommand();
        }
    }
}
