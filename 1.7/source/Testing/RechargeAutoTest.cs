#if DEBUG
using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class RechargeAutoTest
    {
        private static readonly FloatRange SavedRange = new FloatRange(0.15f, 0.85f);

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Recharge: " + what);
        }

        private static bool Near(FloatRange a, FloatRange b)
        {
            return Mathf.Abs(a.min - b.min) < 1e-4f && Mathf.Abs(a.max - b.max) < 1e-4f;
        }

        private static int ExpectedMin(Pawn pawn, FloatRange range)
        {
            return Mathf.RoundToInt(pawn.RaceProps.maxMechEnergy * range.min);
        }

        private static float ExpectedMax(Pawn pawn, FloatRange range)
        {
            return Mathf.RoundToInt(pawn.RaceProps.maxMechEnergy * range.max);
        }

        private static Command_Action RechargeGizmo(Pawn pawn)
        {
            string label = "MechanoidSentience.Recharge.Label".Translate();
            return pawn.GetGizmos().OfType<Command_Action>().FirstOrDefault(c => c.defaultLabel == label);
        }

        private static bool NeedsCharger(Pawn pawn)
        {
            return new JobGiver_GetEnergy_Charger().GetPriority(pawn) > 0f;
        }

        public static void Run(Func<string, Pawn> spawn)
        {
            Pawn ordinary = spawn("Mech_Lifter");
            int ordinaryMin = JobGiver_GetEnergy.GetMinAutorechargeThreshold(ordinary);
            float ordinaryMax = JobGiver_GetEnergy.GetMaxRechargeLimit(ordinary);
            Check(ordinary.TryGetComp<CompSentientRecharge>() == null, "an ordinary mech has no recharge comp");
            Check(ordinaryMin == ordinary.RaceProps.maxMechEnergy && ordinaryMax == ordinary.RaceProps.maxMechEnergy, "an ordinary mech keeps vanilla's thresholds (" + ordinaryMin + ", " + ordinaryMax + ")");
            Check(RechargeGizmo(ordinary) == null, "an ordinary mech has no recharge gizmo");

            Pawn mech = spawn("Mech_Militor");
            Check(SentientMechConverter.TryConvert(mech, showMessages: false), "test mech converted");
            CompSentientRecharge comp = mech.TryGetComp<CompSentientRecharge>();
            if (comp == null)
            {
                MechSentienceAutoTest.Fail("Recharge: no comp on the converted mech, remaining checks skipped");
                return;
            }
            Check(Near(comp.Thresholds, MechanitorControlGroup.DefaultMechRechargeThresholds), "a converted mech starts at the vanilla default range");
            Check(mech.GetMechControlGroup() == null, "the converted mech is outside every control group");
            int maxEnergy = mech.RaceProps.maxMechEnergy;
            int defaultMin = JobGiver_GetEnergy.GetMinAutorechargeThreshold(mech);
            Check(defaultMin == ExpectedMin(mech, comp.Thresholds) && defaultMin < maxEnergy, "default start threshold is 5% of " + maxEnergy + ", not full energy (" + defaultMin + ")");
            Check(JobGiver_GetEnergy.GetMaxRechargeLimit(mech) == maxEnergy, "default stop limit is full energy");

            mech.needs.energy.CurLevelPercentage = 0.99f;
            Check(!NeedsCharger(mech), "a mech at 99% energy does not look for a charger");
            mech.needs.energy.CurLevelPercentage = 0.01f;
            Check(NeedsCharger(mech), "a mech at 1% energy looks for a charger");

            var edited = new FloatRange(0.3f, 0.7f);
            comp.SetThresholds(edited);
            Check(JobGiver_GetEnergy.GetMinAutorechargeThreshold(mech) == ExpectedMin(mech, edited) && JobGiver_GetEnergy.GetMaxRechargeLimit(mech) == ExpectedMax(mech, edited), "an edited range drives both getters (" + JobGiver_GetEnergy.GetMinAutorechargeThreshold(mech) + ", " + JobGiver_GetEnergy.GetMaxRechargeLimit(mech) + ")");
            mech.needs.energy.CurLevelPercentage = 0.4f;
            Check(!NeedsCharger(mech), "40% is above an edited 30% start");
            mech.needs.energy.CurLevelPercentage = 0.2f;
            Check(NeedsCharger(mech), "20% is below an edited 30% start");
            mech.needs.energy.CurLevelPercentage = 1f;

            Pawn other = spawn("Mech_Militor");
            Check(SentientMechConverter.TryConvert(other, showMessages: false), "second test mech converted");
            CompSentientRecharge otherComp = other.TryGetComp<CompSentientRecharge>();
            Check(otherComp != null && Near(otherComp.Thresholds, MechanitorControlGroup.DefaultMechRechargeThresholds) && JobGiver_GetEnergy.GetMinAutorechargeThreshold(other) == ExpectedMin(other, MechanitorControlGroup.DefaultMechRechargeThresholds), "a second mech is unaffected by the first one's range");
            comp.SetThresholds(MechanitorControlGroup.DefaultMechRechargeThresholds);

            Command_Action gizmo = RechargeGizmo(mech);
            Check(gizmo != null && !gizmo.groupable && !gizmo.GroupsWith(gizmo), "a player sentient mech has an ungrouped recharge gizmo");
            Check(mech.GetGizmos().OfType<Command_Action>().Count(c => c.defaultLabel == gizmo?.defaultLabel) == 1, "exactly one recharge gizmo per mech");
            other.Kill(null);
            Check(otherComp != null && !otherComp.CompGetGizmosExtra().Any(), "a dead sentient mech yields no recharge gizmo");
        }

        public static void MarkForSave(Pawn pawn)
        {
            CompSentientRecharge comp = pawn.TryGetComp<CompSentientRecharge>();
            Check(comp != null, "the pawn that is saved has a recharge comp");
            comp?.SetThresholds(SavedRange);
        }

        public static void CheckAfterLoad(Pawn pawn)
        {
            CompSentientRecharge comp = pawn.TryGetComp<CompSentientRecharge>();
            Check(comp != null && Near(comp.Thresholds, SavedRange), "the custom range survived save and load (" + (comp != null ? comp.Thresholds.ToString() : "no comp") + ")");
            Check(comp != null && JobGiver_GetEnergy.GetMinAutorechargeThreshold(pawn) == ExpectedMin(pawn, SavedRange) && JobGiver_GetEnergy.GetMaxRechargeLimit(pawn) == ExpectedMax(pawn, SavedRange), "the getters use the loaded range");
            Check(RechargeGizmo(pawn) != null, "the loaded mech shows the recharge gizmo");
        }
    }
}
#endif
