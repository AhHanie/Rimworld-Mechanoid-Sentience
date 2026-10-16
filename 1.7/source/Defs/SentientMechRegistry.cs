using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechRegistry
    {
        public const string DefPrefix = "MS_Sentient_";

        private static readonly Dictionary<ThingDef, ThingDef> raceToSentient = new Dictionary<ThingDef, ThingDef>();
        private static readonly Dictionary<ThingDef, ThingDef> sentientToRace = new Dictionary<ThingDef, ThingDef>();
        private static readonly Dictionary<PawnKindDef, PawnKindDef> kindToSentient = new Dictionary<PawnKindDef, PawnKindDef>();
        private static readonly HashSet<ThingDef> sentientRaces = new HashSet<ThingDef>();
        private static readonly HashSet<RaceProperties> sentientRaceProps = new HashSet<RaceProperties>();
        private static readonly HashSet<PawnKindDef> sentientKinds = new HashSet<PawnKindDef>();
        private static readonly List<ThingDef> sentientRaceList = new List<ThingDef>();
        private static ThingDef[] sentientRacesByIndex;
        private static bool frozen;

        public static IReadOnlyList<ThingDef> SentientRaces => sentientRaceList;

        public static bool IsSentient(Pawn pawn)
        {
            return pawn != null && ContainsSentientRace(pawn.def);
        }

        public static bool IsSentient(Thing thing)
        {
            return thing != null && ContainsSentientRace(thing.def);
        }

        public static bool IsSentientRace(ThingDef def)
        {
            return def != null && ContainsSentientRace(def);
        }

        private static bool ContainsSentientRace(ThingDef def)
        {
            ThingDef[] table = sentientRacesByIndex;
            if (table == null)
            {
                return sentientRaces.Contains(def);
            }
            return def.index < table.Length && ReferenceEquals(table[def.index], def);
        }

        public static bool IsSentientRace(RaceProperties race)
        {
            return race != null && sentientRaceProps.Contains(race);
        }

        public static bool IsSentientKind(PawnKindDef kind)
        {
            return kind != null && sentientKinds.Contains(kind);
        }

        public static bool TryGetSentient(ThingDef originalRace, PawnKindDef originalKind, out ThingDef sentientRace, out PawnKindDef sentientKind)
        {
            sentientKind = null;
            if (originalRace == null || originalKind == null || !raceToSentient.TryGetValue(originalRace, out sentientRace))
            {
                sentientRace = null;
                return false;
            }
            return kindToSentient.TryGetValue(originalKind, out sentientKind);
        }

        public static T OriginalOf<T>(T sentientDef) where T : Def
        {
            string originalName = sentientDef?.GetModExtension<SentientMechExtension>()?.originalDefName;
            return originalName == null ? null : DefDatabase<T>.GetNamedSilentFail(originalName);
        }

        public static ThingDef OriginalRaceOf(ThingDef sentientRace)
        {
            return sentientRace != null && sentientToRace.Count > 0 && sentientToRace.TryGetValue(sentientRace, out ThingDef original) ? original : null;
        }

        internal static void Register(ThingDef original, ThingDef sentient)
        {
            if (frozen)
            {
                Log.Error("[Mechanoid Sentience] Tried to register a sentient race after the registry was frozen.");
                return;
            }
            raceToSentient[original] = sentient;
            sentientToRace[sentient] = original;
            sentientRaces.Add(sentient);
            sentientRaceProps.Add(sentient.race);
            sentientRaceList.Add(sentient);
        }

        internal static void Register(PawnKindDef original, PawnKindDef sentient)
        {
            if (frozen)
            {
                Log.Error("[Mechanoid Sentience] Tried to register a sentient kind after the registry was frozen.");
                return;
            }
            kindToSentient[original] = sentient;
            sentientKinds.Add(sentient);
        }

        internal static void Freeze()
        {
            BuildIndexTable();
            frozen = true;
        }

        internal static void EnsureIndexTableCurrent()
        {
            if (!frozen || sentientRaceList.Count == 0)
            {
                return;
            }
            ThingDef[] table = sentientRacesByIndex;
            if (table != null)
            {
                bool current = true;
                foreach (ThingDef def in sentientRaceList)
                {
                    if (def.index >= table.Length || !ReferenceEquals(table[def.index], def))
                    {
                        current = false;
                        break;
                    }
                }
                if (current)
                {
                    return;
                }
            }
            BuildIndexTable();
        }

        private static void BuildIndexTable()
        {
            sentientRacesByIndex = null;
            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            var table = new ThingDef[allDefs.Count];
            foreach (ThingDef def in sentientRaceList)
            {
                int index = def.index;
                if (index >= table.Length || !ReferenceEquals(allDefs[index], def) || table[index] != null)
                {
                    Log.Error("[Mechanoid Sentience] Sentient race " + def.defName + " has an invalid def index (" + index + "), using the slower hash set lookup.");
                    return;
                }
                table[index] = def;
            }
            sentientRacesByIndex = table;
        }

        internal static void Reset()
        {
            sentientRacesByIndex = null;
            raceToSentient.Clear();
            sentientToRace.Clear();
            kindToSentient.Clear();
            sentientRaces.Clear();
            sentientRaceProps.Clear();
            sentientKinds.Clear();
            sentientRaceList.Clear();
            frozen = false;
        }
    }
}
