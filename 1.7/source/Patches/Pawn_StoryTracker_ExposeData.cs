using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.ExposeData))]
    public static class Pawn_StoryTracker_ExposeData
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_StoryTracker __instance, Pawn ___pawn)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || !SentientMechRegistry.IsSentient(___pawn))
            {
                return;
            }
            __instance.SkinColorBase = __instance.skinColorOverride ?? SentientMechUtility.NeutralSkinColor;
        }
    }
}
