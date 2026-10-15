using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class FloatMenuOptionProvider_DropEquipment_GetSingleOptionFor
    {
        private static readonly MethodBase Target = AccessTools.Method(typeof(FloatMenuOptionProvider_DropEquipment), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) });

        [HarmonyPrepare]
        public static bool Prepare()
        {
            if (Target == null)
            {
                Logger.Warning("FloatMenuOptionProvider_DropEquipment.GetSingleOptionFor was not found, the disabled drop option is skipped.");
            }
            return Target != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => Target;

        [HarmonyPostfix]
        public static void Postfix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (__result == null)
            {
                return;
            }
            ThingWithComps locked = SentientMechUtility.LockedPrimary(clickedPawn);
            if (locked == null)
            {
                return;
            }
            __result = new FloatMenuOption("CannotDrop".Translate(locked.Label, locked) + ": " + SentientMechUtility.CannotDropReason(clickedPawn, locked), null);
        }
    }
}
