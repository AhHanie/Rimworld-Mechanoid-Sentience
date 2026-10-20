#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class RimpsycheAutoTest
    {
        private const string Ns = "Maux36.RimPsyche.";
        private const float EditedFacet = 37.5f;
        private const float EditedInterest = -12.5f;

        private static string savedSnapshot;
        private static int uiFrames;
        private static Window editPopup;
        private static Pawn uiPawn;
        private static int storedSexualityPawnId;
        private static bool tabSettingChanged;
        private static bool tabSettingBefore;

        public static bool IsActive => RimpsycheCompat.IsAvailable;

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "Rimpsyche: " + what);
        }

        private static Type RType(string name)
        {
            return AccessTools.TypeByName(Ns + name);
        }

        private static object Member(object target, string name)
        {
            Type type = target.GetType();
            PropertyInfo property = AccessTools.Property(type, name);
            if (property != null)
            {
                return property.GetValue(target);
            }
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null)
            {
                throw new MissingMemberException(type.FullName, name);
            }
            return field.GetValue(target);
        }

        private static object StaticMember(string typeName, string name)
        {
            return AccessTools.Field(RType(typeName), name).GetValue(null);
        }

        private static object Call(object target, string name, Type[] types, params object[] args)
        {
            MethodInfo method = AccessTools.Method(target.GetType(), name, types);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, name);
            }
            return method.Invoke(target, args);
        }

        private static object Call(object target, string name)
        {
            return Call(target, name, Type.EmptyTypes);
        }

        private static IList AllDefs(Type defType)
        {
            return (IList)typeof(DefDatabase<>).MakeGenericType(defType).GetProperty("AllDefsListForReading").GetValue(null);
        }

        private static IList InterestList => (IList)StaticMember("RimpsycheDatabase", "InterestList");

        private static bool SexualityModuleLoaded => (bool)StaticMember("Rimpsyche", "SexualityModuleLoaded");

        private static IDictionary CompCache => (IDictionary)StaticMember("PsycheCacheManager", "CompPsycheCache");

        private static ThingComp AccessorResult(Pawn pawn)
        {
            return (ThingComp)RimpsycheCompat.Accessor.Invoke(null, new object[] { pawn });
        }

        private static int PsycheCompCount(Pawn pawn)
        {
            return pawn.AllComps.Count(c => c.GetType() == RimpsycheCompat.CompType);
        }

        private static void SetPsycheTabSetting(bool on)
        {
            FieldInfo field = AccessTools.Field(RType("RimpsycheSettings"), "usePsycheTab");
            if (!tabSettingChanged)
            {
                tabSettingBefore = (bool)field.GetValue(null);
                tabSettingChanged = true;
            }
            field.SetValue(null, on);
        }

        public static void RunStartup()
        {
            SetPsycheTabSetting(true);
            Type tab = RimpsycheCompat.TabType;
            InspectTabBase shared = InspectTabManager.GetSharedInstance(tab);
            int races = 0;
            foreach (ThingDef race in SentientMechRegistry.SentientRaces)
            {
                races++;
                string name = race.defName;
                Check(race.comps.Count(c => c.compClass == RimpsycheCompat.CompType) == 1, name + " has exactly one psyche comp");
                Check(race.inspectorTabs != null && race.inspectorTabs.Contains(tab) && race.inspectorTabsResolved != null && race.inspectorTabsResolved.Contains(shared), name + " has the psyche tab");
                ThingDef corpse = race.race.corpseDef;
                Check(corpse != null && corpse.inspectorTabs != null && corpse.inspectorTabs.Contains(tab), name + "'s corpse has the psyche tab");
                Check(RimpsycheCompat.IsTracked(race), name + " is in Rimpsyche's tracked race hashes");
                Check(RimpsycheCompat.IsNonSexual(race), name + " is in Rimpsyche's nonsexual race hashes");
                ThingDef original = SentientMechRegistry.OriginalOf(race);
                Check(original != null && original.comps.All(c => c.compClass != RimpsycheCompat.CompType) && !RimpsycheCompat.IsTracked(original) && !RimpsycheCompat.IsNonSexual(original) && (original.inspectorTabs == null || !original.inspectorTabs.Contains(tab)), "the ordinary " + (original?.defName ?? "mech") + " gets no psyche comp, tab or hash");
            }
            Check(races > 0, "checked " + races + " sentient races");
            Log.Message("AUTOTEST info Rimpsyche Sexuality module loaded=" + SexualityModuleLoaded);
        }

        public static void CheckPsyche(Pawn pawn, string label, bool checkTab)
        {
            string who = label + " " + pawn.LabelShort;
            ThingComp comp = RimpsycheCompat.PsycheCompOf(pawn);
            Check(comp != null && PsycheCompCount(pawn) == 1, who + " has exactly one live psyche comp");
            if (comp == null)
            {
                return;
            }
            Check(ReferenceEquals(AccessorResult(pawn), comp), who + ": Rimpsyche's accessor returns the live comp");
            Check((bool)Member(comp, "Enabled"), who + ": the psyche is enabled");

            object personality = Member(comp, "Personality");
            object interests = Member(comp, "Interests");
            Check(personality != null && interests != null, who + " has personality and interests");
            try
            {
                Type facetType = RType("Facet");
                foreach (object facet in Enum.GetValues(facetType))
                {
                    float value = (float)Call(personality, "GetFacetValueRaw", new[] { facetType }, facet);
                    if (value < -50f || value > 50f)
                    {
                        Fail(who + ": facet " + facet + " out of range " + value);
                    }
                }
                Type personalityDef = RType("PersonalityDef");
                IList defs = AllDefs(personalityDef);
                Check(defs.Count > 0, "Rimpsyche personality defs are loaded (" + defs.Count + ")");
                foreach (object def in defs)
                {
                    float value = (float)Call(personality, "GetPersonality", new[] { personalityDef }, def);
                    if (value < -1.001f || value > 1.001f)
                    {
                        Fail(who + ": personality " + def + " out of range " + value);
                    }
                }
                Type interestType = RType("Interest");
                foreach (object interest in InterestList)
                {
                    float score = (float)Call(interests, "GetOrGenerateAdjustedInterestScore", new[] { interestType }, interest);
                    if (score < 0f || score > 100.001f)
                    {
                        Fail(who + ": interest score out of range " + score);
                    }
                }
                Check(InterestList.Count > 0, "Rimpsyche interests are loaded (" + InterestList.Count + ")");
            }
            catch (Exception e)
            {
                Fail(who + ": reading personality and interests threw " + e);
            }

            CheckSexualityInert(pawn, comp, who);
            if (checkTab)
            {
                CheckTabVisible(pawn, who);
            }
        }

        private static void Fail(string what)
        {
            MechSentienceAutoTest.Fail("Rimpsyche: " + what);
        }

        private static void CheckSexualityInert(Pawn pawn, ThingComp comp, string who)
        {
            try
            {
                object sexuality = Member(comp, "Sexuality");
                Check(sexuality != null, who + ": the internal sexuality tracker exists (Rimpsyche's conversations read it)");
                Check((bool)Member(sexuality, "Suppressed"), who + ": the sexuality tracker is suppressed");
                Check(!(bool)Call(sexuality, "SexualityExpressed") && !(bool)Call(sexuality, "ShowOnUI"), who + ": sexuality is not expressed and has no panel");
                Check(Member(sexuality, "orientationCategory").ToString() == "None", who + ": no orientation was generated (" + Member(sexuality, "orientationCategory") + ")");
                Check((float)Member(sexuality, "RawAttraction") == 0f && (float)Member(sexuality, "RawSexdrive") == 0f && (float)Member(sexuality, "MKinsey") < 0f, who + ": no attraction, drive or kinsey value was generated");
                Check((float)Call(sexuality, "GetAdjustedAttractionToGender", new[] { typeof(Gender) }, Gender.Male) == 0f && (float)Call(sexuality, "GetAdjustedAttractionToGender", new[] { typeof(Gender) }, Gender.Female) == 0f && (float)Call(sexuality, "GetAdjustedSexdrive") == 0f, who + ": adjusted attraction and drive are zero");
                Check(!pawn.story.traits.HasTrait(TraitDefOf.Gay) && !pawn.story.traits.HasTrait(TraitDefOf.Bisexual) && !pawn.story.traits.HasTrait(TraitDefOf.Asexual), who + " has no sexuality trait");
            }
            catch (Exception e)
            {
                Fail(who + ": reading the sexuality tracker threw " + e);
            }
        }

        private static void CheckTabVisible(Pawn pawn, string who)
        {
            Find.Selector.ClearSelection();
            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            InspectTabBase tab = InspectTabManager.GetSharedInstance(RimpsycheCompat.TabType);
            Check(tab.IsVisible, who + ": the psyche tab is visible");
        }

        public static void CheckAllSentientOnMap(Map map)
        {
            List<Pawn> sentient = map.mapPawns.AllPawnsSpawned.Where(p => SentientMechRegistry.IsSentient(p) && p.thingIDNumber != storedSexualityPawnId).ToList();
            Check(sentient.Count > 1, "checking every spawned sentient pawn on the map (" + sentient.Count + "): live and awakening conversions");
            foreach (Pawn pawn in sentient)
            {
                CheckPsyche(pawn, "converted", checkTab: false);
            }
            if (sentient.Count > 0)
            {
                CheckTabVisible(sentient[0], "converted " + sentient[0].LabelShort);
            }
        }

        public static void CheckStartingAndGenerated()
        {
            PawnKindDef original = DefDatabase<PawnKindDef>.GetNamed("Mech_Militor");
            if (!SentientMechRegistry.TryGetSentient(original.race, original, out _, out PawnKindDef sentientKind))
            {
                Fail("no sentient kind for Mech_Militor");
                return;
            }
            Pawn starter = null;
            try
            {
                starter = PawnGenerator.GeneratePawn(new PawnGenerationRequest(original, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, developmentalStages: DevelopmentalStage.Adult));
                SentientMechConverter.PrepareUnspawned(starter, sentientKind.race, sentientKind);
                starter.relations.everSeenByPlayer = true;
                PawnComponentsUtility.AddComponentsForSpawn(starter);
            }
            catch (Exception e)
            {
                Fail("preparing an unspawned starting mech threw " + e);
            }
            if (starter != null)
            {
                Check(!starter.Spawned && SentientMechRegistry.IsSentient(starter), "the starting mech is sentient and was never spawned");
                CheckPsyche(starter, "starting", checkTab: false);
                Discard(starter);
            }

            Pawn generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            CheckPsyche(generated, "generated", checkTab: false);
            Discard(generated);
        }

        private static void Discard(Pawn pawn)
        {
            if (pawn != null)
            {
                Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }
        }

        public static void CheckConversations(Func<string, Pawn> spawn)
        {
            Pawn a = spawn("Mech_Militor");
            Pawn b = spawn("Mech_Lifter");
            if (!SentientMechConverter.TryConvert(a, showMessages: false) || !SentientMechConverter.TryConvert(b, showMessages: false))
            {
                Fail("the conversation test mechs did not convert");
                return;
            }
            Pawn human = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault(p => !SentientMechRegistry.IsSentient(p) && p.RaceProps.Humanlike && p.DevelopmentalStage == DevelopmentalStage.Adult);
            if (human == null)
            {
                Fail("no human colonist for the conversation test");
                return;
            }

            InteractionDef conversation = DefDatabase<InteractionDef>.GetNamedSilentFail("Rimpsyche_StartConversation");
            Check(conversation != null, "the Rimpsyche conversation interaction exists");
            if (conversation == null)
            {
                return;
            }
            foreach ((Pawn first, Pawn second, string label) in new[] { (a, b, "sentient/sentient"), (b, a, "sentient/sentient (reverse)"), (a, human, "sentient/human"), (human, a, "human/sentient") })
            {
                try
                {
                    float weight = conversation.Worker.RandomSelectionWeight(first, second);
                    Check(weight > 0f, label + ": the conversation can be selected (weight " + weight + ")");
                    var packs = new List<RulePackDef>();
                    conversation.Worker.Interacted(first, second, packs, out _, out _, out _, out _);
                    Check(true, label + ": the conversation ran without an exception");
                    bool played = first.interactions.TryInteractWith(second, conversation);
                    Log.Message("AUTOTEST info " + label + ": TryInteractWith=" + played);
                }
                catch (Exception e)
                {
                    Fail(label + ": the conversation threw " + e);
                }
                Check(InteractionDefOf.RomanceAttempt.Worker.RandomSelectionWeight(first, second) == 0f, label + ": romance weight stays zero");
                Check(InteractionDefOf.MarriageProposal.Worker.RandomSelectionWeight(first, second) == 0f, label + ": marriage weight stays zero");
            }

            ThingComp compA = RimpsycheCompat.PsycheCompOf(a);
            object sexualityA = Member(compA, "Sexuality");
            ((IDictionary)Member(sexualityA, "relationship"))[human.thingIDNumber] = 0.9f;
            ((IDictionary)Member(sexualityA, "relationship"))[b.thingIDNumber] = 0.9f;
            Check((float)Call(sexualityA, "GetRelationshipWith", new[] { typeof(Pawn) }, human) == 0f && (float)Call(sexualityA, "GetRelationshipWith", new[] { typeof(Pawn) }, b) == 0f, "a stored romantic relationship score of a sentient pawn reads zero");
            ThingComp humanComp = RimpsycheCompat.PsycheCompOf(human);
            if (humanComp != null)
            {
                object humanSexuality = Member(humanComp, "Sexuality");
                ((IDictionary)Member(humanSexuality, "relationship"))[a.thingIDNumber] = 0.9f;
                Check((float)Call(humanSexuality, "GetRelationshipWith", new[] { typeof(Pawn) }, a) == 0f, "a human's stored score towards a sentient pawn reads zero");
                ((IDictionary)Member(humanSexuality, "relationship")).Remove(a.thingIDNumber);
                bool control = (bool)Call(humanSexuality, "ShowOnUI");
                Log.Message("AUTOTEST info control: human " + human.LabelShort + " sexuality panel=" + control + (SexualityModuleLoaded && !control ? " (module active but the human shows no panel: control unverified)" : ""));
            }
            ((IDictionary)Member(sexualityA, "relationship")).Clear();

            Pawn c = spawn("Mech_Scyther");
            if (SentientMechConverter.TryConvert(c, showMessages: false))
            {
                storedSexualityPawnId = c.thingIDNumber;
                ThingComp compC = RimpsycheCompat.PsycheCompOf(c);
                object sexualityC = Member(compC, "Sexuality");
                Type orientationType = RType("SexualOrientation");
                AccessTools.Field(sexualityC.GetType(), "orientationCategory").SetValue(sexualityC, Enum.Parse(orientationType, "Heterosexual"));
                Call(sexualityC, "SetAttraction", new[] { typeof(float) }, 0.9f);
                Check(!(bool)Call(sexualityC, "SexualityExpressed") && !(bool)Call(sexualityC, "ShowOnUI"), "stored sexuality values do not make a sentient pawn's sexuality expressed");
                Check((float)Call(sexualityC, "GetAdjustedAttractionToGender", new[] { typeof(Gender) }, Gender.Male) == 0f, "stored attraction reads zero");
                Check((float)Member(sexualityC, "RawAttraction") > 0f, "the stored value itself is kept (not wiped)");
            }
            else
            {
                Fail("the stored sexuality test mech did not convert");
            }
        }

        public static void CheckRollback(Func<string, Pawn> spawn)
        {
            Pawn mech = spawn("Mech_Lifter");
            int id = mech.thingIDNumber;
            string def = mech.def.defName;
            bool cachedDuringConversion = false;
            ThingComp usedComp = null;
            SentientMechConverter.AfterSwapForTesting = pawn =>
            {
                usedComp = AccessorResult(pawn);
                cachedDuringConversion = CompCache.Contains(pawn.thingIDNumber);
            };
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool converted;
            try
            {
                converted = SentientMechConverter.TryConvert(mech, showMessages: false);
            }
            finally
            {
                SentientMechConverter.AfterSwapForTesting = null;
                SentientMechConverter.ForceFailureForTesting = false;
            }
            Check(usedComp != null && cachedDuringConversion, "during the conversion the accessor found the new comp and Rimpsyche cached it (the situation a rollback must undo)");
            Check(!converted && mech.thingIDNumber == id && mech.def.defName == def && !SentientMechRegistry.IsSentient(mech), "the forced failure restored the original mech");
            Check(RimpsycheCompat.PsycheCompOf(mech) == null && PsycheCompCount(mech) == 0, "no psyche comp is left on the restored mech");
            Check(!CompCache.Contains(id), "no stale cache entry remains");
            Check(AccessorResult(mech) == null, "Rimpsyche's accessor returns null for the restored ordinary mech");
            Check(!CompCache.Contains(id), "asking the accessor again caches nothing for it");

            Check(SentientMechConverter.TryConvert(mech, showMessages: false), "the restored mech converts normally afterwards");
            CheckPsyche(mech, "reconverted", checkTab: false);
        }

        public static string PsycheSnapshot(Pawn pawn)
        {
            ThingComp comp = RimpsycheCompat.PsycheCompOf(pawn);
            object personality = Member(comp, "Personality");
            object interests = Member(comp, "Interests");
            var parts = new List<string>();
            Type facetType = RType("Facet");
            foreach (object facet in Enum.GetValues(facetType))
            {
                parts.Add(facet + "=" + ((float)Call(personality, "GetFacetValueRaw", new[] { facetType }, facet)).ToString("R"));
            }
            Type personalityDef = RType("PersonalityDef");
            foreach (object def in AllDefs(personalityDef))
            {
                parts.Add(def + "=" + ((float)Call(personality, "GetPersonality", new[] { personalityDef }, def)).ToString("R"));
            }
            IDictionary scores = (IDictionary)Member(interests, "interestScore");
            foreach (string key in scores.Keys.Cast<string>().OrderBy(k => k, StringComparer.Ordinal))
            {
                parts.Add(key + "=" + ((float)scores[key]).ToString("R"));
            }
            return string.Join("|", parts);
        }

        private static string Differences(string before, string after)
        {
            string[] a = before.Split('|');
            string[] b = after.Split('|');
            IEnumerable<string> missing = a.Except(b).Select(x => "saved " + x);
            IEnumerable<string> added = b.Except(a).Select(x => "loaded " + x);
            return string.Join("; ", missing.Concat(added));
        }

        public static void MarkForSave(Pawn pawn)
        {
            ThingComp comp = RimpsycheCompat.PsycheCompOf(pawn);
            if (comp == null)
            {
                Fail("the pawn that is saved has no psyche comp");
                return;
            }
            object personality = Member(comp, "Personality");
            object interests = Member(comp, "Interests");
            Type facetType = RType("Facet");
            Type interestType = RType("Interest");
            Call(personality, "SetFacetValue", new[] { facetType, typeof(float) }, Enum.Parse(facetType, "Imagination"), EditedFacet);
            Call(personality, "DirtyCache");
            foreach (object interest in InterestList)
            {
                Call(interests, "GetOrGenerateInterestScore", new[] { interestType }, interest);
            }
            object first = InterestList[0];
            ((IDictionary)Member(interests, "interestScore"))[(string)Member(first, "name")] = EditedInterest;
            savedSnapshot = PsycheSnapshot(pawn);
            Log.Message("AUTOTEST info Rimpsyche snapshot " + savedSnapshot.Length + " chars, edited Imagination=" + EditedFacet + ", interest " + Member(first, "name") + "=" + EditedInterest);
            Check(savedSnapshot.Contains("Imagination=" + EditedFacet.ToString("R")), "the edited facet is recorded before saving");
        }

        public static void CheckAfterLoad(Pawn pawn)
        {
            SetPsycheTabSetting(true);
            CheckPsyche(pawn, "after load", checkTab: true);
            ThingComp comp = RimpsycheCompat.PsycheCompOf(pawn);
            if (comp == null || savedSnapshot == null)
            {
                Fail("no psyche or no snapshot to compare after the load");
                return;
            }
            string after = PsycheSnapshot(pawn);
            Check(after == savedSnapshot, "every facet, personality and interest value survived save and load" + (after == savedSnapshot ? "" : ": " + Differences(savedSnapshot, after)));
            Check(after.Contains("Imagination=" + EditedFacet.ToString("R")), "the edited facet survived");
            object first = InterestList[0];
            Check(Math.Abs((float)((IDictionary)Member(Member(comp, "Interests"), "interestScore"))[(string)Member(first, "name")] - EditedInterest) < 1e-4f, "the edited interest survived");
        }

        public static bool TickUi(Pawn pawn, string outputFolder)
        {
            uiFrames++;
            if (uiFrames == 1)
            {
                uiPawn = pawn;
                SetPsycheTabSetting(false);
                Find.Selector.ClearSelection();
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
                InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Character));
            }
            else if (uiFrames == 30)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(outputFolder, "rimpsyche_character_tab.png"));
            }
            else if (uiFrames == 45)
            {
                SetPsycheTabSetting(true);
                InspectPaneUtility.OpenTab(RimpsycheCompat.TabType);
            }
            else if (uiFrames == 55)
            {
                editPopup = (Window)Activator.CreateInstance(RType("PsycheEditPopup"), pawn, false);
                Find.WindowStack.Add(editPopup);
            }
            else if (uiFrames == 85)
            {
                Check(Find.WindowStack.IsOpen(editPopup), "the psyche edit window is open for " + pawn.LabelShort);
                Check(InspectTabManager.GetSharedInstance(RimpsycheCompat.TabType).IsVisible, "the psyche tab is visible for " + pawn.LabelShort);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(outputFolder, "rimpsyche_ui.png"));
            }
            else if (uiFrames == 105)
            {
                editPopup.Close(doCloseSound: false);
                return true;
            }
            return false;
        }
    }
}
#endif
