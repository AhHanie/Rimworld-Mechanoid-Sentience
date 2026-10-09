using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public class CompProperties_Targetable_Mechanoid : CompProperties_Targetable
    {
        public CompProperties_Targetable_Mechanoid()
        {
            compClass = typeof(CompTargetable_Mechanoid);
        }
    }

    public class CompTargetable_Mechanoid : CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;

        protected override TargetingParameters GetTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetAnimals = true,
                canTargetHumans = false,
                canTargetMechs = true,
                validator = target => target.Thing is Pawn pawn && SentientMechConverter.CanConvert(pawn).Accepted
            };
        }

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            if (targetChosenByPlayer != null)
            {
                yield return targetChosenByPlayer;
            }
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!target.HasThing || !(target.Thing is Pawn pawn))
            {
                return false;
            }
            if (CasterPawn != null && pawn.Map != CasterPawn.Map)
            {
                return false;
            }
            AcceptanceReport report = SentientMechConverter.CanConvert(pawn);
            if (!report.Accepted)
            {
                if (showMessages && !report.Reason.NullOrEmpty())
                {
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }
    }
}
