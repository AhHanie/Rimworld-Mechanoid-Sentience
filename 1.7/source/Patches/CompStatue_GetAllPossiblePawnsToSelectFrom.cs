using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompStatue), nameof(CompStatue.GetAllPossiblePawnsToSelectFrom))]
    public static class CompStatue_GetAllPossiblePawnsToSelectFrom
    {
        [HarmonyPrepare]
        public static bool Prepare() => ModsConfig.OdysseyActive;

        [HarmonyPostfix]
        public static void Postfix(List<Pawn> __result)
        {
            __result?.RemoveAll(SentientMechRegistry.IsSentient);
        }
    }
}
