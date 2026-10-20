#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class GestatableApocritonAutoTest
    {
        private const int MaxFramesPerWait = 3000;
        private const int SavedCores = 6;
        private const int SavedFillTarget = 9;
        private const int SavedCharges = 14;
        private const int SavedClockOffset = 20000;

        private static IEnumerator routine;
        private static int savedPawnId;
        private static int savedClockTick;
        private static int legacySentientId;
        private static int legacyOrdinaryId;
        private static Pawn[] legacyPawns;

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
                MechSentienceAutoTest.Fail("Gestatable Apocriton test threw " + e);
                return true;
            }
        }

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Apocriton: " + what);
        }

        private static IEnumerator Run()
        {
            Map map = Find.CurrentMap;
            Check(GestatableApocritonCompat.IsAvailable && GestatableApocritonCompat.BindFailure == null, "the integration is available and every member bound (" + (GestatableApocritonCompat.BindFailure ?? "ok") + ")");
            ThingDef core = GestatableApocritonCompat.CoreDef;
            Type carrierType = GestatableApocritonCompat.CarrierCompType;
            if (core == null || carrierType == null)
            {
                MechSentienceAutoTest.Fail("Apocriton: the core item or the storage comp is missing");
                yield break;
            }
            Check(Harmony.GetPatchInfo(GestatableApocritonCompat.CarrierGizmos) != null, "the core bar iterator is patched");

            IntVec3 anchor = map.mapPawns.FreeColonists.First().Position;
            Pawn Spawn(string kindName)
            {
                Pawn mech = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindName), Faction.OfPlayer);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(anchor, map, 8), map);
                return mech;
            }

            Pawn ordinary = Spawn("Mech_Apocriton");
            ThingComp ordinaryCarrier = GestatableApocritonCompat.CarrierOf(ordinary);
            Check(ordinaryCarrier != null && ordinary.TryGetComp<CompSentientApocritonCoreRecharge>() == null, "an ordinary Apocriton has the core storage and no sentient clock");
            Check(BarCount(ordinary) == 0, "an ordinary Apocriton without an overseer still shows no bar (the overseer answer is scoped to sentient pawns)");
            AddCores(ordinary, core, 7);
            SetFillTarget(ordinaryCarrier, 12);
            object ordinaryOwner = GestatableApocritonCompat.CoresOf(ordinary);

            Check(SentientMechConverter.TryConvert(ordinary, showMessages: false), "the Apocriton converted");
            Check(GestatableApocritonCompat.IsSentientApocriton(ordinary), "it is a sentient Apocriton now");
            Check(ordinary.AllComps.Count(c => c.GetType() == carrierType) == 1, "exactly one core storage comp");
            Check(ReferenceEquals(GestatableApocritonCompat.CarrierOf(ordinary), ordinaryCarrier) && ReferenceEquals(GestatableApocritonCompat.CoresOf(ordinary), ordinaryOwner), "the storage comp and its container are the same instances");
            Check(CoreCount(ordinary, core) == 7 && FillTarget(ordinaryCarrier) == 12, "the stored cores and the fill target survived (" + CoreCount(ordinary, core) + ", " + FillTarget(ordinaryCarrier) + ")");
            Check(ordinary.AllComps.OfType<CompSentientApocritonCoreRecharge>().Count() == 1, "exactly one sentient clock comp");
            Check(ordinary.TryGetComp<CompSentientApocritonPassiveRecharge>() == null, "no free passive clock next to it (Resurrect Enemy Mechanoids, if also active, yields to the core based one)");
            Check(ordinary.abilities?.GetAbility(AbilityDefOf.ResurrectionMech) != null, "the resurrection ability is still there");
            Check(ordinary.GetOverseer() == null, "still no overseer");

            Check(BarCount(ordinary) == 0, "an unselected sentient Apocriton shows no bar");
            Find.Selector.ClearSelection();
            Find.Selector.Select(ordinary, playSound: false, forceDesignatorDeselect: false);
            Check(BarCount(ordinary) == 1, "a single selected sentient Apocriton shows exactly one bar (" + BarCount(ordinary) + ")");
            Find.Selector.ClearSelection();

            var clock = ordinary.TryGetComp<CompSentientApocritonCoreRecharge>();
            CompAbilityEffect_ResurrectMech effect = GestatableApocritonCompat.ResurrectEffectOf(ordinary, out Ability ability);
            if (clock == null || effect == null)
            {
                MechSentienceAutoTest.Fail("Apocriton: no clock or ability to test");
                yield break;
            }
            SetCharges(effect, ability, 10);
            clock.NextRechargeTick = -1;
            clock.CompTickRare();
            int now = Find.TickManager.TicksGame;
            Check(clock.NextRechargeTick == now + GestatableApocritonCompat.RechargeIntervalTicks && effect.ChargesRemaining == 10, "the first rare tick only starts a full wait");
            clock.NextRechargeTick = now + 1;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 10 && CoreCount(ordinary, core) == 7, "one tick before the interval ends nothing happens");
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11 && ability.RemainingCharges == 11 && CoreCount(ordinary, core) == 4, "when due, three cores become one charge in the effect and the ability (" + effect.ChargesRemaining + ", " + ability.RemainingCharges + ", " + CoreCount(ordinary, core) + ")");
            Check(clock.NextRechargeTick == now + GestatableApocritonCompat.RechargeIntervalTicks, "the interval restarted");

            ClearCores(ordinary);
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11 && clock.NextRechargeTick == now, "an empty store grants nothing and stays due");
            AddCores(ordinary, core, 2);
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 11 && CoreCount(ordinary, core) == 2, "two cores are not enough and are kept");
            AddCores(ordinary, core, 1);
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 12 && CoreCount(ordinary, core) == 0, "the first cores added resume recharging at the next rare tick");

            ClearCores(ordinary);
            AddCores(ordinary, core, 25);
            Check(GestatableApocritonCompat.CoresOf(ordinary).Count > 1, "25 cores sit in several stacks (" + GestatableApocritonCompat.CoresOf(ordinary).Count + ")");
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == 13 && CoreCount(ordinary, core) == 22, "with several stacks exactly three cores are spent (" + CoreCount(ordinary, core) + " left)");

            SetCharges(effect, ability, GestatableApocritonCompat.MaxCharges);
            clock.NextRechargeTick = now;
            clock.CompTickRare();
            Check(effect.ChargesRemaining == GestatableApocritonCompat.MaxCharges && CoreCount(ordinary, core) == 22 && clock.NextRechargeTick == now + GestatableApocritonCompat.RechargeIntervalTicks, "full charges spend no cores and restart the interval");

            SetCharges(effect, ability, 5);
            ClearCores(ordinary);
            AddCores(ordinary, core, 3);
            clock.NextRechargeTick = Find.TickManager.TicksGame + 400;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            int waited = 0;
            while (effect.ChargesRemaining == 5 && ++waited < MaxFramesPerWait)
            {
                yield return null;
            }
            Check(effect.ChargesRemaining == 6 && CoreCount(ordinary, core) == 0, "the game's rare ticks recharge a sentient Apocriton (" + waited + " frames)");

            ClearCores(ordinary);
            SetFillTarget(ordinaryCarrier, 4);
            var items = new List<Thing>();
            for (int i = 0; i < 4; i++)
            {
                Thing item = ThingMaker.MakeThing(core);
                items.Add(GenSpawn.Spawn(item, CellFinder.RandomClosewalkCellNear(anchor, map, 6), map));
            }
            waited = 0;
            while (CoreCount(ordinary, core) < 4 && ++waited < MaxFramesPerWait)
            {
                yield return null;
            }
            Check(CoreCount(ordinary, core) == 4, "a hauler delivered the cores into the pawn's own storage (" + CoreCount(ordinary, core) + " after " + waited + " frames)");
            Check(items.All(t => t.Destroyed || !t.Spawned), "the hauled cores left the map");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;

            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Apocriton");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                MechSentienceAutoTest.Fail("Apocriton: no sentient kind for Mech_Apocriton");
                yield break;
            }
            Pawn generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            Check(generated.AllComps.Count(c => c.GetType() == carrierType) == 1 && generated.AllComps.OfType<CompSentientApocritonCoreRecharge>().Count() == 1, "a directly generated sentient Apocriton has one storage and one clock");
            Check(GestatableApocritonCompat.CoresOf(generated) != null && FillTarget(GestatableApocritonCompat.CarrierOf(generated)) == 0, "its storage exists and starts with a fill target of zero");
            GenSpawn.Spawn(generated, CellFinder.RandomClosewalkCellNear(anchor, map, 8), map);
            Find.Selector.ClearSelection();
            Find.Selector.Select(generated, playSound: false, forceDesignatorDeselect: false);
            Check(BarCount(generated) == 1, "its single selection shows exactly one bar");
            Find.Selector.ClearSelection();
            generated.Destroy();

            Pawn rolledBack = Spawn("Mech_Apocriton");
            ThingComp rolledBackCarrier = GestatableApocritonCompat.CarrierOf(rolledBack);
            AddCores(rolledBack, core, 5);
            SetFillTarget(rolledBackCarrier, 8);
            int rolledBackComps = rolledBack.AllComps.Count;
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool converted = SentientMechConverter.TryConvert(rolledBack, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(!converted && !GestatableApocritonCompat.IsSentientApocriton(rolledBack), "the forced failure leaves an ordinary Apocriton");
            Check(rolledBack.AllComps.Count == rolledBackComps && rolledBack.TryGetComp<CompSentientApocritonCoreRecharge>() == null, "the clock created for the sentient def is gone after the rollback");
            Check(ReferenceEquals(GestatableApocritonCompat.CarrierOf(rolledBack), rolledBackCarrier) && CoreCount(rolledBack, core) == 5 && FillTarget(rolledBackCarrier) == 8, "the original storage kept its cores and fill target");
            rolledBack.Destroy();

            Pawn legacySentient = Spawn("Mech_Apocriton");
            Check(SentientMechConverter.TryConvert(legacySentient, showMessages: false), "the legacy test Apocriton converted");
            Pawn legacyOrdinary = Spawn("Mech_Apocriton");
            legacyPawns = new[] { legacySentient, legacyOrdinary };
            legacySentientId = legacySentient.thingIDNumber;
            legacyOrdinaryId = legacyOrdinary.thingIDNumber;

            ClearCores(ordinary);
            AddCores(ordinary, core, SavedCores);
            SetFillTarget(ordinaryCarrier, SavedFillTarget);
            SetCharges(effect, ability, SavedCharges);
            savedClockTick = Find.TickManager.TicksGame + SavedClockOffset;
            clock.NextRechargeTick = savedClockTick;
            savedPawnId = ordinary.thingIDNumber;
        }

        public static void CheckAfterLoad(Map map)
        {
            Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedPawnId);
            Check(pawn != null && GestatableApocritonCompat.IsSentientApocriton(pawn), "the sentient Apocriton is still there after reload");
            if (pawn == null)
            {
                return;
            }
            ThingDef core = GestatableApocritonCompat.CoreDef;
            Check(CoreCount(pawn, core) == SavedCores && FillTarget(GestatableApocritonCompat.CarrierOf(pawn)) == SavedFillTarget, "the stored cores and the fill target survived the reload (" + CoreCount(pawn, core) + ", " + FillTarget(GestatableApocritonCompat.CarrierOf(pawn)) + ")");
            CompAbilityEffect_ResurrectMech effect = GestatableApocritonCompat.ResurrectEffectOf(pawn, out Ability ability);
            Check(effect != null && effect.ChargesRemaining == SavedCharges && ability.RemainingCharges == SavedCharges, "the resurrection charges survived the reload (" + effect?.ChargesRemaining + ")");
            var clock = pawn.TryGetComp<CompSentientApocritonCoreRecharge>();
            Check(clock != null && clock.NextRechargeTick == savedClockTick, "the saved clock survived the reload (" + clock?.NextRechargeTick + ", expected " + savedClockTick + ")");
            Find.Selector.ClearSelection();
            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            Check(BarCount(pawn) == 1, "the loaded pawn shows exactly one bar");
        }

        public static void WithLegacyStorageGone(Action save)
        {
            var field = GestatableApocritonCompat.CarrierCompType.GetField("innerContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var removed = new List<(ThingComp comp, object storage)>();
            foreach (Pawn pawn in legacyPawns)
            {
                ThingComp comp = GestatableApocritonCompat.CarrierOf(pawn);
                removed.Add((comp, field.GetValue(comp)));
                field.SetValue(comp, null);
            }
            try
            {
                save();
            }
            finally
            {
                foreach (var (comp, storage) in removed)
                {
                    field.SetValue(comp, storage);
                }
            }
        }

        public static void CheckLegacyAfterLoad(Map map)
        {
            foreach (int id in new[] { legacySentientId, legacyOrdinaryId })
            {
                string who = id == legacySentientId ? "the sentient" : "the ordinary";
                Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == id);
                ThingComp carrier = pawn == null ? null : GestatableApocritonCompat.CarrierOf(pawn);
                Check(carrier != null, who + " legacy Apocriton is still there after reload");
                if (carrier == null)
                {
                    continue;
                }
                Check(GestatableApocritonCompat.CoresOf(pawn) != null, who + " legacy Apocriton got an empty core storage on load");
                try
                {
                    Check((int)carrier.GetType().GetProperty("IngredientCount").GetValue(carrier) == 0 && (bool)carrier.GetType().GetProperty("LowIngredientCount").GetValue(carrier)
                        && (float)carrier.GetType().GetProperty("PercentageFull").GetValue(carrier) == 0f, who + " legacy Apocriton reports an empty store without throwing");
                }
                catch (Exception e)
                {
                    MechSentienceAutoTest.Fail("Apocriton: " + who + " legacy Apocriton threw reading its core count: " + e.InnerException);
                }
            }
        }

        private static int BarCount(Pawn pawn)
        {
            return pawn.GetGizmos().Count(g => g.GetType().FullName == "ResurrectMechEX.CoreCarrierGizmo");
        }

        private static int CoreCount(Pawn pawn, ThingDef core)
        {
            return GestatableApocritonCompat.CoresOf(pawn)?.TotalStackCountOfDef(core) ?? -1;
        }

        private static void AddCores(Pawn pawn, ThingDef core, int count)
        {
            ThingOwner owner = GestatableApocritonCompat.CoresOf(pawn);
            while (count > 0)
            {
                Thing stack = ThingMaker.MakeThing(core);
                stack.stackCount = Math.Min(count, core.stackLimit);
                count -= stack.stackCount;
                owner.TryAdd(stack, stack.stackCount);
            }
        }

        private static void ClearCores(Pawn pawn)
        {
            GestatableApocritonCompat.CoresOf(pawn).ClearAndDestroyContents();
        }

        private static int FillTarget(ThingComp carrier)
        {
            return (int)carrier.GetType().GetField("maxToFill").GetValue(carrier);
        }

        private static void SetFillTarget(ThingComp carrier, int value)
        {
            carrier.GetType().GetField("maxToFill").SetValue(carrier, value);
        }

        private static void SetCharges(CompAbilityEffect_ResurrectMech effect, Ability ability, int value)
        {
            Traverse.Create(effect).Field("resurrectCharges").SetValue(value);
            ability.RemainingCharges = value;
        }
    }
}
#endif
