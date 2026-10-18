using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class AndroidsCompat_IsUnawakenedAndroid
    {
        [HarmonyPrepare]
        public static bool Prepare() => WantsAndQuirksCompat.IsAvailable;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => WantsAndQuirksCompat.IsUnawakenedAndroidMethod;

        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn != null && pawn.genes == null && SentientMechRegistry.IsSentient(pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
