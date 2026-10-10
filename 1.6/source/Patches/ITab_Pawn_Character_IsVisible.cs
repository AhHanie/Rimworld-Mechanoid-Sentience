using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ITab_Pawn_Character), nameof(ITab_Pawn_Character.IsVisible), MethodType.Getter)]
    public static class ITab_Pawn_Character_IsVisible
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return DeadMansSwitchCompat.IsAvailable;
        }

        [HarmonyPostfix]
        [HarmonyAfter("Fortified")]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref bool __result)
        {
            if (__result)
            {
                return;
            }
            Thing selected = Find.Selector.SingleSelectedThing;
            Pawn pawn = selected as Pawn ?? (selected as Corpse)?.InnerPawn;
            if (pawn?.story != null && DeadMansSwitchCompat.IsSentientHumanlikeMech(pawn))
            {
                __result = true;
            }
        }
    }
}
