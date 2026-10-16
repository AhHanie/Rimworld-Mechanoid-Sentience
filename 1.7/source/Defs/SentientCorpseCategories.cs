using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class SentientCorpseCategories
    {
        public const string CategoryName = "MS_CorpsesSentientMech";
        public const string ShredRecipeName = "MS_ButcherCorpseSentientMech";
        public const string SmashRecipeName = "MS_SmashCorpseSentientMech";

        internal static void Apply()
        {
            ThingCategoryDef category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(CategoryName);
            ThingCategoryDef humanlike = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("CorpsesHumanlike");
            if (category == null || humanlike == null)
            {
                Log.Error("[Mechanoid Sentience] Sentient corpse category is missing, sentient corpses stay humanlike.");
                return;
            }
            foreach (ThingDef race in SentientMechRegistry.SentientRaces)
            {
                ThingDef corpse = race.race.corpseDef;
                if (corpse?.thingCategories == null)
                {
                    continue;
                }
                corpse.thingCategories.Remove(humanlike);
                if (!corpse.thingCategories.Contains(category))
                {
                    corpse.thingCategories.Add(category);
                }
            }
        }
    }
}
