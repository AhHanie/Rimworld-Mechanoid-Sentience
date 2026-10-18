#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    internal static class MaidnoidAutoTest
    {
        public const string PackageId = "silversol.consti";
        private const int MaxFramesPerWait = 2500;

        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static bool started;
        private static readonly List<Pawn> convertedPawns = new List<Pawn>();
        private static readonly List<(int id, string label, string originalDefName, Dictionary<string, int> priorities)> converted = new List<(int, string, string, Dictionary<string, int>)>();

        public static bool IsActive => ModsConfig.IsActive(PackageId);

        public static bool Tick()
        {
            if (!started)
            {
                started = true;
                routines.Push(Run());
            }
            try
            {
                while (routines.Count > 0)
                {
                    IEnumerator top = routines.Peek();
                    if (!top.MoveNext())
                    {
                        routines.Pop();
                        continue;
                    }
                    if (top.Current is IEnumerator nested)
                    {
                        routines.Push(nested);
                        continue;
                    }
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Maidnoid test threw " + e);
                routines.Clear();
                return true;
            }
        }

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Maidnoid: " + what);
        }

        private static void Info(string what)
        {
            Log.Message("AUTOTEST info Maidnoid: " + what);
        }

        private static IEnumerator Run()
        {
            Map map = Find.CurrentMap;
            IntVec3 anchor = map.mapPawns.FreeColonists.First().Position;
            Pawn Spawn(string kindName)
            {
                Pawn mech = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindName), Faction.OfPlayer);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(anchor, map, 8), map);
                return mech;
            }

            var maidRaces = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Pawn && !d.defName.StartsWith(SentientMechRegistry.DefPrefix) && IsMaidnoid(d)).ToList();
            Check(maidRaces.Count > 0, "Maidnoid races are loaded (" + maidRaces.Count + ")");
            foreach (ThingDef race in maidRaces)
            {
                foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.race == race))
                {
                    bool mapped = SentientMechRegistry.TryGetSentient(race, kind, out ThingDef sentientRace, out PawnKindDef sentientKind);
                    Info("race " + race.defName + " kind " + kind.defName + " -> " + (mapped ? sentientRace.defName + " / " + sentientKind.defName : "no sentient clone"));
                    if (race.race.IsMechanoid)
                    {
                        Check(mapped, race.defName + " has a sentient clone");
                    }
                }
            }

            Pawn control = Spawn("Mech_Parvati");
            Check(!SentientMechRegistry.IsSentient(control) && control.def.defName == "Mech_Parvati", "an ordinary Parvati stays an ordinary mechanoid");
            Check(control.RaceProps.IsMechanoid && !control.RaceProps.Humanlike, "an ordinary Parvati is still not humanlike");

            Pawn consti = Convert(Spawn("Mech_Consti"), "Consti");
            if (consti != null)
            {
                CheckWorkSettings(consti, WorkTypeDefOf.Construction, WorkTypeDefOf.Hauling, WorkTypeDefOf.Cleaning);
                yield return ConstructionJob(consti, map, anchor);
                yield return HaulJob(consti, map, anchor);
            }

            Pawn sweepy = Convert(Spawn("Mech_Sweepy"), "Sweepy");
            if (sweepy != null)
            {
                CheckWorkSettings(sweepy, WorkTypeDefOf.Cleaning, WorkTypeDefOf.Hauling, WorkTypeDefOf.Construction);
                yield return CleaningJob(sweepy, map, anchor, withRefusals: true);
                yield return HaulJob(sweepy, map, anchor);
            }

            Pawn alisa = Convert(Spawn("Mech_ALisa"), "ALisa");
            if (alisa != null)
            {
                CheckWorkSettings(alisa, WorkTypeDefOf.Mining, WorkTypeDefOf.Cleaning, WorkTypeDefOf.Hauling);
                yield return DeepDrillJob(alisa, map, anchor);
                yield return CleaningJob(alisa, map, anchor, withRefusals: false);
            }

            Pawn parvati = Convert(Spawn("Mech_Parvati"), "Parvati");
            if (parvati != null)
            {
                CheckWorkSettings(parvati, WorkTypeDefOf.Research, WorkTypeDefOf.Cleaning, WorkTypeDefOf.Hauling);
                yield return ResearchJob(parvati, map, anchor);
                yield return HackJob(parvati, map, anchor);
                yield return CleaningJob(parvati, map, anchor, withRefusals: false);
            }

            Pawn vaniya = Convert(Spawn("Mech_Vaniya"), "Vaniya");
            if (vaniya != null)
            {
                CheckWorkSettings(vaniya, WorkTypeDefOf.Cleaning, WorkTypeDefOf.Hauling, WorkTypeDefOf.Construction, WorkTypeDefOf.Research);
                yield return CleaningJob(vaniya, map, anchor, withRefusals: false);
            }

            foreach (string kindName in new[] { "Mech_Strix", "Mech_LifterU", "Mech_Porterline", "Mech_TechRegina", "Mech_Fulmina", "Mech_Iskorka" })
            {
                if (DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName) == null)
                {
                    continue;
                }
                Pawn other = Convert(Spawn(kindName), kindName);
                if (other != null)
                {
                    yield return CleaningJob(other, map, anchor, withRefusals: false);
                }
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;

            converted.Clear();
            foreach (Pawn pawn in convertedPawns.Where(p => !p.Destroyed))
            {
                converted.Add((pawn.thingIDNumber, pawn.LabelShort, OriginalDefName(pawn), DefDatabase<WorkTypeDef>.AllDefsListForReading.ToDictionary(w => w.defName, w => pawn.workSettings.GetPriority(w))));
            }
        }

        public static void CheckAfterLoad(Map map)
        {
            foreach (var entry in converted)
            {
                Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == entry.id);
                Check(pawn != null && SentientMechRegistry.IsSentient(pawn), entry.label + " is still there and sentient after reload");
                if (pawn == null)
                {
                    continue;
                }
                Check(OriginalDefName(pawn) == entry.originalDefName, entry.label + " still maps to " + entry.originalDefName + " (" + OriginalDefName(pawn) + ")");
                bool same = entry.priorities.All(kv => pawn.workSettings.GetPriority(DefDatabase<WorkTypeDef>.GetNamed(kv.Key)) == kv.Value);
                Check(same, entry.label + " keeps its work priorities after reload");
            }
        }

        private static bool IsMaidnoid(Def def)
        {
            return def.modContentPack != null && string.Equals(def.modContentPack.PackageId, PackageId, StringComparison.OrdinalIgnoreCase);
        }

        private static string OriginalDefName(Pawn pawn)
        {
            return (SentientMechRegistry.OriginalRaceOf(pawn.def) ?? pawn.def).defName;
        }

        private static List<string> AbilityNames(Pawn pawn)
        {
            return pawn.abilities?.abilities == null ? new List<string>() : pawn.abilities.abilities.Select(a => a.def.defName).OrderBy(n => n).ToList();
        }

        private static Pawn Convert(Pawn pawn, string label)
        {
            AcceptanceReport report = SentientMechConverter.CanConvert(pawn);
            Info(label + ": CanConvert=" + report.Accepted + (report.Accepted ? "" : " (" + report.Reason + ")") + " def=" + pawn.def.defName + " kind=" + pawn.kindDef.defName);
            string originalDef = pawn.def.defName;
            List<string> abilities = AbilityNames(pawn);
            ThingWithComps weapon = pawn.equipment?.Primary;
            int hediffs = pawn.health.hediffSet.hediffs.Count;
            bool ok = SentientMechConverter.TryConvert(pawn, showMessages: false);
            Check(ok, label + " converts");
            if (!ok)
            {
                return null;
            }
            convertedPawns.Add(pawn);
            Check(SentientMechRegistry.IsSentient(pawn) && pawn.def.defName == SentientMechRegistry.DefPrefix + originalDef, label + " is sentient (" + pawn.def.defName + ")");
            List<string> abilitiesAfter = AbilityNames(pawn);
            Check(abilities.SequenceEqual(abilitiesAfter), label + " keeps its abilities (" + string.Join(",", abilities) + " -> " + string.Join(",", abilitiesAfter) + ")");
            Check(pawn.equipment?.Primary == weapon && pawn.health.hediffSet.hediffs.Count == hediffs, label + " keeps its weapon and hediffs");
            Check(pawn.workSettings != null && pawn.workSettings.EverWork, label + " has initialized work settings");
            Check(pawn.Faction == Faction.OfPlayer && pawn.GetOverseer() == null, label + " is player controlled without an overseer");
            Info(label + ": effective think tree " + pawn.thinker.MainThinkTree.defName + ", comps " + string.Join(",", pawn.AllComps.Select(c => c.GetType().Name)) + ", disabled work types " + string.Join(",", DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => pawn.WorkTypeIsDisabled(w)).Select(w => w.defName)));
            return pawn;
        }

        private static void CheckWorkSettings(Pawn pawn, params WorkTypeDef[] expectedActive)
        {
            foreach (WorkTypeDef work in expectedActive)
            {
                Check(!pawn.WorkTypeIsDisabled(work), pawn.LabelShort + ": " + work.defName + " is not disabled");
                int before = pawn.workSettings.GetPriority(work);
                pawn.workSettings.SetPriority(work, 3);
                Check(pawn.workSettings.GetPriority(work) == 3, pawn.LabelShort + ": " + work.defName + " priority is editable (was " + before + ")");
                pawn.workSettings.SetPriority(work, before);
            }
        }

        private static void Isolate(Pawn worker, WorkTypeDef work)
        {
            foreach (Pawn other in worker.Map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (other != worker && other.workSettings != null && !other.WorkTypeIsDisabled(work))
                {
                    other.workSettings.SetPriority(work, 0);
                }
            }
            foreach (WorkTypeDef def in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!worker.WorkTypeIsDisabled(def))
                {
                    worker.workSettings.SetPriority(def, def == work ? 1 : 0);
                }
            }
            worker.drafter.Drafted = false;
            worker.jobs.StopAll();
        }

        private static Job Offer(Pawn pawn)
        {
            ThinkResult result = pawn.thinker.MainThinkNodeRoot.TryIssueJobPackage(pawn, default);
            return result.IsValid ? result.Job : null;
        }

        private static string Name(Job job)
        {
            return job?.def.defName ?? "none";
        }

        private static IEnumerator Work(string label, Pawn pawn, WorkTypeDef work, Func<bool> done, params string[] expected)
        {
            Isolate(pawn, work);
            Job offered = Offer(pawn);
            bool offeredOk = offered != null && expected.Contains(offered.def.defName);
            Check(offeredOk, label + ": the think tree offers " + string.Join("/", expected) + " (offered " + Name(offered) + ")");
            if (!offeredOk)
            {
                yield break;
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            bool saw = false;
            int frames = 0;
            while (!done() && ++frames < MaxFramesPerWait)
            {
                if (pawn.CurJobDef != null && expected.Contains(pawn.CurJobDef.defName))
                {
                    saw = true;
                }
                if (frames % 500 == 0)
                {
                    Info(label + ": frame " + frames + " job=" + Name(pawn.CurJob) + " drafted=" + pawn.Drafted + " energy=" + pawn.needs.energy?.CurLevelPercentage.ToString("P0"));
                }
                yield return null;
            }
            Check(done(), label + ": finished (" + frames + " frames, last job " + Name(pawn.CurJob) + ")");
            Check(saw, label + ": the pawn itself ran " + string.Join("/", expected));
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        }

        private static IntVec3 FreeCell(Map map, IntVec3 near, int radius = 10, bool home = false)
        {
            return CellFinder.RandomClosewalkCellNear(near, map, radius, c => c.GetFirstThing<Thing>(map) == null && c.GetEdifice(map) == null && c.GetZone(map) == null && (!home || map.areaManager.Home[c]));
        }

        private static IEnumerator CleaningJob(Pawn pawn, Map map, IntVec3 anchor, bool withRefusals)
        {
            IntVec3 cell = FreeCell(map, anchor, 10, home: true);
            FilthMaker.TryMakeFilth(cell, map, DefDatabase<ThingDef>.GetNamed("Filth_Blood"), out Filth filth);
            string label = pawn.LabelShort + " cleaning";
            if (filth == null)
            {
                Check(false, label + ": filth could not be made");
                yield break;
            }
            AccessTools.Field(typeof(Filth), "growTick").SetValue(filth, Find.TickManager.TicksGame - 1000);
            if (withRefusals)
            {
                Isolate(pawn, WorkTypeDefOf.Cleaning);
                Job offered = Offer(pawn);
                Check(offered?.def == JobDefOf.Clean, label + ": offered before the refusals (" + Name(offered) + ")");
                if (offered?.def != JobDefOf.Clean)
                {
                    yield break;
                }

                pawn.workSettings.SetPriority(WorkTypeDefOf.Cleaning, 0);
                offered = Offer(pawn);
                Check(offered?.def != JobDefOf.Clean, label + ": priority off prevents the work (" + Name(offered) + ")");
                pawn.workSettings.SetPriority(WorkTypeDefOf.Cleaning, 1);

                pawn.drafter.Drafted = true;
                offered = Offer(pawn);
                Check(offered?.def != JobDefOf.Clean, label + ": a drafted pawn does not clean on its own (" + Name(offered) + ")");
                pawn.drafter.Drafted = false;
                pawn.jobs.StopAll();
            }
            yield return Work(label, pawn, WorkTypeDefOf.Cleaning, () => filth.Destroyed || !filth.Spawned, "Clean");
        }

        private static IEnumerator HaulJob(Pawn pawn, Map map, IntVec3 anchor)
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone);
            for (int attempts = 0; zone.Cells.Count < 4 && attempts < 50; attempts++)
            {
                IntVec3 cell = FreeCell(map, anchor, 14);
                if (!zone.ContainsCell(cell))
                {
                    zone.AddCell(cell);
                }
            }
            Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
            steel.stackCount = 25;
            GenSpawn.Spawn(steel, FreeCell(map, anchor, 14), map);
            if (steel.TryGetComp<CompForbiddable>() != null)
            {
                Isolate(pawn, WorkTypeDefOf.Hauling);
                steel.SetForbidden(true, warnOnFail: false);
                Job offered = Offer(pawn);
                Check(offered?.targetA.Thing != steel, pawn.LabelShort + " hauling: a forbidden target is refused (" + Name(offered) + ")");
                steel.SetForbidden(false, warnOnFail: false);
            }
            yield return Work(pawn.LabelShort + " hauling", pawn, WorkTypeDefOf.Hauling, () => steel.Destroyed || steel.IsInAnyStorage(), "HaulToCell");
        }

        private static IEnumerator ConstructionJob(Pawn pawn, Map map, IntVec3 anchor)
        {
            IntVec3 cell = FreeCell(map, anchor, 12);
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, cell, map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            wood.stackCount = 40;
            GenSpawn.Spawn(wood, FreeCell(map, anchor, 12), map);
            yield return Work(pawn.LabelShort + " construction", pawn, WorkTypeDefOf.Construction, () => cell.GetEdifice(map)?.def == ThingDefOf.Wall, "HaulToContainer", "FinishFrame", "Build");
            GC.KeepAlive(blueprint);
        }

        private static IEnumerator DeepDrillJob(Pawn pawn, Map map, IntVec3 anchor)
        {
            IntVec3 cell = FreeCell(map, anchor, 14);
            Thing drill = ThingMaker.MakeThing(ThingDefOf.DeepDrill);
            drill.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(drill, cell, map);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(drill.Position, 4f, true))
            {
                if (c.InBounds(map))
                {
                    map.deepResourceGrid.SetAt(c, ThingDefOf.Steel, 200);
                }
            }
            CompPowerTrader power = drill.TryGetComp<CompPowerTrader>();
            if (power != null)
            {
                power.PowerOn = true;
            }
            CompDeepDrill comp = drill.TryGetComp<CompDeepDrill>();
            Check(comp != null && comp.CanDrillNow(), pawn.LabelShort + " deep drilling: the test drill can drill");
            if (comp == null)
            {
                yield break;
            }
            string label = pawn.LabelShort + " deep drilling";
            Isolate(pawn, WorkTypeDefOf.Mining);
            pawn.workSettings.SetPriority(WorkTypeDefOf.Mining, 0);
            Job offered = Offer(pawn);
            Check(offered?.def != JobDefOf.OperateDeepDrill, label + ": priority off prevents the work (" + Name(offered) + ")");
            yield return Work(label, pawn, WorkTypeDefOf.Mining, () => comp.ProgressToNextPortionPercent > 0.02f || drill.Destroyed, "OperateDeepDrill");
        }

        private static IEnumerator ResearchJob(Pawn pawn, Map map, IntVec3 anchor)
        {
            ThingDef benchDef = DefDatabase<ThingDef>.GetNamedSilentFail("SimpleResearchBench");
            string label = pawn.LabelShort + " research";
            if (benchDef == null)
            {
                Info(label + ": no simple research bench in this game, skipped");
                yield break;
            }
            Thing bench = ThingMaker.MakeThing(benchDef, ThingDefOf.WoodLog);
            bench.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bench, FreeCell(map, anchor, 14), map, Rot4.North);
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => p.CanStartNow && p.knowledgeCategory == null && p.CanBeResearchedAt((Building_ResearchBench)bench, true)).OrderBy(p => p.baseCost).FirstOrDefault();
            if (project == null)
            {
                Info(label + ": no research project can start, skipped");
                yield break;
            }
            Find.ResearchManager.SetCurrentProject(project);
            float start = Find.ResearchManager.GetProgress(project);
            yield return Work(label, pawn, WorkTypeDefOf.Research, () => Find.ResearchManager.GetProgress(project) > start + 3f, "Research");
        }

        private static IEnumerator HackJob(Pawn pawn, Map map, IntVec3 anchor)
        {
            string label = pawn.LabelShort + " hacking";
            ThingDef target = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.category == ThingCategory.Building && d.comps != null && d.comps.Any(c => c is CompProperties_Hackable) && d.size.x <= 2 && d.size.z <= 2 && d.defName != "AncientEnemyTerminal");
            if (target == null)
            {
                Info(label + ": no hackable building in this game, skipped");
                yield break;
            }
            Thing thing = ThingMaker.MakeThing(target, target.MadeFromStuff ? GenStuff.DefaultStuffFor(target) : null);
            GenSpawn.Spawn(thing, FreeCell(map, anchor, 14), map);
            CompHackable hackable = thing.TryGetComp<CompHackable>();
            AccessTools.Field(typeof(CompHackable), "autohack").SetValue(hackable, true);
            float start = hackable.ProgressPercent;
            Info(label + ": target " + target.defName);
            yield return Work(label, pawn, WorkTypeDefOf.Research, () => hackable.ProgressPercent > start || hackable.IsHacked, "Hack");
        }
    }
}
#endif
