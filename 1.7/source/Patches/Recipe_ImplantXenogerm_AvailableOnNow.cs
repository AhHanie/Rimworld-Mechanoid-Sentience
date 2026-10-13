using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Recipe_ImplantXenogerm), nameof(Recipe_ImplantXenogerm.AvailableOnNow))]
    public static class Recipe_ImplantXenogerm_AvailableOnNow
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, ref bool __result)
        {
            if (__result && thing is Pawn pawn && SentientMechRegistry.IsSentient(pawn))
            {
                __result = false;
            }
        }
    }
}
