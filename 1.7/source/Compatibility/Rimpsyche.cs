using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class RimpsycheCompat
    {
        public const string PackageId = "maux36.rimpsyche";
        public const string PropsTypeName = "Maux36.RimPsyche.CompProperties_Psyche";
        public const string CompTypeName = "Maux36.RimPsyche.CompPsyche";
        public const string TabTypeName = "Maux36.RimPsyche.ITab_Pawn_Psyche";
        public const string ExtensionsTypeName = "Maux36.RimPsyche.PawnExtensions";
        public const string CacheManagerTypeName = "Maux36.RimPsyche.PsycheCacheManager";
        public const string SexualityHelperTypeName = "Maux36.RimPsyche.SexualityHelper";
        public const string DatabaseTypeName = "Maux36.RimPsyche.RimpsycheDatabase";
        public const string SexualityTrackerTypeName = "Maux36.RimPsyche.Pawn_SexualityTracker";
        public const string AccessorName = "compPsyche";
        public const string PawnParameterName = "pawn";

        private static bool? available;
        private static Type compType;
        private static Type tabType;
        private static MethodBase accessor;
        private static FieldInfo trackingHashesField;
        private static FieldInfo nonSexualHashesField;
        private static FieldInfo mindlessHashesField;
        private static Action<Pawn> clearCache;
        private static MethodBase checkSuppressed;
        private static MethodBase suppressedGetter;
        private static MethodBase relationshipWith;
        private static Func<object, Pawn> trackerPawn;

        public static string BindFailure { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = ModsConfig.IsActive(PackageId);
                    available = active && Bind();
                    if (active && !available.Value)
                    {
                        Log.Warning("[Mechanoid Sentience] Rimpsyche is active but its API changed (" + BindFailure + "). Sentient mechanoids will not get a psyche.");
                    }
                }
                return available.Value;
            }
        }

        public static Type CompType => IsAvailable ? compType : null;

        public static Type TabType => IsAvailable ? tabType : null;

        public static MethodBase Accessor => IsAvailable ? accessor : null;

        public static MethodBase CheckSuppressedMethod => IsAvailable ? checkSuppressed : null;

        public static MethodBase SuppressedGetter => IsAvailable ? suppressedGetter : null;

        public static MethodBase RelationshipWithMethod => IsAvailable ? relationshipWith : null;

        public static bool Bind()
        {
            var failures = new List<string>();
            Type props = AccessTools.TypeByName(PropsTypeName);
            Type comp = AccessTools.TypeByName(CompTypeName);
            Type tab = AccessTools.TypeByName(TabTypeName);
            if (props == null || !typeof(CompProperties).IsAssignableFrom(props))
            {
                failures.Add(PropsTypeName + " (a CompProperties)");
            }
            if (comp == null || !typeof(ThingComp).IsAssignableFrom(comp))
            {
                failures.Add(CompTypeName + " (a ThingComp)");
                comp = null;
            }
            if (tab == null || !typeof(InspectTabBase).IsAssignableFrom(tab))
            {
                failures.Add(TabTypeName + " (an InspectTabBase)");
            }

            MethodInfo accessorMethod = null;
            Type extensions = AccessTools.TypeByName(ExtensionsTypeName);
            if (extensions != null && comp != null)
            {
                accessorMethod = AccessTools.DeclaredMethod(extensions, AccessorName, new[] { typeof(Pawn) });
            }
            if (accessorMethod == null || !accessorMethod.IsStatic || accessorMethod.ReturnType != comp || accessorMethod.GetParameters()[0].Name != PawnParameterName)
            {
                failures.Add(ExtensionsTypeName + "." + AccessorName + "(Pawn)");
                accessorMethod = null;
            }

            FieldInfo tracking = null;
            Action<Pawn> clear = null;
            Type cache = AccessTools.TypeByName(CacheManagerTypeName);
            if (cache != null)
            {
                tracking = StaticHashSetField(cache, "TrackingDefHash");
                MethodInfo clearMethod = AccessTools.DeclaredMethod(cache, "ClearCacheForPawn", new[] { typeof(Pawn) });
                if (clearMethod != null && clearMethod.IsStatic && clearMethod.ReturnType == typeof(void))
                {
                    clear = (Action<Pawn>)Delegate.CreateDelegate(typeof(Action<Pawn>), clearMethod);
                }
            }
            if (tracking == null)
            {
                failures.Add(CacheManagerTypeName + ".TrackingDefHash (a static HashSet<int>)");
            }
            if (clear == null)
            {
                failures.Add(CacheManagerTypeName + ".ClearCacheForPawn(Pawn)");
            }

            FieldInfo nonSexual = null;
            Type sexualityHelper = AccessTools.TypeByName(SexualityHelperTypeName);
            if (sexualityHelper != null)
            {
                nonSexual = StaticHashSetField(sexualityHelper, "NonSexualDefShorthashSet");
            }
            if (nonSexual == null)
            {
                failures.Add(SexualityHelperTypeName + ".NonSexualDefShorthashSet (a static HashSet<int>)");
            }

            FieldInfo mindless = null;
            Type database = AccessTools.TypeByName(DatabaseTypeName);
            if (database != null)
            {
                mindless = StaticHashSetField(database, "MindlessDefShorthashSet");
            }

            MethodInfo check = null;
            MethodInfo suppressed = null;
            MethodInfo relationship = null;
            Func<object, Pawn> pawnOfTracker = null;
            Type tracker = AccessTools.TypeByName(SexualityTrackerTypeName);
            if (tracker != null)
            {
                check = AccessTools.DeclaredMethod(tracker, "CheckSexualitySuppressed", Type.EmptyTypes);
                suppressed = AccessTools.DeclaredPropertyGetter(tracker, "Suppressed");
                relationship = AccessTools.DeclaredMethod(tracker, "GetRelationshipWith", new[] { typeof(Pawn) });
                FieldInfo pawnField = AccessTools.DeclaredField(tracker, "pawn");
                if (pawnField != null && pawnField.FieldType == typeof(Pawn) && !pawnField.IsStatic)
                {
                    ParameterExpression instance = Expression.Parameter(typeof(object), "tracker");
                    pawnOfTracker = Expression.Lambda<Func<object, Pawn>>(Expression.Field(Expression.Convert(instance, tracker), pawnField), instance).Compile();
                }
            }
            if (check == null || check.IsStatic || check.ReturnType != typeof(bool))
            {
                failures.Add(SexualityTrackerTypeName + ".CheckSexualitySuppressed()");
                check = null;
            }
            if (relationship == null || relationship.IsStatic || relationship.ReturnType != typeof(float) || relationship.GetParameters()[0].Name != "target")
            {
                failures.Add(SexualityTrackerTypeName + ".GetRelationshipWith(Pawn target)");
                relationship = null;
            }
            if (pawnOfTracker == null)
            {
                failures.Add(SexualityTrackerTypeName + ".pawn (a Pawn field)");
            }
            if (suppressed != null && (suppressed.IsStatic || suppressed.ReturnType != typeof(bool)))
            {
                suppressed = null;
            }

            BindFailure = failures.Count == 0 ? null : string.Join(", ", failures);
            if (failures.Count > 0)
            {
                compType = null;
                tabType = null;
                accessor = null;
                trackingHashesField = null;
                nonSexualHashesField = null;
                mindlessHashesField = null;
                clearCache = null;
                checkSuppressed = null;
                suppressedGetter = null;
                relationshipWith = null;
                trackerPawn = null;
                return false;
            }
            compType = comp;
            tabType = tab;
            accessor = accessorMethod;
            trackingHashesField = tracking;
            nonSexualHashesField = nonSexual;
            mindlessHashesField = mindless;
            clearCache = clear;
            checkSuppressed = check;
            suppressedGetter = suppressed;
            relationshipWith = relationship;
            trackerPawn = pawnOfTracker;
            return true;
        }

        private static FieldInfo StaticHashSetField(Type type, string fieldName)
        {
            FieldInfo field = AccessTools.DeclaredField(type, fieldName);
            return field != null && field.IsStatic && field.FieldType == typeof(HashSet<int>) ? field : null;
        }

        private static HashSet<int> Hashes(FieldInfo field)
        {
            return field?.GetValue(null) as HashSet<int>;
        }

        public static ThingComp PsycheCompOf(Pawn pawn)
        {
            Type type = CompType;
            if (type == null || pawn == null)
            {
                return null;
            }
            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i].GetType() == type)
                {
                    return comps[i];
                }
            }
            return null;
        }

        public static void ClearCache(Pawn pawn)
        {
            if (pawn != null && IsAvailable)
            {
                clearCache(pawn);
            }
        }

        public static bool IsSentientTracker(object tracker)
        {
            return trackerPawn != null && tracker != null && SentientMechRegistry.IsSentient(trackerPawn(tracker));
        }

        public static int RegisterAndVerify(IEnumerable<ThingDef> sentientRaces)
        {
            if (!IsAvailable)
            {
                return 0;
            }
            HashSet<int> tracking = Hashes(trackingHashesField);
            HashSet<int> nonSexual = Hashes(nonSexualHashesField);
            HashSet<int> mindless = Hashes(mindlessHashesField);
            if (tracking == null || nonSexual == null)
            {
                Log.Error("[Mechanoid Sentience] Rimpsyche's tracked race hashes or nonsexual race hashes are not available, so sentient mechanoids get no psyche.");
                return 0;
            }
            int ready = 0;
            foreach (ThingDef race in sentientRaces)
            {
                if (mindless != null && mindless.Contains(race.shortHash))
                {
                    Logger.Message(race.defName + " is excluded by Rimpsyche (mindless), so it gets no psyche.");
                    continue;
                }
                int comps = race.comps == null ? 0 : race.comps.Count(c => c != null && c.compClass == compType);
                if (comps != 1)
                {
                    Log.Error("[Mechanoid Sentience] " + race.defName + " has " + comps + " Rimpsyche psyche comps (expected exactly 1): Rimpsyche did not add its comp to this race, or added it more than once.");
                    continue;
                }
                if (!tracking.Contains(race.shortHash))
                {
                    Log.Error("[Mechanoid Sentience] " + race.defName + " is not in Rimpsyche's tracked race hashes, so its pawns would not find their psyche comp.");
                    continue;
                }
                if (EnsureTab(race))
                {
                    Logger.Message(race.defName + " had no Rimpsyche psyche tab, added it.");
                }
                if (race.race?.corpseDef != null && EnsureTab(race.race.corpseDef))
                {
                    Logger.Message(race.race.corpseDef.defName + " had no Rimpsyche psyche tab, added it.");
                }
                nonSexual.Add(race.shortHash);
                ready++;
            }
            return ready;
        }

        private static bool EnsureTab(ThingDef def)
        {
            InspectTabBase shared = InspectTabManager.GetSharedInstance(tabType);
            bool changed = false;
            if (def.inspectorTabs == null)
            {
                def.inspectorTabs = new List<Type>();
            }
            if (!def.inspectorTabs.Contains(tabType))
            {
                def.inspectorTabs.Add(tabType);
                changed = true;
            }
            if (def.inspectorTabsResolved == null)
            {
                def.inspectorTabsResolved = new List<InspectTabBase>();
                foreach (Type type in def.inspectorTabs)
                {
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(type));
                }
                changed = true;
            }
            else if (!def.inspectorTabsResolved.Contains(shared))
            {
                def.inspectorTabsResolved.Add(shared);
                changed = true;
            }
            return changed;
        }

        public static bool IsTracked(ThingDef race) => IsAvailable && Hashes(trackingHashesField).Contains(race.shortHash);

        public static bool IsNonSexual(ThingDef race) => IsAvailable && Hashes(nonSexualHashesField).Contains(race.shortHash);
    }
}
