using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    public static class WantsAndQuirksCompat
    {
        public const string PackageId = "ferny.characterdevelopment";
        public const string AndroidsCompatTypeName = "WantsAndQuirks.AndroidsCompat";
        public const string MethodName = "IsUnawakenedAndroid";
        public const string PawnParameterName = "pawn";

        private static bool? available;

        public static string BindFailure { get; private set; }

        public static MethodBase IsUnawakenedAndroidMethod { get; private set; }

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
                        Log.Warning("[Mechanoid Sentience] Character Development is active but its API changed (" + BindFailure + "). Converting a mechanoid may fail while VRE Androids is active.");
                    }
                }
                return available.Value;
            }
        }

        public static bool Bind()
        {
            BindFailure = null;
            Type type = AccessTools.TypeByName(AndroidsCompatTypeName);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, MethodName, new[] { typeof(Pawn) });
            if (method == null || !method.IsStatic || method.ReturnType != typeof(bool) || method.GetParameters()[0].Name != PawnParameterName)
            {
                BindFailure = AndroidsCompatTypeName + "." + MethodName + " missing or changed";
                return false;
            }
            IsUnawakenedAndroidMethod = method;
            return true;
        }
    }
}
