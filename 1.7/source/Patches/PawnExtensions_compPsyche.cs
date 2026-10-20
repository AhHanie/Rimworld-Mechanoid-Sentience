using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class PawnExtensions_compPsyche
    {
        [HarmonyPrepare]
        public static bool Prepare() => RimpsycheCompat.IsAvailable;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => RimpsycheCompat.Accessor;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref ThingComp __result)
        {
            if (__result == null && SentientMechRegistry.IsSentient(pawn))
            {
                __result = RimpsycheCompat.PsycheCompOf(pawn);
            }
        }
    }
}
