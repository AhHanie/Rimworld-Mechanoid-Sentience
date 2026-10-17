using System;
using System.Collections.Generic;
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

        public static bool IsOrdinaryMechanoid(bool isMechanoid, RaceProperties race)
        {
            return isMechanoid && !SentientMechRegistry.IsSentientRace(race);
        }

        public static bool IsHumanlikeVisuals(bool humanlike, RaceProperties race)
        {
            return humanlike && !SentientMechRegistry.IsSentientRace(race);
        }

        public static bool FleshOrSentient(bool isFlesh, RaceProperties race)
        {
            return isFlesh || SentientMechRegistry.IsSentientRace(race);
        }

        public static List<BodyPartRecord> GetPartsWithDefOrArtificialBrain(List<BodyPartRecord> parts, BodyDef body, BodyPartDef def)
        {
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

        public static bool IsRealColonyMech(bool isColonyMech, Pawn pawn)
        {
            return isColonyMech && !SentientMechRegistry.IsSentient(pawn);
        }

        [ThreadStatic]
        private static int verbCommandDepth;

        public static void EnterVerbCommand()
        {
            verbCommandDepth++;
        }

        public static void ExitVerbCommand()
        {
            if (verbCommandDepth > 0)
            {
                verbCommandDepth--;
            }
        }

        public static bool EverControllableOrSentient(bool everControllable, Pawn pawn)
        {
            return everControllable || (verbCommandDepth > 0 && SentientMechRegistry.IsSentient(pawn));
        }

        public static bool IsPlayerControlledOrSentient(bool playerControlled, Pawn pawn)
        {
            return playerControlled || (pawn.IsColonyMech && SentientMechRegistry.IsSentient(pawn));
        }

        public static bool IsLockedWeapon(Pawn pawn, Thing weapon)
        {
            if (weapon == null || pawn == null || pawn.Dead || !SentientMechRegistry.IsSentient(pawn))
            {
                return false;
            }
            return weapon.def.destroyOnDrop || SharesWeaponTag(pawn.kindDef.weaponTags, weapon.def.weaponTags);
        }

        private static bool SharesWeaponTag(List<string> kindTags, List<string> weaponTags)
        {
            if (kindTags == null || weaponTags == null)
            {
                return false;
            }
            for (int i = 0; i < weaponTags.Count; i++)
            {
                if (kindTags.Contains(weaponTags[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static ThingWithComps LockedPrimary(Pawn pawn)
        {
            ThingWithComps primary = pawn?.equipment?.Primary;
            return IsLockedWeapon(pawn, primary) ? primary : null;
        }

        public static string CannotSwapReason(Pawn pawn, Thing weapon)
        {
            return "MechanoidSentience.CannotSwapIntegratedWeapon".Translate(weapon.LabelNoCount, pawn.LabelShort);
        }

        public static string CannotDropReason(Pawn pawn, Thing weapon)
        {
            return "MechanoidSentience.CannotDropIntegratedWeapon".Translate(weapon.LabelNoCount, pawn.LabelShort);
        }

        public static Pawn CarrierGizmoOverseer(Pawn overseer, Pawn pawn)
        {
            if (pawn.IsColonyMech && SentientMechRegistry.IsSentient(pawn))
            {
                return pawn;
            }
            return overseer;
        }
    }
}
