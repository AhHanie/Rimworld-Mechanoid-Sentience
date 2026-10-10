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
                foreach (string problem in RaceProblems(original, sentient))
                {
                    Log.Error("[Mechanoid Sentience] " + sentient.defName + ": " + problem);
                }
            }
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
            Logger.Message("Validated " + checkedPairs + " sentient mechanoid race(s)" + (vefExtras > 0 ? " (" + vefExtras + " with the optional VEF ability comp)." : "."));
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
            if (sentient.GetCompProperties<CompProperties_OverseerSubject>() != null)
            {
                yield return "still has the overseer subject comp";
            }
            string compDifference = CompListComparer.FirstDifference(original.comps, sentient.comps, out bool hasVefExtra);
            if (compDifference != null)
            {
                yield return "comp list differs from " + original.defName + ". " + compDifference;
            }
            else if (hasVefExtra)
            {
                vefExtras++;
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
