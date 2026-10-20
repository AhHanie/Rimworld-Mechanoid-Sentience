#if DEBUG
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace USH_HE
{
    internal class CompProperties_MechanoidHackable : CompProperties
    {
        public CompProperties_MechanoidHackable() { compClass = typeof(CompMechanoidHackable); }
    }

    internal class CompMechanoidHackable : ThingComp { }

    internal class CompProperties_DataSourceProtected : CompProperties
    {
        public CompProperties_DataSourceProtected() { compClass = typeof(CompDataSourceProtected); }
    }

    internal class CompDataSourceProtected : ThingComp { }
}

namespace Mechanoid_Sentience
{
    internal static class HackingExpansionAutoTest
    {
        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "HE: " + what);
        }

        private class ForeignHackable : CompProperties
        {
            public ForeignHackable() { compClass = typeof(USH_HE.CompMechanoidHackable); }
        }

        private static ThingComp LiveComp(CompProperties props)
        {
            var comp = (ThingComp)System.Activator.CreateInstance(props.compClass);
            comp.props = props;
            return comp;
        }

        public static void RunPure()
        {
            var hackable = new USH_HE.CompProperties_MechanoidHackable();
            var dataSource = new USH_HE.CompProperties_DataSourceProtected();

            Check(HackingExpansionCompat.IsTargetComp(CompSignature.Of(hackable)) && HackingExpansionCompat.IsTargetComp(CompSignature.Of(dataSource)), "both exact pairs are recognized");
            Check(!HackingExpansionCompat.IsTargetComp(CompSignature.Of(new ForeignHackable())), "a look-alike with only the comp class is not recognized");
            Check(!HackingExpansionCompat.IsTargetComp(new CompSignature(typeof(USH_HE.CompProperties_MechanoidHackable), typeof(USH_HE.CompDataSourceProtected))), "mixed pairs are not recognized");
            Check(!HackingExpansionCompat.IsTargetComp(default(CompSignature)) && !HackingExpansionCompat.IsTargetComp(CompSignature.Of(new CompProperties_SentientRecharge())), "null and unrelated signatures are not recognized");

            ThingComp other1 = LiveComp(new CompProperties_SentientRecharge());
            ThingComp live1 = LiveComp(hackable);
            ThingComp other2 = LiveComp(new ForeignHackable());
            ThingComp live2 = LiveComp(dataSource);
            List<ThingComp> kept = HackingExpansionCompat.WithoutTargetComps(new List<ThingComp> { other1, live1, other2, live2 }, out List<ThingComp> removed);
            Check(kept.SequenceEqual(new[] { other1, other2 }) && removed.SequenceEqual(new[] { live1, live2 }), "the live filter removes exactly the two target comps and keeps the rest in order");
            Check(HackingExpansionCompat.WithoutTargetComps(null, out List<ThingComp> none).Count == 0 && none.Count == 0, "a null live list is handled");

            bool Removal(CompSignature s) => SentientCompRules.IsRemovedFromSentient(DmsRaceKind.None, s);
            var original = new List<CompProperties> { new CompProperties_OverseerSubject(), hackable, dataSource };
            var cleanClone = new List<CompProperties> { new CompProperties_OverseerSubject(), new CompProperties_SentientRecharge() };
            Check(CompListComparer.FirstDifference(original, cleanClone, Removal, out _) == null, "a clone without the pair matches an original that has it");
            Check(CompListComparer.FirstDifference(new List<CompProperties> { new CompProperties_OverseerSubject() }, cleanClone, Removal, out _) == null, "a clone matches an original that never had the pair (mech hacking disabled)");

            var clonePairAfterRecharge = new List<CompProperties>(cleanClone) { new USH_HE.CompProperties_MechanoidHackable(), new USH_HE.CompProperties_DataSourceProtected() };
            Check(CompListComparer.FirstDifference(original, clonePairAfterRecharge, Removal, out _) != null, "a clone that kept the pair is reported as drift");
            var cloneDuplicate = new List<CompProperties>(cleanClone) { new USH_HE.CompProperties_MechanoidHackable(), new USH_HE.CompProperties_MechanoidHackable() };
            Check(CompListComparer.FirstDifference(original, cloneDuplicate, Removal, out _) != null, "a duplicate hack comp on a clone is reported");
            var cloneUnrelated = new List<CompProperties>(cleanClone) { new ForeignHackable() };
            Check(CompListComparer.FirstDifference(original, cloneUnrelated, Removal, out _) != null, "an unrelated extra comp is still reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { new CompProperties_OverseerSubject() }, Removal, out _) != null, "a missing recharge comp is still reported");
        }
    }
}
#endif
