using System.Linq;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientStartScenario
    {
        public const int MechCount = 2;
        public const int HumanCount = 1;
        public const int RosterSize = MechCount + HumanCount;

        public static bool IsActive
        {
            get
            {
                return Current.Game != null && Find.GameInitData != null && Find.Scenario != null
                    && Find.Scenario.AllParts.Any(p => p is ScenPart_ConfigPage_SentientStart);
            }
        }

        public static bool TryGetMechSlot(int index, out PawnKindDef sentientKind)
        {
            sentientKind = null;
            if (index < 0 || index >= RosterSize || !IsActive)
            {
                return false;
            }
            PawnKindDef kind = StartingPawnUtility.GetGenerationRequest(index).KindDef;
            if (!SentientMechRegistry.IsSentientKind(kind))
            {
                return false;
            }
            sentientKind = kind;
            return true;
        }
    }
}
