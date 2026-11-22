#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    public class MechSentienceAutoTest : GameComponent
    {
        private enum Step { WaitForGame, ConvertAndRollback, Run, MechanoidUpgrades, GestatableApocriton, ResurrectEnemyMechanoids, Maidnoid, Rimpsyche, Screenshot, Save, WaitForLoad, VerifyAfterLoad, VerifyRockySecondLoad, Done }

        private const string SaveName = "mechsentienceautotest";
        private const int MaxFramesPerStep = 4000;
        private const int MaxFramesMaidnoidStep = 30000;

        private static bool argRead;
        private static string outputFolder;
        private static Step step = Step.WaitForGame;
        private static int stepFrames;
        private static int failures;
        private static int sentientThingId;
        private static string snapshot;
        private static Game gameBeforeLoad;
        private static int runStartTick;
        private static int downedMechId;
        private static int downedWeaponId;
        private static int catalystTargetId;
        private static bool catalystSettingBefore;
        private static bool catalystModePending;

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
            if (stepFrames > (step == Step.Maidnoid || step == Step.GestatableApocriton || step == Step.ResurrectEnemyMechanoids ? MaxFramesMaidnoidStep : MaxFramesPerStep))
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
                    Next(Step.Run);
                    try
                    {
                        ConvertAndRollback();
                        if (catalystTargetId != 0)
                        {
                            catalystSettingBefore = ModSettings.UseIdentityProfiles;
                            ModSettings.UseIdentityProfiles = true;
                            catalystModePending = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Fail("ConvertAndRollback threw " + e);
                    }
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    runStartTick = Find.TickManager.TicksGame;
                    break;
                case Step.Run:
                    if (Find.TickManager.TicksGame - runStartTick >= 1500)
                    {
                        if (catalystTargetId != 0)
                        {
                            Pawn target = Find.CurrentMap.mapPawns.AllPawnsSpawned
                                .FirstOrDefault(p => p.thingIDNumber == catalystTargetId);
                            SentientIdentityProfileExtension targetProfile = target != null && SentientMechRegistry.IsSentient(target)
                                ? SentientIdentityProfiles.Resolve(target.def) : null;
                            Check(targetProfile != null && targetProfile.childhoodBackstories.Contains(target.story.Childhood)
                                && targetProfile.adulthoodBackstories.Contains(target.story.Adulthood),
                                "catalyst job converted with the race profile");
                            ModSettings.UseIdentityProfiles = catalystSettingBefore;
                            catalystModePending = false;
                        }
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
                            Next(MechanoidUpgradesCompat.IsAvailable ? Step.MechanoidUpgrades : NextAfterMechanoidUpgrades());
                        }
                        else
                        {
                            Finish();
                        }
                    }
                    break;
                case Step.MechanoidUpgrades:
                    if (MechanoidUpgradesAutoTest.Tick())
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(FindSentient(), playSound: false, forceDesignatorDeselect: false);
                        Next(NextAfterMechanoidUpgrades());
                    }
                    break;
                case Step.GestatableApocriton:
                    if (GestatableApocritonAutoTest.Tick())
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(FindSentient(), playSound: false, forceDesignatorDeselect: false);
                        Next(NextAfterGestatableApocriton());
                    }
                    break;
                case Step.ResurrectEnemyMechanoids:
                    if (ResurrectEnemyMechanoidsAutoTest.Tick())
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(FindSentient(), playSound: false, forceDesignatorDeselect: false);
                        Next(NextAfterResurrectEnemyMechanoids());
                    }
                    break;
                case Step.Maidnoid:
                    if (MaidnoidAutoTest.Tick())
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(FindSentient(), playSound: false, forceDesignatorDeselect: false);
                        Next(NextAfterMaidnoid());
                    }
                    break;
                case Step.Rimpsyche:
                    if (RimpsycheAutoTest.TickUi(FindSentient(), outputFolder))
                    {
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(FindSentient(), playSound: false, forceDesignatorDeselect: false);
                        Next(Step.Screenshot);
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
                        RechargeAutoTest.MarkForSave(pawn);
                        if (RimpsycheAutoTest.IsActive)
                        {
                            RimpsycheAutoTest.MarkForSave(pawn);
                        }
                        if (IsekaiLevelingAutoTest.IsActive)
                        {
                            IsekaiLevelingAutoTest.MarkForSave();
                        }
                        snapshot = Snapshot(pawn);
                        sentientThingId = pawn.thingIDNumber;
                        Log.Message("AUTOTEST info snapshot " + snapshot);
                        Next(Step.Save);
                    }
                    break;
                case Step.Save:
                    RockyAutoTest.MarkLegacyAppearance(Find.CurrentMap);
                    if (GestatableApocritonCompat.IsAvailable)
                    {
                        GestatableApocritonAutoTest.WithLegacyStorageGone(() => GameDataSaveLoader.SaveGame(SaveName));
                    }
                    else
                    {
                        GameDataSaveLoader.SaveGame(SaveName);
                    }
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
                        RockyAutoTest.VerifyAfterLoad(Find.CurrentMap, firstLoad: true);
                        Pawn pawn = FindSentient();
                        Check(pawn != null && pawn.thingIDNumber == sentientThingId, "same pawn (ThingID " + sentientThingId + ") after reload");
                        WeaponlessAutoTest.VerifyAfterLoad(Find.CurrentMap);
                        Pawn downedMech = Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == downedMechId);
                        Check(downedMech != null && downedMech.Downed && downedMech.equipment?.Primary != null && downedMech.equipment.Primary.thingIDNumber == downedWeaponId, "downed lancer still has the same weapon after save and load (found=" + (downedMech != null) + ", downed=" + downedMech?.Downed + ", dead=" + downedMech?.Dead + ", weapon=" + (downedMech?.equipment?.Primary?.ToStringSafe() ?? "none") + ", expected id " + downedWeaponId + ")");
                        if (pawn != null)
                        {
                            string after = Snapshot(pawn);
                            Check(after == snapshot, "identity unchanged by save and load" + (after == snapshot ? "" : ": " + after));
                            RechargeAutoTest.CheckAfterLoad(pawn);
                            if (RimpsycheAutoTest.IsActive)
                            {
                                RimpsycheAutoTest.CheckAfterLoad(pawn);
                            }
                            if (WorkModesCompat.IsActive)
                            {
                                WorkModesAutoTest.CheckAfterLoad(pawn);
                            }
                            if (IsekaiLevelingAutoTest.IsActive)
                            {
                                IsekaiLevelingAutoTest.CheckAfterLoad(Find.CurrentMap);
                            }
                            if (GestatableApocritonCompat.IsAvailable)
                            {
                                GestatableApocritonAutoTest.CheckAfterLoad(Find.CurrentMap);
                                GestatableApocritonAutoTest.CheckLegacyAfterLoad(Find.CurrentMap);
                            }
                            if (ResurrectEnemyMechanoidsCompat.IsAvailable && !ResurrectEnemyMechanoidsCompat.GestatableApocritonActive)
                            {
                                ResurrectEnemyMechanoidsAutoTest.CheckAfterLoad(Find.CurrentMap);
                            }
                            if (MaidnoidAutoTest.IsActive)
                            {
                                MaidnoidAutoTest.CheckAfterLoad(Find.CurrentMap);
                            }
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
                        if (RockyAutoTest.PrepareSecondLoad(Find.CurrentMap))
                        {
                            GameDataSaveLoader.SaveGame("mechsentienceautotest_rocky_repaired");
                            gameBeforeLoad = Current.Game;
                            GameDataSaveLoader.LoadGame("mechsentienceautotest_rocky_repaired");
                            Next(Step.VerifyRockySecondLoad);
                        }
                        else
                        {
                            Finish();
                        }
                    }
                    break;
                case Step.VerifyRockySecondLoad:
                    if (Current.Game != gameBeforeLoad && Find.CurrentMap != null && !LongEventHandler.AnyEventNowOrWaiting)
                    {
                        RockyAutoTest.VerifyAfterLoad(Find.CurrentMap, firstLoad: false);
                        Finish();
                    }
                    break;
            }
        }

        private static Step NextAfterMechanoidUpgrades()
        {
            return GestatableApocritonCompat.IsAvailable ? Step.GestatableApocriton : NextAfterGestatableApocriton();
        }

        private static Step NextAfterGestatableApocriton()
        {
            return ResurrectEnemyMechanoidsCompat.IsAvailable && !ResurrectEnemyMechanoidsCompat.GestatableApocritonActive ? Step.ResurrectEnemyMechanoids : NextAfterResurrectEnemyMechanoids();
        }

        private static Step NextAfterResurrectEnemyMechanoids()
        {
            return MaidnoidAutoTest.IsActive ? Step.Maidnoid : NextAfterMaidnoid();
        }

        private static Step NextAfterMaidnoid()
        {
            return RimpsycheAutoTest.IsActive ? Step.Rimpsyche : Step.Screenshot;
        }

        private static void ConvertAndRollback()
        {
            bool previousProfileMode = ModSettings.UseIdentityProfiles;
            ModSettings.UseIdentityProfiles = false;
            try
            {
                ConvertAndRollbackCore();
            }
            finally
            {
                ModSettings.UseIdentityProfiles = previousProfileMode;
            }
        }

        private static void ConvertAndRollbackCore()
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
            ModSettings.UseIdentityProfiles = true;
            bool ok = SentientMechConverter.TryConvert(converted, showMessages: false);
            ModSettings.UseIdentityProfiles = false;
            Check(ok, "TryConvert succeeded");
            Check(converted.thingIDNumber == id && converted.Spawned && !converted.Dead, "same pawn, still spawned");
            Check(converted.def.defName == SentientMechRegistry.DefPrefix + originalDef, "def is " + SentientMechRegistry.DefPrefix + originalDef + " (was " + converted.def.defName + ")");
            Check(SentientMechRegistry.IsSentient(converted) && SentientMechRegistry.IsSentientRace(converted.RaceProps), "registry reports sentient by def and by race props");
            Check(converted.AllComps.OfType<CompSentientRecharge>().Count() == 1, "the live conversion created exactly one recharge comp");
            Check(converted.health.hediffSet.hediffs.Count == hediffs, "hediffs kept");
            Check(converted.equipment?.Primary == weapon, "weapon kept");
            Check(converted.story != null && converted.skills != null && converted.workSettings != null && converted.Name != null, "humanlike identity trackers and a name");
            Check(ok && SentientIdentityProfiles.Resolve(converted.def)?.childhoodBackstories.Contains(converted.story.Childhood) == true,
                "main save/reload pawn starts with a profile childhood");
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
            Check(rolledBack.TryGetComp<CompSentientRecharge>() == null, "the recharge comp created for the sentient def is gone after the rollback");
            Check(rolledBack.story == null || rolledBack.story.Childhood == null, "no identity left behind");

            Pawn spare = Spawn("Mech_Militor");
            int before = SentientCount(map);
            HashSet<int> beforeIds = map.mapPawns.AllPawnsSpawned.Where(SentientMechRegistry.IsSentient)
                .Select(p => p.thingIDNumber).ToHashSet();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.forced = true;
            ModSettings.UseIdentityProfiles = true;
            bool fired = DefDatabase<IncidentDef>.GetNamed("MS_MechanoidAwakening").Worker.TryExecute(parms);
            ModSettings.UseIdentityProfiles = false;
            Check(fired && SentientCount(map) == before + 1, "awakening incident converted one more mech (" + before + " -> " + SentientCount(map) + ")");
            Pawn awakened = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => SentientMechRegistry.IsSentient(p) && !beforeIds.Contains(p.thingIDNumber));
            SentientIdentityProfileExtension awakenedProfile = awakened == null ? null : SentientIdentityProfiles.Resolve(awakened.def);
            Check(awakenedProfile != null && awakenedProfile.childhoodBackstories.Contains(awakened.story.Childhood)
                && awakenedProfile.adulthoodBackstories.Contains(awakened.story.Adulthood),
                "awakening incident used the race profile");
            GC.KeepAlive(spare);

            Log.Message("AUTOTEST info WVC - Work Modes active=" + WorkModesCompat.IsActive);
            if (WorkModesCompat.IsActive)
            {
                WorkModesAutoTest.Run(Spawn);
            }
            Log.Message("AUTOTEST info [KCB] Mini Expeditions active=" + (HarmonyLib.AccessTools.TypeByName(CompListComparer.ExpeditionPropsTypeName) != null));
            ExpeditionAutoTest.RunPure();
            Log.Message("AUTOTEST info Hacking Expansion active=" + HackingExpansionCompat.IsActive + ", Glittertech Expansion active=" + GlittertechExpansionCompat.IsActive);
            if (HackingExpansionCompat.IsActive)
            {
                HackingExpansionAutoTest.RunPure();
            }
            if (GlittertechExpansionCompat.IsActive)
            {
                GlittertechExpansionAutoTest.RunPure();
            }
            Log.Message("AUTOTEST info Isekai Leveling active=" + IsekaiLevelingAutoTest.IsActive);
            if (IsekaiLevelingAutoTest.IsActive)
            {
                IsekaiLevelingAutoTest.RunStartup();
                IsekaiLevelingAutoTest.Run(Spawn);
            }
            RechargeAutoTest.Run(Spawn);
            CheckSpecialtySkills(Spawn);
            CheckWeaponLock(Spawn);
            WeaponlessAutoTest.Run(Spawn);
            CheckSentientCorpseFilters();
            CheckRealSentientCorpses(Spawn);
            CheckMetalhorrorExclusion(Spawn);
            CheckDirectGeneration();
            DmsLadyAutoTest.Run(Spawn);
            RockyAutoTest.Run(Spawn);
            CheckUnknownBackstories(Spawn);
            CheckIdentityProfiles(Spawn);

            Log.Message("AUTOTEST info Rimpsyche active=" + RimpsycheAutoTest.IsActive);
            if (RimpsycheAutoTest.IsActive)
            {
                RimpsycheAutoTest.RunStartup();
                RimpsycheAutoTest.CheckRollback(Spawn);
                RimpsycheAutoTest.CheckStartingAndGenerated();
                RimpsycheAutoTest.CheckConversations(Spawn);
                RimpsycheAutoTest.CheckAllSentientOnMap(map);
            }

            Pawn worker = map.mapPawns.FreeColonists
                .FirstOrDefault(p => p.RaceProps.Humanlike && !SentientMechRegistry.IsSentient(p));
            Check(worker != null, "human worker is available for the catalyst job");
            if (worker != null)
            {
                Pawn catalystMech = Spawn("Mech_Constructoid");
                Thing catalyst = ThingMaker.MakeThing(MS_ThingDefOf.MS_MechSentienceCatalyst);
                GenSpawn.Spawn(catalyst, CellFinder.RandomClosewalkCellNear(worker.Position, map, 3), map);
                Job job = JobMaker.MakeJob(MS_JobDefOf.MS_ApplyMechSentienceCatalyst, catalystMech, catalyst);
                job.count = 1;
                job.playerForced = true;
                bool started = worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                Check(started, "catalyst job started");
                if (started) catalystTargetId = catalystMech.thingIDNumber;
            }
        }

        private static void CheckMetalhorrorExclusion(Func<string, Pawn> spawn)
        {
            if (!ModsConfig.AnomalyActive)
            {
                Log.Message("AUTOTEST info Anomaly inactive, metalhorror checks skipped");
                return;
            }
            Pawn carrier = spawn("Mech_Militor");
            carrier.health.AddHediff(HediffDefOf.MetalhorrorImplant);
            Check(carrier.health.hediffSet.HasHediff(HediffDefOf.MetalhorrorImplant), "an ordinary mech can carry an implant");
            Check(SentientMechConverter.TryConvert(carrier, showMessages: false), "implant carrier converted");
            Check(!carrier.health.hediffSet.HasHediff(HediffDefOf.MetalhorrorImplant), "conversion removed the metalhorror implant");

            Pawn ordinary = spawn("Mech_Lifter");
            Pawn sentient = carrier;
            Pawn human = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            Check(!MetalhorrorUtility.CanBeInfected(sentient) && !MetalhorrorUtility.CanBeInfected(ordinary), "neither mech can be infected");
            Check(MetalhorrorUtility.CanBeInfected(human), "an eligible human can still be infected");

            MetalhorrorUtility.Infect(sentient);
            MetalhorrorUtility.Infect(sentient);
            Check(!sentient.health.hediffSet.HasHediff(HediffDefOf.MetalhorrorImplant), "Infect leaves a sentient mech without an implant");

            int hediffs = sentient.health.hediffSet.hediffs.Count;
            sentient.health.hediffSet.AddDirect(HediffMaker.MakeHediff(HediffDefOf.MetalhorrorImplant, sentient));
            Check(!sentient.health.hediffSet.HasHediff(HediffDefOf.MetalhorrorImplant) && sentient.health.hediffSet.hediffs.Count == hediffs, "AddDirect refuses the implant on a sentient mech");
            sentient.health.AddHediff(HediffDefOf.Anesthetic);
            Check(sentient.health.hediffSet.HasHediff(HediffDefOf.Anesthetic), "an unrelated hediff still adds to a sentient mech");

            MetalhorrorUtility.Infect(human);
            Check(human.health.hediffSet.HasHediff(HediffDefOf.MetalhorrorImplant), "a human is still infected normally");
            human.health.RemoveHediff(human.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.MetalhorrorImplant));
            Find.WorldPawns.PassToWorld(human, RimWorld.Planet.PawnDiscardDecideMode.Discard);
        }

        private static void CheckDirectGeneration()
        {
            Log.Message("AUTOTEST info Softer Hair Colors active=" + (ModLister.GetActiveModWithIdentifier("sinner.softerhaircolors", ignorePostfix: true) != null));
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Militor");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                Fail("no sentient kind for Mech_Militor");
                return;
            }
            Pawn generated = null;
            try
            {
                generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            }
            catch (Exception e)
            {
                Fail("generating a sentient pawn kind threw " + e);
            }
            Check(generated != null && SentientMechRegistry.IsSentient(generated) && generated.genes == null, "directly generated sentient pawn is returned and stays gene-free");
            Check(generated?.story?.Childhood != null && generated.story.Adulthood != null, "directly generated sentient pawn has both backstories");
            Check(generated != null && generated.AllComps.OfType<CompSentientRecharge>().Count() == 1, "directly generated sentient pawn has exactly one recharge comp");
            if (generated != null)
            {
                Find.WorldPawns.PassToWorld(generated, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }

            PawnKindDef lancerOriginal = DefDatabase<PawnKindDef>.GetNamed("Mech_Lancer");
            if (!SentientMechRegistry.TryGetSentient(lancerOriginal.race, lancerOriginal, out _, out PawnKindDef lancerKind))
            {
                Fail("no sentient kind for Mech_Lancer");
            }
            else
            {
                bool previous = ModSettings.UseIdentityProfiles;
                try
                {
                    foreach (bool profileMode in new[] { false, true })
                    {
                        ModSettings.UseIdentityProfiles = profileMode;
                        Faction faction = FactionUtility.DefaultFactionFrom(lancerKind.defaultFactionDef);
                        Pawn lancer = PawnGenerator.GeneratePawn(lancerKind, faction, Find.CurrentMap.Tile);
                        try
                        {
                            SkillRecord shooting = lancer.skills.GetSkill(DefDatabase<SkillDef>.GetNamed("Shooting"));
                            SentientIdentityProfileExtension profile = SentientIdentityProfiles.Resolve(lancer.def);
                            bool valid = profileMode
                                ? shooting.levelInt >= 12 && shooting.levelInt <= 15
                                    && (shooting.passion == Passion.Minor || shooting.passion == Passion.Major)
                                    && profile.childhoodBackstories.Contains(lancer.story.Childhood)
                                    && profile.adulthoodBackstories.Contains(lancer.story.Adulthood)
                                : shooting.levelInt == 10 && shooting.passion == Passion.Major;
                            Check(valid, "directly spawned sentient lancer has expected Shooting and stories (profiles "
                                + (profileMode ? "on" : "off") + ")");
                        }
                        finally
                        {
                            Find.WorldPawns.PassToWorld(lancer, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                        }
                    }
                }
                finally
                {
                    ModSettings.UseIdentityProfiles = previous;
                }
            }

            Pawn human = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            Check(human.genes != null, "ordinary human still gets a gene tracker");
            Find.WorldPawns.PassToWorld(human, RimWorld.Planet.PawnDiscardDecideMode.Discard);
        }

        private static void CheckUnknownBackstories(Func<string, Pawn> spawn)
        {
            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("MS_Childhood_Unknown");
            BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("MS_Adulthood_Unknown");
            Check(childhood != null && adulthood != null, "both unknown backstories loaded");
            if (childhood == null || adulthood == null)
            {
                return;
            }
            Check(childhood.slot == BackstorySlot.Childhood && adulthood.slot == BackstorySlot.Adulthood, "unknown backstories are in their own slots");
            foreach (BackstoryDef backstory in new[] { childhood, adulthood })
            {
                Check(backstory.spawnCategories.Contains(SentientDefGenerator.BackstoryCategory) && backstory.requiresSpawnCategory, backstory.defName + " is sentient-only");
                Check(backstory.skillGains.NullOrEmpty() && backstory.forcedTraits.NullOrEmpty() && backstory.disallowedTraits.NullOrEmpty() && backstory.workDisables == WorkTags.None && backstory.requiredWorkTags == WorkTags.None, backstory.defName + " grants no skills, traits or work restrictions");
                Check(backstory.ConfigErrors().Count() == 0, backstory.defName + " has no config errors");
            }

            Pawn mech = spawn("Mech_Militor");
            if (!SentientMechConverter.TryConvert(mech, showMessages: false))
            {
                Fail("unknown backstory test mech did not convert");
                return;
            }
            mech.story.Childhood = childhood;
            mech.story.Adulthood = adulthood;
            SentientMechGenerator.RegenerateSkills(mech);
            Check(mech.story.Childhood == childhood && mech.story.Adulthood == adulthood && mech.skills.skills.All(s => s.Level >= 0), "skills roll normally with both unknown backstories forced");

            bool sawChildhood = false;
            bool sawAdulthood = false;
            for (int i = 0; i < 400 && !(sawChildhood && sawAdulthood); i++)
            {
                Pawn rolled = spawn("Mech_Militor");
                SentientMechConverter.TryConvert(rolled, showMessages: false);
                sawChildhood |= rolled.story?.Childhood == childhood;
                sawAdulthood |= rolled.story?.Adulthood == adulthood;
            }
            Check(sawChildhood && sawAdulthood, "random rolls can pick both unknown backstories");
        }

        private static void CheckSentientCorpseFilters()
        {
            RecipeDef butcher = DefDatabase<RecipeDef>.GetNamed("ButcherCorpseFlesh");
            RecipeDef shred = DefDatabase<RecipeDef>.GetNamed("ButcherCorpseMechanoid");
            RecipeDef smash = DefDatabase<RecipeDef>.GetNamed("SmashCorpseMechanoid");
            RecipeDef sentientShred = DefDatabase<RecipeDef>.GetNamed(SentientCorpseCategories.ShredRecipeName);
            RecipeDef sentientSmash = DefDatabase<RecipeDef>.GetNamed(SentientCorpseCategories.SmashRecipeName);
            RecipeDef cremate = DefDatabase<RecipeDef>.GetNamed("CremateCorpse");
            ThingFilter graveFilter = DefDatabase<ThingDef>.GetNamed("Grave").building.fixedStorageSettings?.filter;
            ThingCategoryDef sentientCategory = DefDatabase<ThingCategoryDef>.GetNamed(SentientCorpseCategories.CategoryName);
            ThingCategoryDef humanlikeCategory = DefDatabase<ThingCategoryDef>.GetNamed("CorpsesHumanlike");
            ThingDef vanillaCorpse = SentientMechRegistry.OriginalOf(SentientMechRegistry.SentientRaces[0]).race.corpseDef;

            Check(!butcher.fixedIngredientFilter.Allows(vanillaCorpse), "vanilla mech corpse is not allowed by butcher creature");
            Check(shred.fixedIngredientFilter.Allows(vanillaCorpse) && shred.defaultIngredientFilter.Allows(vanillaCorpse), "vanilla mech corpse: shred allows it, selected by default");
            Check(smash.fixedIngredientFilter.Allows(vanillaCorpse) && smash.defaultIngredientFilter.Allows(vanillaCorpse), "vanilla mech corpse: smash allows it, selected by default");
            Check(!sentientShred.fixedIngredientFilter.Allows(vanillaCorpse) && !sentientSmash.fixedIngredientFilter.Allows(vanillaCorpse), "vanilla mech corpse is not allowed by the sentient recipes");
            Check(DefDatabase<ThingDef>.GetNamed("TableMachining").AllRecipes.Contains(sentientShred), "sentient shred is on the machining table");
            Check(DefDatabase<ThingDef>.GetNamed("CraftingSpot").AllRecipes.Contains(sentientSmash), "sentient smash is on the crafting spot");

            int checkedCorpses = 0;
            foreach (ThingDef race in SentientMechRegistry.SentientRaces)
            {
                ThingDef corpse = race.race.corpseDef;
                string name = corpse.defName;
                checkedCorpses++;
                Check(corpse.IsWithinCategory(sentientCategory) && corpse.IsWithinCategory(humanlikeCategory), name + " is in the sentient category and still within humanlike corpses");
                Check(!butcher.fixedIngredientFilter.Allows(corpse), name + " is not allowed by butcher creature");
                Check(!shred.fixedIngredientFilter.Allows(corpse) && !smash.fixedIngredientFilter.Allows(corpse), name + " is not allowed by the vanilla shred and smash recipes");
                Check(sentientShred.fixedIngredientFilter.Allows(corpse) && sentientShred.defaultIngredientFilter.Allows(corpse), name + " is allowed and selected by default in " + sentientShred.defName);
                Check(sentientSmash.fixedIngredientFilter.Allows(corpse) && sentientSmash.defaultIngredientFilter.Allows(corpse), name + " is allowed and selected by default in " + sentientSmash.defName);
                Check(cremate.fixedIngredientFilter.Allows(corpse), name + " is still allowed by cremate corpse");
                Check(graveFilter == null || graveFilter.Allows(corpse), name + " is still allowed in a grave");
                Check(race.race.meatDef == ThingDefOf.Steel, name + " still yields steel when shredded");

                Bill_Production butcherBill = (Bill_Production)butcher.MakeNewBill();
                butcherBill.ingredientFilter.SetAllow(corpse, true);
                Check(!butcherBill.IsFixedOrAllowedIngredient(corpse), name + " is rejected by a butcher creature bill even when ticked");
            }
            Check(checkedCorpses > 0, "checked " + checkedCorpses + " sentient corpse defs");
        }

        private static void CheckRealSentientCorpses(Func<string, Pawn> spawn)
        {
            RecipeDef shred = DefDatabase<RecipeDef>.GetNamed("ButcherCorpseMechanoid");
            RecipeDef smash = DefDatabase<RecipeDef>.GetNamed("SmashCorpseMechanoid");
            RecipeDef butcher = DefDatabase<RecipeDef>.GetNamed("ButcherCorpseFlesh");
            RecipeDef cremate = DefDatabase<RecipeDef>.GetNamed("CremateCorpse");
            RecipeDef[] sentientRecipes =
            {
                DefDatabase<RecipeDef>.GetNamed(SentientCorpseCategories.ShredRecipeName),
                DefDatabase<RecipeDef>.GetNamed(SentientCorpseCategories.SmashRecipeName),
            };
            foreach (bool hostile in new[] { false, true })
            {
                string who = hostile ? "stranger" : "colonist";
                Pawn mech = spawn("Mech_Militor");
                if (!SentientMechConverter.TryConvert(mech, showMessages: false))
                {
                    Fail(who + " corpse test mech did not convert");
                    continue;
                }
                if (hostile)
                {
                    mech.SetFaction(Faction.OfMechanoids);
                }
                mech.Kill(null);
                Corpse corpse = mech.Corpse;
                if (corpse == null)
                {
                    Fail(who + " corpse test: no corpse");
                    continue;
                }
                Check(corpse.InnerPawn == mech && (corpse.InnerPawn.Faction == Faction.OfPlayer) != hostile, who + " sentient corpse has the expected faction");
                foreach (RecipeDef recipe in sentientRecipes)
                {
                    Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                    Check(recipe.fixedIngredientFilter.Allows(corpse), who + " corpse passes the fixed filter of " + recipe.defName);
                    Check(bill.IsFixedOrAllowedIngredient(corpse), who + " corpse is accepted by a new " + recipe.defName + " bill");
                    Check(bill.recipe.ingredients.Any(i => i.filter.Allows(corpse)), who + " corpse matches the recipe ingredient of " + recipe.defName);
                }
                foreach (RecipeDef recipe in new[] { shred, smash })
                {
                    Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                    bill.ingredientFilter.SetAllow(corpse.def, true);
                    Check(!recipe.fixedIngredientFilter.Allows(corpse) && !bill.IsFixedOrAllowedIngredient(corpse), who + " corpse is rejected by the vanilla " + recipe.defName);
                }
                Bill_Production butcherBill = (Bill_Production)butcher.MakeNewBill();
                butcherBill.ingredientFilter.SetAllow(corpse.def, true);
                Check(!butcherBill.IsFixedOrAllowedIngredient(corpse), who + " corpse is rejected by butcher creature");
                Check(cremate.fixedIngredientFilter.Allows(corpse), who + " corpse still passes the cremate fixed filter");
            }
        }

        private static void CheckIdentityProfiles(Func<string, Pawn> spawn)
        {
            bool previous = ModSettings.UseIdentityProfiles;
            ModSettings.UseIdentityProfiles = true;
            try
            {
                int checkedRaces = 0;
                bool sawNone = false;
                bool sawMinor = false;
                bool sawMajor = false;
                foreach (ThingDef sentient in SentientMechRegistry.SentientRaces)
                {
                    ThingDef original = SentientMechRegistry.OriginalRaceOf(sentient);
                    if (!SentientIdentityProfiles.IsShippedRace(original)) continue;
                    checkedRaces++;
                    SentientIdentityProfileExtension profile = SentientIdentityProfiles.Resolve(sentient);
                    Check(profile != null, original.defName + " has a valid identity profile");
                    if (profile == null) continue;
                    Check(profile.childhoodBackstories.Count == 2 && profile.adulthoodBackstories.Count == 2,
                        original.defName + " has two choices in each story slot");
                    PawnKindDef kind = DefDatabase<PawnKindDef>.AllDefsListForReading
                        .FirstOrDefault(k => k.race == original && k.defName == original.defName)
                        ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == original);
                    if (kind == null)
                    {
                        Fail(original.defName + " has no pawn kind for profile conversion");
                        continue;
                    }
                    Pawn pawn = spawn(kind.defName);
                    if (!SentientMechConverter.TryConvert(pawn, showMessages: false))
                    {
                        Fail(original.defName + " did not convert in profile mode");
                        continue;
                    }
                    bool storiesOk = profile.childhoodBackstories.Contains(pawn.story.Childhood)
                        && profile.adulthoodBackstories.Contains(pawn.story.Adulthood);
                    bool levelsOk = true;
                    bool passionsOk = true;
                    bool traitsOk = true;
                    BackstoryDef childhood = pawn.story.Childhood;
                    BackstoryDef adulthood = pawn.story.Adulthood;
                    List<Trait> traits = pawn.story.traits.allTraits.ToList();
                    for (int roll = 0; roll < 16; roll++)
                    {
                        int seed = 12000 + checkedRaces * 100 + roll;
                        Rand.PushState(seed);
                        SentientMechGenerator.GenerateBaselineSkillsForTesting(pawn);
                        Dictionary<SkillDef, Passion> baseline = pawn.skills.skills.ToDictionary(s => s.def, s => s.passion);
                        Rand.PopState();
                        Rand.PushState(seed);
                        SentientMechGenerator.RegenerateSkills(pawn);
                        Rand.PopState();
                        storiesOk &= pawn.story.Childhood == childhood && pawn.story.Adulthood == adulthood;
                        traitsOk &= pawn.story.traits.allTraits.SequenceEqual(traits);
                        foreach (SkillRecord skill in pawn.skills.skills)
                        {
                            PreferredSkill preferred = profile.preferredSkills.FirstOrDefault(p => p.skill == skill.def);
                            int minLevel = preferred?.minLevel ?? profile.defaultSkillMinLevel;
                            int maxLevel = preferred?.maxLevel ?? profile.defaultSkillMaxLevel;
                            levelsOk &= skill.Level >= minLevel && skill.Level <= maxLevel;
                            if (preferred == null)
                            {
                                passionsOk &= skill.passion == baseline[skill.def];
                                continue;
                            }
                            bool required = traits.Any(t => t.def.RequiresPassion(skill.def));
                            bool forbidden = skill.TotallyDisabled || traits.Any(t => t.def.ConflictsWithPassion(skill.def));
                            if (forbidden) passionsOk &= skill.passion == Passion.None;
                            else
                            {
                                int minPassion = required ? Math.Max(1, preferred.minPassion) : preferred.minPassion;
                                passionsOk &= (int)skill.passion >= minPassion && (int)skill.passion <= preferred.maxPassion;
                                if (!required && preferred.minPassion == 0 && preferred.maxPassion == 1)
                                {
                                    sawNone |= skill.passion == Passion.None;
                                    sawMinor |= skill.passion == Passion.Minor;
                                }
                                if (preferred.minPassion == 1 && preferred.maxPassion == 2)
                                {
                                    sawMinor |= skill.passion == Passion.Minor;
                                    sawMajor |= skill.passion == Passion.Major;
                                }
                                if (preferred.minPassion == 2 && preferred.maxPassion == 2)
                                    passionsOk &= skill.passion == Passion.Major;
                            }
                        }
                    }
                    foreach (Trait trait in traits)
                    {
                        traitsOk &= trait.GetDisabledWorkTypes().Count == 0 && trait.def.disabledWorkTags == WorkTags.None;
                        traitsOk &= !(trait.def.defName == "PsychicSensitivity" && trait.Degree <= -2);
                        traitsOk &= !profile.preferredSkills.Any(p => p.maxPassion > 0 && trait.def.ConflictsWithPassion(p.skill));
                        traitsOk &= !new[] { "Nudist", "DrugDesire", "Gourmand", "Cannibal", "CreepyBreathing",
                            "Immunity", "Beauty", "BodyPurist", "Gay", "Bisexual", "Asexual", "QuickSleeper" }
                            .Contains(trait.def.defName);
                    }
                    Check(storiesOk && traitsOk, original.defName + " profile stories and traits remain valid across rerolls");
                    Check(levelsOk && passionsOk, original.defName + " profile level and passion bounds; other passions match the baseline");
                }
                Check(checkedRaces == (ModsConfig.OdysseyActive ? 24 : 23),
                    "all loaded vanilla races have profile coverage (" + checkedRaces + ")");
                Check(sawNone && sawMinor && sawMajor, "inclusive passion ranges produced none, minor and major");

                SentientIdentityProfileExtension constructoidProfile = SentientIdentityProfiles.Resolve(
                    SentientMechRegistry.SentientOf(DefDatabase<ThingDef>.GetNamed("Mech_Constructoid")));
                bool[] childhoodSeen = new bool[2];
                bool[] adulthoodSeen = new bool[2];
                for (int i = 0; i < 24 && !(childhoodSeen.All(x => x) && adulthoodSeen.All(x => x)); i++)
                {
                    Pawn pawn = spawn("Mech_Constructoid");
                    if (!SentientMechConverter.TryConvert(pawn, showMessages: false))
                    {
                        Fail("constructoid story choice conversion failed");
                        break;
                    }
                    for (int choice = 0; choice < 2; choice++)
                    {
                        childhoodSeen[choice] |= pawn.story.Childhood == constructoidProfile.childhoodBackstories[choice];
                        adulthoodSeen[choice] |= pawn.story.Adulthood == constructoidProfile.adulthoodBackstories[choice];
                    }
                }
                Check(childhoodSeen.All(x => x) && adulthoodSeen.All(x => x),
                    "both constructoid profile choices appear in each story slot");

                PawnKindDef originalKind = DefDatabase<PawnKindDef>.GetNamed("Mech_Constructoid");
                Check(SentientMechRegistry.TryGetSentient(originalKind.race, originalKind, out ThingDef sentientRace,
                    out PawnKindDef sentientKind), "constructoid starting kind resolves");
                Pawn starter = null;
                try
                {
                    starter = PawnGenerator.GeneratePawn(new PawnGenerationRequest(originalKind, Faction.OfPlayer,
                        PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                        developmentalStages: DevelopmentalStage.Adult));
                    SentientMechConverter.PrepareUnspawned(starter, sentientRace, sentientKind);
                    Check(!starter.Spawned && constructoidProfile.childhoodBackstories.Contains(starter.story.Childhood)
                        && constructoidProfile.adulthoodBackstories.Contains(starter.story.Adulthood),
                        "unspawned starting mech used the race profile");
                }
                catch (Exception ex)
                {
                    Fail("unspawned starting profile conversion threw " + ex);
                }
                finally
                {
                    if (starter != null)
                        Find.WorldPawns.PassToWorld(starter, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                }
            }
            finally
            {
                ModSettings.UseIdentityProfiles = previous;
            }
        }

        private static void CheckSpecialtySkills(Func<string, Pawn> spawn)
        {
            var cases = new (string kind, string[] skills)[]
            {
                ("Mech_Tunneler", new[] { "Mining" }),
                ("Mech_Constructoid", new[] { "Construction" }),
                ("Mech_Agrihand", new[] { "Plants" }),
                ("Mech_Fabricor", new[] { "Crafting", "Cooking", "Intellectual" }),
                ("Mech_Paramedic", new[] { "Medicine" }),
                ("Mech_Lancer", new[] { "Shooting" }),
                ("Mech_Diabolus", new[] { "Shooting" }),
                ("Mech_Warqueen", new[] { "Shooting" }),
                ("Mech_Scyther", new[] { "Melee" }),
            };
            foreach (var (kind, skills) in cases)
            {
                Pawn mech = spawn(kind);
                if (!SentientMechConverter.TryConvert(mech, showMessages: false))
                {
                    Fail(kind + " did not convert");
                    continue;
                }
                bool allOk = true;
                int differentOthers = 0;
                string previous = null;
                for (int roll = 0; roll < 20; roll++)
                {
                    SentientMechGenerator.RegenerateSkills(mech);
                    foreach (string skillName in skills)
                    {
                        SkillRecord record = mech.skills.GetSkill(DefDatabase<SkillDef>.GetNamed(skillName));
                        allOk &= record.Level == 10 && record.passion != Passion.None;
                    }
                    allOk &= mech.skills.GetSkill(DefDatabase<SkillDef>.GetNamed(skills[0])).passion == Passion.Major;
                    string others = string.Join(",", mech.skills.skills.Where(s => !skills.Contains(s.def.defName)).Select(s => s.Level + "" + s.passion));
                    if (others != previous)
                    {
                        differentOthers++;
                    }
                    previous = others;
                }
                Check(allOk, kind + ": " + string.Join("+", skills) + " is 10 with a passion on 20 rerolls (primary Major)");
                Check(differentOthers > 1, kind + ": the other skills still vary between rerolls");
            }
        }

        private static void CheckWeaponLock(Func<string, Pawn> spawn)
        {
            ThingDef pistolDef = DefDatabase<ThingDef>.GetNamed("Gun_Autopistol");

            Pawn diabolus = spawn("Mech_Diabolus");
            ThingWithComps gun = diabolus.equipment?.Primary;
            Check(gun != null && gun.def.destroyOnDrop, "diabolus carries a destroyOnDrop weapon (" + gun?.def.defName + ")");
            Check(SentientMechConverter.TryConvert(diabolus, showMessages: false), "diabolus converted");
            Check(!diabolus.kindDef.destroyGearOnDrop, "sentient kind no longer destroys gear on drop");
            Check(SentientMechUtility.IsLockedWeapon(diabolus, gun), "its weapon is locked");

            Check(!diabolus.equipment.TryDropEquipment(gun, out ThingWithComps dropped, diabolus.Position) && dropped == null && diabolus.equipment.Primary == gun, "TryDropEquipment is refused");
            ThingWithComps pistol = (ThingWithComps)ThingMaker.MakeThing(pistolDef);
            GenSpawn.Spawn(pistol, diabolus.Position, diabolus.Map);
            Check(!EquipmentUtility.CanEquip(pistol, diabolus, out string reason) && !string.IsNullOrEmpty(reason), "CanEquip refuses another weapon: " + reason);
            diabolus.equipment.MakeRoomFor(pistol);
            Check(diabolus.equipment.Primary == gun, "MakeRoomFor keeps the weapon");
            pistol.DeSpawn();
            diabolus.equipment.AddEquipment(pistol);
            Check(diabolus.equipment.Primary == gun && pistol.Spawned, "AddEquipment puts the other weapon back on the ground");
            Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, pistol);
            diabolus.jobs.TryTakeOrderedJob(equipJob, JobTag.Misc);
            Check(diabolus.CurJob?.def != JobDefOf.Equip, "an Equip job is not started");

            Pawn lancer = spawn("Mech_Lancer");
            ThingWithComps lancerGun = lancer.equipment?.Primary;
            CompMechRepairable repairable = lancer.GetComp<CompMechRepairable>();
            if (repairable != null)
            {
                repairable.autoRepair = false;
            }
            Check(SentientMechConverter.TryConvert(lancer, showMessages: false), "lancer converted");
            Check(lancerGun != null && SentientMechUtility.IsLockedWeapon(lancer, lancerGun), "lancer's weapon is locked (" + lancerGun?.def.defName + ", destroyOnDrop=" + lancerGun?.def.destroyOnDrop + ")");
            DownByRemovingLegs(lancer);
            Check(lancer.Downed && !lancer.Dead, "lancer is downed");
            Check(lancer.equipment.Primary == lancerGun && !lancerGun.Destroyed, "downed lancer keeps its weapon (same thing)");
            downedMechId = lancer.thingIDNumber;
            downedWeaponId = lancerGun.thingIDNumber;

            Pawn lifter = spawn("Mech_Lifter");
            Check(SentientMechConverter.TryConvert(lifter, showMessages: false), "lifter converted");
            ThingWithComps rifle = (ThingWithComps)ThingMaker.MakeThing(pistolDef);
            lifter.equipment.AddEquipment(rifle);
            Check(lifter.equipment.Primary == rifle && !SentientMechUtility.IsLockedWeapon(lifter, rifle), "lifter holds a normal weapon that is not locked");
            DownByRemovingLegs(lifter);
            Check(lifter.Downed && lifter.equipment.Primary == null && !rifle.Destroyed && rifle.Spawned, "downed lifter drops its normal weapon without destroying it");
        }

        private static void DownByRemovingLegs(Pawn pawn)
        {
            foreach (BodyPartRecord part in pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.MovingLimbCore).ToList())
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            }
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

        internal static void Check(bool condition, string what)
        {
            if (!condition)
            {
                failures++;
            }
            Log.Message((condition ? "AUTOTEST PASS " : "AUTOTEST FAIL ") + what);
        }

        internal static void Fail(string what)
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
            if (catalystModePending)
            {
                ModSettings.UseIdentityProfiles = catalystSettingBefore;
                catalystModePending = false;
            }
            Log.Message("AUTOTEST DONE failures=" + failures);
            step = Step.Done;
            Root.Shutdown();
        }
    }
}
#endif
