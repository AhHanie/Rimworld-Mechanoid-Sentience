using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.LimitInitialActiveWorks), MethodType.Getter)]
    public static class Pawn_WorkSettings_LimitInitialActiveWorks
    {
        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, Pawn> PawnOf = AccessTools.FieldRefAccess<Pawn_WorkSettings, Pawn>("pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_WorkSettings __instance, ref bool __result)
        {
            if (!__result && SentientMechRegistry.IsSentient(PawnOf(__instance)))
            {
                __result = true;
            }
        }
    }
}
