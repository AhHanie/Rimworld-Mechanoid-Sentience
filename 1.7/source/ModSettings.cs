using Verse;

namespace Mechanoid_Sentience
{
    public class ModSettings : Verse.ModSettings
    {
        public static bool EnableSpontaneousAwakening = true;
        public static bool UseCloseUpPortraits = false;
        public static bool EnableDebugLogging = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableSpontaneousAwakening, "enableSpontaneousAwakening", true);
            Scribe_Values.Look(ref UseCloseUpPortraits, "useCloseUpPortraits", false);
            Scribe_Values.Look(ref EnableDebugLogging, "enableDebugLogging", false);
        }
    }
}
