using LudeonTK;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class DebugActions
    {
        [DebugAction("Pawns", "Make mechanoid sentient", requiresBiotech: true, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeMechanoidSentient(Pawn pawn)
        {
            SentientMechConverter.TryConvert(pawn);
        }

        [DebugAction("Pawns", "Re-roll sentient mech skills", requiresBiotech: true, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RerollSentientMechSkills(Pawn pawn)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                Messages.Message(pawn.LabelShort + " is not a sentient mechanoid.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            SentientMechGenerator.RegenerateSkills(pawn);
        }

        [DebugAction("Pawns", "Make mechanoid sentient (force rollback)", requiresBiotech: true, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, hideInSubMenu = true)]
        private static void MakeMechanoidSentientForceRollback(Pawn pawn)
        {
            SentientMechConverter.ForceFailureForTesting = true;
            try
            {
                SentientMechConverter.TryConvert(pawn);
            }
            finally
            {
                SentientMechConverter.ForceFailureForTesting = false;
            }
        }
    }
}
