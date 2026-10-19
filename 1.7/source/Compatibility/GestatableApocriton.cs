using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public static class GestatableApocritonCompat
    {
        public const string PackageId = "eclair155.balancedapocriton";
        public const string OriginalRaceDefName = "Mech_Apocriton";
        public const string CoreDefName = "AP_SubcoreApocriton";

        public const int RechargeIntervalTicks = 30000;
        public const int CoresPerCharge = 3;
        public const int MaxCharges = 30;

        private const string CarrierCompName = "ResurrectMechEX.CompCoreCarrier";

        private static bool? available;
        private static Type carrierCompType;
        private static MethodBase carrierGizmos;
        private static AccessTools.FieldRef<CompAbilityEffect_ResurrectMech, int> chargesField;

        public static string BindFailure { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = ModsConfig.IsActive(PackageId);
                    available = active;
                    if (active)
                    {
                        Bind();
                        if (BindFailure != null)
                        {
                            Log.Warning("[Mechanoid Sentience] Gestatable Apocriton is active but part of its API changed (" + BindFailure + "). The matching sentient Apocriton support is disabled.");
                        }
                    }
                }
                return available.Value;
            }
        }

        public static Type CarrierCompType => IsAvailable ? carrierCompType : null;

        public static MethodBase CarrierGizmos => IsAvailable ? carrierGizmos : null;

        private static bool RechargeBound => IsAvailable && carrierCompType != null && chargesField != null;

        public static void Bind()
        {
            var failures = new List<string>();
            carrierCompType = AccessTools.TypeByName(CarrierCompName);
            carrierGizmos = null;
            chargesField = null;
            if (!typeof(ThingComp).IsAssignableFrom(carrierCompType) || !typeof(IThingHolder).IsAssignableFrom(carrierCompType))
            {
                failures.Add(CarrierCompName + " (a ThingComp and IThingHolder)");
                carrierCompType = null;
            }
            else
            {
                MethodBase moveNext = null;
                try
                {
                    moveNext = AccessTools.StateMachineMoveNext(AccessTools.DeclaredMethod(carrierCompType, "CompGetGizmosExtra"));
                }
                catch (Exception)
                {
                }
                int overseerReads = moveNext == null ? -1 : MechanoidUpgradesCompat.CountCalls(moveNext, typeof(MechanitorUtility), nameof(MechanitorUtility.GetOverseer));
                if (overseerReads == 1)
                {
                    carrierGizmos = moveNext;
                }
                else
                {
                    failures.Add("CompCoreCarrier.CompGetGizmosExtra() reads the overseer " + overseerReads + " times (expected 1)");
                }
            }
            try
            {
                chargesField = AccessTools.FieldRefAccess<CompAbilityEffect_ResurrectMech, int>("resurrectCharges");
            }
            catch (Exception)
            {
                failures.Add("CompAbilityEffect_ResurrectMech.resurrectCharges");
            }
            BindFailure = failures.Count == 0 ? null : string.Join(", ", failures);
        }

        public static ThingDef CoreDef => DefDatabase<ThingDef>.GetNamedSilentFail(CoreDefName);

        public static bool NeedsCoreRecharge(ThingDef original)
        {
            return original != null && original.defName == OriginalRaceDefName && RechargeBound && CoreDef != null
                && original.comps != null && original.comps.Any(c => c.compClass == carrierCompType);
        }

        public static bool IsSentientApocriton(Pawn pawn)
        {
            return pawn != null && SentientMechRegistry.OriginalRaceOf(pawn.def)?.defName == OriginalRaceDefName;
        }

        public static ThingComp CarrierOf(Pawn pawn)
        {
            Type type = CarrierCompType;
            if (type == null)
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

        public static ThingOwner CoresOf(Pawn pawn)
        {
            return (CarrierOf(pawn) as IThingHolder)?.GetDirectlyHeldThings();
        }

        public static CompAbilityEffect_ResurrectMech ResurrectEffectOf(Pawn pawn, out Ability ability)
        {
            ability = pawn.abilities?.GetAbility(AbilityDefOf.ResurrectionMech);
            return ability?.EffectComps?.OfType<CompAbilityEffect_ResurrectMech>().FirstOrDefault();
        }

        public static bool TryRecharge(Pawn pawn)
        {
            ThingDef core = CoreDef;
            ThingOwner cores = CoresOf(pawn);
            Ability ability = null;
            CompAbilityEffect_ResurrectMech effect = RechargeBound ? ResurrectEffectOf(pawn, out ability) : null;
            if (core == null || cores == null || effect == null || cores.TotalStackCountOfDef(core) < CoresPerCharge)
            {
                return false;
            }
            if (effect.ChargesRemaining >= MaxCharges)
            {
                return true;
            }
            int remaining = CoresPerCharge;
            for (int i = cores.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing stack = cores[i];
                if (stack.def != core)
                {
                    continue;
                }
                int take = Mathf.Min(remaining, stack.stackCount);
                cores.Take(stack, take).Destroy();
                remaining -= take;
            }
            int charges = effect.ChargesRemaining + 1;
            chargesField(effect) = charges;
            ability.RemainingCharges = charges;
            return true;
        }
    }

    public class CompProperties_SentientApocritonCoreRecharge : CompProperties
    {
        public CompProperties_SentientApocritonCoreRecharge()
        {
            compClass = typeof(CompSentientApocritonCoreRecharge);
        }
    }

    public class CompSentientApocritonCoreRecharge : ThingComp
    {
        public const string ExposeKey = "msApocritonNextRecharge";

        private int nextRechargeTick = -1;

        public int NextRechargeTick
        {
            get => nextRechargeTick;
            internal set => nextRechargeTick = value;
        }

        public override void CompTickRare()
        {
            if (!(parent is Pawn pawn) || pawn.Dead || pawn.Faction != Faction.OfPlayer || !GestatableApocritonCompat.IsSentientApocriton(pawn))
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (nextRechargeTick < 0 || nextRechargeTick - now > GestatableApocritonCompat.RechargeIntervalTicks)
            {
                nextRechargeTick = now + GestatableApocritonCompat.RechargeIntervalTicks;
                return;
            }
            if (now >= nextRechargeTick && GestatableApocritonCompat.TryRecharge(pawn))
            {
                nextRechargeTick = now + GestatableApocritonCompat.RechargeIntervalTicks;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nextRechargeTick, ExposeKey, -1);
        }
    }
}
