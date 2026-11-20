using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class PawnGenerator_TryGenerateNewPawnInternal_GenerateIdentity
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (__result != null && SentientMechRegistry.IsSentient(__result))
            {
                if (__result.story?.traits != null)
                {
                    foreach (Trait trait in __result.story.traits.allTraits.ToList())
                    {
                        __result.story.traits.RemoveTrait(trait);
                    }
                }
                SentientMechGenerator.Generate(__result);
            }
        }
    }
}
