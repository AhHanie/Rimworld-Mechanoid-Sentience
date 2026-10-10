using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public enum DmsRaceKind
    {
        None,

        Automatroid,

        HumanlikeAutomatroid,

        PlatformDrone
    }

    public static class DeadMansSwitchCompat
    {
        public const string FffPackageId = "AOBA.Framework";
        public const string DmsPackageId = "Aoba.DeadManSwitch.Core";

        public const string DeadManSwitchPropsName = "Fortified.CompProperties_DeadManSwitch";
        public const string DeadManSwitchCompName = "Fortified.CompDeadManSwitch";
        public const string InteractePropsName = "Fortified.CompPropertiesInteracte";
        public const string InteracteCompName = "Fortified.CompInteracte";
        public const string MechApparelPropsName = "Fortified.CompProperties_MechApparel";
        public const string MechApparelCompName = "Fortified.CompMechApparel";
        public const string DronePropsName = "Fortified.CompProperties_Drone";
        public const string DroneCompName = "Fortified.CompDrone";
        public const string HumanlikeMechInterfaceName = "Fortified.IHumanlikeMech";

        private const string GameComponentName = "Fortified.GameComponent_DMS";
        private const string OutgoingMechName = "Fortified.OutgoingMech";
        private const string WeaponUsableMechName = "Fortified.WeaponUsableMech";
        private const string CachedDeadManSwitchFieldName = "cachedDeadManSwitch";

        private static readonly Dictionary<ThingDef, DmsRaceKind> originalKinds = new Dictionary<ThingDef, DmsRaceKind>();
        private static readonly Dictionary<ThingDef, DmsRaceKind> sentientKinds = new Dictionary<ThingDef, DmsRaceKind>();
        private static readonly List<string> summary = new List<string>();

        private static bool? available;
        private static Type deadManSwitchCompType;
        private static Type droneCompType;
        private static Type gameComponentType;
        private static FieldInfo outgoingField;
        private static MethodInfo outgoingMechesGetter;
        private static FieldInfo outgoingMechField;
        private static MethodBase weaponUsableMechSpawnSetup;

        public static string BindFailure { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = ModsConfig.IsActive(FffPackageId);
                    available = active;
                    if (active)
                    {
                        Bind();
                        if (BindFailure != null)
                        {
                            Log.Warning("[Mechanoid Sentience] Fortified Features Framework is active but part of its API changed (" + BindFailure + "). The matching Dead Man's Switch protections are disabled.");
                        }
                    }
                }
                return available.Value;
            }
        }

        public static MethodBase WeaponUsableMechSpawnSetup => IsAvailable ? weaponUsableMechSpawnSetup : null;

        public static void Bind()
        {
            var failures = new List<string>();
            deadManSwitchCompType = AccessTools.TypeByName(DeadManSwitchCompName);
            droneCompType = AccessTools.TypeByName(DroneCompName);
            gameComponentType = AccessTools.TypeByName(GameComponentName);
            Type outgoingMechType = AccessTools.TypeByName(OutgoingMechName);
            Type weaponUsableMech = AccessTools.TypeByName(WeaponUsableMechName);

            outgoingField = deadManSwitchCompType == null ? null : AccessTools.Field(deadManSwitchCompType, "outgoing");
            if (outgoingField == null || outgoingField.FieldType != typeof(bool))
            {
                outgoingField = null;
                failures.Add("CompDeadManSwitch.outgoing");
            }
            outgoingMechesGetter = gameComponentType == null ? null : AccessTools.PropertyGetter(gameComponentType, "OutgoingMeches");
            outgoingMechField = outgoingMechType == null ? null : AccessTools.Field(outgoingMechType, "mech");
            if (outgoingMechesGetter == null || outgoingMechField == null)
            {
                outgoingMechesGetter = null;
                outgoingMechField = null;
                failures.Add("GameComponent_DMS.OutgoingMeches");
            }
            if (droneCompType == null)
            {
                failures.Add("CompDrone");
            }
            weaponUsableMechSpawnSetup = weaponUsableMech == null ? null : AccessTools.DeclaredMethod(weaponUsableMech, "SpawnSetup", new[] { typeof(Map), typeof(bool) });
            if (weaponUsableMechSpawnSetup == null)
            {
                failures.Add("WeaponUsableMech.SpawnSetup(Map, bool)");
            }
            BindFailure = failures.Count == 0 ? null : string.Join(", ", failures) + " not found";
        }

        public static DmsRaceKind ClassifyRace(ThingDef race)
        {
            if (race?.comps == null)
            {
                return DmsRaceKind.None;
            }
            bool drone = false;
            bool ownership = false;
            foreach (CompProperties props in race.comps)
            {
                CompSignature signature = CompSignature.Of(props);
                if (Matches(signature, DronePropsName, DroneCompName))
                {
                    drone = true;
                }
                else if (Matches(signature, DeadManSwitchPropsName, DeadManSwitchCompName) || Matches(signature, InteractePropsName, InteracteCompName))
                {
                    ownership = true;
                }
            }
            if (drone)
            {
                return DmsRaceKind.PlatformDrone;
            }
            if (!ownership)
            {
                return DmsRaceKind.None;
            }
            return ImplementsHumanlikeMech(race.thingClass) ? DmsRaceKind.HumanlikeAutomatroid : DmsRaceKind.Automatroid;
        }

        private static bool ImplementsHumanlikeMech(Type thingClass)
        {
            return thingClass != null && thingClass.GetInterfaces().Any(i => i.FullName == HumanlikeMechInterfaceName);
        }

        private static bool Matches(CompSignature signature, string propsName, string compName)
        {
            return signature.PropsType != null && signature.CompClass != null && signature.PropsType.FullName == propsName && signature.CompClass.FullName == compName;
        }

        public static bool IsOwnershipComp(CompSignature signature)
        {
            return Matches(signature, DeadManSwitchPropsName, DeadManSwitchCompName)
                || Matches(signature, InteractePropsName, InteracteCompName)
                || Matches(signature, MechApparelPropsName, MechApparelCompName);
        }

        public static bool IsRemovedFromSentient(DmsRaceKind kind, CompSignature signature)
        {
            switch (kind)
            {
                case DmsRaceKind.Automatroid:
                    return Matches(signature, DeadManSwitchPropsName, DeadManSwitchCompName) || Matches(signature, InteractePropsName, InteracteCompName);
                case DmsRaceKind.HumanlikeAutomatroid:
                    return IsOwnershipComp(signature);
                default:
                    return false;
            }
        }

        internal static void Reset()
        {
            originalKinds.Clear();
            sentientKinds.Clear();
            summary.Clear();
        }

        internal static void Record(ThingDef original, ThingDef sentient, DmsRaceKind kind)
        {
            if (kind == DmsRaceKind.None)
            {
                return;
            }
            originalKinds[original] = kind;
            sentientKinds[sentient] = kind;
            summary.Add(original.defName + " (" + (original.modContentPack?.Name ?? "unknown mod") + ") -> " + sentient.defName + ": " + kind);
        }

        public static DmsRaceKind KindOfOriginal(ThingDef original)
        {
            return original != null && originalKinds.Count > 0 && originalKinds.TryGetValue(original, out DmsRaceKind kind) ? kind : DmsRaceKind.None;
        }

        public static DmsRaceKind KindOfSentient(ThingDef sentient)
        {
            return sentient != null && sentientKinds.Count > 0 && sentientKinds.TryGetValue(sentient, out DmsRaceKind kind) ? kind : DmsRaceKind.None;
        }

        internal static void LogSummary()
        {
            if (summary.Count > 0)
            {
                Logger.Message("Dead Man's Switch / FFF races (" + summary.Count + "):\n" + string.Join("\n", summary));
            }
        }

        public static IEnumerable<string> CompositionFindings(ThingDef original, DmsRaceKind kind)
        {
            if (original?.comps == null || kind == DmsRaceKind.None)
            {
                yield break;
            }
            List<CompSignature> signatures = original.comps.Select(CompSignature.Of).ToList();
            bool hasSwitch = signatures.Any(s => Matches(s, DeadManSwitchPropsName, DeadManSwitchCompName));
            bool hasInteracte = signatures.Any(s => Matches(s, InteractePropsName, InteracteCompName));
            if (kind == DmsRaceKind.PlatformDrone)
            {
                if (hasSwitch || hasInteracte)
                {
                    yield return "a platform drone also carries the dead man's switch comps; it stays unconvertible";
                }
                yield break;
            }
            if (hasInteracte && !hasSwitch)
            {
                yield return "has the interaction comp without the dead man's switch comp it depends on";
            }
        }

        public static bool IsPlatformDrone(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (KindOfOriginal(pawn.def) == DmsRaceKind.PlatformDrone)
            {
                return true;
            }
            if (!IsAvailable || droneCompType == null)
            {
                return false;
            }
            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (droneCompType.IsInstanceOfType(comps[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsLeaving(Pawn pawn)
        {
            if (pawn == null || !IsAvailable || deadManSwitchCompType == null)
            {
                return false;
            }
            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (deadManSwitchCompType.IsInstanceOfType(comps[i]))
                {
                    if (outgoingField != null && (bool)outgoingField.GetValue(comps[i]))
                    {
                        return true;
                    }
                    return IsRegisteredOutgoing(pawn);
                }
            }
            return false;
        }

        private static bool IsRegisteredOutgoing(Pawn pawn)
        {
            if (gameComponentType == null || outgoingMechesGetter == null || outgoingMechField == null || Current.Game == null)
            {
                return false;
            }
            object component = Current.Game.GetComponent(gameComponentType);
            if (component == null || !(outgoingMechesGetter.Invoke(component, null) is IEnumerable entries))
            {
                return false;
            }
            foreach (object entry in entries)
            {
                if (entry != null && ReferenceEquals(outgoingMechField.GetValue(entry), pawn))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<ThingComp> WithoutOwnershipComps(IList<ThingComp> live, DmsRaceKind kind, out List<ThingComp> removed)
        {
            removed = new List<ThingComp>();
            var kept = new List<ThingComp>();
            for (int i = 0; i < (live?.Count ?? 0); i++)
            {
                ThingComp comp = live[i];
                if (comp?.props != null && IsRemovedFromSentient(kind, CompSignature.Of(comp.props)))
                {
                    removed.Add(comp);
                }
                else
                {
                    kept.Add(comp);
                }
            }
            return kept;
        }

        public static void ClearCachedDeadManSwitch(Pawn pawn)
        {
            AccessTools.Field(pawn.GetType(), CachedDeadManSwitchFieldName)?.SetValue(pawn, null);
        }

        internal static void EnsureCharacterTabs(ThingDef sentientRace)
        {
            List<Type> tabs = sentientRace.inspectorTabs;
            if (tabs == null)
            {
                return;
            }
            if (!tabs.Contains(typeof(ITab_Pawn_Character)))
            {
                int needs = tabs.IndexOf(typeof(ITab_Pawn_Needs));
                tabs.Insert(needs >= 0 ? needs + 1 : tabs.Count, typeof(ITab_Pawn_Character));
            }
            if (!tabs.Contains(typeof(ITab_Pawn_Social)))
            {
                int before = tabs.IndexOf(typeof(ITab_Pawn_Gear));
                if (before < 0)
                {
                    before = tabs.IndexOf(typeof(ITab_Pawn_Log));
                }
                tabs.Insert(before >= 0 ? before : tabs.Count, typeof(ITab_Pawn_Social));
            }
        }

        public static bool IsSentientHumanlikeMech(Pawn pawn)
        {
            return pawn != null && KindOfSentient(pawn.def) == DmsRaceKind.HumanlikeAutomatroid;
        }
    }

    public static class SkillLevelGuard
    {
        public static int[] Capture(IList<SkillRecord> skills)
        {
            if (skills == null || skills.Count == 0)
            {
                return null;
            }
            var levels = new int[skills.Count];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = skills[i].levelInt;
            }
            return levels;
        }

        public static void Restore(IList<SkillRecord> skills, int[] levels)
        {
            if (levels == null || skills == null || skills.Count != levels.Length)
            {
                return;
            }
            for (int i = 0; i < levels.Length; i++)
            {
                skills[i].levelInt = levels[i];
            }
        }
    }
}
