using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider), nameof(FloatMenuOptionProvider.SelectedPawnValid))]
    public static class FloatMenuOptionProvider_SelectedPawnValid
    {
        [HarmonyInfix(typeof(RaceProperties), nameof(RaceProperties.IsMechanoid), InnerTargetKind.Getter)]
        public static bool Postfix(bool __result, RaceProperties __instance)
        {
            return SentientMechUtility.IsOrdinaryMechanoid(__result, __instance);
        }
    }
}
