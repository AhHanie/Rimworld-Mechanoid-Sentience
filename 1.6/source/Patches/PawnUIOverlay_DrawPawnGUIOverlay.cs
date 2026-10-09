using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
    public static class PawnUIOverlay_DrawPawnGUIOverlay
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn.Spawned && SentientMechRegistry.IsSentient(___pawn) && ___pawn.IsSelfShutdown())
            {
                ___pawn.Map.overlayDrawer.DrawOverlay(___pawn, OverlayTypes.SelfShutdown);
            }
        }
    }
}
