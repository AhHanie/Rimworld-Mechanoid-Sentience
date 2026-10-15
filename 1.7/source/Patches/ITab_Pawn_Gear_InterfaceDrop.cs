using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class ITab_Pawn_Gear_InterfaceDrop
    {
        private static readonly MethodBase Target = AccessTools.Method(typeof(ITab_Pawn_Gear), "InterfaceDrop", new[] { typeof(Thing) });
        private static readonly MethodInfo SelPawnForGearGetter = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

        [HarmonyPrepare]
        public static bool Prepare()
        {
            bool found = Target != null && SelPawnForGearGetter != null;
            if (!found)
            {
                Logger.Warning("ITab_Pawn_Gear.InterfaceDrop was not found, the gear tab drop message is skipped.");
            }
            return found;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => Target;

        [HarmonyPrefix]
        public static bool Prefix(ITab_Pawn_Gear __instance, Thing t)
        {
            var pawn = (Pawn)SelPawnForGearGetter.Invoke(__instance, Array.Empty<object>());
            if (!SentientMechUtility.IsLockedWeapon(pawn, t))
            {
                return true;
            }
            Messages.Message(SentientMechUtility.CannotDropReason(pawn, t), pawn, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}
