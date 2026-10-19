#if DEBUG
using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class ResurrectEnemyMechanoidsAutoTest
    {
        private const int MaxFramesPerWait = 3000;
        private const int SavedCharges = 17;
        private const int SavedClockOffset = 9000;

        private static IEnumerator routine;
        private static int savedPawnId;
        private static int savedClockTick;

        public static bool Tick()
        {
            if (routine == null)
            {
                routine = Run();
            }
            try
            {
                return !routine.MoveNext();
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Resurrect Enemy Mechanoids test threw " + e);
                return true;
            }
        }

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Resurrect Enemy Mechanoids: " + what);
        }

        private static IEnumerator Run()
        {
            Map map = Find.CurrentMap;
            Check(ResurrectEnemyMechanoidsCompat.IsAvailable && ResurrectEnemyMechanoidsCompat.BindFailure == null, "the integration is available and bound (" + (ResurrectEnemyMechanoidsCompat.BindFailure ?? "ok") + ")");
            Check(!ResurrectEnemyMechanoidsCompat.GestatableApocritonActive, "Gestatable Apocriton is not active (the two are incompatible)");
            Check(ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks == 12000 && ResurrectEnemyMechanoidsCompat.MaxCharges == 30, "the interval is 12000 ticks and the cap 30");
            var tickRare = typeof(Pawn).GetMethod(nameof(Pawn.TickRare));
            Check(Harmony.GetPatchInfo(tickRare)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.Namespace == "ResurrectEnemyMechanoids") == true, "the other mod's Pawn.TickRare postfix is still installed");

            IntVec3 anchor = map.mapPawns.FreeColonists.First().Position;
            Pawn Spawn(string kindName)
            {
                Pawn mech = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindName), Faction.OfPlayer);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(anchor, map, 8), map);
                return mech;
            }

            Pawn ordinary = Spawn("Mech_Apocriton");
            Check(ordinary.TryGetComp<CompSentientApocritonPassiveRecharge>() == null && ordinary.TryGetComp<CompSentientApocritonCoreRecharge>() == null, "an ordinary Apocriton has no sentient clock");

            Check(SentientMechConverter.TryConvert(ordinary, showMessages: false), "the Apocriton converted");
            Check(ResurrectEnemyMechanoidsCompat.IsSentientApocriton(ordinary), "it is a sentient Apocriton now");
            Check(ordinary.AllComps.OfType<CompSentientApocritonPassiveRecharge>().Count() == 1, "exactly one passive clock comp");
            Check(ordinary.AllComps.OfType<CompSentientApocritonCoreRecharge>().Count() == 0, "no core based clock comp");
            Check(ordinary.abilities?.GetAbility(AbilityDefOf.ResurrectionMech) != null, "the resurrection ability is still there");

            var clock = ordinary.TryGetComp<CompSentientApocritonPassiveRecharge>();
            CompAbilityEffect_ResurrectMech effect = ResurrectionChargeAccess.EffectOf(ordinary, out Ability ability);
            if (clock == null || effect == null)
            {
                MechSentienceAutoTest.Fail("Resurrect Enemy Mechanoids: no clock or ability to test");
                yield break;
            }
            Check(!ability.UsesCharges, "the ability has no charge definition of its own, so only the effect decides castability");

            ResurrectionChargeAccess.Set(effect, ability, 10);
            clock.NextRechargeTick = -1;
            clock.CompTickRare();
            int now = Find.TickManager.TicksGame;
            Check(clock.NextRechargeTick == now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks && effect.ChargesRemaining == 10, "the first rare tick (also on an older save without the key) only starts a full wait");
            clock.NextRechargeTick = now + 1;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 10, "one tick before the interval ends nothing happens");
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11 && ability.RemainingCharges == 11, "when due, exactly one charge in the effect and the ability (" + effect.ChargesRemaining + ", " + ability.RemainingCharges + ")");
            Check(clock.NextRechargeTick == now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks, "the interval restarted");
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11, "no second charge before another interval");
            clock.NextRechargeTick = now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks * 3;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11 && clock.NextRechargeTick == now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks, "a saved clock further ahead than one interval (a backward game clock) starts a fresh wait");

            ResurrectionChargeAccess.Set(effect, ability, ResurrectEnemyMechanoidsCompat.MaxCharges);
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == ResurrectEnemyMechanoidsCompat.MaxCharges && ability.RemainingCharges == ResurrectEnemyMechanoidsCompat.MaxCharges && clock.NextRechargeTick == now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks, "full charges stay at 30 and restart the interval");

            ordinary.abilities.RemoveAbility(AbilityDefOf.ResurrectionMech);
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(clock.NextRechargeTick == now, "without the ability the clock stays due");
            ordinary.abilities.GainAbility(AbilityDefOf.ResurrectionMech);
            effect = ResurrectionChargeAccess.EffectOf(ordinary, out ability);
            Check(effect != null, "the ability and its effect are back");
            if (effect == null)
            {
                yield break;
            }
            ResurrectionChargeAccess.Set(effect, ability, 4);
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 5 && clock.NextRechargeTick == now + ResurrectEnemyMechanoidsCompat.RechargeIntervalTicks, "the due clock resumes as soon as the ability is back");

            ResurrectionChargeAccess.Set(effect, ability, 5);
            clock.NextRechargeTick = Find.TickManager.TicksGame + 400;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            int waited = 0;
            while (effect.ChargesRemaining == 5 && ++waited < MaxFramesPerWait)
            {
                yield return null;
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Check(effect.ChargesRemaining == 6 && ability.RemainingCharges == 6, "the game's rare ticks recharge a sentient Apocriton (" + waited + " frames)");

            ResurrectionChargeAccess.Set(effect, ability, 0);
            Check(!effect.CanCast, "no charges, no cast");
            clock.NextRechargeTick = Find.TickManager.TicksGame;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 1 && effect.CanCast, "one recharged charge makes the ability castable again");
            Pawn fallen = Spawn("Mech_Militor");
            fallen.Kill(null);
            Corpse corpse = fallen.Corpse;
            Check(corpse != null, "a light mech corpse to resurrect");
            if (corpse != null)
            {
                effect.Apply(new LocalTargetInfo(corpse), LocalTargetInfo.Invalid);
                Check(effect.ChargesRemaining == 0 && !fallen.Dead && !effect.CanCast, "casting spends the recharged charge (" + effect.ChargesRemaining + " left, dead=" + fallen.Dead + ")");
            }

            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Apocriton");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                MechSentienceAutoTest.Fail("Resurrect Enemy Mechanoids: no sentient kind for Mech_Apocriton");
                yield break;
            }
            Pawn generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            Check(generated.AllComps.OfType<CompSentientApocritonPassiveRecharge>().Count() == 1 && generated.AllComps.OfType<CompSentientApocritonCoreRecharge>().Count() == 0, "a directly generated sentient Apocriton has one passive clock and no core clock");
            Find.WorldPawns.PassToWorld(generated, RimWorld.Planet.PawnDiscardDecideMode.Discard);

            ThingDef sentientRace = ordinary.def;
            Check(CompListComparer.FirstDifference(original.race.comps, sentientRace.comps, out _) == null, "the sentient race's comp list matches the expected sequence");
            var both = sentientRace.comps.ToList();
            both.Add(new CompProperties_SentientApocritonCoreRecharge());
            Check(CompListComparer.FirstDifference(original.race.comps, both, out _) != null, "the comparer rejects the passive and the core based clock together");
            var twice = sentientRace.comps.ToList();
            twice.Add(new CompProperties_SentientApocritonPassiveRecharge());
            Check(CompListComparer.FirstDifference(original.race.comps, twice, out _) != null, "the comparer rejects a duplicate passive clock");

            Pawn rolledBack = Spawn("Mech_Apocriton");
            int rolledBackComps = rolledBack.AllComps.Count;
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool converted = SentientMechConverter.TryConvert(rolledBack, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(!converted && !ResurrectEnemyMechanoidsCompat.IsSentientApocriton(rolledBack), "the forced failure leaves an ordinary Apocriton");
            Check(rolledBack.AllComps.Count == rolledBackComps && rolledBack.TryGetComp<CompSentientApocritonPassiveRecharge>() == null, "the clock created for the sentient def is gone after the rollback");
            rolledBack.Destroy();

            ResurrectionChargeAccess.Set(effect, ability, SavedCharges);
            savedClockTick = Find.TickManager.TicksGame + SavedClockOffset;
            clock.NextRechargeTick = savedClockTick;
            savedPawnId = ordinary.thingIDNumber;
        }

        public static void CheckAfterLoad(Map map)
        {
            Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedPawnId);
            Check(pawn != null && ResurrectEnemyMechanoidsCompat.IsSentientApocriton(pawn), "the sentient Apocriton is still there after reload");
            if (pawn == null)
            {
                return;
            }
            CompAbilityEffect_ResurrectMech effect = ResurrectionChargeAccess.EffectOf(pawn, out Ability ability);
            Check(effect != null && effect.ChargesRemaining == SavedCharges, "the resurrection charges survived the reload (" + effect?.ChargesRemaining + ")");
            var clock = pawn.TryGetComp<CompSentientApocritonPassiveRecharge>();
            Check(clock != null && clock.NextRechargeTick == savedClockTick, "the saved clock survived the reload (" + clock?.NextRechargeTick + ", expected " + savedClockTick + ")");
            Check(pawn.AllComps.OfType<CompSentientApocritonPassiveRecharge>().Count() == 1, "still exactly one passive clock after reload");
        }
    }
}
#endif
