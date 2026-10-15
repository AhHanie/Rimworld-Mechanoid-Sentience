using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public enum CombatSkill
    {
        None,
        Shooting,
        Melee
    }

    public readonly struct SpecialtySkill
    {
        public readonly SkillDef Skill;
        public readonly int Level;
        public readonly bool Primary;

        public SpecialtySkill(SkillDef skill, int level, bool primary)
        {
            Skill = skill;
            Level = level;
            Primary = primary;
        }
    }

    public static class SentientMechSpecialty
    {
        private const int MaxSkillLevel = 20;

        public static List<SpecialtySkill> Resolve(Pawn pawn)
        {
            var result = new List<SpecialtySkill>();
            ThingDef race = SentientMechRegistry.OriginalOf(pawn.def);
            PawnKindDef kind = SentientMechRegistry.OriginalOf(pawn.kindDef);
            if (race?.race == null || kind == null)
            {
                Logger.Warning("Could not find the original mechanoid of " + pawn + ", it gets no specialty skills.");
                return result;
            }

            int level = Mathf.Clamp(race.race.mechFixedSkillLevel, 0, MaxSkillLevel);
            var skills = new List<SkillDef>();

            List<WorkTypeDef> workTypes = race.race.mechEnabledWorkTypes;
            bool hasWorkTypes = !workTypes.NullOrEmpty();
            if (hasWorkTypes)
            {
                foreach (WorkTypeDef workType in workTypes)
                {
                    if (workType.relevantSkills == null)
                    {
                        continue;
                    }
                    foreach (SkillDef skill in workType.relevantSkills)
                    {
                        if (!skills.Contains(skill))
                        {
                            skills.Add(skill);
                        }
                    }
                }
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            bool carriesRanged = primary != null && primary.def.IsRangedWeapon;
            bool hasTurret = race.comps != null && race.comps.Exists(c => c is CompProperties_TurretGun);
            switch (ClassifyCombat(!kind.weaponTags.NullOrEmpty(), carriesRanged, hasTurret, kind.isFighter, hasWorkTypes, !race.tools.NullOrEmpty()))
            {
                case CombatSkill.Shooting:
                    AddIfMissing(skills, SkillDefOf.Shooting);
                    break;
                case CombatSkill.Melee:
                    AddIfMissing(skills, SkillDefOf.Melee);
                    break;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                result.Add(new SpecialtySkill(skills[i], level, primary: i == 0));
            }
            return result;
        }

        public static CombatSkill ClassifyCombat(bool hasWeaponTags, bool carriesRanged, bool hasTurret, bool isFighter, bool hasWorkTypes, bool hasMeleeTools)
        {
            if (hasWeaponTags || carriesRanged || hasTurret)
            {
                return CombatSkill.Shooting;
            }
            if (isFighter && !hasWorkTypes && hasMeleeTools)
            {
                return CombatSkill.Melee;
            }
            return CombatSkill.None;
        }

        private static void AddIfMissing(List<SkillDef> skills, SkillDef skill)
        {
            if (skill != null && !skills.Contains(skill))
            {
                skills.Add(skill);
            }
        }
    }
}
