using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class PawnGenerator_TryGenerateNewPawnInternal_CompleteAppearance
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (__result != null && SentientMechRegistry.IsSentient(__result))
            {
                SentientMechGenerator.ApplyAppearanceDefaults(__result);
            }
        }
    }
}
