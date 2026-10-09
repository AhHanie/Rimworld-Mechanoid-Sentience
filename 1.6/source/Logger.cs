using System;
using Verse;

namespace Mechanoid_Sentience
{
    public static class Logger
    {
        private const string Prefix = "[Mechanoid Sentience] ";

        public static void Message(string message)
        {
            if (!ModSettings.EnableDebugLogging)
            {
                return;
            }

            Log.Message(Prefix + message);
        }

        public static void Warning(string message)
        {
            if (!ModSettings.EnableDebugLogging)
            {
                return;
            }

            Log.Warning(Prefix + message);
        }

        public static void Error(string message)
        {
            if (!ModSettings.EnableDebugLogging)
            {
                return;
            }

            Log.Error(Prefix + message);
        }

        public static void Exception(Exception exception, string context = null)
        {
            if (!ModSettings.EnableDebugLogging || exception == null)
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(context) ? Prefix : Prefix + context + ": ";
            Log.Error(prefix + exception);
        }
    }
}
