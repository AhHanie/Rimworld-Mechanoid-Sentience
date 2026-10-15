using System.Linq;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public class Page_ConfigureSentientStartPawns : Page_ConfigureStartingPawns
    {
        protected override bool CanDoNext()
        {
            AcceptanceReport report = RosterReport();
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            return base.CanDoNext();
        }

        private static AcceptanceReport RosterReport()
        {
            GameInitData data = Find.GameInitData;
            if (data.startingPawnCount != SentientStartScenario.RosterSize || data.startingAndOptionalPawns.Count < SentientStartScenario.RosterSize)
            {
                return "MechanoidSentience.Scenario.Error.RosterSize".Translate(SentientStartScenario.RosterSize).ToString();
            }
            var selected = data.startingAndOptionalPawns.Take(SentientStartScenario.RosterSize).ToList();
            int mechs = selected.Count(p => SentientMechRegistry.IsSentient(p));
            int humans = selected.Count(p => !SentientMechRegistry.IsSentient(p) && p.RaceProps.Humanlike);
            if (mechs != SentientStartScenario.MechCount || humans != SentientStartScenario.HumanCount)
            {
                return "MechanoidSentience.Scenario.Error.Composition".Translate(SentientStartScenario.MechCount, SentientStartScenario.HumanCount).ToString();
            }
            foreach (Pawn pawn in selected)
            {
                if (pawn.Name == null || !pawn.Name.IsValid)
                {
                    return "EveryoneNeedsValidName".Translate().ToString();
                }
                if ((pawn.DevelopmentalStage & DevelopmentalStage.Adult) == 0)
                {
                    return "MechanoidSentience.Scenario.Error.NotAdult".Translate(pawn.LabelShort).ToString();
                }
            }
            return true;
        }
    }
}
