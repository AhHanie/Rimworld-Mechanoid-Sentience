#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public class MechSentienceAutoTest : GameComponent
    {
        private enum Step { WaitForGame, ConvertAndRollback, Run, Screenshot, Save, WaitForLoad, VerifyAfterLoad, Done }

        private const string SaveName = "mechsentienceautotest";
        private const int MaxFramesPerStep = 4000;

        private static bool argRead;
        private static string outputFolder;
        private static Step step = Step.WaitForGame;
        private static int stepFrames;
        private static int failures;
        private static int sentientThingId;
        private static string snapshot;
        private static Game gameBeforeLoad;
        private static int runStartTick;

        public MechSentienceAutoTest(Game game)
        {
        }

        public override void GameComponentUpdate()
        {
            if (!argRead)
            {
                argRead = true;
                GenCommandLine.TryGetCommandLineArg("mechsentienceautotest", out outputFolder);
            }
            if (outputFolder == null || step == Step.Done)
            {
                return;
            }

            stepFrames++;
            if (stepFrames > MaxFramesPerStep)
            {
                Fail("timeout in step " + step);
                Finish();
                return;
            }
            switch (step)
            {
                case Step.WaitForGame:
                    if (stepFrames > 180 && Find.CurrentMap != null && !LongEventHandler.AnyEventNowOrWaiting)
                    {
                        Next(Step.ConvertAndRollback);
                    }
                    break;
                case Step.ConvertAndRollback:
                    ConvertAndRollback();
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    runStartTick = Find.TickManager.TicksGame;
                    Next(Step.Run);
                    break;
                case Step.Run:
                    if (Find.TickManager.TicksGame - runStartTick >= 1500)
                    {
                        Pawn pawn = FindSentient();
                        Check(pawn != null && !pawn.Dead, "sentient pawn alive after 1500 ticks");
                        if (pawn != null)
                        {
                            Check(pawn.needs.energy != null && pawn.needs.mood != null, "energy and mood needs present");
                            Check(pawn.needs.food == null && pawn.needs.rest == null, "no food or rest need");
                            Log.Message("AUTOTEST info job=" + (pawn.CurJob?.def.defName ?? "none") + " energy=" + pawn.needs.energy.CurLevelPercentage.ToString("P0") + " mood=" + pawn.needs.mood.CurLevelPercentage.ToString("P0"));
                            Find.Selector.ClearSelection();
                            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
                            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                            Next(Step.Screenshot);
                        }
                        else
                        {
                            Finish();
                        }
                    }
                    break;
                case Step.Screenshot:
                    if (stepFrames == 30)
                    {
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(outputFolder, "sentient_before_save.png"));
                    }
                    if (stepFrames > 60)
                    {
                        Pawn pawn = FindSentient();
                        RenderTexture portrait = PortraitsCache.Get(pawn, new Vector2(128f, 128f), Rot4.South);
                        Check(portrait != null, "portrait renders");
                        snapshot = Snapshot(pawn);
                        sentientThingId = pawn.thingIDNumber;
                        Log.Message("AUTOTEST info snapshot " + snapshot);
                        Next(Step.Save);
                    }
                    break;
                case Step.Save:
                    GameDataSaveLoader.SaveGame(SaveName);
                    gameBeforeLoad = Current.Game;
                    GameDataSaveLoader.LoadGame(SaveName);
                    Next(Step.WaitForLoad);
                    break;
                case Step.WaitForLoad:
                    if (stepFrames > 120 && Current.Game != gameBeforeLoad && Find.CurrentMap != null && !LongEventHandler.AnyEventNowOrWaiting)
                    {
                        Next(Step.VerifyAfterLoad);
                    }
                    break;
                case Step.VerifyAfterLoad:
                    if (stepFrames == 30)
                    {
                        Pawn pawn = FindSentient();
                        Check(pawn != null && pawn.thingIDNumber == sentientThingId, "same pawn (ThingID " + sentientThingId + ") after reload");
                        if (pawn != null)
                        {
                            string after = Snapshot(pawn);
                            Check(after == snapshot, "identity unchanged by save and load" + (after == snapshot ? "" : ": " + after));
                            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
                        }
                    }
                    if (stepFrames == 60)
                    {
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(outputFolder, "sentient_after_load.png"));
                    }
                    if (stepFrames > 100)
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    }
                    if (stepFrames > 400)
                    {
                        Check(FindSentient() != null && !FindSentient().Dead, "sentient pawn alive after reload and more ticks");
                        Finish();
                    }
                    break;
            }
        }

        private static void ConvertAndRollback()
        {
            Map map = Find.CurrentMap;
            IntVec3 near = map.mapPawns.FreeColonists.First().Position;

            Pawn Spawn(string kindName)
            {
                Pawn mech = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindName), Faction.OfPlayer);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(near, map, 6), map);
                return mech;
            }

            Pawn converted = Spawn("Mech_Militor");
            int id = converted.thingIDNumber;
            string originalDef = converted.def.defName;
            int hediffs = converted.health.hediffSet.hediffs.Count;
            ThingWithComps weapon = converted.equipment?.Primary;
            bool ok = SentientMechConverter.TryConvert(converted, showMessages: false);
            Check(ok, "TryConvert succeeded");
            Check(converted.thingIDNumber == id && converted.Spawned && !converted.Dead, "same pawn, still spawned");
            Check(converted.def.defName == SentientMechRegistry.DefPrefix + originalDef, "def is " + SentientMechRegistry.DefPrefix + originalDef + " (was " + converted.def.defName + ")");
            Check(SentientMechRegistry.IsSentient(converted) && SentientMechRegistry.IsSentientRace(converted.RaceProps), "registry reports sentient by def and by race props");
            Check(converted.health.hediffSet.hediffs.Count == hediffs, "hediffs kept");
            Check(converted.equipment?.Primary == weapon, "weapon kept");
            Check(converted.story != null && converted.skills != null && converted.workSettings != null && converted.Name != null, "humanlike identity trackers and a name");
            Check(converted.Faction == Faction.OfPlayer && converted.GetOverseer() == null, "player faction, no overseer");
            Check(converted.RaceProps.Humanlike && !SentientMechUtility.IsHumanlikeVisuals(converted.RaceProps.Humanlike, converted.RaceProps), "humanlike for trackers, not for visuals (infix result)");
            Check(!SentientMechUtility.IsOrdinaryMechanoid(converted.RaceProps.IsMechanoid, converted.RaceProps), "not an ordinary mechanoid");
            Check(WorkGiver_Scanner_Any(converted), "has an enabled work type");
            Check(map.mapPawns.FreeColonists.Contains(converted), "listed as a free colonist");
            Check(Find.ColonistBar.GetColonistsInOrder().Contains(converted), "on the colonist bar");

            Pawn rolledBack = Spawn("Mech_Lifter");
            int rbId = rolledBack.thingIDNumber;
            string rbDef = rolledBack.def.defName;
            int rbComps = rolledBack.AllComps.Count;
            int rbHediffs = rolledBack.health.hediffSet.hediffs.Count;
            IntVec3 rbCell = rolledBack.Position;
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool rbOk = SentientMechConverter.TryConvert(rolledBack, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(!rbOk, "forced failure reports not converted");
            Check(rolledBack.thingIDNumber == rbId && rolledBack.def.defName == rbDef && !SentientMechRegistry.IsSentient(rolledBack), "rolled back pawn keeps ThingID and original def");
            Check(rolledBack.Spawned && rolledBack.Position == rbCell, "rolled back pawn is respawned in place");
            Check(rolledBack.AllComps.Count == rbComps && rolledBack.health.hediffSet.hediffs.Count == rbHediffs, "comps and hediffs restored");
            Check(rolledBack.story == null || rolledBack.story.Childhood == null, "no identity left behind");

            Pawn spare = Spawn("Mech_Militor");
            int before = SentientCount(map);
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.forced = true;
            bool fired = DefDatabase<IncidentDef>.GetNamed("MS_MechanoidAwakening").Worker.TryExecute(parms);
            Check(fired && SentientCount(map) == before + 1, "awakening incident converted one more mech (" + before + " -> " + SentientCount(map) + ")");
            GC.KeepAlive(spare);
        }

        private static bool WorkGiver_Scanner_Any(Pawn pawn)
        {
            return DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(w => !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.WorkIsActive(w));
        }

        private static int SentientCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned.Count(SentientMechRegistry.IsSentient);
        }

        private static Pawn FindSentient()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return null;
            }
            IEnumerable<Pawn> sentient = map.mapPawns.AllPawnsSpawned.Where(SentientMechRegistry.IsSentient);
            return sentientThingId != 0 ? sentient.FirstOrDefault(p => p.thingIDNumber == sentientThingId) : sentient.FirstOrDefault();
        }

        private static string Snapshot(Pawn pawn)
        {
            return string.Join("|", new[]
            {
                pawn.ThingID,
                pawn.def.defName,
                pawn.kindDef.defName,
                pawn.Name?.ToStringFull,
                pawn.story.Childhood?.defName,
                pawn.story.Adulthood?.defName,
                string.Join(",", pawn.story.traits.allTraits.Select(t => t.def.defName + t.Degree)),
                string.Join(",", pawn.skills.skills.Select(s => s.def.defName + s.Level + s.passion)),
                pawn.Faction?.def.defName,
                pawn.health.hediffSet.hediffs.Count.ToString(),
                pawn.equipment?.Primary?.def.defName,
                pawn.AllComps.Count.ToString(),
                pawn.needs.energy != null ? "energy" : "noenergy",
            });
        }

        private static void Check(bool condition, string what)
        {
            if (!condition)
            {
                failures++;
            }
            Log.Message((condition ? "AUTOTEST PASS " : "AUTOTEST FAIL ") + what);
        }

        private static void Fail(string what)
        {
            failures++;
            Log.Message("AUTOTEST FAIL " + what);
        }

        private static void Next(Step next)
        {
            step = next;
            stepFrames = 0;
        }

        private static void Finish()
        {
            Log.Message("AUTOTEST DONE failures=" + failures);
            step = Step.Done;
            Root.Shutdown();
        }
    }
}
#endif
