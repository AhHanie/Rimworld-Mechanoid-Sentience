using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_PawnJoined))]
    public static class Faction_Notify_PawnJoined
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.Humanlike), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsHumanlikeVisuals(__result, __instance);
        }
    }
}
