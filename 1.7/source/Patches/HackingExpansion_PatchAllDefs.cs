using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class HackingExpansion_PatchAllDefs
    {
        private static MethodBase target;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            target = HackingExpansionCompat.InjectorMethod();
            return target != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return target ?? HackingExpansionCompat.InjectorMethod();
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            int removed = HackingExpansionCompat.RemoveTargetCompsFromSentientRaces();
            if (removed > 0)
            {
                Logger.Message("Removed " + removed + " Hacking Expansion hack target comp(s) from the sentient mechanoid races.");
            }
        }
    }
}
