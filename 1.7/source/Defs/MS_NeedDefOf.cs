using RimWorld;

namespace Mechanoid_Sentience
{
    [DefOf]
    public static class MS_NeedDefOf
    {
        public static NeedDef Mood;
        public static NeedDef Joy;
        public static NeedDef Beauty;

        [MayRequireBiotech]
        public static NeedDef MechEnergy;

        static MS_NeedDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MS_NeedDefOf));
        }
    }
}
