using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Mechanitor), nameof(FloatMenuOptionProvider_Mechanitor.GetOptionsFor))]
    public static class FloatMenuOptionProvider_Mechanitor_GetOptionsFor
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn clickedPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!SentientMechRegistry.IsSentient(clickedPawn))
            {
                return true;
            }
            __result = Enumerable.Empty<FloatMenuOption>();
            return false;
        }
    }
}
