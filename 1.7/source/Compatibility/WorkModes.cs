using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    public static class WorkModesCompat
    {
        public const string PackageId = "wvc.sergkart.biotech.MoreMechanoidsWorkModes";
        public const string PropsName = "WVC_WorkModes.CompProperties_MechSettings";
        public const string CompName = "WVC_WorkModes.CompMechSettings";
        public const string ShutdownUtilityName = "WVC_WorkModes.ShutdownUtility";
        public const string ResetCacheMethodName = "ResetAllStaticCache";
        public const string WorkMechsTabName = "WVC_WorkModes.MainTabWindow_Work_Mechs";
        public const string PawnsGetterName = "Pawns";

        public static bool IsActive => ModsConfig.IsActive(PackageId);

        private static bool cacheBound;
        private static MethodInfo resetCache;
        private static bool warned;

        public static bool IsOverseerBoundComp(CompSignature signature)
        {
            return signature.PropsType != null && signature.CompClass != null
                && signature.PropsType.FullName == PropsName && signature.CompClass.FullName == CompName;
        }

        public static List<ThingComp> WithoutOverseerBoundComps(IList<ThingComp> live, out List<ThingComp> removed)
        {
            removed = new List<ThingComp>();
            var kept = new List<ThingComp>();
            for (int i = 0; i < (live?.Count ?? 0); i++)
            {
                ThingComp comp = live[i];
                if (comp?.props != null && IsOverseerBoundComp(CompSignature.Of(comp.props)))
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

        public static void NotifyCompsRemoved()
        {
            if (!cacheBound)
            {
                cacheBound = true;
                Type utility = AccessTools.TypeByName(ShutdownUtilityName);
                if (utility != null)
                {
                    resetCache = AccessTools.Method(utility, ResetCacheMethodName, Type.EmptyTypes);
                    if (resetCache == null || !resetCache.IsStatic)
                    {
                        resetCache = null;
                        Warn(ShutdownUtilityName + "." + ResetCacheMethodName + " not found; its cached settings of a converted mech may stay stale until the next reset");
                    }
                }
            }
            if (resetCache == null)
            {
                return;
            }
            try
            {
                resetCache.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Warn(ResetCacheMethodName + " threw: " + (ex.InnerException ?? ex).Message);
            }
        }

        public static MethodBase WorkMechsPawnsGetter()
        {
            Type tab = AccessTools.TypeByName(WorkMechsTabName);
            if (tab == null)
            {
                return null;
            }
            MethodBase getter = AccessTools.DeclaredPropertyGetter(tab, PawnsGetterName);
            if (getter == null)
            {
                Warn(WorkMechsTabName + "." + PawnsGetterName + " not found; sentient mechanoids will appear in its Mechs work tab");
            }
            return getter;
        }

        private static void Warn(string message)
        {
            if (warned)
            {
                return;
            }
            warned = true;
            Log.Warning("[Mechanoid Sentience] WVC - Work Modes is active but part of its API changed (" + message + ").");
        }
    }

    public static class SentientCompRules
    {
        public static bool IsRemovedFromSentient(DmsRaceKind dmsKind, CompSignature signature)
        {
            return WorkModesCompat.IsOverseerBoundComp(signature)
                || HackingExpansionCompat.IsTargetComp(signature)
                || DeadMansSwitchCompat.IsRemovedFromSentient(dmsKind, signature);
        }
    }
}
