using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [DefOf]
    public static class MS_JobDefOf
    {
        public static JobDef MS_ApplyMechSentienceCatalyst;

        static MS_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MS_JobDefOf));
        }
    }
}
