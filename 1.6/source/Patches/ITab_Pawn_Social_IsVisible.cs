using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ITab_Pawn_Social), nameof(ITab_Pawn_Social.IsVisible), MethodType.Getter)]
    public static class ITab_Pawn_Social_IsVisible
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (__result)
            {
                return;
            }
            Thing selected = Find.Selector.SingleSelectedThing;
            Pawn pawn = selected as Pawn ?? (selected as Corpse)?.InnerPawn;
            if (SentientMechRegistry.IsSentient(pawn))
            {
                __result = true;
            }
        }
    }
}
