using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), "DrawXenotypeEditorButton")]
    public static class Page_ConfigureStartingPawns_DrawXenotypeEditorButton
    {
        [HarmonyPrefix]
        public static bool Prefix(int ___curPawnIndex)
        {
            var roster = Find.GameInitData?.startingAndOptionalPawns;
            if (roster == null || ___curPawnIndex < 0 || ___curPawnIndex >= roster.Count)
            {
                return true;
            }
            return !SentientMechRegistry.IsSentient(roster[___curPawnIndex]);
        }
    }
}
