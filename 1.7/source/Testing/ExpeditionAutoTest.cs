#if DEBUG
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VEF.Apparels
{
    internal class CompProperties_ShieldBubble : CompProperties
    {
        public CompProperties_ShieldBubble() { compClass = typeof(VEF.Abilities.CompAbilities); }
    }
}

namespace VEF.Abilities
{
    internal class CompAbilities : ThingComp { }
}

namespace Mechanoid_Sentience
{
    internal static class ExpeditionAutoTest
    {
        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Expedition: " + what);
        }

        private static CompProperties Plain(Type compClass)
        {
            return new CompProperties { compClass = compClass };
        }

        private static CompProperties NewExpeditionProps()
        {
            Type props = AccessTools.TypeByName(CompListComparer.ExpeditionPropsTypeName);
            return props == null ? null : (CompProperties)Activator.CreateInstance(props);
        }

        private static string Diff(List<CompProperties> original, List<CompProperties> clone, out bool vef)
        {
            return CompListComparer.FirstDifference(original, clone, s => SentientCompRules.IsRemovedFromSentient(DmsRaceKind.None, s), out vef);
        }

        private static string Diff(List<CompProperties> original, List<CompProperties> clone)
        {
            return Diff(original, clone, out _);
        }

        public static void RunPure()
        {
            CompProperties expedition = NewExpeditionProps();
            Check(CompSignature.Of(expedition).IsExpeditionCancel == (expedition != null), "the real comp, when Expedition is active, is recognized by its exact pair");
            Check(!CompSignature.Of(null).IsExpeditionCancel && !CompSignature.Of(new CompProperties_SentientRecharge()).IsExpeditionCancel, "null and unrelated signatures are not recognized");

            CompProperties overseer = new CompProperties_OverseerSubject();
            CompProperties a = Plain(typeof(CompForbiddable));
            CompProperties b = Plain(typeof(CompFlickable));
            CompProperties removed = new USH_HE.CompProperties_MechanoidHackable();
            CompProperties R() => new CompProperties_SentientRecharge();

            var plainSource = new List<CompProperties> { overseer, a, removed, b };
            Check(Diff(plainSource, new List<CompProperties> { a, b, R() }) == null, "baseline: source plus recharge passes");
            Check(Diff(plainSource, new List<CompProperties> { a, R(), b }) != null, "baseline: a reordered clone fails");
            Check(Diff(plainSource, new List<CompProperties> { a, b }) != null, "baseline: a missing recharge comp fails");
            Check(Diff(plainSource, new List<CompProperties> { a, b, R(), R() }) != null, "baseline: a duplicate recharge comp fails");

            if (expedition == null)
            {
                Check(Diff(plainSource, new List<CompProperties> { a, b, R(), Plain(typeof(CompSentientRecharge)) }) != null, "baseline: an unrelated extra comp fails");
                return;
            }

            CompProperties E() => NewExpeditionProps();
            var source = new List<CompProperties> { overseer, a, removed, b, E() };
            bool vef;

            Check(Diff(source, new List<CompProperties> { a, b, R(), E() }, out vef) == null && !vef, "late comp after recharge passes (with an allowed removal in the source)");

            CompProperties core = new CompProperties_SentientApocritonCoreRecharge();
            CompProperties passive = new CompProperties_SentientApocritonPassiveRecharge();
            Check(Diff(source, new List<CompProperties> { a, b, R(), core, E() }) == null, "Apocriton core recharge, then the late comp, passes");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), core }) == null, "late comp, then Apocriton core recharge, passes");
            Check(Diff(source, new List<CompProperties> { a, b, R(), passive, E() }) == null, "Apocriton passive recharge, then the late comp, passes");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), passive }) == null, "late comp, then Apocriton passive recharge, passes");
            Check(Diff(source, new List<CompProperties> { a, b, R(), core, E(), passive }) != null, "core and passive recharge together still fail");

            CompProperties vefProps = new VEF.Apparels.CompProperties_ShieldBubble();
            Check(Diff(source, new List<CompProperties> { a, b, R(), vefProps, E() }, out vef) == null && vef, "VEF comp, then the late comp, passes and reports the VEF comp");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), vefProps }, out vef) == null && vef, "late comp, then the VEF comp, passes and reports the VEF comp");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), vefProps, new VEF.Apparels.CompProperties_ShieldBubble() }, out vef) != null && !vef, "a duplicate VEF comp still fails");

            Check(Diff(new List<CompProperties> { overseer, a, removed, b }, new List<CompProperties> { a, b, R(), E() }) != null, "a comp missing from the original fails");
            Check(Diff(source, new List<CompProperties> { a, b, R() }) != null, "a comp missing from the clone fails");
            Check(Diff(new List<CompProperties> { overseer, a, b, E(), E() }, new List<CompProperties> { a, b, R(), E() }) != null, "two comps in the original fail");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), E() }) != null, "two comps in the clone fail");
            Check(Diff(source, new List<CompProperties> { E(), a, b, R() }) != null, "the comp moved to the front of the inherited comps fails");
            Check(Diff(source, new List<CompProperties> { a, b, E() }) != null, "a missing recharge comp fails");
            Check(Diff(source, new List<CompProperties> { a, b, R(), R(), E() }) != null, "a duplicate recharge comp fails");
            Check(Diff(source, new List<CompProperties> { a, b, R(), E(), Plain(typeof(CompSentientRecharge)) }) != null, "an unrelated extra comp fails");
            Check(Diff(source, new List<CompProperties> { b, a, R(), E() }) != null, "an unrelated reorder fails");
            Check(Diff(source, new List<CompProperties> { a, b, removed, R(), E() }) != null, "a comp that should have been removed fails");

            CompProperties mixedProps = NewExpeditionProps();
            mixedProps.compClass = typeof(CompSentientRecharge);
            var foreignProps = new CompProperties { compClass = AccessTools.TypeByName(CompListComparer.ExpeditionCompClassName) };
            Check(!CompSignature.Of(mixedProps).IsExpeditionCancel && !CompSignature.Of(foreignProps).IsExpeditionCancel, "a pair with only the properties type or only the comp class is not recognized");
            Check(Diff(source, new List<CompProperties> { a, b, R(), mixedProps }) != null, "a clone whose late comp has the right properties type but another comp class fails");
            Check(Diff(source, new List<CompProperties> { a, b, R(), foreignProps }) != null, "a clone whose late comp has the right comp class but other properties fails");

            string missing = Diff(source, new List<CompProperties> { a, R(), E() });
            Check(missing != null && missing.Contains("missing") && missing.Contains(nameof(CompFlickable)) && missing.Contains("CompExCancel"), "a missing inherited comp is reported with the full sequences (" + missing + ")");
            string duplicate = Diff(source, new List<CompProperties> { a, b, R(), R(), E() });
            Check(duplicate != null && duplicate.Contains("duplicate of the sentient recharge comp"), "a duplicate recharge comp is described (" + duplicate + ")");
            string twice = Diff(source, new List<CompProperties> { a, b, R(), E(), E() });
            Check(twice != null && twice.Contains("CompExCancel"), "two late comps are reported with the full sequences (" + twice + ")");

            Check(Diff(new List<CompProperties> { overseer, a, removed, E(), b }, new List<CompProperties> { a, E(), b, R() }) == null, "a comp that was already in the source stays in place and passes the strict route");
            Check(Diff(source, new List<CompProperties> { a, b, E(), R() }) == null, "a comp that was already last in the source and stays before the recharge comp passes the strict route");
            Check(CompListComparer.FirstDifference(new List<CompProperties> { overseer, a, E() }, new List<CompProperties> { a, E(), R() }, out _) == null, "the strict route works without a removal rule");
        }
    }
}
#endif
