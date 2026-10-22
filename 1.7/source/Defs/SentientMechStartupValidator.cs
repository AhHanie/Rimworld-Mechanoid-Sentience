using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [StaticConstructorOnStartup]
    public static class SentientMechStartupValidator
    {
        private static bool validated;
        private static int vefExtras;
        private static int dmsRaces;
        private static readonly HashSet<string> warnedMissingRecipes = new HashSet<string>();

        static SentientMechStartupValidator()
        {
            LongEventHandler.ExecuteWhenFinished(Validate);
        }

        private static void Validate()
        {
            if (validated)
            {
                return;
            }
            validated = true;
            SentientMechRegistry.EnsureIndexTableCurrent();
            int checkedPairs = 0;
            foreach (ThingDef sentient in SentientMechRegistry.SentientRaces)
            {
                ThingDef original = SentientMechRegistry.OriginalOf(sentient);
                if (original == null)
                {
                    Log.Error("[Mechanoid Sentience] " + sentient.defName + " has no resolvable original race.");
                    continue;
                }
                checkedPairs++;
                DmsRaceKind originalKind = DeadMansSwitchCompat.KindOfOriginal(original);
                if (originalKind != DmsRaceKind.None)
                {
                    dmsRaces++;
                    foreach (string finding in DeadMansSwitchCompat.CompositionFindings(original, originalKind))
                    {
                        Log.Warning("[Mechanoid Sentience] " + original.defName + " (" + originalKind + "): " + finding + ".");
                    }
                }
                foreach (string problem in RaceProblems(original, sentient))
                {
                    Log.Error("[Mechanoid Sentience] " + sentient.defName + ": " + problem);
                }
            }
            int psycheRaces = RimpsycheCompat.RegisterAndVerify(SentientMechRegistry.SentientRaces);
            foreach (PawnKindDef sentientKind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(SentientMechRegistry.IsSentientKind))
            {
                PawnKindDef originalKind = SentientMechRegistry.OriginalOf(sentientKind);
                if (originalKind == null)
                {
                    Log.Error("[Mechanoid Sentience] " + sentientKind.defName + " has no resolvable original kind.");
                    continue;
                }
                foreach (string problem in KindProblems(originalKind, sentientKind))
                {
                    Log.Error("[Mechanoid Sentience] " + sentientKind.defName + ": " + problem);
                }
            }
            Logger.Message("Validated " + checkedPairs + " sentient mechanoid race(s)" + (vefExtras > 0 ? " (" + vefExtras + " with the optional VEF ability comp)" : "") + (dmsRaces > 0 ? " (" + dmsRaces + " Dead Man's Switch / FFF race(s))" : "") + (psycheRaces > 0 ? " (" + psycheRaces + " with a Rimpsyche psyche, nonsexual)" : "") + ".");
        }

        private static IEnumerable<string> RaceProblems(ThingDef original, ThingDef sentient)
        {
            RaceProperties o = original.race;
            RaceProperties s = sentient.race;
            if (s.body != o.body)
            {
                yield return "body differs from " + original.defName;
            }
            if (s.renderTree != o.renderTree || s.renderTree == null)
            {
                yield return "render tree differs from " + original.defName;
            }
            if (!s.Humanlike || !s.IsMechanoid || s.IsFlesh)
            {
                yield return "race is not a humanlike, non-flesh mechanoid";
            }
            if (s.EatsFood || s.needsRest)
            {
                yield return "race still eats or rests";
            }
            if (s.lifeStageAges.Count != o.lifeStageAges.Count || s.lifeStageAges.Zip(o.lifeStageAges, (a, b) => SentientMechRegistry.OriginalOf(a.def) != b.def).Any(differs => differs))
            {
                yield return "life stages differ from " + original.defName;
            }
            if (s.mechEnabledWorkTypes.Count != DefDatabase<WorkTypeDef>.DefCount)
            {
                yield return "not every work type is enabled";
            }
            if (s.thinkTreeMain == null || s.thinkTreeConstant == null)
            {
                yield return "missing think tree";
            }
            if (s.corpseDef == null)
            {
                yield return "no corpse def was generated";
            }
            else
            {
                foreach (string problem in CorpseFilterProblems(s.corpseDef))
                {
                    yield return problem;
                }
            }
            if (sentient.GetCompProperties<CompProperties_OverseerSubject>() != null)
            {
                yield return "still has the overseer subject comp";
            }
            DmsRaceKind dmsKind = DeadMansSwitchCompat.KindOfSentient(sentient);
            string compDifference = CompListComparer.FirstDifference(original.comps, sentient.comps, s2 => SentientCompRules.IsRemovedFromSentient(dmsKind, s2), out bool hasVefExtra);
            if (compDifference != null)
            {
                yield return "comp list differs from " + original.defName + ". " + compDifference;
            }
            else if (hasVefExtra)
            {
                vefExtras++;
            }
            foreach (string problem in IsekaiLevelingCompat.RaceProblems(sentient))
            {
                yield return problem;
            }
            if (dmsKind == DmsRaceKind.HumanlikeAutomatroid && (sentient.inspectorTabs == null || !sentient.inspectorTabs.Contains(typeof(ITab_Pawn_Character)) || !sentient.inspectorTabs.Contains(typeof(ITab_Pawn_Social))))
            {
                yield return "humanlike Dead Man's Switch mech lacks the Character or Social tab";
            }
            foreach (CompProperties comp in sentient.comps ?? new List<CompProperties>())
            {
                if (comp.compClass == null)
                {
                    yield return "comp with null class";
                }
            }
            if (sentient.GetModExtension<SentientMechExtension>()?.originalDefName != original.defName)
            {
                yield return "original def extension is missing";
            }
        }

        private static IEnumerable<string> CorpseFilterProblems(ThingDef corpse)
        {
            ThingCategoryDef category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(SentientCorpseCategories.CategoryName);
            ThingCategoryDef humanlike = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("CorpsesHumanlike");
            if (category == null || !corpse.IsWithinCategory(category))
            {
                yield return "corpse is not in " + SentientCorpseCategories.CategoryName;
            }
            if (humanlike == null || !corpse.IsWithinCategory(humanlike))
            {
                yield return "corpse is no longer within the humanlike corpses category";
            }
            foreach (string vanillaName in new[] { "ButcherCorpseFlesh", "ButcherCorpseMechanoid", "SmashCorpseMechanoid" })
            {
                RecipeDef vanilla = DefDatabase<RecipeDef>.GetNamedSilentFail(vanillaName);
                if (vanilla == null)
                {
                    WarnMissingRecipe(vanillaName);
                }
                else if (vanilla.fixedIngredientFilter.Allows(corpse))
                {
                    yield return "corpse is allowed by the vanilla recipe " + vanillaName;
                }
            }
            foreach (string ownName in new[] { SentientCorpseCategories.ShredRecipeName, SentientCorpseCategories.SmashRecipeName })
            {
                RecipeDef own = DefDatabase<RecipeDef>.GetNamedSilentFail(ownName);
                if (own == null)
                {
                    yield return "recipe " + ownName + " does not exist";
                    continue;
                }
                if (!own.fixedIngredientFilter.Allows(corpse) || !own.defaultIngredientFilter.Allows(corpse))
                {
                    yield return "corpse is not selected by default in " + ownName;
                }
                if (own.AllRecipeUsers.EnumerableNullOrEmpty())
                {
                    yield return "recipe " + ownName + " is not on any work table";
                }
            }
        }

        private static void WarnMissingRecipe(string recipeName)
        {
            if (warnedMissingRecipes.Add(recipeName))
            {
                Log.Warning("[Mechanoid Sentience] Recipe " + recipeName + " does not exist, skipping the sentient corpse check for it.");
            }
        }

        private static IEnumerable<string> KindProblems(PawnKindDef original, PawnKindDef sentient)
        {
            if (sentient.race == null || !SentientMechRegistry.IsSentientRace(sentient.race))
            {
                yield return "race is not a sentient race";
                yield break;
            }
            if (!sentient.initialResistanceRange.HasValue)
            {
                yield return "missing initial resistance range";
            }
            if (!sentient.initialWillRange.HasValue)
            {
                yield return "missing initial will range";
            }
            if (sentient.lifeStages.Count != original.lifeStages.Count)
            {
                yield return "life stage count differs from " + original.defName;
                yield break;
            }
            for (int i = 0; i < sentient.lifeStages.Count; i++)
            {
                if (sentient.lifeStages[i].bodyGraphicData?.Graphic == null || sentient.lifeStages[i].bodyGraphicData.Graphic == BaseContent.BadGraphic)
                {
                    yield return "life stage " + i + " has no valid body graphic";
                }
            }
            if (sentient.backstoryFiltersOverride.NullOrEmpty())
            {
                yield return "no sentient backstory filter";
            }
        }
    }
}
