using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Mechanoid_Sentience
{
    public class JobDriver_ApplyMechSentienceCatalyst : JobDriver
    {
        private const TargetIndex MechInd = TargetIndex.A;
        private const TargetIndex ItemInd = TargetIndex.B;
        private const int DurationTicks = 600;

        private string rejectReason;
        private bool applied;

        private Pawn Mech => job.GetTarget(MechInd).Thing as Pawn;

        private Thing Item => job.GetTarget(ItemInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed) && pawn.Reserve(Item, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOn(() =>
            {
                if (applied)
                {
                    return false;
                }
                AcceptanceReport report = SentientMechConverter.CanConvert(Mech);
                if (report.Accepted)
                {
                    return false;
                }
                rejectReason = report.Reason;
                return true;
            });
            AddFinishAction(condition =>
            {
                if (condition != JobCondition.Succeeded && !rejectReason.NullOrEmpty() && pawn.IsColonistPlayerControlled)
                {
                    Messages.Message(rejectReason, MessageTypeDefOf.RejectInput, historical: false);
                }
            });

            yield return Toils_Goto.GotoThing(ItemInd, PathEndMode.Touch).FailOnDespawnedOrNull(ItemInd).FailOnDespawnedOrNull(MechInd);
            yield return Toils_Haul.StartCarryThing(ItemInd);
            yield return Toils_Goto.GotoThing(MechInd, PathEndMode.Touch).FailOnDespawnedOrNull(MechInd);
            Toil wait = Toils_General.WaitWith(MechInd, DurationTicks, useProgressBar: false, maintainPosture: true, maintainSleep: false, MechInd);
            wait.WithProgressBarToilDelay(MechInd);
            wait.FailOnDespawnedOrNull(MechInd);
            wait.FailOnCannotTouch(MechInd, PathEndMode.Touch);
            yield return wait;
            yield return Toils_General.Do(Apply);
        }

        private void Apply()
        {
            Pawn mech = Mech;
            Thing catalyst = pawn.carryTracker.CarriedThing;
            if (catalyst == null || catalyst.def != MS_ThingDefOf.MS_MechSentienceCatalyst)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (!SentientMechConverter.TryConvert(mech, showMessages: true))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            applied = true;
            SoundDef sound = catalyst.TryGetComp<CompTargetEffect_MakeMechSentient>()?.Props.soundOnUsed;
            if (sound != null && mech.Spawned && mech.Map == Find.CurrentMap)
            {
                sound.PlayOneShot(SoundInfo.InMap(mech));
            }
            catalyst.SplitOff(1).Destroy();
        }
    }
}
