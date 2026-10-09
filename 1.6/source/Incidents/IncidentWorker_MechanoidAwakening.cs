using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public class IncidentWorker_MechanoidAwakening : IncidentWorker
    {
        private static readonly List<Pawn> CandidateBuffer = new List<Pawn>();

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map && AnyCandidate(map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }

            GatherCandidates(map, CandidateBuffer);
            Pawn pawn;
            try
            {
                if (!CandidateBuffer.TryRandomElement(out pawn))
                {
                    return false;
                }
            }
            finally
            {
                CandidateBuffer.Clear();
            }

            string originalLabel = pawn.LabelShort;
            if (!SentientMechConverter.TryConvert(pawn, showMessages: false))
            {
                return false;
            }

            SendStandardLetter(
                baseLetterLabel: "MechanoidSentience.AwakeningLetterLabel".Translate(),
                baseLetterText: "MechanoidSentience.AwakeningLetterText".Translate(originalLabel, pawn.Named("PAWN")),
                baseLetterDef: LetterDefOf.PositiveEvent,
                parms: parms,
                lookTargets: pawn);
            return true;
        }

        private static bool AnyCandidate(Map map)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsCandidate(pawns[i], map))
                {
                    return true;
                }
            }
            return false;
        }

        private static void GatherCandidates(Map map, List<Pawn> result)
        {
            result.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsCandidate(pawns[i], map))
                {
                    result.Add(pawns[i]);
                }
            }
        }

        private static bool IsCandidate(Pawn pawn, Map map)
        {
            return pawn.Spawned
                && pawn.Map == map
                && pawn.Faction == Faction.OfPlayer
                && pawn.RaceProps.IsMechanoid
                && !SentientMechRegistry.IsSentient(pawn)
                && SentientMechConverter.CanConvert(pawn).Accepted;
        }
    }
}
