using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(SilhouetteUtility), "GetColorInt")]
    public static class SilhouetteUtility_GetColorInt
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, ref Color __result)
        {
            if (thing is Pawn pawn && SentientMechRegistry.IsSentient(pawn))
            {
                __result = PawnNameColorUtility.PawnNameColorOf(pawn);
            }
        }
    }
}
