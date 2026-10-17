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
        private enum Step { WaitForGame, ConvertAndRollback, Run, MechanoidUpgrades, Screenshot, Save, WaitForLoad, VerifyAfterLoad, Done }

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
        private static int downedMechId;
        private static int downedWeaponId;

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
                            Next(MechanoidUpgradesCompat.IsAvailable ? Step.MechanoidUpgrades : Step.Screenshot);
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
                        Pawn downedMech = Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == downedMechId);
                        Check(downedMech != null && downedMech.Downed && downedMech.equipment?.Primary != null && downedMech.equipment.Primary.thingIDNumber == downedWeaponId, "downed lancer still has the same weapon after save and load (found=" + (downedMech != null) + ", downed=" + downedMech?.Downed + ", dead=" + downedMech?.Dead + ", weapon=" + (downedMech?.equipment?.Primary?.ToStringSafe() ?? "none") + ", expected id " + downedWeaponId + ")");
                        if (pawn != null)
                        {
                            string after = Snapshot(pawn);
                            Check(after == snapshot, "identity unchanged by save and load" + (after == snapshot ? "" : ": " + after));
                            if (WorkModesCompat.IsActive)
                            {
                                WorkModesAutoTest.CheckAfterLoad(pawn);
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

            Log.Message("AUTOTEST info WVC - Work Modes active=" + WorkModesCompat.IsActive);
            if (WorkModesCompat.IsActive)
            {
                WorkModesAutoTest.Run(Spawn);
            }
            CheckSpecialtySkills(Spawn);
            CheckWeaponLock(Spawn);
            CheckSentientCorpseFilters();
            CheckRealSentientCorpses(Spawn);
            CheckDirectGeneration();
            CheckUnknownBackstories(Spawn);
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
            if (generated != null)
            {
                Find.WorldPawns.PassToWorld(generated, RimWorld.Planet.PawnDiscardDecideMode.Discard);
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
            Log.Message("AUTOTEST DONE failures=" + failures);
            step = Step.Done;
            Root.Shutdown();
        }
    }
}
#endif
