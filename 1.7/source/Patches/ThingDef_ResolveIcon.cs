using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(ThingDef), "ResolveIcon")]
    public static class ThingDef_ResolveIcon
    {
        [HarmonyPostfix]
        public static void Postfix(ThingDef __instance)
        {
            if (!SentientMechRegistry.IsSentientRace(__instance) || !__instance.uiIconPath.NullOrEmpty())
            {
                return;
            }
            PawnKindDef kind = __instance.race.AnyPawnKind;
            GraphicData graphicData = kind?.lifeStages?.FirstOrDefault()?.bodyGraphicData;
            if (graphicData == null)
            {
                return;
            }
            Material material = graphicData.Graphic.MatAt(Rot4.East);
            __instance.uiIcon = (Texture2D)material.mainTexture;
            __instance.uiIconColor = material.color;
            if (ShaderDatabase.TryGetUIShader(material.shader, out Shader uiShader) && MaterialPool.TryGetRequestForMat(material, out MaterialRequest request))
            {
                request.shader = uiShader;
                __instance.uiIconMaterial = MaterialPool.MatFrom(request);
            }
        }
    }
}
