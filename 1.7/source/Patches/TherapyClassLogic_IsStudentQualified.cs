using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class TherapyClassLogic_IsStudentQualified
    {
        [HarmonyPrepare]
        public static bool Prepare() => TherapyCompat.IsAvailable;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => TherapyCompat.StudentQualifiedMethod;

        [HarmonyPostfix]
        public static void Postfix(object __instance, Pawn student, ref AcceptanceReport __result)
        {
            if (__result.Accepted && TherapyCompat.IsFocusWithoutEffect(__instance, student))
            {
                __result = new AcceptanceReport("MechanoidSentience.Therapy.NoComposure".Translate(student.LabelShort));
            }
        }
    }
}
