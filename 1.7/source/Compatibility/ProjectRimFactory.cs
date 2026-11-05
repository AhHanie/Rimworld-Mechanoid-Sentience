using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    public static class ProjectRimFactoryCompat
    {
        public const string PackageId = "spdskatr.projectrimfactory";

        private const string QualityPatchName = "ProjectRimFactory.Common.HarmonyPatches.Patch_GenRecipe_GenerateQualityCreatedByPawn";
        private const string IsMechanoidName = "IsMechanoid";

        private static bool? active;
        private static MethodBase qualityIsMechanoid;
        private static bool warned;

        public static string BindFailure { get; private set; }

        public static bool IsActive
        {
            get
            {
                if (active == null)
                {
                    bool isActive = ModsConfig.IsActive(PackageId);
                    active = isActive;
                    if (isActive)
                    {
                        Bind();
                    }
                }
                return active.Value;
            }
        }

        public static MethodBase QualityIsMechanoid => IsActive ? qualityIsMechanoid : null;

        public static void Bind()
        {
            qualityIsMechanoid = null;
            BindFailure = null;
            Type patch = AccessTools.TypeByName(QualityPatchName);
            MethodInfo method = patch == null ? null : AccessTools.DeclaredMethod(patch, IsMechanoidName, new[] { typeof(Pawn) });
            if (method == null || !method.IsStatic || method.ReturnType != typeof(bool))
            {
                BindFailure = patch == null ? QualityPatchName + " not found" : QualityPatchName + "." + IsMechanoidName + "(Pawn) not found or has another signature";
                if (!warned)
                {
                    warned = true;
                    Log.Warning("[Mechanoid Sentience] Project RimFactory Revived is active but part of its API changed (" + BindFailure + "). Sentient mechanoids use the race's fixed skill level for item quality until this integration is updated.");
                }
                return;
            }
            qualityIsMechanoid = method;
        }

        public static bool IsMechanoidForQuality(bool prfAnswer, Pawn pawn)
        {
            return prfAnswer && !SentientMechRegistry.IsSentient(pawn);
        }
    }
}
