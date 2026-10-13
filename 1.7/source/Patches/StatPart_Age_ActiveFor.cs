using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class StatPart_Age_ActiveFor
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StatPart_Age), "ActiveFor");
            yield return AccessTools.Method(typeof(StatPart_AgeOffset), "ActiveFor");
        }

        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
