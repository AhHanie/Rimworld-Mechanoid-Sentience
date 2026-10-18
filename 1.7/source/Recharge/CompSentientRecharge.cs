using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public class CompProperties_SentientRecharge : CompProperties
    {
        public CompProperties_SentientRecharge()
        {
            compClass = typeof(CompSentientRecharge);
        }
    }

    public class CompSentientRecharge : ThingComp
    {
        public const string ExposeKey = "msRechargeThresholds";

        private FloatRange thresholds = SentientRechargeUtility.DefaultThresholds;

        public FloatRange Thresholds => thresholds;

        public void SetThresholds(FloatRange value)
        {
            thresholds = SentientRechargeUtility.Sanitize(value);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref thresholds, ExposeKey, SentientRechargeUtility.DefaultThresholds);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                thresholds = SentientRechargeUtility.Sanitize(thresholds);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is Pawn pawn && pawn.Spawned && !pawn.Dead && pawn.Faction == Faction.OfPlayer && SentientMechRegistry.IsSentient(pawn))
            {
                yield return SentientRechargeUtility.MakeGizmo(this, pawn);
            }
        }
    }

    public static class SentientRechargeUtility
    {
        public static FloatRange DefaultThresholds => MechanitorControlGroup.DefaultMechRechargeThresholds;

        private static readonly CachedTexture Icon = new CachedTexture("UI/Icons/MechRechargeSettings");

        public static FloatRange Sanitize(FloatRange range)
        {
            if (float.IsNaN(range.min) || float.IsNaN(range.max))
            {
                return DefaultThresholds;
            }
            float min = Mathf.Clamp01(GenMath.RoundTo(range.min, 0.01f));
            float max = Mathf.Clamp01(GenMath.RoundTo(range.max, 0.01f));
            return min <= max ? new FloatRange(min, max) : new FloatRange(max, min);
        }

        public static int MinThreshold(int maxEnergy, FloatRange range)
        {
            return Mathf.RoundToInt(maxEnergy * range.min);
        }

        public static float MaxLimit(int maxEnergy, FloatRange range)
        {
            return Mathf.RoundToInt(maxEnergy * range.max);
        }

        public static FloatRange RangeOf(Pawn pawn)
        {
            CompSentientRecharge comp = pawn.TryGetComp<CompSentientRecharge>();
            if (comp != null)
            {
                return comp.Thresholds;
            }
            Log.ErrorOnce("[Mechanoid Sentience] " + pawn.def.defName + " has no " + nameof(CompProperties_SentientRecharge) + " (broken def), its recharge limits fall back to the default.", pawn.def.shortHash ^ 0x5E17C4A);
            return DefaultThresholds;
        }

        public static string Describe(FloatRange range)
        {
            return "MechanoidSentience.Recharge.Current".Translate(range.min.ToStringPercent(), range.max.ToStringPercent());
        }

        internal static Command_Action MakeGizmo(CompSentientRecharge comp, Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "MechanoidSentience.Recharge.Label".Translate(),
                defaultDesc = "MechanoidSentience.Recharge.Tooltip".Translate(pawn.LabelShortCap, Describe(comp.Thresholds)),
                icon = Icon.Texture,
                groupable = false,
                Order = -88f,
                action = () => Find.WindowStack.Add(new Dialog_SentientRechargeSettings(comp, pawn))
            };
        }
    }
}
