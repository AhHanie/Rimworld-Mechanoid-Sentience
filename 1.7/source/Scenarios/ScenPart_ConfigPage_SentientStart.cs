using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public class ScenPart_ConfigPage_SentientStart : ScenPart_ConfigPage_ConfigureStartingPawnsBase
    {
        private const int MaxRosterAttempts = 20;

        public ScenPart_ConfigPage_SentientStart()
        {
            pawnChoiceCount = SentientStartScenario.RosterSize;
        }

        protected override int TotalPawnCount => SentientStartScenario.RosterSize;

        public override string Summary(Scenario scen)
        {
            return "MechanoidSentience.Scenario.Summary".Translate();
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect rect = listing.GetScenPartRect(this, ScenPart.RowHeight);
            using (new TextBlock(Color.gray))
            {
                Widgets.Label(rect, "MechanoidSentience.Scenario.FixedRoster".Translate());
            }
        }

        public override bool Valid()
        {
            return base.Valid() && pawnChoiceCount == SentientStartScenario.RosterSize;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (pawnChoiceCount != SentientStartScenario.RosterSize)
            {
                yield return "MechanoidSentience.Scenario.Error.RosterSize".Translate(SentientStartScenario.RosterSize).ToString();
            }
            if (ModsConfig.BiotechActive && SentientMechStartingPawnFactory.StarterKinds().Count == 0)
            {
                yield return "MechanoidSentience.Scenario.Error.NoMechTypes".Translate().ToString();
            }
        }

        public override void PostIdeoChosen()
        {
            GameInitData data = Find.GameInitData;
            data.allowedDevelopmentalStages = DevelopmentalStage.Adult;
            data.startingPawnKind = null;
            data.startingPawnsRequired = null;
            data.startingXenotypesRequired = null;
            data.startingMutantsRequired = null;
            data.startingSkillsRequired = null;
            base.PostIdeoChosen();
        }

        protected override void GenerateStartingPawns()
        {
            if (SentientMechStartingPawnFactory.StarterKinds().Count == 0)
            {
                Log.Error("[Mechanoid Sentience] No sentient mechanoid types are available for the sentient start; the roster cannot be created.");
                return;
            }

            int attempt = 0;
            do
            {
                StartingPawnUtility.ClearAllStartingPawns();
                for (int slot = 0; slot < SentientStartScenario.RosterSize; slot++)
                {
                    PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(slot);
                    if (slot < SentientStartScenario.MechCount)
                    {
                        request.KindDef = SentientMechStartingPawnFactory.ChooseKind(null);
                        request.ForcedXenotype = null;
                        request.ForcedCustomXenotype = null;
                    }
                    else
                    {
                        request.KindDef = Faction.OfPlayer.def.basicMemberKind;
                    }
                    StartingPawnUtility.SetGenerationRequest(slot, request);
                    StartingPawnUtility.AddNewPawn(slot);
                }
                attempt++;
            }
            while (attempt <= MaxRosterAttempts && !StartingPawnUtility.WorkTypeRequirementsSatisfied());
        }
    }
}
