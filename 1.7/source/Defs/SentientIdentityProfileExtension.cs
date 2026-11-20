using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public class PreferredSkill
    {
        public SkillDef skill;
        public int minLevel = -1;
        public int maxLevel = -1;
        public int minPassion = -1;
        public int maxPassion = -1;
    }

    public class SentientIdentityProfileExtension : DefModExtension
    {
        public int defaultSkillMinLevel = 1;
        public int defaultSkillMaxLevel = 5;
        public List<PreferredSkill> preferredSkills;
        public List<BackstoryDef> childhoodBackstories;
        public List<BackstoryDef> adulthoodBackstories;
    }

    public static class SentientIdentityProfiles
    {
        private static readonly HashSet<string> ShippedRaceNames = new HashSet<string>
        {
            "Mech_CentipedeBlaster", "Mech_CentipedeGunner", "Mech_CentipedeBurner", "Mech_Lancer",
            "Mech_Scyther", "Mech_Pikeman", "Mech_Termite", "Mech_Tunneler", "Mech_Militor",
            "Mech_Lifter", "Mech_Constructoid", "Mech_Fabricor", "Mech_Agrihand", "Mech_Cleansweeper",
            "Mech_WarUrchin", "Mech_Paramedic", "Mech_Legionary", "Mech_Tesseron", "Mech_Scorcher",
            "Mech_Apocriton", "Mech_Centurion", "Mech_Warqueen", "Mech_Diabolus", "Mech_Cyclops"
        };
        private static readonly Dictionary<ThingDef, SentientIdentityProfileExtension> valid = new Dictionary<ThingDef, SentientIdentityProfileExtension>();
        private static readonly HashSet<ThingDef> checkedRaces = new HashSet<ThingDef>();

        internal static bool IsShippedRace(ThingDef original) => original != null && ShippedRaceNames.Contains(original.defName);

        public static SentientIdentityProfileExtension Resolve(ThingDef sentientRace)
        {
            ThingDef original = SentientMechRegistry.OriginalRaceOf(sentientRace) ?? SentientMechRegistry.OriginalOf(sentientRace);
            if (original == null) return null;
            if (!checkedRaces.Contains(original)) Validate(original);
            valid.TryGetValue(original, out SentientIdentityProfileExtension profile);
            return profile;
        }

        public static void Validate(ThingDef original)
        {
            if (!checkedRaces.Add(original)) return;
            SentientIdentityProfileExtension profile = original.GetModExtension<SentientIdentityProfileExtension>();
            if (profile == null)
            {
                if (ShippedRaceNames.Contains(original.defName))
                    Log.Warning("[Mechanoid Sentience] Missing identity profile for " + original.defName + "; using the original generator.");
                return;
            }
            string problem = Problem(profile);
            if (problem != null)
                Log.Warning("[Mechanoid Sentience] Invalid identity profile for " + original.defName + ": " + problem + "; using the original generator.");
            else
                valid[original] = profile;
        }

        private static string Problem(SentientIdentityProfileExtension profile)
        {
            if (!LevelRange(profile.defaultSkillMinLevel, profile.defaultSkillMaxLevel)) return "invalid fallback level range";
            if (profile.preferredSkills == null) return "missing preferred skills list";
            var skills = new HashSet<SkillDef>();
            foreach (PreferredSkill preferred in profile.preferredSkills)
            {
                if (preferred == null || preferred.skill == null || !skills.Add(preferred.skill)) return "null or duplicate preferred skill";
                if (!LevelRange(preferred.minLevel, preferred.maxLevel)) return "invalid level range for " + preferred.skill.defName;
                if (preferred.minPassion < 0 || preferred.minPassion > preferred.maxPassion || preferred.maxPassion > 2)
                    return "invalid passion range for " + preferred.skill.defName;
            }
            return StoryProblem(profile.childhoodBackstories, BackstorySlot.Childhood)
                ?? StoryProblem(profile.adulthoodBackstories, BackstorySlot.Adulthood);
        }

        private static bool LevelRange(int min, int max) => min >= 0 && min <= max && max <= 20;

        private static string StoryProblem(List<BackstoryDef> stories, BackstorySlot slot)
        {
            if (stories == null || stories.Count < 2) return "fewer than two " + slot + " stories";
            var seen = new HashSet<BackstoryDef>();
            foreach (BackstoryDef story in stories)
            {
                if (story == null || !seen.Add(story)) return "null or duplicate " + slot + " story";
                if (story.slot != slot || story.spawnCategories == null || !story.spawnCategories.Contains(SentientDefGenerator.BackstoryCategory))
                    return "wrong slot or category for " + story.defName;
                if (story.workDisables != WorkTags.None || !story.DisabledWorkTypes.NullOrEmpty())
                    return "work restriction in " + story.defName;
            }
            return null;
        }
    }
}
