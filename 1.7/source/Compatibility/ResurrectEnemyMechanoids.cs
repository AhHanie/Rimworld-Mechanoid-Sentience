using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class ResurrectionChargeAccess
    {
        private const string FieldDescription = "CompAbilityEffect_ResurrectMech.resurrectCharges";

        private static AccessTools.FieldRef<CompAbilityEffect_ResurrectMech, int> chargesField;

        public static bool IsBound => chargesField != null;

        public static string Bind()
        {
            try
            {
                chargesField = AccessTools.FieldRefAccess<CompAbilityEffect_ResurrectMech, int>("resurrectCharges");
                return null;
            }
            catch (Exception)
            {
                chargesField = null;
                return FieldDescription;
            }
        }

        public static CompAbilityEffect_ResurrectMech EffectOf(Pawn pawn, out Ability ability)
        {
            ability = pawn.abilities?.GetAbility(AbilityDefOf.ResurrectionMech);
            CompAbilityEffect_ResurrectMech found = null;
            if (ability?.EffectComps != null)
            {
                for (int i = 0; i < ability.EffectComps.Count && found == null; i++)
                {
                    found = ability.EffectComps[i] as CompAbilityEffect_ResurrectMech;
                }
            }
            return found;
        }

        public static void Set(CompAbilityEffect_ResurrectMech effect, Ability ability, int charges)
        {
            chargesField(effect) = charges;
            ability.RemainingCharges = charges;
        }
    }

    public static class ResurrectEnemyMechanoidsCompat
    {
        public const string PackageId = "nikidigi.resurrectenemymechanoids";
        public const string OriginalRaceDefName = "Mech_Apocriton";

        public const int RechargeIntervalTicks = GenDate.TicksPerDay / 5;
        public const int MaxCharges = 30;

        private static bool? available;

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
                        BindFailure = ResurrectionChargeAccess.Bind();
                        if (BindFailure != null)
                        {
                            available = false;
                            Log.Warning("[Mechanoid Sentience] Resurrect Enemy Mechanoids is active but part of the game's API changed (" + BindFailure + "). Sentient Apocritons do not regain resurrection charges.");
                        }
                    }
                }
                return available.Value;
            }
        }

        public static bool GestatableApocritonActive => ModsConfig.IsActive(GestatableApocritonCompat.PackageId);

        public static bool NeedsPassiveRecharge(ThingDef original)
        {
            if (original == null || original.defName != OriginalRaceDefName || !IsAvailable)
            {
                return false;
            }
            if (GestatableApocritonActive)
            {
                Log.Warning("[Mechanoid Sentience] Resurrect Enemy Mechanoids and Gestatable Apocriton are both active (they are incompatible with each other). Sentient Apocritons use the Gestatable Apocriton core based recharge only.");
                return false;
            }
            return true;
        }

        public static bool IsSentientApocriton(Pawn pawn)
        {
            return pawn != null && SentientMechRegistry.OriginalRaceOf(pawn.def) == ThingDefOf.Mech_Apocriton;
        }

        public static bool TryRecharge(Pawn pawn)
        {
            Ability ability = null;
            CompAbilityEffect_ResurrectMech effect = ResurrectionChargeAccess.IsBound ? ResurrectionChargeAccess.EffectOf(pawn, out ability) : null;
            if (effect == null)
            {
                return false;
            }
            if (effect.ChargesRemaining < MaxCharges)
            {
                ResurrectionChargeAccess.Set(effect, ability, effect.ChargesRemaining + 1);
            }
            return true;
        }
    }

    public class CompProperties_SentientApocritonPassiveRecharge : CompProperties
    {
        public CompProperties_SentientApocritonPassiveRecharge()
        {
            compClass = typeof(CompSentientApocritonPassiveRecharge);
        }
    }

    public class CompSentientApocritonPassiveRecharge : ThingComp
    {
        public const string ExposeKey = "msApocritonPassiveNextRecharge";

        private int nextRechargeTick = -1;

        public int NextRechargeTick
        {
            get => nextRechargeTick;
            internal set => nextRechargeTick = value;
        }

        public override void CompTickRare()
        {
            if (!(parent is Pawn pawn) || pawn.Dead || pawn.Faction != Faction.OfPlayer || !ResurrectEnemyMechanoidsCompat.IsSentientApocriton(pawn)
                || ResurrectEnemyMechanoidsCompat.GestatableApocritonActive || !ResurrectEnemyMechanoidsCompat.IsAvailable)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (nextRechargeTick < 0 || nextRechargeTick - now > ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks)
            {
                nextRechargeTick = now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks;
                return;
            }
            if (now >= nextRechargeTick && ResurrectEnemyMechanoidsCompat.TryRecharge(pawn))
            {
                nextRechargeTick = now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nextRechargeTick, ExposeKey, -1);
        }
    }
}
