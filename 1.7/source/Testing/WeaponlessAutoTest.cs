#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    internal static class WeaponlessAutoTest
    {
        private static int armedScytherId;
        private static int armedWeaponId;
        private static int inventoryScytherId;
        private static int inventoryWeaponId;

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Weaponless: " + what);
        }

        internal static void Run(Func<string, Pawn> spawn)
        {
            Map map = Find.CurrentMap;
            ThingDef pistolDef = DefDatabase<ThingDef>.GetNamed("Gun_Autopistol");
            Pawn scyther = ConvertedSpawn(spawn, "Mech_Scyther");
            Pawn lifter = ConvertedSpawn(spawn, "Mech_Lifter");
            if (scyther == null || lifter == null)
            {
                return;
            }
            CheckTemperatureThoughts(map, scyther, lifter);
            CheckMeleeKept(scyther);
            CheckEquipEntryPoints(spawn, scyther, lifter, pistolDef);
            CheckStalePickup(scyther, lifter, pistolDef);
            CheckDisarm(spawn, pistolDef);
            CheckConversionOfArmedScyther(spawn, pistolDef);
            CheckStarterOfArmedScyther(pistolDef);
            CheckBlacklistIsDataDriven(spawn, pistolDef);
            CheckSaveFixtures(spawn, pistolDef);
        }

        private static Pawn ConvertedSpawn(Func<string, Pawn> spawn, string kind)
        {
            Pawn pawn = spawn(kind);
            if (!SentientMechConverter.TryConvert(pawn, showMessages: false))
            {
                MechSentienceAutoTest.Fail("Weaponless: " + kind + " did not convert");
                return null;
            }
            return pawn;
        }

        private static ThingWithComps MakeWeapon(ThingDef def)
        {
            return (ThingWithComps)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        }

        private static void ForceEquip(Pawn pawn, ThingWithComps weapon)
        {
            if (!pawn.equipment.GetDirectlyHeldThings().TryAdd(weapon))
            {
                MechSentienceAutoTest.Fail("Weaponless: could not arm " + pawn.ToStringSafe() + " for the test");
            }
        }

        private static void CheckTemperatureThoughts(Map map, Pawn scyther, Pawn lifter)
        {
            Pawn human = map.mapPawns.FreeColonists.FirstOrDefault(p => p.RaceProps.Humanlike && !SentientMechRegistry.IsSentient(p) && p.needs?.mood != null);
            Check(human != null, "a human colonist exists for the control");
            ThoughtDef cold = DefDatabase<ThoughtDef>.GetNamed("EnvironmentCold");
            ThoughtDef hot = DefDatabase<ThoughtDef>.GetNamed("EnvironmentHot");
            foreach (Pawn pawn in new[] { scyther, lifter, human })
            {
                if (pawn == null)
                {
                    continue;
                }
                bool sentient = SentientMechRegistry.IsSentient(pawn);
                string who = (sentient ? "sentient " : "") + pawn.def.defName;
                Room room = pawn.Position.GetRoom(pawn.Map);
                if (room == null)
                {
                    MechSentienceAutoTest.Fail("Weaponless: no room at " + who);
                    continue;
                }
                float original = room.Temperature;
                try
                {
                    CheckThoughtAt(pawn, room, cold, pawn.GetStatValue(StatDefOf.ComfyTemperatureMin) - 35f, sentient, who + " in a cold room");
                    CheckThoughtAt(pawn, room, hot, pawn.GetStatValue(StatDefOf.ComfyTemperatureMax) + 35f, sentient, who + " in a hot room");
                }
                finally
                {
                    room.Temperature = original;
                    pawn.needs.mood.thoughts.situational.Notify_SituationalThoughtsDirty();
                }
            }
            ThoughtDef dark = DefDatabase<ThoughtDef>.GetNamed("EnvironmentDark");
            ThoughtDef wet = DefDatabase<ThoughtDef>.GetNamed("SoakingWet");
            Check(ThoughtUtility.CanGetThought(scyther, dark) && ThoughtUtility.CanGetThought(scyther, wet), "other mood thoughts are still available to a sentient mechanoid");
        }

        private static void CheckThoughtAt(Pawn pawn, Room room, ThoughtDef thought, float temperature, bool sentient, string where)
        {
            room.Temperature = temperature;
            Check(Math.Abs(pawn.AmbientTemperature - temperature) < 0.01f, where + ": the ambient temperature is " + temperature.ToString("F0") + " (was " + pawn.AmbientTemperature.ToString("F0") + ")");
            bool canGet = ThoughtUtility.CanGetThought(pawn, thought);
            bool active = thought.Worker.CurrentState(pawn).Active;
            pawn.needs.mood.thoughts.situational.Notify_SituationalThoughtsDirty();
            var thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughts);
            bool inMood = thoughts.Any(t => t.def == thought);
            Check(canGet == !sentient && active == !sentient && inMood == !sentient,
                where + ": " + thought.defName + (sentient ? " is blocked" : " is active") + " (canGet=" + canGet + ", worker active=" + active + ", in mood=" + inMood + ")");
        }

        private static void CheckMeleeKept(Pawn scyther)
        {
            Check(scyther.def.tools != null && scyther.def.tools.Count >= 2 && scyther.equipment.Primary == null, "the sentient Scyther keeps its " + scyther.def.tools?.Count + " blade tools and holds no weapon");
            List<Verb> verbs = scyther.meleeVerbs.GetUpdatedAvailableVerbsList(terrainTools: false).Select(v => v.verb).ToList();
            Check(verbs.Count >= 2 && verbs.All(v => v.tool != null), "its melee attack verbs come from the body part tools (" + verbs.Count + ")");
        }

        private static void CheckEquipEntryPoints(Func<string, Pawn> spawn, Pawn scyther, Pawn lifter, ThingDef pistolDef)
        {
            ThingWithComps onGround = MakeWeapon(pistolDef);
            GenSpawn.Spawn(onGround, scyther.Position, scyther.Map);
            Check(!EquipmentUtility.CanEquip(onGround, scyther, out string reason) && !string.IsNullOrEmpty(reason), "CanEquip refuses a ground weapon for the Scyther: " + reason);
            Check(EquipmentUtility.CanEquip(onGround, lifter), "CanEquip accepts it for the sentient Lifter");

            scyther.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Equip, onGround), JobTag.Misc);
            Check(scyther.CurJob?.def != JobDefOf.Equip, "an ordered Equip job is not started");
            scyther.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Equip, onGround), JobCondition.InterruptForced);
            Check(scyther.CurJob?.def != JobDefOf.Equip && scyther.equipment.Primary == null && onGround.Spawned, "a directly started Equip job fails before the weapon is touched");
            scyther.jobs.StopAll();

            ThingWithComps detached = MakeWeapon(pistolDef);
            scyther.equipment.AddEquipment(detached);
            Check(!scyther.equipment.HasAnything() && !detached.Destroyed && (detached.Spawned || scyther.inventory.Contains(detached)), "AddEquipment refuses a loose weapon and keeps it on the ground or in the inventory");
            ThingWithComps held = MakeWeapon(pistolDef);
            scyther.inventory.innerContainer.TryAdd(held);
            scyther.equipment.AddEquipment(held);
            Check(!scyther.equipment.HasAnything() && scyther.inventory.Contains(held) && !held.Destroyed, "AddEquipment leaves an item the inventory holds where it is");

            ThingWithComps liftersGun = MakeWeapon(pistolDef);
            lifter.equipment.AddEquipment(liftersGun);
            Check(lifter.equipment.Primary == liftersGun, "the sentient Lifter can equip a normal weapon");
            Pawn ordinary = spawn("Mech_Scyther");
            ThingWithComps ordinaryGun = MakeWeapon(pistolDef);
            GenSpawn.Spawn(ordinaryGun, ordinary.Position, ordinary.Map);
            Check(EquipmentUtility.CanEquip(ordinaryGun, ordinary), "vanilla equipment eligibility is unchanged for an ordinary Scyther");
            Pawn human = scyther.Map.mapPawns.FreeColonists.FirstOrDefault(p => p.RaceProps.Humanlike && !SentientMechRegistry.IsSentient(p));
            Check(human == null || EquipmentUtility.CanEquip(ordinaryGun, human), "vanilla equipment eligibility is unchanged for a human");
        }

        private static void CheckStalePickup(Pawn scyther, Pawn lifter, ThingDef pistolDef)
        {
            ThingWithComps dropped = MakeWeapon(pistolDef);
            GenSpawn.Spawn(dropped, scyther.Position, scyther.Map);
            scyther.mindState.droppedWeapon = dropped;
            var giver = new JobGiver_PickupDroppedWeapon();
            ThinkResult result = giver.TryIssueJobPackage(scyther, default);
            Check(result.Job == null && scyther.mindState.droppedWeapon == null, "no pickup job for the Scyther's stale dropped weapon, and the reference is cleared");

            lifter.mindState.droppedWeapon = dropped;
            ThinkResult liftersResult = giver.TryIssueJobPackage(lifter, default);
            Check(liftersResult.Job?.def == JobDefOf.Equip, "the sentient Lifter is still sent to pick its dropped weapon up");
            lifter.mindState.droppedWeapon = null;
        }

        private static void CheckDisarm(Func<string, Pawn> spawn, ThingDef pistolDef)
        {
            Pawn scyther = ConvertedSpawn(spawn, "Mech_Scyther");
            if (scyther == null)
            {
                return;
            }
            ThingDef builtInDef = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.IsWeapon && d.destroyOnDrop && d.equipmentType == EquipmentType.Primary);
            ThingWithComps ordinary = MakeWeapon(pistolDef);
            ThingWithComps builtIn = builtInDef != null ? MakeWeapon(builtInDef) : null;
            ForceEquip(scyther, ordinary);
            if (builtIn != null)
            {
                ForceEquip(scyther, builtIn);
            }
            scyther.mindState.droppedWeapon = ordinary;
            Check(SentientMechWeaponRules.Disarm(scyther, allowDrop: true), "Disarm reports success");
            Check(!scyther.equipment.HasAnything(), "an armed sentient Scyther holds nothing after Disarm (all equipped items, not only the primary)");
            Check(!ordinary.Destroyed && (ordinary.Spawned || scyther.inventory.Contains(ordinary)), "the ordinary weapon survives, on the map or in the inventory");
            Check(builtIn == null || (!builtIn.Destroyed && scyther.inventory.Contains(builtIn)), "a destroyOnDrop weapon is moved to the inventory, not destroyed (" + builtInDef?.defName + ")");
            Check(scyther.mindState.droppedWeapon == null, "droppedWeapon is cleared");
            Check(SentientMechWeaponRules.Disarm(scyther, allowDrop: true), "Disarm again is a no-op");
        }

        private static void CheckConversionOfArmedScyther(Func<string, Pawn> spawn, ThingDef pistolDef)
        {
            Pawn armed = spawn("Mech_Scyther");
            ThingWithComps weapon = MakeWeapon(pistolDef);
            ForceEquip(armed, weapon);
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool rolledBack = !SentientMechConverter.TryConvert(armed, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(rolledBack && !SentientMechRegistry.IsSentient(armed) && armed.equipment.Primary == weapon, "a rolled back conversion leaves the original Scyther with its original equipment");

            Check(SentientMechConverter.TryConvert(armed, showMessages: false), "the armed Scyther converts");
            Check(!armed.equipment.HasAnything() && !weapon.Destroyed && (weapon.Spawned || armed.inventory.Contains(weapon)), "after conversion the Scyther is unarmed and its weapon survives");
        }

        private static void CheckStarterOfArmedScyther(ThingDef pistolDef)
        {
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                MechSentienceAutoTest.Fail("Weaponless: no sentient kind for Mech_Scyther");
                return;
            }
            Pawn starter = PawnGenerator.GeneratePawn(new PawnGenerationRequest(original, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, developmentalStages: DevelopmentalStage.Adult));
            ThingWithComps weapon = MakeWeapon(pistolDef);
            ForceEquip(starter, weapon);
            SentientMechConverter.PrepareUnspawned(starter, sentientKind.race, sentientKind);
            Check(!starter.Spawned && !starter.equipment.HasAnything() && starter.inventory.Contains(weapon) && !weapon.Destroyed, "a starting Scyther is unarmed and its weapon is in its inventory");

            ThingWithComps loose = MakeWeapon(pistolDef);
            starter.equipment.AddEquipment(loose);
            Check(!starter.equipment.HasAnything() && starter.inventory.Contains(loose), "AddEquipment puts a refused weapon into the inventory of a pawn that is not on a map");
            Find.WorldPawns.PassToWorld(starter, RimWorld.Planet.PawnDiscardDecideMode.Discard);
        }

        private static void CheckBlacklistIsDataDriven(Func<string, Pawn> spawn, ThingDef pistolDef)
        {
            ThingDef scytherRace = DefDatabase<ThingDef>.GetNamed("Mech_Scyther");
            foreach (ThingDef sentient in SentientMechRegistry.SentientRaces)
            {
                ThingDef original = SentientMechRegistry.OriginalRaceOf(sentient);
                bool listed = DefDatabase<SentientWeaponBlacklistDef>.AllDefsListForReading.Any(b => b.races.Contains(original));
                if (SentientMechWeaponRules.IsWeaponlessRace(sentient) != listed)
                {
                    MechSentienceAutoTest.Fail("Weaponless: " + sentient.defName + " weaponless=" + !listed + " but listed=" + listed);
                    return;
                }
            }
            Check(SentientMechWeaponRules.IsWeaponlessRace(SentientMechRegistry.SentientOf(scytherRace)), "the Scyther is on the shipped blacklist");

            SentientWeaponBlacklistDef blacklist = DefDatabase<SentientWeaponBlacklistDef>.AllDefsListForReading.First();
            ThingDef lifterRace = DefDatabase<ThingDef>.GetNamed("Mech_Lifter");
            Pawn lifter = ConvertedSpawn(spawn, "Mech_Lifter");
            if (lifter == null)
            {
                return;
            }
            ThingWithComps gun = MakeWeapon(pistolDef);
            lifter.equipment.AddEquipment(gun);
            Check(lifter.equipment.Primary == gun, "before listing the Lifter it can hold a weapon");
            blacklist.races.Add(lifterRace);
            try
            {
                SentientMechWeaponRules.Rebuild();
                Check(SentientMechWeaponRules.CannotEquipWeapons(lifter), "a race added to the blacklist is weaponless");
                Check(SentientMechRegistry.TryGetSentient(lifterRace, DefDatabase<PawnKindDef>.GetNamed("Mech_Lifter"), out _, out PawnKindDef lifterKind)
                    && SentientMechWeaponRules.IsWeaponlessRace(lifterKind.race), "and so is every sentient kind of that race");
                Check(!EquipmentUtility.CanEquip(MakeWeapon(pistolDef), lifter, out string reason) && !string.IsNullOrEmpty(reason), "CanEquip now refuses it: " + reason);
                Check(SentientMechWeaponRules.Disarm(lifter, allowDrop: true) && !lifter.equipment.HasAnything() && !gun.Destroyed, "its held weapon is reconciled without being destroyed");
            }
            finally
            {
                blacklist.races.Remove(lifterRace);
                SentientMechWeaponRules.Rebuild();
            }
            Check(!SentientMechWeaponRules.CannotEquipWeapons(lifter), "after the entry is removed the Lifter is no longer weaponless");
        }

        private static void CheckSaveFixtures(Func<string, Pawn> spawn, ThingDef pistolDef)
        {
            Pawn armed = ConvertedSpawn(spawn, "Mech_Scyther");
            Pawn stowed = ConvertedSpawn(spawn, "Mech_Scyther");
            if (armed == null || stowed == null)
            {
                return;
            }
            ThingWithComps weapon = MakeWeapon(pistolDef);
            ForceEquip(armed, weapon);
            armedScytherId = armed.thingIDNumber;
            armedWeaponId = weapon.thingIDNumber;
            Check(armed.equipment.Primary == weapon, "a sentient Scyther is saved still armed, to exercise the load migration");

            ThingWithComps stowedWeapon = MakeWeapon(pistolDef);
            ForceEquip(stowed, stowedWeapon);
            SentientMechWeaponRules.Disarm(stowed, allowDrop: false);
            inventoryScytherId = stowed.thingIDNumber;
            inventoryWeaponId = stowedWeapon.thingIDNumber;
            Check(stowed.inventory.Contains(stowedWeapon), "another Scyther is saved with its weapon in the inventory");
        }

        internal static void VerifyAfterLoad(Map map)
        {
            if (armedScytherId == 0)
            {
                return;
            }
            Pawn armed = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == armedScytherId);
            Check(armed != null && !armed.equipment.HasAnything(), "after reload the Scyther that was saved armed is unarmed");
            Check(armed != null && armed.inventory.innerContainer.Any(t => t.thingIDNumber == armedWeaponId), "and its weapon is in its inventory");
            Pawn stowed = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == inventoryScytherId);
            Check(stowed != null && !stowed.equipment.HasAnything() && stowed.inventory.innerContainer.Any(t => t.thingIDNumber == inventoryWeaponId), "after reload the stowed weapon is still in the other Scyther's inventory");
        }
    }
}
#endif
