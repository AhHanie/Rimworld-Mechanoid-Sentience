using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(Building_MechCharger), nameof(Building_MechCharger.IsCompatibleWithCharger), new[] { typeof(ThingDef), typeof(ThingDef) })]
    public static class Building_MechCharger_IsCompatibleWithCharger
    {
        [HarmonyPostfix]
        public static void Postfix(ThingDef chargerDef, ThingDef mechRace, ref bool __result)
        {
            if (__result || !SentientMechRegistry.IsSentientRace(mechRace))
            {
                return;
            }
            MechWeightClassDef weightClass = mechRace.race.mechWeightClass;
            __result = weightClass != null && chargerDef.building.requiredMechWeightClasses.NotNullAndContains(weightClass);
        }
    }
}
