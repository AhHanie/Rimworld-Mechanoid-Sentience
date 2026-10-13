using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.DoEffect))]
    public static class CompUseEffect_InstallImplant_DoEffect_BrainPart
    {
        [HarmonyInfix(typeof(BodyDef), nameof(BodyDef.GetPartsWithDef))]
        public static List<BodyPartRecord> Postfix(List<BodyPartRecord> __result, BodyDef __instance, BodyPartDef def)
        {
            return SentientMechUtility.GetPartsWithDefOrArtificialBrain(__result, __instance, def);
        }
    }

    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.CanBeUsedBy))]
    public static class CompUseEffect_InstallImplant_CanBeUsedBy_BrainPart
    {
        [HarmonyInfix(typeof(BodyDef), nameof(BodyDef.GetPartsWithDef))]
        public static List<BodyPartRecord> Postfix(List<BodyPartRecord> __result, BodyDef __instance, BodyPartDef def)
        {
            return SentientMechUtility.GetPartsWithDefOrArtificialBrain(__result, __instance, def);
        }
    }

    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.GetExistingImplant))]
    public static class CompUseEffect_InstallImplant_GetExistingImplant_BrainPart
    {
        [HarmonyInfix(typeof(BodyDef), nameof(BodyDef.GetPartsWithDef))]
        public static List<BodyPartRecord> Postfix(List<BodyPartRecord> __result, BodyDef __instance, BodyPartDef def)
        {
            return SentientMechUtility.GetPartsWithDefOrArtificialBrain(__result, __instance, def);
        }
    }
}
