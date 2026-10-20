using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    public static class HackingExpansionCompat
    {
        public const string PackageId = "Ushanka.HackingExpansion";
        public const string HackablePropsName = "USH_HE.CompProperties_MechanoidHackable";
        public const string HackableCompName = "USH_HE.CompMechanoidHackable";
        public const string DataSourcePropsName = "USH_HE.CompProperties_DataSourceProtected";
        public const string DataSourceCompName = "USH_HE.CompDataSourceProtected";
        public const string DisabledHediffName = "USH_Disabled";
        public const string InjectorTypeName = "USH_HE.Patch_DefOfHelper_RebindAllDefOfs";
        public const string InjectorMethodName = "PatchAllDefs";

        public static bool IsActive => ModsConfig.IsActive(PackageId);

        private static bool warned;

        public static bool IsTargetComp(CompSignature signature)
        {
            if (signature.PropsType == null || signature.CompClass == null)
            {
                return false;
            }
            string props = signature.PropsType.FullName;
            string comp = signature.CompClass.FullName;
            return (props == HackablePropsName && comp == HackableCompName) || (props == DataSourcePropsName && comp == DataSourceCompName);
        }

        public static List<ThingComp> WithoutTargetComps(IList<ThingComp> live, out List<ThingComp> removed)
        {
            removed = new List<ThingComp>();
            var kept = new List<ThingComp>();
            for (int i = 0; i < (live?.Count ?? 0); i++)
            {
                ThingComp comp = live[i];
                if (comp?.props != null && IsTargetComp(CompSignature.Of(comp.props)))
                {
                    removed.Add(comp);
                }
                else
                {
                    kept.Add(comp);
                }
            }
            return kept;
        }

        public static int RemoveTargetCompsFromSentientRaces()
        {
            int removed = 0;
            foreach (ThingDef race in SentientMechRegistry.SentientRaces)
            {
                removed += race.comps?.RemoveAll(c => IsTargetComp(CompSignature.Of(c))) ?? 0;
            }
            return removed;
        }

        public static System.Reflection.MethodBase InjectorMethod()
        {
            if (!IsActive)
            {
                return null;
            }
            Type injector = AccessTools.TypeByName(InjectorTypeName);
            System.Reflection.MethodInfo method = injector == null ? null : AccessTools.DeclaredMethod(injector, InjectorMethodName);
            if (method == null || !method.IsStatic)
            {
                Warn(InjectorTypeName + "." + InjectorMethodName + " not found; sentient mechanoids may keep its hack target comps and fail the startup comp check");
                return null;
            }
            foreach (string typeName in new[] { HackablePropsName, HackableCompName, DataSourcePropsName, DataSourceCompName })
            {
                if (AccessTools.TypeByName(typeName) == null)
                {
                    Warn(typeName + " not found; its hack target comps may not be recognized on sentient mechanoids");
                    break;
                }
            }
            return method;
        }

        public static bool RemoveDisabledHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || !IsActive)
            {
                return false;
            }
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(DisabledHediffName);
            Hediff disabled = def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (disabled == null)
            {
                return false;
            }
            pawn.health.RemoveHediff(disabled);
            Logger.Message("Removed the Hacking Expansion \"" + DisabledHediffName + "\" hediff from " + pawn.ToStringSafe() + ".");
            return true;
        }

        private static void Warn(string message)
        {
            if (warned)
            {
                return;
            }
            warned = true;
            Log.Warning("[Mechanoid Sentience] Hacking Expansion is active but part of its API changed (" + message + ").");
        }
    }
}
