using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Patch_GenRecipe_GenerateQualityCreatedByPawn_Sentient
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return ProjectRimFactoryCompat.QualityIsMechanoid != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return ProjectRimFactoryCompat.QualityIsMechanoid;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            __result = ProjectRimFactoryCompat.IsMechanoidForQuality(__result, pawn);
        }
    }
}
