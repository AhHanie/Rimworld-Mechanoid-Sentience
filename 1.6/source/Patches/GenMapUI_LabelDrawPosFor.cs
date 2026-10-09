using HarmonyLib;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor), new[] { typeof(Thing), typeof(float) })]
    public static class GenMapUI_LabelDrawPosFor
    {
        [HarmonyPrefix]
        public static void Prefix(Thing thing, ref float worldOffsetZ)
        {
            if (thing is Pawn pawn && SentientMechRegistry.IsSentient(pawn))
            {
                worldOffsetZ -= Mathf.Max(0f, SentientMechUtility.BodyDrawSize(pawn) / 2f - 0.5f);
            }
        }
    }
}
