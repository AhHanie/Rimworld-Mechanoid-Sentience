using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CharacterCardUtility), "LifestageAndXenotypeOptions")]
    public static class CharacterCardUtility_LifestageAndXenotypeOptions
    {
        private static readonly FieldInfo HighlightRect = AccessTools.Field(typeof(CharacterCardUtility), "highlightRect");

        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, Rect randomizeRect)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return true;
            }
            HighlightRect.SetValue(null, randomizeRect);
            return false;
        }
    }
}
