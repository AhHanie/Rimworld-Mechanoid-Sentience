using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
    public static class Pawn_ExposeData_Appearance
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SentientMechRegistry.IsSentient(__instance) && SentientMechGenerator.RepairAppearance(__instance))
            {
                __instance.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(__instance);
            }
        }
    }
}
