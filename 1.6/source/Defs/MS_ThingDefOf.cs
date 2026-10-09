using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [DefOf]
    public static class MS_ThingDefOf
    {
        public static ThingDef MS_MechSentienceCatalyst;

        static MS_ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MS_ThingDefOf));
        }
    }
}
