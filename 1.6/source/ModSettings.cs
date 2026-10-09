using Verse;

namespace Mechanoid_Sentience
{
    public class ModSettings : Verse.ModSettings
    {
        public static bool EnableDebugLogging = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableDebugLogging, "enableDebugLogging", false);
        }
    }
}
