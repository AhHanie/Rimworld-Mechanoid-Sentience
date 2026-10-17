using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(PortraitsCache), nameof(PortraitsCache.Get))]
    public static class PortraitsCache_Get
    {
        private const float Margin = 0.9f;
        private const float CloseUpZoomMultiplier = 1.6f;
        private const float CloseUpFocusHeight = 0.18f;

        [HarmonyPrefix]
        public static void Prefix(Pawn pawn, Vector2 size, ref Rot4 rotation, ref Vector3 cameraOffset, ref float cameraZoom)
        {
            if (!SentientMechRegistry.IsSentient(pawn))
            {
                return;
            }
            bool closeUp = ModSettings.UseCloseUpPortraits && rotation == Rot4.South;
            if (rotation == Rot4.South && !closeUp)
            {
                rotation = Rot4.East;
            }
            float drawSize = SentientMechUtility.BodyDrawSize(pawn);
            float aspect = size.y > 0f ? size.x / size.y : 1f;
            float visible = 1f;
            if (size == ColonistBarColonistDrawer.PawnTextureSize)
            {
                visible = (ColonistBar.BaseSize.y - 2f) / (size.y - 2f);
            }
            cameraZoom = Margin * 2f * Mathf.Min(visible, aspect) / drawSize;
            if (closeUp)
            {
                cameraZoom *= CloseUpZoomMultiplier;
            }
            cameraOffset = new Vector3(0f, 0f, (1f - visible) / cameraZoom + (closeUp ? CloseUpFocusHeight * drawSize : 0f));
        }
    }
}
