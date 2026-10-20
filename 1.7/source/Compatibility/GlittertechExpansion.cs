using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class GlittertechExpansionCompat
    {
        public const string PackageId = "Ushanka.GlittertechExpansion";
        public const string TelepadTypeName = "USH_GE.CompTelepad";
        public const string TeleportablePawnsMethodName = "TeleportablePawns";

        public static bool IsActive => ModsConfig.IsActive(PackageId);

        private static bool warned;

        public static int Deduplicate(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count < 2)
            {
                return 0;
            }
            var seen = new HashSet<Pawn>();
            return pawns.RemoveAll(pawn => !seen.Add(pawn));
        }

        public static MethodBase TeleportablePawnsMethod()
        {
            if (!IsActive)
            {
                return null;
            }
            Type telepad = AccessTools.TypeByName(TelepadTypeName);
            MethodInfo method = telepad == null
                ? null
                : AccessTools.DeclaredMethod(telepad, TeleportablePawnsMethodName, new[] { typeof(bool) });
            if (method == null || method.IsStatic || method.ReturnType != typeof(List<Pawn>))
            {
                Warn(TelepadTypeName + "." + TeleportablePawnsMethodName + "(bool) not found; sentient mechanoids may be listed twice for bulk teleporting");
                return null;
            }
            return method;
        }

        private static void Warn(string message)
        {
            if (warned)
            {
                return;
            }
            warned = true;
            Log.Warning("[Mechanoid Sentience] Glittertech Expansion is active but part of its API changed (" + message + ").");
        }
    }
}
