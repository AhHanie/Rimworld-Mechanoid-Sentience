using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.ShowDraftGizmo), MethodType.Getter)]
    public static class Pawn_DraftController_ShowDraftGizmo
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (!__result && SentientMechRegistry.IsSentient(__instance.pawn))
            {
                __result = true;
            }
        }
    }
}
