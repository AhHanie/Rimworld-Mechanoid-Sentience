using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace Mechanoid_Sentience
{
    public static class RimHudEnergyRow
    {
        private const string PackageId = "Jaxe.RimHUD";
        private const string RowLayerTypeName = "RimHUD.Interface.Hud.Layers.RowLayer";
        private const string WidgetLayerTypeName = "RimHUD.Interface.Hud.Layers.WidgetLayer";
        private const string BaseLayerTypeName = "RimHUD.Interface.Hud.Layers.BaseLayer";
        private const string HudContentTypeName = "RimHUD.Interface.Hud.HudContent";
        private const string BlankWidgetTypeName = "RimHUD.Interface.Hud.Widgets.BlankWidget";
        private const string EnergyWidgetId = "NeedEnergy";

        private const int PlayerHumanlike = 1;
        private const int PlayerCreature = 2;

        private static readonly object True = true;
        private static readonly object False = false;

        private static readonly ConditionalWeakTable<object, object> energyOnlyRows = new ConditionalWeakTable<object, object>();

        private static Type rowType;
        private static Type blankWidgetType;
        private static Type targetsType;
        private static MethodInfo rowChildren;
        private static MethodInfo layerId;
        private static MethodInfo layerArgs;
        private static MethodInfo argsTargets;
        private static MethodInfo setArgsTargets;
        private static MethodInfo getWidget;
        private static FieldInfo layerWidget;
        private static bool? available;

        public static string BindFailure { get; private set; }

        public static MethodBase RowPrepareMethod { get; private set; }

        public static MethodBase RowDrawMethod { get; private set; }

        public static MethodBase WidgetBuildMethod { get; private set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = IsRimHudActive();
                    available = active && Bind();
                    if (active && !available.Value)
                    {
                        Log.Warning("[Mechanoid Sentience] RimHUD is active but its API changed (" + BindFailure + "). The energy bar fix for sentient mechs is disabled; add H to the row containing NeedEnergy in RimHUD's layout editor instead.");
                    }
                }
                return available.Value;
            }
        }

        private static bool IsRimHudActive()
        {
            return ModsConfig.IsActive(PackageId);
        }

        public static bool Bind()
        {
            BindFailure = null;
            rowType = AccessTools.TypeByName(RowLayerTypeName);
            Type widgetType = AccessTools.TypeByName(WidgetLayerTypeName);
            Type baseLayerType = AccessTools.TypeByName(BaseLayerTypeName);
            Type hudContentType = AccessTools.TypeByName(HudContentTypeName);
            blankWidgetType = AccessTools.TypeByName(BlankWidgetTypeName);
            if (rowType == null || widgetType == null || baseLayerType == null || hudContentType == null || blankWidgetType == null)
            {
                return Fail("layer or widget type missing");
            }

            RowPrepareMethod = AccessTools.DeclaredMethod(rowType, "Prepare", Type.EmptyTypes);
            RowDrawMethod = rowType.GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            WidgetBuildMethod = AccessTools.DeclaredMethod(widgetType, "Build", Type.EmptyTypes);
            rowChildren = AccessTools.DeclaredProperty(rowType, "Children")?.GetGetMethod(true);
            layerId = AccessTools.PropertyGetter(widgetType, "Id");
            layerArgs = AccessTools.PropertyGetter(baseLayerType, "Args");
            layerWidget = AccessTools.Field(widgetType, "Widget");
            Type argsType = layerArgs?.ReturnType;
            argsTargets = argsType == null ? null : AccessTools.PropertyGetter(argsType, "Targets");
            setArgsTargets = argsType == null ? null : AccessTools.PropertySetter(argsType, "Targets");
            getWidget = argsType == null ? null : AccessTools.Method(hudContentType, "GetWidget", new[] { typeof(string), argsType });

            if (RowPrepareMethod == null || RowDrawMethod == null || WidgetBuildMethod == null || rowChildren == null || layerId == null
                || layerArgs == null || layerWidget == null || argsTargets == null || setArgsTargets == null || getWidget == null)
            {
                return Fail("member missing");
            }
            targetsType = argsTargets.ReturnType;
            if (!targetsType.IsEnum || targetsType.GetEnumUnderlyingType() != typeof(int)
                || rowChildren.ReturnType != widgetType.MakeArrayType() || layerId.ReturnType != typeof(string)
                || getWidget.ReturnType != layerWidget.FieldType || !getWidget.IsStatic)
            {
                return Fail("member signature changed");
            }
            return true;
        }

        private static bool Fail(string reason)
        {
            BindFailure = reason;
            return false;
        }

        public static bool IsCreatureOnlyMask(int targets)
        {
            return (targets & PlayerCreature) != 0 && (targets & PlayerHumanlike) == 0;
        }

        public static bool IsEnergyOnlyRow(object row)
        {
            if (energyOnlyRows.TryGetValue(row, out object cached))
            {
                return ReferenceEquals(cached, True);
            }
            var children = (Array)rowChildren.Invoke(row, null);
            bool energyOnly = children != null && children.Length == 1 && (string)layerId.Invoke(children.GetValue(0), null) == EnergyWidgetId;
            energyOnlyRows.Add(row, energyOnly ? True : False);
            return energyOnly;
        }

        private static bool IsSelectedSentientWithEnergy()
        {
            var pawn = Find.Selector?.SingleSelectedThing as Pawn;
            return pawn != null && SentientMechRegistry.IsSentient(pawn) && pawn.needs?.energy != null;
        }

        public static int WidenRow(object row)
        {
            if (!IsSelectedSentientWithEnergy())
            {
                return 0;
            }
            object args = layerArgs.Invoke(row, null);
            int mask = (int)argsTargets.Invoke(args, null);
            if (!IsCreatureOnlyMask(mask) || !IsEnergyOnlyRow(row))
            {
                return 0;
            }
            setArgsTargets.Invoke(args, new[] { Enum.ToObject(targetsType, mask | PlayerHumanlike) });
            return mask;
        }

        public static void RestoreRow(object row, int originalMask)
        {
            if (originalMask != 0)
            {
                setArgsTargets.Invoke(layerArgs.Invoke(row, null), new[] { Enum.ToObject(targetsType, originalMask) });
            }
        }

        public static void RebuildBlankedWidget(object layer)
        {
            if ((string)layerId.Invoke(layer, null) != EnergyWidgetId
                || layerWidget.GetValue(layer)?.GetType() != blankWidgetType
                || !IsSelectedSentientWithEnergy())
            {
                return;
            }
            object args = layerArgs.Invoke(layer, null);
            if (IsCreatureOnlyMask((int)argsTargets.Invoke(args, null)))
            {
                layerWidget.SetValue(layer, getWidget.Invoke(null, new[] { EnergyWidgetId, args }));
            }
        }
    }
}
