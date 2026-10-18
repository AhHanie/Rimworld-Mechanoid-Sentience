using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    public static class SentientMechConverter
    {
        private static readonly FieldInfo CompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
        private static readonly FieldInfo CompsByTypeField = AccessTools.Field(typeof(ThingWithComps), "compsByType");
        private static readonly FieldInfo OverseerSubjectField = AccessTools.Field(typeof(Pawn), "overseerSubject");

        public static bool ForceFailureForTesting;

        public static AcceptanceReport CanConvert(Pawn pawn)
        {
            return CanConvert(pawn, out _, out _);
        }

        private static AcceptanceReport CanConvert(Pawn pawn, out ThingDef sentientRace, out PawnKindDef sentientKind)
        {
            sentientRace = null;
            sentientKind = null;
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return "MechanoidSentience.Reject.NoPawn".Translate();
            }
            if (!ModsConfig.BiotechActive)
            {
                return "MechanoidSentience.Reject.MissingDlc".Translate();
            }
            if (SentientMechRegistry.IsSentient(pawn))
            {
                return "MechanoidSentience.Reject.AlreadySentient".Translate(pawn.LabelShort);
            }
            if (!pawn.RaceProps.IsMechanoid)
            {
                return "MechanoidSentience.Reject.NotMechanoid".Translate(pawn.LabelShort);
            }
            if (DeadMansSwitchCompat.IsPlatformDrone(pawn))
            {
                return "MechanoidSentience.Reject.PlatformDrone".Translate(pawn.LabelShort);
            }
            if (!pawn.Spawned || pawn.Map == null)
            {
                return "MechanoidSentience.Reject.NotSpawned".Translate(pawn.LabelShort);
            }
            if (pawn.IsGestating())
            {
                return "MechanoidSentience.Reject.Gestating".Translate(pawn.LabelShort);
            }
            if (pawn.IsDeactivated())
            {
                return "MechanoidSentience.Reject.Deactivated".Translate(pawn.LabelShort);
            }
            if (!SentientMechRegistry.TryGetSentient(pawn.def, pawn.kindDef, out sentientRace, out sentientKind))
            {
                return "MechanoidSentience.Reject.Unsupported".Translate(pawn.def.LabelCap);
            }
            if (pawn.health?.hediffSet?.GetBrain() == null)
            {
                return "MechanoidSentience.Reject.NoBrain".Translate(pawn.LabelShort);
            }
            if (DeadMansSwitchCompat.IsLeaving(pawn))
            {
                return "MechanoidSentience.Reject.DmsLeaving".Translate(pawn.LabelShort);
            }
            if (ModsConfig.IdeologyActive && Faction.OfPlayer.ideos?.PrimaryIdeo == null)
            {
                return "MechanoidSentience.Reject.NoIdeo".Translate();
            }
            return true;
        }

        public static bool TryConvert(Pawn pawn)
        {
            return TryConvert(pawn, showMessages: true);
        }

        public static bool TryConvert(Pawn pawn, bool showMessages)
        {
            AcceptanceReport report = CanConvert(pawn, out ThingDef sentientRace, out PawnKindDef sentientKind);
            if (!report.Accepted)
            {
                if (showMessages)
                {
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            var state = new ConversionState(pawn);
            try
            {
                Convert(pawn, sentientRace, sentientKind, state);
            }
            catch (Exception ex)
            {
                bool completed = state.Stage == ConversionStage.Respawned;
                Log.Error("[Mechanoid Sentience] " + (completed ? "A late step after converting " : "Converting ") + pawn.ToStringSafe() + (completed ? " failed; the conversion itself is complete and the pawn is kept: " : " failed, restoring it: ") + ex);
                Rollback(pawn, state);
                if (!completed)
                {
                    if (showMessages)
                    {
                        Messages.Message("MechanoidSentience.ConversionFailed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent, historical: false);
                    }
                    return false;
                }
            }
            if (showMessages)
            {
                Messages.Message("MechanoidSentience.ConversionSucceeded".Translate(pawn.LabelShort, pawn.kindDef.LabelCap), pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }
            return true;
        }

        private static void Convert(Pawn pawn, ThingDef sentientRace, PawnKindDef sentientKind, ConversionState state)
        {
            Map map = pawn.Map;
            DmsRaceKind dmsKind = DeadMansSwitchCompat.KindOfOriginal(pawn.def);

            pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
            if (pawn.Drafted)
            {
                pawn.drafter.Drafted = false;
            }
            DisconnectFromOverseer(pawn);
            pawn.jobs?.StopAll();

            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            pawn.apparel?.DropAll(pawn.Position, forbid: false);
            RemoveOverseerComp(pawn, map);

            state.Stage = ConversionStage.Despawned;
            pawn.DeSpawn();

            SwapToSentientDefs(pawn, sentientRace, sentientKind, dmsKind);

            if (ForceFailureForTesting)
            {
                ForceFailureForTesting = false;
                throw new InvalidOperationException("Forced failure after despawn and comp reconciliation (SentientMechConverter.ForceFailureForTesting).");
            }

            BuildSentientIdentity(pawn);

            GenSpawn.Spawn(pawn, state.Cell, map, state.Rotation, WipeMode.Vanish, respawningAfterLoad: false);
            state.Stage = ConversionStage.Respawned;

            SentientMetalhorror.RemoveMetalhorrorImplants(pawn);

            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            Find.ColonistBar.MarkColonistsDirty();
            if (state.WasSelected)
            {
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            }
        }

        internal static void PrepareUnspawned(Pawn pawn, ThingDef sentientRace, PawnKindDef sentientKind)
        {
            DmsRaceKind dmsKind = DeadMansSwitchCompat.KindOfOriginal(pawn.def);
            RemoveOverseerComp(pawn, null);
            SwapToSentientDefs(pawn, sentientRace, sentientKind, dmsKind);
            BuildSentientIdentity(pawn);
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
        }

        private static void SwapToSentientDefs(Pawn pawn, ThingDef sentientRace, PawnKindDef sentientKind, DmsRaceKind dmsKind)
        {
            RemoveDeadMansSwitchComps(pawn, dmsKind);
            RemoveOverseerBoundComps(pawn);
            pawn.def = sentientRace;
            pawn.kindDef = sentientKind;
            ReconcileComps(pawn, sentientRace);
        }

        private static void BuildSentientIdentity(Pawn pawn)
        {
            pawn.verbTracker = new VerbTracker(pawn);
            pawn.meleeVerbs = new Pawn_MeleeVerbs(pawn);
            PawnComponentsUtility.CreateInitialComponents(pawn);

            SentientMechGenerator.Generate(pawn);

            if (ModsConfig.IdeologyActive)
            {
                pawn.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);
            }
            pawn.needs.AddOrRemoveNeedsAsAppropriate();
            VerifyNeeds(pawn);

            pawn.Notify_DisabledWorkTypesChanged();
            pawn.workSettings.EnableAndInitialize();
            ClearSleepSchedule(pawn);
            if (pawn.playerSettings != null)
            {
                pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            }
            pawn.health.capacities.Notify_CapacityLevelsDirty();
            pawn.health.hediffSet.DirtyCache();
        }

        private static void DisconnectFromOverseer(Pawn pawn)
        {
            Pawn overseer = pawn.GetOverseer();
            if (overseer == null)
            {
                return;
            }
            overseer.mechanitor?.UnassignPawnFromAnyControlGroup(pawn);
            if (overseer.relations.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, pawn))
            {
                pawn.OverseerSubject?.Notify_DisconnectedFromOverseer();
            }
            overseer.mechanitor?.Notify_BandwidthChanged();
        }

        private static void RemoveOverseerComp(Pawn pawn, Map map)
        {
            if (CompsField.GetValue(pawn) is List<ThingComp> comps)
            {
                CompOverseerSubject overseerComp = comps.OfType<CompOverseerSubject>().FirstOrDefault();
                if (overseerComp != null)
                {
                    if (map != null)
                    {
                        overseerComp.PostDeSpawn(map);
                    }
                    SetComps(pawn, comps.Where(c => c != overseerComp).ToList());
                }
            }
            OverseerSubjectField.SetValue(pawn, null);
        }

        private static void RemoveDeadMansSwitchComps(Pawn pawn, DmsRaceKind kind)
        {
            if (kind != DmsRaceKind.Automatroid && kind != DmsRaceKind.HumanlikeAutomatroid)
            {
                return;
            }
            if (!(CompsField.GetValue(pawn) is List<ThingComp> live))
            {
                return;
            }
            List<ThingComp> kept = DeadMansSwitchCompat.WithoutOwnershipComps(live, kind, out List<ThingComp> removed);
            if (removed.Count == 0)
            {
                return;
            }
            SetComps(pawn, kept);
            DeadMansSwitchCompat.ClearCachedDeadManSwitch(pawn);
            Logger.Message("Removed " + removed.Count + " Dead Man's Switch comp(s) from " + pawn.ToStringSafe() + ": " + removed.Select(c => c.GetType().FullName).ToCommaList());
        }

        private static void RemoveOverseerBoundComps(Pawn pawn)
        {
            if (!(CompsField.GetValue(pawn) is List<ThingComp> live))
            {
                return;
            }
            List<ThingComp> kept = WorkModesCompat.WithoutOverseerBoundComps(live, out List<ThingComp> removed);
            if (removed.Count == 0)
            {
                return;
            }
            SetComps(pawn, kept);
            WorkModesCompat.NotifyCompsRemoved();
            Logger.Message("Removed " + removed.Count + " overseer bound comp(s) from " + pawn.ToStringSafe() + ": " + removed.Select(c => c.GetType().FullName).ToCommaList());
        }

        private static void ReconcileComps(Pawn pawn, ThingDef sentientRace)
        {
            var live = CompsField.GetValue(pawn) as List<ThingComp>;
            List<ThingComp> reconciled;
            List<ThingComp> created;
            try
            {
                reconciled = CompReconciler.Reconcile(live, sentientRace.comps, props => CreateComp(pawn, props), out created);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not bring the comps of " + pawn.ToStringSafe() + " in line with " + sentientRace.defName + ": " + ex.Message, ex);
            }
            SetComps(pawn, reconciled);
            if (created.Count > 0)
            {
                Logger.Message("Added " + created.Count + " comp(s) to " + pawn.ToStringSafe() + ": " + created.Select(c => c.GetType().FullName).ToCommaList());
            }
        }

        private static ThingComp CreateComp(Pawn pawn, CompProperties props)
        {
            var comp = (ThingComp)Activator.CreateInstance(props.compClass);
            comp.parent = pawn;
            comp.Initialize(props);
            comp.PostPostMake();
            return comp;
        }

        internal static void SetComps(Pawn pawn, List<ThingComp> comps)
        {
            if (comps == null || comps.Count == 0)
            {
                CompsField.SetValue(pawn, null);
                CompsByTypeField.SetValue(pawn, null);
            }
            else
            {
                CompsField.SetValue(pawn, comps);
                CompsByTypeField.SetValue(pawn, comps.GroupBy(c => c.GetType()).ToDictionary(g => g.Key, g => g.ToArray()));
            }
            pawn.compStyleable = pawn.GetComp<CompStyleable>();
            pawn.compForbiddable = pawn.GetComp<CompForbiddable>();
            pawn.compQuality = pawn.GetComp<CompQuality>();
        }

        private static void ClearSleepSchedule(Pawn pawn)
        {
            if (pawn.timetable == null)
            {
                return;
            }
            for (int hour = 0; hour < 24; hour++)
            {
                if (pawn.timetable.GetAssignment(hour) == TimeAssignmentDefOf.Sleep)
                {
                    pawn.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
                }
            }
        }

        private static void VerifyNeeds(Pawn pawn)
        {
            Pawn_NeedsTracker needs = pawn.needs;
            if (needs.mood == null || needs.joy == null || needs.beauty == null || needs.energy == null || needs.food != null || needs.rest != null || needs.AllNeeds.Count != 4)
            {
                Log.Error("[Mechanoid Sentience] " + pawn.ToStringSafe() + " should have exactly mood, recreation, beauty and mech energy but has: " + needs.AllNeeds.Select(n => n.def.defName).ToCommaList());
            }
        }

        private static void Rollback(Pawn pawn, ConversionState state)
        {
            try
            {
                if (state.Stage == ConversionStage.Respawned)
                {
                    return;
                }
                if (state.Stage == ConversionStage.Despawned)
                {
                    state.Restore(pawn);
                    WorkModesCompat.NotifyCompsRemoved();
                    if (!pawn.Spawned)
                    {
                        GenSpawn.Spawn(pawn, state.Cell, state.Map, state.Rotation, WipeMode.Vanish, respawningAfterLoad: false);
                    }
                }
                else
                {
                    state.RestoreComps(pawn);
                }
                if (state.Faction != pawn.Faction)
                {
                    pawn.SetFaction(state.Faction);
                }
                if (pawn.Spawned)
                {
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                    if (state.WasSelected)
                    {
                        Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Mechanoid Sentience] Rollback of " + pawn.ToStringSafe() + " also failed: " + ex);
            }
        }

        private enum ConversionStage
        {
            Preflight,
            Despawned,
            Respawned
        }

        private sealed class ConversionState
        {
            public ConversionStage Stage;
            public readonly Map Map;
            public readonly IntVec3 Cell;
            public readonly Rot4 Rotation;
            public readonly bool WasSelected;
            public readonly Faction Faction;

            private readonly ThingDef def;
            private readonly PawnKindDef kind;
            private readonly Name name;
            private readonly Pawn_SkillTracker skills;
            private readonly Pawn_StoryTracker story;
            private readonly Pawn_GuestTracker guest;
            private readonly Pawn_GuiltTracker guilt;
            private readonly Pawn_RoyaltyTracker royalty;
            private readonly Pawn_IdeoTracker ideo;
            private readonly Pawn_StyleTracker style;
            private readonly Pawn_SurroundingsTracker surroundings;
            private readonly Pawn_StyleObserverTracker styleObserver;
            private readonly Pawn_GeneTracker genes;
            private readonly Pawn_RelationsTracker relations;
            private readonly Pawn_PsychicEntropyTracker psychicEntropy;
            private readonly Pawn_WorkSettings workSettings;
            private readonly Pawn_TimetableTracker timetable;
            private readonly VerbTracker verbTracker;
            private readonly Pawn_MeleeVerbs meleeVerbs;
            private readonly List<Need> needs;
            private readonly List<ThingComp> comps;

            public ConversionState(Pawn pawn)
            {
                Map = pawn.Map;
                Cell = pawn.Position;
                Rotation = pawn.Rotation;
                WasSelected = Find.Selector.IsSelected(pawn);
                Faction = pawn.Faction;
                def = pawn.def;
                kind = pawn.kindDef;
                name = pawn.Name;
                skills = pawn.skills;
                story = pawn.story;
                guest = pawn.guest;
                guilt = pawn.guilt;
                royalty = pawn.royalty;
                ideo = pawn.ideo;
                style = pawn.style;
                surroundings = pawn.surroundings;
                styleObserver = pawn.styleObserver;
                genes = pawn.genes;
                relations = pawn.relations;
                psychicEntropy = pawn.psychicEntropy;
                workSettings = pawn.workSettings;
                timetable = pawn.timetable;
                verbTracker = pawn.verbTracker;
                meleeVerbs = pawn.meleeVerbs;
                needs = pawn.needs != null ? new List<Need>(pawn.needs.AllNeeds) : null;
                comps = CompsField.GetValue(pawn) is List<ThingComp> liveComps ? new List<ThingComp>(liveComps) : null;
            }

            public void RestoreComps(Pawn pawn)
            {
                SetComps(pawn, comps != null ? new List<ThingComp>(comps) : null);
                OverseerSubjectField.SetValue(pawn, null);
            }

            public void Restore(Pawn pawn)
            {
                pawn.def = def;
                pawn.kindDef = kind;
                RestoreComps(pawn);
                pawn.Name = name;
                pawn.skills = skills;
                pawn.story = story;
                pawn.guest = guest;
                pawn.guilt = guilt;
                pawn.royalty = royalty;
                pawn.ideo = ideo;
                pawn.style = style;
                pawn.surroundings = surroundings;
                pawn.styleObserver = styleObserver;
                pawn.genes = genes;
                pawn.relations = relations;
                pawn.psychicEntropy = psychicEntropy;
                pawn.workSettings = workSettings;
                pawn.timetable = timetable;
                pawn.verbTracker = verbTracker;
                pawn.meleeVerbs = meleeVerbs;
                if (pawn.needs != null && needs != null)
                {
                    pawn.needs.AllNeeds.Clear();
                    pawn.needs.AllNeeds.AddRange(needs);
                    pawn.needs.BindDirectNeedFields();
                }
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.Notify_DisabledWorkTypesChanged();
            }
        }
    }
}
