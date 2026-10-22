using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class IsekaiLevelingCompat
    {
        public const string PackageId = "JellyCreative.IsekaiLeveling";
        public const string MobRankPropsName = "IsekaiLeveling.MobRanking.CompProperties_MobRank";
        public const string MobRankCompName = "IsekaiLeveling.MobRanking.MobRankComponent";
        public const string HumanlikePropsName = "IsekaiLeveling.IsekaiComponentProperties";
        public const string HumanlikeCompName = "IsekaiLeveling.IsekaiComponent";
        public const string StatAllocationName = "IsekaiLeveling.IsekaiStatAllocation";
        public const string StatGeneratorName = "IsekaiLeveling.PawnStatGenerator";
        public const string UpdateRankTraitMethodName = "UpdateRankTraitFromStats";
        public const string InvalidateStatCacheMethodName = "InvalidateStatCache";

        private static readonly string[] StatFieldNames = { "strength", "dexterity", "vitality", "intelligence", "wisdom", "charisma", "availableStatPoints" };

        public static bool IsActive => ModsConfig.IsActive(PackageId);

        private static bool? available;
        private static bool warned;
        private static Type humanlikePropsType;
        private static Type humanlikeCompType;
        private static Type mobRankCompType;
        private static Type statAllocationType;
        private static FieldInfo mobLevel;
        private static FieldInfo mobXp;
        private static FieldInfo mobStats;
        private static FieldInfo mobStatsInitialized;
        private static FieldInfo humanlikeLevel;
        private static FieldInfo humanlikeXp;
        private static FieldInfo humanlikeStats;
        private static FieldInfo humanlikeStatsInitialized;
        private static FieldInfo[] statFields;
        private static MethodInfo invalidateStatCache;
        private static MethodInfo updateRankTrait;

        public static string BindFailure { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = IsActive;
                    available = active && Bind();
                    if (active && !available.Value)
                    {
                        Warn(BindFailure);
                    }
                }
                return available.Value;
            }
        }

        public static bool IsMobRankComp(CompSignature signature)
        {
            return signature.PropsType != null && signature.CompClass != null
                && signature.PropsType.FullName == MobRankPropsName && signature.CompClass.FullName == MobRankCompName;
        }

        public static bool IsHumanlikeComp(CompSignature signature)
        {
            return signature.PropsType != null && signature.CompClass != null
                && signature.PropsType.FullName == HumanlikePropsName && signature.CompClass.FullName == HumanlikeCompName;
        }

        public static List<ThingComp> WithoutMobRankComps(IList<ThingComp> live, out List<ThingComp> removed)
        {
            removed = new List<ThingComp>();
            var kept = new List<ThingComp>();
            for (int i = 0; i < (live?.Count ?? 0); i++)
            {
                ThingComp comp = live[i];
                if (comp?.props != null && IsMobRankComp(CompSignature.Of(comp.props)))
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

        public static int HumanlikePropertyCount(ThingDef race)
        {
            return race?.comps?.Count(c => IsHumanlikeComp(CompSignature.Of(c))) ?? 0;
        }

        public static bool AddHumanlikeProperties(ThingDef race)
        {
            if (race == null || !IsAvailable || HumanlikePropertyCount(race) > 0)
            {
                return false;
            }
            if (race.comps == null)
            {
                race.comps = new List<CompProperties>();
            }
            race.comps.Add((CompProperties)Activator.CreateInstance(humanlikePropsType));
            return true;
        }

        public static ThingComp HumanlikeCompOf(Pawn pawn)
        {
            return IsAvailable ? pawn?.AllComps.FirstOrDefault(c => humanlikeCompType.IsInstanceOfType(c)) : null;
        }

        public static int HumanlikeCompCount(Pawn pawn)
        {
            return IsAvailable && pawn != null ? pawn.AllComps.Count(c => humanlikeCompType.IsInstanceOfType(c)) : 0;
        }

        public static int MobRankCompCount(Pawn pawn)
        {
            return IsAvailable && pawn != null ? pawn.AllComps.Count(c => mobRankCompType.IsInstanceOfType(c)) : 0;
        }

        public sealed class Progression
        {
            public readonly int Level;
            public readonly int Xp;

            public readonly int[] Stats;

            public Progression(int level, int xp, int[] stats)
            {
                Level = level;
                Xp = xp;
                Stats = (int[])stats.Clone();
            }

            public override string ToString()
            {
                return "level " + Level + ", " + Xp + " XP, stats " + string.Join("/", Stats);
            }
        }

        public static Progression CaptureProgression(IList<ThingComp> removed)
        {
            ThingComp source = null;
            foreach (ThingComp comp in removed ?? new List<ThingComp>())
            {
                if (comp == null || !IsMobRankComp(CompSignature.Of(comp.props)))
                {
                    continue;
                }
                if (source != null)
                {
                    throw new InvalidOperationException("The pawn has more than one Isekai Leveling creature rank comp.");
                }
                source = comp;
            }
            if (source == null)
            {
                return null;
            }
            RequireBound();
            if (!(bool)mobStatsInitialized.GetValue(source))
            {
                return null;
            }
            object stats = mobStats.GetValue(source);
            if (stats == null)
            {
                throw new InvalidOperationException("The Isekai Leveling creature rank comp has initialized stats but no stat allocation.");
            }
            var values = new int[statFields.Length];
            for (int i = 0; i < statFields.Length; i++)
            {
                values[i] = (int)statFields[i].GetValue(stats);
            }
            return new Progression((int)mobLevel.GetValue(source), (int)mobXp.GetValue(source), values);
        }

        public static void ApplyProgression(Pawn pawn, Progression progression)
        {
            if (progression == null)
            {
                return;
            }
            RequireBound();
            int count = HumanlikeCompCount(pawn);
            if (count != 1)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has " + count + " Isekai Leveling progression comps; exactly one is needed to carry over " + progression + ".");
            }
            if (pawn.story?.traits == null)
            {
                throw new InvalidOperationException(pawn.ToStringSafe() + " has no traits yet, so the Isekai Leveling rank trait cannot be set.");
            }
            ThingComp comp = HumanlikeCompOf(pawn);
            object stats = humanlikeStats.GetValue(comp);
            if (stats == null)
            {
                stats = Activator.CreateInstance(statAllocationType);
                humanlikeStats.SetValue(comp, stats);
            }
            for (int i = 0; i < statFields.Length; i++)
            {
                statFields[i].SetValue(stats, progression.Stats[i]);
            }
            humanlikeLevel.SetValue(comp, progression.Level);
            humanlikeXp.SetValue(comp, progression.Xp);
            humanlikeStatsInitialized.SetValue(comp, true);
            try
            {
                invalidateStatCache.Invoke(comp, null);
                updateRankTrait.Invoke(null, new object[] { pawn, comp });
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException("Isekai Leveling threw while applying " + progression + " to " + pawn.ToStringSafe() + ": " + (ex.InnerException ?? ex).Message, ex.InnerException ?? ex);
            }
            if ((int)humanlikeLevel.GetValue(comp) != progression.Level || (int)humanlikeXp.GetValue(comp) != progression.Xp)
            {
                throw new InvalidOperationException("The Isekai Leveling comp of " + pawn.ToStringSafe() + " did not keep " + progression + ".");
            }
        }

        public static IEnumerable<string> RaceProblems(ThingDef sentient)
        {
            if (!IsAvailable)
            {
                yield break;
            }
            int humanlike = HumanlikePropertyCount(sentient);
            if (humanlike != 1)
            {
                yield return "has " + humanlike + " Isekai Leveling progression comp properties (" + HumanlikePropsName + "), expected exactly 1";
            }
            if (sentient.comps != null && sentient.comps.Any(c => IsMobRankComp(CompSignature.Of(c))))
            {
                yield return "still has the Isekai Leveling creature rank comp (" + MobRankPropsName + "); a sentient mechanoid is humanlike and uses " + HumanlikeCompName + " instead";
            }
        }

        public static bool Bind()
        {
            var failures = new List<string>();
            Type mobProps = AccessTools.TypeByName(MobRankPropsName);
            Type mobComp = AccessTools.TypeByName(MobRankCompName);
            Type humanProps = AccessTools.TypeByName(HumanlikePropsName);
            Type humanComp = AccessTools.TypeByName(HumanlikeCompName);
            Type stats = AccessTools.TypeByName(StatAllocationName);
            Type generator = AccessTools.TypeByName(StatGeneratorName);
            if (mobProps == null || !typeof(CompProperties).IsAssignableFrom(mobProps))
            {
                failures.Add(MobRankPropsName + " (a CompProperties)");
            }
            if (mobComp == null || !typeof(ThingComp).IsAssignableFrom(mobComp))
            {
                failures.Add(MobRankCompName + " (a ThingComp)");
                mobComp = null;
            }
            if (humanProps == null || !typeof(CompProperties).IsAssignableFrom(humanProps))
            {
                failures.Add(HumanlikePropsName + " (a CompProperties)");
                humanProps = null;
            }
            if (humanComp == null || !typeof(ThingComp).IsAssignableFrom(humanComp))
            {
                failures.Add(HumanlikeCompName + " (a ThingComp)");
                humanComp = null;
            }
            if (stats == null)
            {
                failures.Add(StatAllocationName);
            }
            if (humanProps != null && humanComp != null)
            {
                try
                {
                    if (((CompProperties)Activator.CreateInstance(humanProps)).compClass != humanComp)
                    {
                        failures.Add(HumanlikePropsName + " no longer points at " + HumanlikeCompName);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(HumanlikePropsName + " cannot be constructed (" + ex.Message + ")");
                }
            }

            var fields = new List<FieldInfo>();
            if (stats != null)
            {
                foreach (string name in StatFieldNames)
                {
                    FieldInfo field = Field(stats, name, typeof(int), failures);
                    fields.Add(field);
                }
            }
            FieldInfo mobLevelField = Field(mobComp, "currentLevel", typeof(int), failures);
            FieldInfo mobXpField = Field(mobComp, "currentXP", typeof(int), failures);
            FieldInfo mobStatsField = Field(mobComp, "stats", stats, failures);
            FieldInfo mobInitField = Field(mobComp, "statsInitialized", typeof(bool), failures);
            FieldInfo humanLevelField = Field(humanComp, "currentLevel", typeof(int), failures);
            FieldInfo humanXpField = Field(humanComp, "currentXP", typeof(int), failures);
            FieldInfo humanStatsField = Field(humanComp, "stats", stats, failures);
            FieldInfo humanInitField = Field(humanComp, "statsInitialized", typeof(bool), failures);
            MethodInfo invalidate = humanComp == null ? null : AccessTools.Method(humanComp, InvalidateStatCacheMethodName, Type.EmptyTypes);
            if (humanComp != null && (invalidate == null || invalidate.IsStatic))
            {
                failures.Add(HumanlikeCompName + "." + InvalidateStatCacheMethodName + "()");
            }
            MethodInfo update = humanComp == null || generator == null ? null : AccessTools.Method(generator, UpdateRankTraitMethodName, new[] { typeof(Pawn), humanComp });
            if (update == null || !update.IsStatic)
            {
                failures.Add(StatGeneratorName + "." + UpdateRankTraitMethodName + "(Pawn, " + HumanlikeCompName + ")");
                update = null;
            }

            if (failures.Count > 0)
            {
                BindFailure = "not found or changed: " + string.Join(", ", failures);
                return false;
            }
            BindFailure = null;
            humanlikePropsType = humanProps;
            humanlikeCompType = humanComp;
            mobRankCompType = mobComp;
            statAllocationType = stats;
            statFields = fields.ToArray();
            mobLevel = mobLevelField;
            mobXp = mobXpField;
            mobStats = mobStatsField;
            mobStatsInitialized = mobInitField;
            humanlikeLevel = humanLevelField;
            humanlikeXp = humanXpField;
            humanlikeStats = humanStatsField;
            humanlikeStatsInitialized = humanInitField;
            invalidateStatCache = invalidate;
            updateRankTrait = update;
            return true;
        }

        private static FieldInfo Field(Type owner, string name, Type fieldType, List<string> failures)
        {
            FieldInfo field = owner == null ? null : AccessTools.Field(owner, name);
            if (owner != null && (field == null || field.IsStatic || field.FieldType != fieldType))
            {
                failures.Add(owner.FullName + "." + name + " (" + fieldType?.Name + ")");
                return null;
            }
            return field;
        }

        private static void RequireBound()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException("Isekai Leveling is not bound" + (BindFailure != null ? " (" + BindFailure + ")" : "") + ".");
            }
        }

        private static void Warn(string message)
        {
            if (warned)
            {
                return;
            }
            warned = true;
            Log.Warning("[Mechanoid Sentience] Isekai Leveling is active but part of its API changed (" + message + "). Sentient mechanoids will get no Isekai progression comp, and converting a mechanoid that carries its creature rank comp will fail until this is updated.");
        }
    }
}
