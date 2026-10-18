using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
    public static class Pawn_ExposeData_Needs
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SentientMechNeeds.MigrateLoadedPawn(__instance);
            }
        }
    }
}
