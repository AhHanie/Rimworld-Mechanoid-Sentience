#if DEBUG
using System.Collections.Generic;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class GlittertechExpansionAutoTest
    {
        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "GE: " + what);
        }

        public static void RunPure()
        {
            var a = new Pawn();
            var b = new Pawn();
            var c = new Pawn();

            var list = new List<Pawn> { a, b, c, b, a, c };
            int removed = GlittertechExpansionCompat.Deduplicate(list);
            Check(removed == 3 && list.Count == 3 && list[0] == a && list[1] == b && list[2] == c, "duplicates are removed, first-seen order is kept");

            var unique = new List<Pawn> { c, a, b };
            Check(GlittertechExpansionCompat.Deduplicate(unique) == 0 && unique[0] == c && unique[1] == a && unique[2] == b, "a list without duplicates is unchanged");
            Check(GlittertechExpansionCompat.Deduplicate(null) == 0 && GlittertechExpansionCompat.Deduplicate(new List<Pawn>()) == 0, "null and empty lists are fine");
        }
    }
}
#endif
