#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class IsekaiLevelingAutoTest
    {
        private const int Level = 37;
        private const int Xp = 1234;
        private static readonly int[] StatValues = { 20, 9, 15, 11, 7, 6, 4 };
        private static readonly string[] StatNames = { "strength", "dexterity", "vitality", "intelligence", "wisdom", "charisma", "availableStatPoints" };
        private const string RankTraitPrefix = "Isekai_Rank_";

        private static int savedPawnId;
        private static int savedOrdinaryId;
        private static int savedOrdinaryLevel;
        private static string savedProgression;

        public static bool IsActive => IsekaiLevelingCompat.IsActive;

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Isekai: " + what);
        }

        private static object Get(object target, string field)
        {
            return AccessTools.Field(target.GetType(), field).GetValue(target);
        }

        private static void Set(object target, string field, object value)
        {
            AccessTools.Field(target.GetType(), field).SetValue(target, value);
        }

        private static ThingComp MobRank(Pawn pawn)
        {
            return pawn.AllComps.FirstOrDefault(c => IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(c.props)));
        }

        private static void SetProgression(ThingComp comp, int level, int xp, int[] stats)
        {
            Set(comp, "currentLevel", level);
            Set(comp, "currentXP", xp);
            Set(comp, "statsInitialized", true);
            object allocation = Get(comp, "stats");
            for (int i = 0; i < StatNames.Length; i++)
            {
                Set(allocation, StatNames[i], stats[i]);
            }
        }

        private static string Describe(ThingComp comp)
        {
            if (comp == null)
            {
                return "no comp";
            }
            object allocation = Get(comp, "stats");
            return "level " + Get(comp, "currentLevel") + ", " + Get(comp, "currentXP") + " XP, init " + Get(comp, "statsInitialized") + ", stats " + string.Join("/", StatNames.Select(n => Get(allocation, n)));
        }

        private static string ExpectedRankTrait(int level)
        {
            var method = AccessTools.Method(AccessTools.TypeByName("IsekaiLeveling.MobRanking.MobRankUtility"), "RankStringFromLevel", new[] { typeof(int) });
            return RankTraitPrefix + (string)method.Invoke(null, new object[] { level });
        }

        private static List<Trait> RankTraits(Pawn pawn)
        {
            return pawn.story?.traits?.allTraits.Where(t => t.def.defName.StartsWith(RankTraitPrefix)).ToList() ?? new List<Trait>();
        }

        private static void CheckProgression(Pawn pawn, int level, int xp, int[] stats, string who)
        {
            ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(pawn);
            Check(comp != null, who + " has the humanlike comp");
            if (comp == null)
            {
                return;
            }
            object allocation = Get(comp, "stats");
            bool statsMatch = Enumerable.Range(0, StatNames.Length).All(i => (int)Get(allocation, StatNames[i]) == stats[i]);
            Check((int)Get(comp, "currentLevel") == level && (int)Get(comp, "currentXP") == xp && statsMatch, who + " keeps level " + level + ", " + xp + " XP and the stats " + string.Join("/", stats) + " (" + Describe(comp) + ")");
            Check((bool)Get(comp, "statsInitialized"), who + " counts as initialized");
            List<Trait> ranks = RankTraits(pawn);
            Check(ranks.Count == 1 && ranks[0].def.defName == ExpectedRankTrait(level), who + " has the rank trait " + ExpectedRankTrait(level) + " (" + string.Join(", ", ranks.Select(t => t.def.defName)) + ")");
        }

        private static void CheckSingleHumanlike(Pawn pawn, string who)
        {
            Check(IsekaiLevelingCompat.HumanlikeCompCount(pawn) == 1, who + " has exactly one Isekai humanlike comp (" + IsekaiLevelingCompat.HumanlikeCompCount(pawn) + ")");
            Check(IsekaiLevelingCompat.MobRankCompCount(pawn) == 0, who + " has no Isekai creature rank comp");
            ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(pawn);
            Check(comp != null && comp.props != null && IsekaiLevelingCompat.IsHumanlikeComp(CompSignature.Of(comp.props)), who + "'s humanlike comp carries real properties");
        }

        public static void RunStartup()
        {
            Check(IsekaiLevelingCompat.IsAvailable, "the integration bound (" + (IsekaiLevelingCompat.BindFailure ?? "no failure") + ")");
            if (!IsekaiLevelingCompat.IsAvailable)
            {
                return;
            }
            Type tab = AccessTools.TypeByName("IsekaiLeveling.UI.ITab_IsekaiStats");
            int races = 0;
            foreach (ThingDef sentient in SentientMechRegistry.SentientRaces)
            {
                ThingDef original = SentientMechRegistry.OriginalOf(sentient);
                races++;
                int originalMob = original.comps?.Count(c => IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(c))) ?? 0;
                int sentientMob = sentient.comps?.Count(c => IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(c))) ?? 0;
                Check(originalMob == 1, original.defName + " (ordinary) has one creature rank property (" + originalMob + ")");
                Check(sentientMob == 0 && IsekaiLevelingCompat.HumanlikePropertyCount(sentient) == 1, sentient.defName + " has no creature rank property and one humanlike one");
                Check(IsekaiLevelingCompat.HumanlikePropertyCount(original) == 0, original.defName + " (ordinary) has no humanlike property");
                DmsRaceKind dmsKind = DeadMansSwitchCompat.KindOfSentient(sentient);
                string drift = CompListComparer.FirstDifference(original.comps, sentient.comps, s => SentientCompRules.IsRemovedFromSentient(dmsKind, s), out _);
                Check(drift == null, sentient.defName + " has no comp drift" + (drift == null ? "" : ": " + drift));
                Check(tab != null && sentient.inspectorTabsResolved != null && sentient.inspectorTabsResolved.Any(t => t.GetType() == tab), sentient.defName + " has Isekai's humanlike Status tab");
            }
            Check(races > 0, "checked " + races + " sentient races");
            RunPure();
        }

        private static ThingComp LiveComp(CompProperties props)
        {
            var comp = (ThingComp)Activator.CreateInstance(props.compClass);
            comp.props = props;
            return comp;
        }

        private static void RunPure()
        {
            CompProperties mobProps = (CompProperties)Activator.CreateInstance(AccessTools.TypeByName(IsekaiLevelingCompat.MobRankPropsName));
            CompProperties humanProps = (CompProperties)Activator.CreateInstance(AccessTools.TypeByName(IsekaiLevelingCompat.HumanlikePropsName));
            Type humanComp = AccessTools.TypeByName(IsekaiLevelingCompat.HumanlikeCompName);
            Type mobComp = AccessTools.TypeByName(IsekaiLevelingCompat.MobRankCompName);

            Check(IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(mobProps)) && !IsekaiLevelingCompat.IsHumanlikeComp(CompSignature.Of(mobProps)), "the creature property is recognized as the creature one only");
            Check(IsekaiLevelingCompat.IsHumanlikeComp(CompSignature.Of(humanProps)) && !IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(humanProps)), "the humanlike property is recognized as the humanlike one only");
            Check(!IsekaiLevelingCompat.IsMobRankComp(new CompSignature(mobProps.GetType(), humanComp)) && !IsekaiLevelingCompat.IsMobRankComp(new CompSignature(humanProps.GetType(), mobComp)), "mixed pairs are not the creature comp");
            Check(!IsekaiLevelingCompat.IsMobRankComp(default(CompSignature)) && !IsekaiLevelingCompat.IsMobRankComp(CompSignature.Of(new CompProperties_SentientRecharge())), "null and unrelated signatures are not the creature comp");

            ThingComp other1 = LiveComp(new CompProperties_SentientRecharge());
            ThingComp mob = LiveComp(mobProps);
            ThingComp other2 = LiveComp(humanProps);
            List<ThingComp> kept = IsekaiLevelingCompat.WithoutMobRankComps(new List<ThingComp> { other1, mob, other2 }, out List<ThingComp> removed);
            Check(kept.SequenceEqual(new[] { other1, other2 }) && removed.SequenceEqual(new[] { mob }), "the live filter removes only the creature comp and keeps the rest in order");
            Check(IsekaiLevelingCompat.WithoutMobRankComps(null, out List<ThingComp> none).Count == 0 && none.Count == 0, "a null live list is handled");

            Check(IsekaiLevelingCompat.CaptureProgression(new List<ThingComp>()) == null && IsekaiLevelingCompat.CaptureProgression(null) == null, "no removed comp captures nothing");
            Check(IsekaiLevelingCompat.CaptureProgression(new List<ThingComp> { mob }) == null, "an uninitialized creature comp captures nothing");
            SetProgression(mob, Level, Xp, StatValues);
            string before = Describe(mob);
            IsekaiLevelingCompat.Progression captured = IsekaiLevelingCompat.CaptureProgression(new List<ThingComp> { mob });
            Check(captured != null && captured.Level == Level && captured.Xp == Xp && captured.Stats.SequenceEqual(StatValues), "an initialized creature comp is captured exactly (" + captured + ")");
            Check(Describe(mob) == before, "capturing does not change the source comp");
            Set(Get(mob, "stats"), "strength", 99);
            Check(captured != null && captured.Stats[0] == StatValues[0], "the capture shares nothing with the source");
            bool threw = false;
            try
            {
                IsekaiLevelingCompat.CaptureProgression(new List<ThingComp> { mob, LiveComp(mobProps) });
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Check(threw, "two creature comps are refused");

            bool Removal(CompSignature s) => SentientCompRules.IsRemovedFromSentient(DmsRaceKind.None, s);
            var original = new List<CompProperties> { new CompProperties_OverseerSubject(), mobProps };
            var recharge = new CompProperties_SentientRecharge();
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge, humanProps }, Removal, out _) == null, "a clone with one humanlike comp matches an original with the creature comp");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge }, Removal, out _) == null, "a clone without it also passes the generic comparison (presence is the validator's job)");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge, humanProps, new CompProperties_SentientRecharge() }, Removal, out _) != null, "a duplicate recharge comp is still reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge, humanProps, (CompProperties)Activator.CreateInstance(humanProps.GetType()) }, Removal, out _) != null, "a duplicate humanlike comp is reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge, humanProps, (CompProperties)Activator.CreateInstance(mobProps.GetType()) }, Removal, out _) != null, "a creature comp left on the clone is reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { (CompProperties)Activator.CreateInstance(mobProps.GetType()), recharge, humanProps }, Removal, out _) != null, "a creature comp inserted ahead of the recharge comp is reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { humanProps, recharge }, Removal, out _) != null, "a humanlike comp ahead of the recharge comp is reported (the generator appends it after)");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { humanProps }, Removal, out _) != null, "a missing recharge comp is still reported");
            Check(CompListComparer.FirstDifference(original, new List<CompProperties> { recharge, humanProps, new CompProperties() }, Removal, out _) != null, "an unknown extra comp is still reported");
        }

        public static void Run(Func<string, Pawn> spawn)
        {
            if (!IsekaiLevelingCompat.IsAvailable)
            {
                return;
            }
            CheckLiveConversion(spawn);
            CheckRollbackAfterReconcile(spawn);
            CheckRollbackAfterProgression(spawn);
            CheckUninitializedSource(spawn);
            CheckExclusionSetting(spawn);
            CheckStartingAndGenerated();
        }

        private static Pawn SpawnConstructoid(Func<string, Pawn> spawn, int level, int xp, int[] stats)
        {
            Pawn mech = spawn("Mech_Constructoid");
            ThingComp mob = MobRank(mech);
            if (mob == null)
            {
                MechSentienceAutoTest.Fail("Isekai: an ordinary Constructoid has no creature rank comp");
                return mech;
            }
            SetProgression(mob, level, xp, stats);
            return mech;
        }

        private static void CheckLiveConversion(Func<string, Pawn> spawn)
        {
            Pawn mech = SpawnConstructoid(spawn, Level, Xp, StatValues);
            int id = mech.thingIDNumber;
            Check(IsekaiLevelingCompat.MobRankCompCount(mech) == 1 && IsekaiLevelingCompat.HumanlikeCompCount(mech) == 0, "an ordinary Constructoid has one creature comp and no humanlike one");
            ThingComp mobBefore = MobRank(mech);
            List<ThingComp> unrelated = mech.AllComps.Where(c => c != mobBefore && !(c is CompOverseerSubject)).ToList();
            string mobDescription = Describe(mobBefore);

            Check(SentientMechConverter.TryConvert(mech, showMessages: false), "the Constructoid with earned progression converted");
            Check(mech.thingIDNumber == id && mech.Spawned && SentientMechRegistry.IsSentient(mech), "same pawn, spawned and sentient");
            CheckSingleHumanlike(mech, "the converted Constructoid");
            CheckProgression(mech, Level, Xp, StatValues, "the converted Constructoid");
            Check(unrelated.All(c => mech.AllComps.Contains(c)), "every unrelated comp instance is still on the pawn");
            Check(!mech.AllComps.Contains(mobBefore) && Describe(mobBefore) == mobDescription, "the creature comp is gone and was not changed");

            ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(mech);
            var gain = AccessTools.Method(comp.GetType(), "GainXP");
            int xpBefore = (int)Get(comp, "currentXP");
            int levelBefore = (int)Get(comp, "currentLevel");
            gain.Invoke(comp, new object[] { 100, "AutoTest" });
            Check((int)Get(comp, "currentXP") > xpBefore || (int)Get(comp, "currentLevel") > levelBefore, "the converted pawn gains Isekai XP (" + Describe(comp) + ")");
            object allocation = Get(comp, "stats");
            float withVitality = mech.HealthScale;
            Set(allocation, "vitality", 5);
            float withoutVitality = mech.HealthScale;
            Set(allocation, "vitality", StatValues[2]);
            Check(withVitality > withoutVitality, "its vitality changes its health scale (" + withVitality + " vs " + withoutVitality + ")");

            savedPawnId = id;
            Pawn ordinary = SpawnConstructoid(spawn, 12, 5, new[] { 6, 6, 6, 6, 6, 6, 0 });
            savedOrdinaryId = ordinary.thingIDNumber;
            savedOrdinaryLevel = 12;
        }

        private static void CheckRollbackAfterReconcile(Func<string, Pawn> spawn)
        {
            Pawn mech = SpawnConstructoid(spawn, 21, 55, new[] { 8, 9, 10, 11, 12, 13, 2 });
            List<ThingComp> compsBefore = mech.AllComps.ToList();
            ThingComp mob = MobRank(mech);
            int index = compsBefore.IndexOf(mob);
            string description = Describe(mob);
            string defName = mech.def.defName;

            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool converted = SentientMechConverter.TryConvert(mech, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(!converted && !SentientMechRegistry.IsSentient(mech) && mech.def.defName == defName, "the forced failure after reconciliation rolled the Constructoid back");
            CheckRolledBack(mech, compsBefore, mob, index, description, "after reconciliation");
        }

        private static void CheckRollbackAfterProgression(Func<string, Pawn> spawn)
        {
            Pawn mech = SpawnConstructoid(spawn, 21, 55, new[] { 8, 9, 10, 11, 12, 13, 2 });
            List<ThingComp> compsBefore = mech.AllComps.ToList();
            ThingComp mob = MobRank(mech);
            int index = compsBefore.IndexOf(mob);
            string description = Describe(mob);
            string defName = mech.def.defName;

            bool humanlikeAtHook = false;
            bool rankTraitAtHook = false;
            SentientMechConverter.AfterProgressionForTesting = p =>
            {
                CheckProgression(p, 21, 55, new[] { 8, 9, 10, 11, 12, 13, 2 }, "at the hook the half converted Constructoid");
                humanlikeAtHook = IsekaiLevelingCompat.HumanlikeCompCount(p) == 1 && IsekaiLevelingCompat.MobRankCompCount(p) == 0;
                rankTraitAtHook = RankTraits(p).Count == 1;
            };
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureAfterProgressionForTesting = true;
            bool converted;
            try
            {
                converted = SentientMechConverter.TryConvert(mech, showMessages: false);
            }
            finally
            {
                SentientMechConverter.ForceFailureAfterProgressionForTesting = false;
                SentientMechConverter.AfterProgressionForTesting = null;
            }
            Check(humanlikeAtHook && rankTraitAtHook, "the hook saw the new humanlike comp and its rank trait before the forced failure");
            Check(!converted && !SentientMechRegistry.IsSentient(mech) && mech.def.defName == defName, "the forced failure after the progression copy rolled the Constructoid back");
            CheckRolledBack(mech, compsBefore, mob, index, description, "after the progression copy");
        }

        private static void CheckRolledBack(Pawn mech, List<ThingComp> compsBefore, ThingComp mob, int index, string description, string when)
        {
            List<ThingComp> compsAfter = mech.AllComps.ToList();
            Check(compsAfter.SequenceEqual(compsBefore), "the comps are the same instances in the same order " + when);
            Check(compsAfter.IndexOf(mob) == index && ReferenceEquals(MobRank(mech), mob), "the creature comp is back at index " + index + " " + when);
            Check(Describe(mob) == description, "the creature progression is unchanged " + when + " (" + Describe(mob) + ")");
            Check(IsekaiLevelingCompat.HumanlikeCompCount(mech) == 0, "the new humanlike comp is gone " + when);
            Check(mech.story == null || RankTraits(mech).Count == 0, "no Isekai rank trait is left " + when);
            Check(mech.Spawned && mech.Faction == Faction.OfPlayer, "the Constructoid is spawned again " + when);
        }

        private static void CheckUninitializedSource(Func<string, Pawn> spawn)
        {
            Pawn mech = spawn("Mech_Constructoid");
            ThingComp mob = MobRank(mech);
            Check(mob != null, "a fresh Constructoid has a creature comp");
            if (mob == null)
            {
                return;
            }
            Set(mob, "statsInitialized", false);
            Set(mob, "currentLevel", 1);
            Check(SentientMechConverter.TryConvert(mech, showMessages: false), "a Constructoid whose creature stats were never initialized converted");
            CheckSingleHumanlike(mech, "the unranked Constructoid");
            ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(mech);
            Check(comp != null && (bool)Get(comp, "statsInitialized"), "its humanlike comp initialized itself on spawn (" + Describe(comp) + ")");
            Check(RankTraits(mech).Count == 1, "it got exactly one rank trait from Isekai's own initialization");
        }

        private static void CheckExclusionSetting(Func<string, Pawn> spawn)
        {
            object settings = AccessTools.Property(AccessTools.TypeByName("IsekaiLeveling.IsekaiMod"), "Settings")?.GetValue(null);
            var field = settings == null ? null : AccessTools.Field(settings.GetType(), "ExcludeMechsFromRanking");
            Check(field != null, "Isekai's mech ranking exclusion setting is reachable");
            if (field == null)
            {
                return;
            }
            object previous = field.GetValue(settings);
            field.SetValue(settings, true);
            try
            {
                Pawn mech = spawn("Mech_Constructoid");
                ThingComp mob = MobRank(mech);
                Check(mob != null && !(bool)Get(mob, "statsInitialized"), "with mechs excluded the creature comp never ranked (" + Describe(mob) + ")");
                Check(SentientMechConverter.TryConvert(mech, showMessages: false), "the excluded Constructoid converted");
                CheckSingleHumanlike(mech, "the excluded Constructoid");
                ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(mech);
                Check(comp != null && (bool)Get(comp, "statsInitialized") && RankTraits(mech).Count == 1, "its humanlike progression initialized once despite the exclusion (" + Describe(comp) + ")");
            }
            finally
            {
                field.SetValue(settings, previous);
            }
        }

        private static void CheckStartingAndGenerated()
        {
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Constructoid");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                MechSentienceAutoTest.Fail("Isekai: no sentient kind for Mech_Constructoid");
                return;
            }
            Pawn starter = null;
            try
            {
                starter = PawnGenerator.GeneratePawn(new PawnGenerationRequest(original, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, developmentalStages: DevelopmentalStage.Adult));
                SentientMechConverter.PrepareUnspawned(starter, sentientKind.race, sentientKind);
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Isekai: preparing an unspawned starting Constructoid threw " + e);
            }
            Check(starter != null && !starter.Spawned && SentientMechRegistry.IsSentient(starter), "an unspawned starting sentient Constructoid was prepared");
            if (starter != null)
            {
                CheckSingleHumanlike(starter, "the starting Constructoid");
                Find.WorldPawns.PassToWorld(starter, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }

            Pawn generated = null;
            try
            {
                generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Isekai: generating a sentient Constructoid threw " + e);
            }
            Check(generated != null, "a sentient Constructoid was generated directly");
            if (generated != null)
            {
                CheckSingleHumanlike(generated, "the generated Constructoid");
                ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(generated);
                Check(comp != null && (bool)Get(comp, "statsInitialized") && RankTraits(generated).Count == 1, "its progression initialized once (" + Describe(comp) + ")");
                Find.WorldPawns.PassToWorld(generated, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }
        }

        public static void MarkForSave()
        {
            Pawn pawn = Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedPawnId);
            Check(pawn != null && SentientMechRegistry.IsSentient(pawn), "the converted pawn is on the map before the save");
            CheckSingleHumanlike(pawn, "the converted pawn before the save");
            savedProgression = Describe(IsekaiLevelingCompat.HumanlikeCompOf(pawn));
        }

        public static void CheckAfterLoad(Map map)
        {
            Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedPawnId);
            Check(pawn != null, "the converted Constructoid is on the map after the reload");
            if (pawn != null)
            {
                CheckSingleHumanlike(pawn, "the reloaded Constructoid");
                ThingComp comp = IsekaiLevelingCompat.HumanlikeCompOf(pawn);
                Check(comp != null && Describe(comp) == savedProgression, "its level, XP and stats survived the reload (" + Describe(comp) + ", saved " + savedProgression + ")");
                int level = comp == null ? 0 : (int)Get(comp, "currentLevel");
                List<Trait> ranks = RankTraits(pawn);
                Check(ranks.Count == 1 && ranks[0].def.defName == ExpectedRankTrait(level), "its rank trait matches its level after the reload (" + string.Join(", ", ranks.Select(t => t.def.defName)) + ")");
                if (comp != null)
                {
                    int xpBefore = (int)Get(comp, "currentXP");
                    int levelBefore = (int)Get(comp, "currentLevel");
                    AccessTools.Method(comp.GetType(), "GainXP").Invoke(comp, new object[] { 100, "AutoTest" });
                    Check((int)Get(comp, "currentXP") > xpBefore || (int)Get(comp, "currentLevel") > levelBefore, "the reloaded pawn still gains Isekai XP");
                }
            }

            Pawn ordinary = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedOrdinaryId);
            Check(ordinary != null && !SentientMechRegistry.IsSentient(ordinary), "the unconverted Constructoid is still ordinary after the reload");
            if (ordinary != null)
            {
                ThingComp mob = MobRank(ordinary);
                Check(IsekaiLevelingCompat.MobRankCompCount(ordinary) == 1 && IsekaiLevelingCompat.HumanlikeCompCount(ordinary) == 0 && mob != null && (int)Get(mob, "currentLevel") == savedOrdinaryLevel, "it keeps its creature ranking (" + Describe(mob) + ")");
            }
        }
    }
}
#endif
