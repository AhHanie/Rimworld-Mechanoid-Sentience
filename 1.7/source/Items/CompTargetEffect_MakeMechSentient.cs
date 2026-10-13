using RimWorld;
using Verse;
using Verse.AI;

namespace Mechanoid_Sentience
{
    public class CompProperties_TargetEffect_MakeMechSentient : CompProperties
    {
        public SoundDef soundOnUsed;

        public CompProperties_TargetEffect_MakeMechSentient()
        {
            compClass = typeof(CompTargetEffect_MakeMechSentient);
        }
    }

    public class CompTargetEffect_MakeMechSentient : CompTargetEffect
    {
        public CompProperties_TargetEffect_MakeMechSentient Props => (CompProperties_TargetEffect_MakeMechSentient)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (!user.IsColonistPlayerControlled)
            {
                return;
            }
            if (!(target is Pawn mech))
            {
                return;
            }
            AcceptanceReport report = SentientMechConverter.CanConvert(mech);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Job job = JobMaker.MakeJob(MS_JobDefOf.MS_ApplyMechSentienceCatalyst, mech, parent);
            job.count = 1;
            job.playerForced = true;
            if (!user.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                Messages.Message("MechanoidSentience.CatalystCannotStart".Translate(user.LabelShort, mech.LabelShort), user, MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }
}
