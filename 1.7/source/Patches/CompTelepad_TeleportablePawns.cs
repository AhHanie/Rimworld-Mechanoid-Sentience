using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class CompTelepad_TeleportablePawns
    {
        private static MethodBase target;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            target = GlittertechExpansionCompat.TeleportablePawnsMethod();
            return target != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return target ?? GlittertechExpansionCompat.TeleportablePawnsMethod();
        }

        [HarmonyPostfix]
        public static void Postfix(List<Pawn> __result)
        {
            GlittertechExpansionCompat.Deduplicate(__result);
        }
    }
}
