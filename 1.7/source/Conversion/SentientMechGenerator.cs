using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public static class SentientMechGenerator
    {
        private const float SkillAge = 30f;

        private static readonly IntRange TraitCountRange = new IntRange(2, 3);
        private const int MaxTraitAttempts = 60;

        private static readonly HashSet<string> ExcludedTraitDefNames = new HashSet<string>
        {
            "Nudist", "DrugDesire", "Gourmand", "Cannibal", "CreepyBreathing", "Immunity", "Beauty", "BodyPurist",
            "Gay", "Bisexual", "Asexual", "QuickSleeper"
        };

        private static readonly SimpleCurve LevelRandomCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.5f, 150f),
            new CurvePoint(4f, 150f),
            new CurvePoint(5f, 25f),
            new CurvePoint(10f, 5f),
            new CurvePoint(15f, 0f)
        };

        private static readonly SimpleCurve AgeSkillMaxFactorCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(10f, 0.7f),
            new CurvePoint(35f, 1f),
            new CurvePoint(60f, 1.6f)
        };

        private static readonly SimpleCurve LevelFinalAdjustmentCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(10f, 10f),
            new CurvePoint(20f, 16f),
            new CurvePoint(27f, 20f)
        };

        public static void Generate(Pawn pawn)
        {
            ApplyAppearanceDefaults(pawn);
            GiveBackstories(pawn);
            GiveName(pawn);
            List<SpecialtySkill> specialty = SentientMechSpecialty.Resolve(pawn);
            GiveTraits(pawn, specialty);
            GiveSkillsAndPassions(pawn, specialty);
        }

        public static void RegenerateSkills(Pawn pawn)
        {
            GiveSkillsAndPassions(pawn, SentientMechSpecialty.Resolve(pawn));
        }

        internal static void ApplyAppearanceDefaults(Pawn pawn)
        {
            CompleteAppearance(pawn, overwrite: true);
        }

        internal static bool RepairAppearance(Pawn pawn)
        {
            return CompleteAppearance(pawn, overwrite: false);
        }

        private static bool CompleteAppearance(Pawn pawn, bool overwrite)
        {
            Pawn_StoryTracker story = pawn.story;
            if (story == null)
            {
                return false;
            }
            bool changed = false;
            DeadMansSwitchCompat.GetHumanlikeAppearanceOverrides(pawn.def, out BodyTypeDef bodyOverride, out HeadTypeDef headOverride);
            if (overwrite || story.bodyType == null)
            {
                story.bodyType = bodyOverride ?? DefDatabase<BodyTypeDef>.GetNamed(SentientDefGenerator.BodyTypeName);
                changed = true;
            }
            if (overwrite || story.hairDef == null)
            {
                story.hairDef = HairDefOf.Bald;
                changed = true;
            }
            if (overwrite)
            {
                story.skinColorOverride = SentientMechUtility.NeutralSkinColor;
                story.SkinColorBase = SentientMechUtility.NeutralSkinColor;
            }
            if (overwrite || story.headType == null)
            {
                if (headOverride != null)
                {
                    story.headType = headOverride;
                }
                else if (!story.TryGetRandomHeadFromSet(DefDatabase<HeadTypeDef>.AllDefs.Where(h => h.randomChosen)) || story.headType == null)
                {
                    story.headType = HeadTypeDefOf.Skull;
                }
                changed = true;
            }
            if (pawn.style != null)
            {
                if (overwrite || pawn.style.beardDef == null)
                {
                    pawn.style.beardDef = BeardDefOf.NoBeard;
                    changed = true;
                }
                if (overwrite || pawn.style.FaceTattoo == null || !pawn.style.FaceTattoo.noGraphic || pawn.style.BodyTattoo == null || !pawn.style.BodyTattoo.noGraphic)
                {
                    if (ModsConfig.IdeologyActive)
                    {
                        pawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                        pawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
                    }
                    else
                    {
                        pawn.style.SetupTattoos_NoIdeology();
                    }
                    changed = true;
                }
            }
            if (ModsConfig.IdeologyActive && story.favoriteColor == null)
            {
                ColorDef color = DefDatabase<ColorDef>.AllDefs.Where(c => c.colorType == ColorType.Ideo || c.colorType == ColorType.Misc).RandomElementWithFallback();
                story.favoriteColor = color;
                changed = true;
            }
            return changed;
        }

        private static void GiveBackstories(Pawn pawn)
        {
            pawn.story.Childhood = PickBackstory(BackstorySlot.Childhood);
            pawn.story.Adulthood = PickBackstory(BackstorySlot.Adulthood);
        }

        private static BackstoryDef PickBackstory(BackstorySlot slot)
        {
            BackstoryDef result = DefDatabase<BackstoryDef>.AllDefs
                .Where(b => b.slot == slot && b.spawnCategories.Contains(SentientDefGenerator.BackstoryCategory))
                .RandomElementWithFallback();
            if (result == null)
            {
                Log.Error("[Mechanoid Sentience] No " + slot + " backstory found for a sentient mechanoid, falling back to any shuffleable one.");
                result = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == slot).RandomElement();
            }
            return result;
        }

        private static void GiveName(Pawn pawn)
        {
            Name name = PawnBioAndNameGenerator.GeneratePawnName(pawn, NameStyle.Full);
            if (name == null || !name.IsValid)
            {
                name = PawnBioAndNameGenerator.GeneratePawnName(pawn, NameStyle.Numeric);
            }
            pawn.Name = name;
        }

        private static void GiveTraits(Pawn pawn, List<SpecialtySkill> specialty)
        {
            TraitSet traits = pawn.story.traits;
            foreach (BackstoryDef backstory in pawn.story.AllBackstories)
            {
                if (backstory?.forcedTraits == null)
                {
                    continue;
                }
                foreach (BackstoryTrait forced in backstory.forcedTraits)
                {
                    if (forced.def != null && !traits.HasTrait(forced.def))
                    {
                        traits.GainTrait(new Trait(forced.def, forced.degree));
                    }
                }
            }

            int target = TraitCountRange.RandomInRange;
            for (int attempt = 0; attempt < MaxTraitAttempts && traits.allTraits.Count < target; attempt++)
            {
                Trait candidate = PawnGenerator.GenerateTraitsFor(pawn, 1).FirstOrDefault();
                if (candidate != null && IsTraitAllowed(candidate, specialty))
                {
                    traits.GainTrait(candidate);
                }
            }
        }

        private static bool IsTraitAllowed(Trait trait, List<SpecialtySkill> specialty)
        {
            if (ExcludedTraitDefNames.Contains(trait.def.defName))
            {
                return false;
            }
            foreach (SpecialtySkill skill in specialty)
            {
                if (trait.def.ConflictsWithPassion(skill.Skill))
                {
                    return false;
                }
            }
            if (trait.def.defName == "PsychicSensitivity" && trait.Degree <= -2)
            {
                return false;
            }
            return trait.GetDisabledWorkTypes().Count == 0 && trait.def.disabledWorkTags == WorkTags.None;
        }

        private static void GiveSkillsAndPassions(Pawn pawn, List<SpecialtySkill> specialty)
        {
            foreach (SkillDef skillDef in DefDatabase<SkillDef>.AllDefsListForReading)
            {
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                record.passion = Passion.None;
                int specialtyIndex = specialty.FindIndex(s => s.Skill == skillDef);
                record.Level = specialtyIndex >= 0 ? specialty[specialtyIndex].Level : Mathf.Clamp(FinalLevelOfSkill(pawn, skillDef), 0, 20);
            }

            int minorPassions = 0;
            int majorPassions = 0;
            float budget = 5f + Mathf.Clamp(Rand.Gaussian(), -4f, 4f);
            while (budget >= 1f)
            {
                if (budget >= 1.5f && Rand.Bool)
                {
                    majorPassions++;
                    budget -= 1.5f;
                }
                else
                {
                    minorPassions++;
                    budget -= 1f;
                }
            }

            void CreatePassion(SkillRecord record, bool force)
            {
                if (majorPassions > 0)
                {
                    record.passion = Passion.Major;
                    majorPassions--;
                }
                else if (minorPassions > 0 || force)
                {
                    record.passion = Passion.Minor;
                    minorPassions--;
                }
            }

            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill.TotallyDisabled)
                {
                    continue;
                }
                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    if (trait.def.RequiresPassion(skill.def))
                    {
                        CreatePassion(skill, force: true);
                    }
                }
            }

            void Pay(Passion passion)
            {
                if (passion == Passion.Major && majorPassions > 0)
                {
                    majorPassions--;
                }
                else if (minorPassions > 0)
                {
                    minorPassions--;
                }
                else if (majorPassions > 0)
                {
                    majorPassions--;
                }
            }

            foreach (SpecialtySkill special in specialty)
            {
                SkillRecord skill = pawn.skills.GetSkill(special.Skill);
                if (skill.TotallyDisabled || skill.passion != Passion.None || ConflictsWithPassion(pawn, skill.def))
                {
                    continue;
                }
                skill.passion = special.Primary ? Passion.Major : Passion.Minor;
                Pay(skill.passion);
            }

            foreach (SkillRecord skill in pawn.skills.skills.OrderByDescending(s => s.GetLevel(includeAptitudes: false)))
            {
                if (skill.TotallyDisabled || skill.passion != Passion.None)
                {
                    continue;
                }
                bool conflicts = false;
                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    if (trait.def.ConflictsWithPassion(skill.def))
                    {
                        conflicts = true;
                        break;
                    }
                }
                if (!conflicts)
                {
                    CreatePassion(skill, force: false);
                }
            }
        }

        private static bool ConflictsWithPassion(Pawn pawn, SkillDef skill)
        {
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait.def.ConflictsWithPassion(skill))
                {
                    return true;
                }
            }
            return false;
        }

        private static int FinalLevelOfSkill(Pawn pawn, SkillDef skill)
        {
            float level = skill.usuallyDefinedInBackstories ? Rand.RangeInclusive(0, 4) : Rand.ByCurve(LevelRandomCurve);
            foreach (BackstoryDef backstory in pawn.story.AllBackstories)
            {
                if (backstory == null)
                {
                    continue;
                }
                foreach (SkillGain gain in backstory.skillGains)
                {
                    if (gain.skill == skill)
                    {
                        level += gain.amount * Rand.Range(1f, 1.4f);
                    }
                }
            }
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait.Suppressed || trait.CurrentData.skillGains.NullOrEmpty())
                {
                    continue;
                }
                foreach (SkillGain gain in trait.CurrentData.skillGains)
                {
                    if (gain.skill == skill)
                    {
                        level += gain.amount;
                        break;
                    }
                }
            }
            level *= Rand.Range(1f, AgeSkillMaxFactorCurve.Evaluate(SkillAge));
            level = LevelFinalAdjustmentCurve.Evaluate(level);
            if (level > 0f)
            {
                level += pawn.kindDef.extraSkillLevels;
            }
            return Mathf.RoundToInt(level);
        }
    }
}
