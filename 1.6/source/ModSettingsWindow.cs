using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public static class ModSettingsWindow
    {
        public static void Draw(Rect parent)
        {
            var listing = new Listing_Standard();
            listing.Begin(parent);
            listing.CheckboxLabeled(
                "MechanoidSentience.EnableSpontaneousAwakening".Translate(),
                ref ModSettings.EnableSpontaneousAwakening,
                "MechanoidSentience.EnableSpontaneousAwakeningTooltip".Translate());
            listing.CheckboxLabeled(
                "MechanoidSentience.EnableDebugLogging".Translate(),
                ref ModSettings.EnableDebugLogging,
                "MechanoidSentience.EnableDebugLoggingTooltip".Translate());
            listing.End();
        }
    }
}
