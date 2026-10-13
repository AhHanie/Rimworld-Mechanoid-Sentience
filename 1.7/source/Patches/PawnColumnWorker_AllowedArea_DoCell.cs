using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnColumnWorker_AllowedArea), nameof(PawnColumnWorker_AllowedArea.DoCell))]
    public static class PawnColumnWorker_AllowedArea_DoCell
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            if (pawn.Faction == Faction.OfPlayer && pawn.playerSettings != null && pawn.playerSettings.SupportsAllowedAreas)
            {
                AreaAllowedGUI.DoAllowedAreaSelectors(rect, pawn);
            }
            return false;
        }
    }
}
