using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechUtility
    {
        public static readonly Color NeutralSkinColor = new Color(0.62f, 0.64f, 0.68f, 1f);

        public static bool CanBeDoneByMech(bool canBeDoneByMechs, Pawn pawn)
        {
            return canBeDoneByMechs || SentientMechRegistry.IsSentient(pawn);
        }

        public static bool IsOrdinaryMechanoid(RaceProperties race)
        {
            return race.IsMechanoid && !SentientMechRegistry.IsSentientRace(race);
        }

        public static bool IsHumanlikeVisuals(RaceProperties race)
        {
            return race.Humanlike && !SentientMechRegistry.IsSentientRace(race);
        }

        public static bool FleshOrSentient(RaceProperties race)
        {
            return race.IsFlesh || SentientMechRegistry.IsSentientRace(race);
        }

        public static List<BodyPartRecord> GetPartsWithDefOrArtificialBrain(BodyDef body, BodyPartDef def)
        {
            List<BodyPartRecord> parts = body.GetPartsWithDef(def);
            if (parts.Count > 0 || def.defName != "Brain")
            {
                return parts;
            }
            var result = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in body.GetPartsWithTag(BodyPartTagDefOf.ConsciousnessSource))
            {
                result.Add(part);
            }
            return result;
        }

        public static float BodyDrawSize(Pawn pawn)
        {
            GraphicData graphicData = pawn.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            return graphicData == null ? 1f : Mathf.Max(1f, graphicData.drawSize.x);
        }

        public static bool IsRealColonyMech(Pawn pawn)
        {
            return pawn.IsColonyMech && !SentientMechRegistry.IsSentient(pawn);
        }

        public static bool EverControllableOrSentient(Pawn pawn)
        {
            return SentientMechRegistry.IsSentient(pawn) || MechanitorUtility.EverControllable(pawn);
        }

        public static bool IsCarrierPlayerControlled(Pawn pawn)
        {
            return pawn.IsColonyMechPlayerControlled || (pawn.IsColonyMech && SentientMechRegistry.IsSentient(pawn));
        }

        public static Pawn CarrierGizmoOverseer(Pawn pawn)
        {
            if (pawn.IsColonyMech && SentientMechRegistry.IsSentient(pawn))
            {
                return pawn;
            }
            return MechanitorUtility.GetOverseer(pawn);
        }

        public static IEnumerable<CodeInstruction> ReplaceCalls(IEnumerable<CodeInstruction> instructions, MethodBase original, MethodInfo replacement, int expectedCount, string patchName)
        {
            int count = 0;
            var result = new List<CodeInstruction>();
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call) && instruction.operand as MethodBase == original)
                {
                    count++;
                    var replaced = new CodeInstruction(OpCodes.Call, replacement);
                    replaced.labels.AddRange(instruction.labels);
                    replaced.blocks.AddRange(instruction.blocks);
                    result.Add(replaced);
                }
                else
                {
                    result.Add(instruction);
                }
            }
            if (count != expectedCount)
            {
                throw new InvalidOperationException("[Mechanoid Sentience] " + patchName + ": expected " + expectedCount + " call site(s) of " + original.DeclaringType?.Name + "." + original.Name + " but found " + count + ". The game code changed, review this transpiler.");
            }
            return result;
        }
    }
}
