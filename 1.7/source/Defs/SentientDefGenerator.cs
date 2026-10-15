using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientDefGenerator
    {
        public const string MainThinkTreeName = "MS_SentientMech";
        public const string ConstantThinkTreeName = "MS_SentientMechConstant";
        public const string BodyTypeName = "MS_SentientMech";
        public const string BackstoryCategory = "MS_SentientMech";

        private const int FixedSkillLevel = 20;

        public static void Generate()
        {
            SentientMechRegistry.Reset();
            DeadMansSwitchCompat.Reset();
            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            ThinkTreeDef mainTree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(MainThinkTreeName);
            ThinkTreeDef constantTree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(ConstantThinkTreeName);
            if (mainTree == null || constantTree == null)
            {
                Log.Error("[Mechanoid Sentience] Sentient think trees are missing, no sentient mechanoid defs were generated.");
                return;
            }

            List<WorkTypeDef> allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            List<PawnKindDef> allKinds = DefDatabase<PawnKindDef>.AllDefsListForReading;

            foreach (ThingDef race in DefDatabase<ThingDef>.AllDefsListForReading.ToList())
            {
                if (!IsSupportedMechRace(race))
                {
                    continue;
                }
                List<PawnKindDef> kinds = allKinds.Where(k => k.race == race).ToList();
                if (kinds.Count == 0)
                {
                    continue;
                }

                DmsRaceKind dmsKind = DeadMansSwitchCompat.ClassifyRace(race);
                ThingDef sentientRace = MakeSentientRace(race, mainTree, constantTree, allWorkTypes, dmsKind);
                CloneLifeStages(sentientRace, kinds[0]);
                SentientMechRegistry.Register(race, sentientRace);
                DeadMansSwitchCompat.Record(race, sentientRace, dmsKind);
                DefGenerator.AddImpliedDef(sentientRace);
                AttachRecipes(race, sentientRace);

                foreach (PawnKindDef kind in kinds)
                {
                    PawnKindDef sentientKind = MakeSentientKind(kind, sentientRace);
                    SentientMechRegistry.Register(kind, sentientKind);
                    DefGenerator.AddImpliedDef(sentientKind);
                }
            }
            SentientMechRegistry.Freeze();
            DeadMansSwitchCompat.LogSummary();
        }

        private static bool IsSupportedMechRace(ThingDef race)
        {
            RaceProperties props = race.race;
            if (race.category != ThingCategory.Pawn || props == null || race.defName.StartsWith(SentientMechRegistry.DefPrefix))
            {
                return false;
            }
            if (props.FleshType == null || props.FleshType.defName != "Mechanoid" || props.Humanlike)
            {
                return false;
            }
            return props.body != null && props.mechWeightClass != null;
        }

        private static ThingDef MakeSentientRace(ThingDef original, ThinkTreeDef mainTree, ThinkTreeDef constantTree, List<WorkTypeDef> allWorkTypes, DmsRaceKind dmsKind)
        {
            ThingDef clone = DefCloner.Clone(original);
            clone.defName = SentientMechRegistry.DefPrefix + original.defName;
            clone.ResolveDefNameHash();
            clone.modContentPack = Mod.ContentPack;
            clone.shortHash = 0;
            clone.index = 0;
            clone.generated = true;

            RaceProperties race = clone.race;
            race.intelligence = Intelligence.Humanlike;
            race.thinkTreeMain = mainTree;
            race.thinkTreeConstant = constantTree;
            race.foodType = FoodTypeFlags.None;
            race.needsRest = false;
            race.mechFixedSkillLevel = FixedSkillLevel;
            race.mechEnabledWorkTypes = new List<WorkTypeDef>(allWorkTypes);
            race.mechWorkTypePriorities = null;

            clone.comps?.RemoveAll(c => c is CompProperties_OverseerSubject);

            if (dmsKind == DmsRaceKind.Automatroid || dmsKind == DmsRaceKind.HumanlikeAutomatroid)
            {
                clone.comps?.RemoveAll(c => DeadMansSwitchCompat.IsRemovedFromSentient(dmsKind, CompSignature.Of(c)));
            }
            if (dmsKind == DmsRaceKind.HumanlikeAutomatroid)
            {
                DeadMansSwitchCompat.EnsureCharacterTabs(clone);
            }

            clone.statBases?.RemoveAll(s => s.stat == StatDefOf.WorkSpeedGlobal);

            AddExtension(clone, original.defName);
            return clone;
        }

        private static void CloneLifeStages(ThingDef sentientRace, PawnKindDef kind)
        {
            float drawSize = kind.lifeStages.Count > 0 && kind.lifeStages[0].bodyGraphicData != null ? kind.lifeStages[0].bodyGraphicData.drawSize.x : 1f;
            float distanceFactor = Mathf.Max(1f, drawSize / 1.5f);
            var clones = new Dictionary<LifeStageDef, LifeStageDef>();
            foreach (LifeStageAge age in sentientRace.race.lifeStageAges)
            {
                if (age.def == null)
                {
                    continue;
                }
                if (!clones.TryGetValue(age.def, out LifeStageDef clone))
                {
                    clone = DefCloner.Clone(age.def);
                    clone.defName = SentientMechRegistry.DefPrefix + sentientRace.defName.Substring(SentientMechRegistry.DefPrefix.Length) + "_" + age.def.defName;
                    clone.ResolveDefNameHash();
                    clone.modContentPack = Mod.ContentPack;
                    clone.shortHash = 0;
                    clone.index = 0;
                    clone.generated = true;
                    clone.equipmentDrawDistanceFactor = distanceFactor;
                    AddExtension(clone, age.def.defName);
                    DefGenerator.AddImpliedDef(clone);
                    clones[age.def] = clone;
                }
                age.def = clone;
            }
        }

        private static PawnKindDef MakeSentientKind(PawnKindDef original, ThingDef sentientRace)
        {
            PawnKindDef clone = DefCloner.Clone(original);
            clone.defName = SentientMechRegistry.DefPrefix + original.defName;
            clone.ResolveDefNameHash();
            clone.modContentPack = Mod.ContentPack;
            clone.shortHash = 0;
            clone.index = 0;
            clone.generated = true;
            clone.race = sentientRace;

            if (!clone.initialResistanceRange.HasValue)
            {
                clone.initialResistanceRange = new FloatRange(13f, 21f);
            }
            if (!clone.initialWillRange.HasValue)
            {
                clone.initialWillRange = FloatRange.Zero;
            }

            clone.backstoryCategories = null;
            clone.backstoryFilters = null;
            clone.backstoryFiltersOverride = new List<BackstoryCategoryFilter>
            {
                new BackstoryCategoryFilter
                {
                    categories = new List<string> { BackstoryCategory },
                    commonality = 1f
                }
            };
            clone.fixedChildBackstories = new List<BackstoryDef>();
            clone.fixedAdultBackstories = new List<BackstoryDef>();
            clone.preventIdeo = false;

            clone.destroyGearOnDrop = false;

            AddExtension(clone, original.defName);
            return clone;
        }

        private static void AddExtension(Def clone, string originalDefName)
        {
            if (clone.modExtensions == null)
            {
                clone.modExtensions = new List<DefModExtension>();
            }
            clone.modExtensions.Add(new SentientMechExtension { originalDefName = originalDefName });
        }

        private static void AttachRecipes(ThingDef original, ThingDef clone)
        {
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.recipeUsers != null && recipe.recipeUsers.Contains(original) && !recipe.recipeUsers.Contains(clone))
                {
                    recipe.recipeUsers.Add(clone);
                }
            }
        }
    }
}
