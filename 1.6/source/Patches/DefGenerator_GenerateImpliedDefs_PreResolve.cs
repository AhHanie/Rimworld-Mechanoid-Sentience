using HarmonyLib;
using RimWorld;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class DefGenerator_GenerateImpliedDefs_PreResolve
    {
        [HarmonyPrefix]
        public static void Prefix(bool hotReload)
        {
            if (hotReload)
            {
                return;
            }
            SentientDefGenerator.Generate();
        }
    }
}
