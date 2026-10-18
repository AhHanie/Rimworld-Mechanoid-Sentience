using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class TherapyCompat
    {
        public const string PackageId = "ferny.ProgressionTherapy";
        public const string ClassLogicTypeName = "ProgressionTherapy.TherapyClassLogic";
        public const string QualifiedMethodName = "IsStudentQualified";
        public const string StudentParameterName = "student";
        public const string FocusFieldName = "focusType";
        public const string MentalStabilityFocusName = "MentalStability";

        private static FieldInfo focusField;
        private static object mentalStabilityFocus;
        private static bool? available;

        public static string BindFailure { get; private set; }

        public static MethodBase StudentQualifiedMethod { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = ModsConfig.IsActive(PackageId);
                    available = active && Bind();
                    if (active && !available.Value)
                    {
                        Log.Warning("[Mechanoid Sentience] Progression: Therapy is active but its API changed (" + BindFailure + "). Sentient mechanoids can be enrolled in its composure therapy, which does nothing for them.");
                    }
                }
                return available.Value;
            }
        }

        public static bool Bind()
        {
            BindFailure = null;
            Type logic = AccessTools.TypeByName(ClassLogicTypeName);
            if (logic == null)
            {
                return Fail(ClassLogicTypeName + " missing");
            }
            MethodInfo method = AccessTools.DeclaredMethod(logic, QualifiedMethodName, new[] { typeof(Pawn) });
            focusField = AccessTools.Field(logic, FocusFieldName);
            if (method == null || method.ReturnType != typeof(AcceptanceReport) || method.GetParameters()[0].Name != StudentParameterName)
            {
                return Fail(QualifiedMethodName + " missing or changed");
            }
            if (focusField == null || !focusField.FieldType.IsEnum || !Enum.IsDefined(focusField.FieldType, MentalStabilityFocusName))
            {
                return Fail(FocusFieldName + " or its " + MentalStabilityFocusName + " value missing");
            }
            mentalStabilityFocus = Enum.Parse(focusField.FieldType, MentalStabilityFocusName);
            StudentQualifiedMethod = method;
            return true;
        }

        private static bool Fail(string reason)
        {
            BindFailure = reason;
            return false;
        }

        public static bool IsFocusWithoutEffect(object classLogic, Pawn student)
        {
            return SentientMechRegistry.IsSentient(student) && Equals(focusField.GetValue(classLogic), mentalStabilityFocus);
        }
    }
}
