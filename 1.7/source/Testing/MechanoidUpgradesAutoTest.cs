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
    internal static class MechanoidUpgradesAutoTest
    {
        private const string UpgraderDefName = "MU_MechUpgrader";
        private const int MaxFramesPerWait = 3000;

        private static IEnumerator routine;

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
                MechSentienceAutoTest.Fail("Mechanoid Upgrades test threw " + e);
                return true;
            }
        }

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "MU: " + what);
        }

        private static IEnumerator Run()
        {
            Map map = Find.CurrentMap;
            Type utility = AccessTools.TypeByName("MU.MechUpgradeUtility");
            Type upgradeDefType = AccessTools.TypeByName("MU.MechUpgradeDef");
            if (utility == null || upgradeDefType == null)
            {
                MechSentienceAutoTest.Fail("MU: Mechanoid Upgrades types not found");
                yield break;
            }
            Check(MechanoidUpgradesCompat.IsAvailable && MechanoidUpgradesCompat.BindFailure == null, "the integration is available and every member bound (" + (MechanoidUpgradesCompat.BindFailure ?? "ok") + ")");
            Check(MechanoidUpgradesCompat.UpgraderTickPatched, "the upgrader tick rewrite is in effect");

            CheckThinkTree();
            CheckRaceMapping(utility);

            IntVec3 anchor = map.mapPawns.FreeColonists.First().Position;
            Pawn Spawn(string kindName)
            {
                Pawn mech = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindName), Faction.OfPlayer);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(anchor, map, 8), map);
                return mech;
            }
            ThingDef upgraderDef = DefDatabase<ThingDef>.GetNamedSilentFail(UpgraderDefName);
            if (upgraderDef == null)
            {
                MechSentienceAutoTest.Fail("MU: " + UpgraderDefName + " not found");
                yield break;
            }

            Pawn probe = Spawn("Mech_Lancer");
            if (!SentientMechConverter.TryConvert(probe, showMessages: false))
            {
                MechSentienceAutoTest.Fail("MU: lancer did not convert");
                yield break;
            }
            dynamic comp = probe.AllComps.First(c => c.GetType().FullName == "MU.CompUpgradableMechanoid");
            int maxPoints = (int)utility.GetMethod("MaxUpgradePoints").Invoke(null, new object[] { probe });
            Check(maxPoints > 0, "a sentient lancer has upgrade points (" + maxPoints + ")");
            var candidates = ((IEnumerable)utility.GetProperty("UpgradesDatabase").GetValue(null)).Cast<Def>()
                .Where(d => (int)Reflect(d, "upgradePoints") <= maxPoints && Reflect(d, "linkedThingDef") != null && Reflect(d, "ability") == null && (bool)Accepts(d, probe.def)).ToList();
            Check(candidates.Count >= 2, "the sentient lancer is offered upgrades (" + candidates.Count + ")");
            if (candidates.Count < 2)
            {
                yield break;
            }
            Def first = candidates[0];
            Def second = candidates.FirstOrDefault(d => d != first && (int)Reflect(d, "upgradePoints") + (int)Reflect(first, "upgradePoints") <= maxPoints) ?? candidates[1];
            Log.Message("AUTOTEST info MU upgrades used: " + first.defName + ", " + second.defName);

            Pawn ordinary = Spawn("Mech_Militor");
            Thing upgrader = SpawnUpgrader(upgraderDef, map, anchor);
            dynamic building = upgrader;
            upgrader.TryGetComp<CompPowerTrader>().PowerOn = false;
            AcceptanceReport unpowered = ((Building_Enterable)upgrader).CanAcceptPawn(probe);
            Check(!unpowered.Accepted && !unpowered.Reason.NullOrEmpty(), "an unpowered upgrader refuses a sentient mech with a reason (" + unpowered.Reason + ")");
            upgrader.TryGetComp<CompPowerTrader>().PowerOn = true;
            AcceptanceReport ordinaryReport = ((Building_Enterable)upgrader).CanAcceptPawn(ordinary);
            Check(!ordinaryReport.Accepted, "an ordinary mech without an overseer is still refused by MU's own check");

            AcceptanceReport accepted = ((Building_Enterable)upgrader).CanAcceptPawn(probe);
            Check(accepted.Accepted, "a powered upgrader accepts a sentient mech");
            ((Building_Enterable)upgrader).SelectedPawn = probe;
            Check(!((Building_Enterable)upgrader).CanAcceptPawn(ordinary).Accepted, "a second mech is refused while the first is selected");
            ((Building_Enterable)upgrader).TryAcceptPawn(probe);
            Check(!probe.Spawned && (object)building.Occupant == probe, "the sentient mech is inside the upgrader");
            Check(Find.WindowStack.Windows.Any(w => w.GetType().FullName == "MU.Dialog_ConfigureUpgrades"), "the configuration dialog opened");
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            Window dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().FullName == "MU.Dialog_ConfigureUpgrades");
            if (dialog != null)
            {
                Find.WindowStack.TryRemove(dialog);
            }
            string inspect = ((Thing)upgrader).GetInspectString();
            Check(inspect != null && !inspect.Contains("mechanitor"), "the upgrader's inspect text does not wait for a mechanitor (" + inspect.Replace('\n', '|') + ")");

            int itemsBefore = map.listerThings.ThingsOfDef((ThingDef)Reflect(first, "linkedThingDef")).Count;
            object upgradeA = utility.GetMethod("MakeUpgrade", new[] { upgradeDefType }).Invoke(null, new object[] { first });
            QueueOperations(utility, building, comp, upgradeA, 1);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            int waited = 0;
            while (building.operations != null && building.operations.Count > 0 && ++waited < MaxFramesPerWait)
            {
                yield return null;
            }
            yield return null;
            Check(waited < MaxFramesPerWait, "the add operation finished (" + waited + " frames)");
            Check(((IList)comp.upgrades).Contains(upgradeA), "the upgrade was added to the sentient mech (not discarded)");
            Check(probe.Spawned && (object)building.Occupant == null, "the mech was ejected after the operation");
            Check(map.listerThings.ThingsOfDef((ThingDef)Reflect(first, "linkedThingDef")).Count == itemsBefore, "no upgrade item was spawned back by an aborted operation");
            Check(probe.GetGizmos().ToList().Count > 0, "gizmos of an upgraded sentient mech enumerate without error");

            ((Building_Enterable)upgrader).SelectedPawn = probe;
            ((Building_Enterable)upgrader).TryAcceptPawn(probe);
            dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().FullName == "MU.Dialog_ConfigureUpgrades");
            if (dialog != null)
            {
                Find.WindowStack.TryRemove(dialog);
            }
            QueueOperations(utility, building, comp, null, 1);
            waited = 0;
            while (building.operations != null && building.operations.Count > 0 && ++waited < MaxFramesPerWait)
            {
                yield return null;
            }
            yield return null;
            Check(((IList)comp.upgrades).Count == 0, "the upgrade was removed again");
            Check(map.listerThings.ThingsOfDef((ThingDef)Reflect(first, "linkedThingDef")).Count == itemsBefore + 1, "the removed upgrade came back as an item");

            ((Building_Enterable)upgrader).SelectedPawn = probe;
            ((Building_Enterable)upgrader).TryAcceptPawn(probe);
            dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().FullName == "MU.Dialog_ConfigureUpgrades");
            if (dialog != null)
            {
                Find.WindowStack.TryRemove(dialog);
            }
            object upgradeB = utility.GetMethod("MakeUpgrade", new[] { upgradeDefType }).Invoke(null, new object[] { second });
            QueueOperations(utility, building, comp, upgradeB, 100000);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Check(building.operations != null && building.operations.Count == 1 && (object)building.Occupant == probe, "a long operation is still running with the sentient mech inside (not wiped by the tick)");
            int itemsOfSecond = map.listerThings.ThingsOfDef((ThingDef)Reflect(second, "linkedThingDef")).Count;
            upgrader.Destroy();
            yield return null;
            Check(probe.Spawned && !probe.Dead, "destroying the upgrader ejected the sentient mech");
            Check(map.listerThings.ThingsOfDef((ThingDef)Reflect(second, "linkedThingDef")).Count == itemsOfSecond + 1, "destroying the upgrader returned the queued upgrade item");
            Check(((IList)comp.upgrades).Count == 0, "no half applied upgrade was left on the mech");

            Pawn early = Spawn("Mech_Lancer");
            dynamic earlyComp = early.AllComps.First(c => c.GetType().FullName == "MU.CompUpgradableMechanoid");
            earlyComp.AddUpgrade((dynamic)first);
            int before = ((IList)earlyComp.upgrades).Count;
            object instance = before > 0 ? ((IList)earlyComp.upgrades)[0] : null;
            Check(before == 1, "a lancer received an upgrade before the conversion");
            Check(SentientMechConverter.TryConvert(early, showMessages: false), "the upgraded lancer converted");
            dynamic afterComp = early.AllComps.First(c => c.GetType().FullName == "MU.CompUpgradableMechanoid");
            Check(((IList)afterComp.upgrades).Count == 1 && ((IList)afterComp.upgrades)[0] == instance, "the upgrade survived the conversion as the same instance");
            Check(early.GetGizmos().ToList().Count > 0, "gizmos of a converted upgraded mech enumerate without error");

            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        }

        private static void QueueOperations(Type utility, dynamic building, dynamic comp, object add, int ticksLeft)
        {
            Type upgradeType = AccessTools.TypeByName("MU.MechUpgrade");
            IList wanted = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(upgradeType));
            if (add != null)
            {
                wanted.Add(add);
            }
            IEnumerable operations = (IEnumerable)utility.GetMethod("GetOperationsFromLists").Invoke(null, new object[] { comp.upgrades, wanted, building });
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(AccessTools.TypeByName("MU.MechUpgradeOperation")));
            foreach (object operation in operations)
            {
                list.Add(operation);
            }
            building.operations = (dynamic)list;
            building.fabricationTicksLeft = ticksLeft;
        }

        private static Thing SpawnUpgrader(ThingDef def, Map map, IntVec3 near)
        {
            Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(near, map, 14);
            GenSpawn.Spawn(thing, cell, map, Rot4.North, WipeMode.Vanish);
            return thing;
        }

        private static object Reflect(object target, string field)
        {
            return AccessTools.Field(target.GetType(), field).GetValue(target);
        }

        private static AcceptanceReport Accepts(Def upgradeDef, ThingDef race)
        {
            return (AcceptanceReport)AccessTools.Method(upgradeDef.GetType(), "CanAdd").Invoke(upgradeDef, new object[] { race, null });
        }

        private static void CheckRaceMapping(Type utility)
        {
            var upgrades = ((IEnumerable)utility.GetProperty("UpgradesDatabase").GetValue(null)).Cast<Def>().ToList();
            int compared = 0;
            int different = 0;
            foreach (ThingDef sentient in SentientMechRegistry.SentientRaces)
            {
                ThingDef original = SentientMechRegistry.OriginalOf(sentient);
                foreach (Def upgrade in upgrades)
                {
                    compared++;
                    if (Accepts(upgrade, sentient).Accepted != Accepts(upgrade, original).Accepted)
                    {
                        different++;
                    }
                }
            }
            Check(compared > 0 && different == 0, "every upgrade is offered to a sentient race exactly as to its original (" + compared + " comparisons, " + different + " different)");
        }

        private static void CheckThinkTree()
        {
            ThinkTreeDef tree = DefDatabase<ThinkTreeDef>.GetNamed("MS_SentientMech");
            Check(ContainsNode(tree.thinkRoot, "MU.ThinkNode_ConditionalUpgradeNeedsReload"), "the sentient think tree contains MU's reload node");
        }

        private static bool ContainsNode(Verse.AI.ThinkNode node, string className)
        {
            if (node.GetType().FullName == className)
            {
                return true;
            }
            return node.subNodes != null && node.subNodes.Any(n => ContainsNode(n, className));
        }
    }
}
#endif
