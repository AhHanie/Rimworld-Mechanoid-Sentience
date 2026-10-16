#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class WorkModesAutoTest
    {
        private const string SettingsCompName = WorkModesCompat.CompName;

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, "WVC: " + what);
        }

        private static List<ThingComp> SettingsComps(Pawn pawn)
        {
            return pawn.AllComps.Where(c => c.GetType().FullName == SettingsCompName).ToList();
        }

        public static void Run(Func<string, Pawn> spawn)
        {
            Type utility = AccessTools.TypeByName(WorkModesCompat.ShutdownUtilityName);
            Type canShutdown = AccessTools.TypeByName("WVC_WorkModes.ThinkNode_CanShutdown");
            if (utility == null || canShutdown == null)
            {
                MechSentienceAutoTest.Fail("WVC: ShutdownUtility or ThinkNode_CanShutdown not found (WVC is active, so the integration cannot bind)");
                return;
            }
            MethodInfo getSettings = AccessTools.Method(utility, "GetMechSettings", new[] { typeof(Pawn) });
            PropertyInfo nonShutdown = AccessTools.Property(canShutdown, "NonShutdownMechs");
            Check(getSettings != null && nonShutdown != null, "WVC's cache accessors bind (" + (getSettings != null) + ", " + (nonShutdown != null) + ")");
            if (getSettings == null || nonShutdown == null)
            {
                return;
            }

            Pawn lancer = spawn("Mech_Lancer");
            List<ThingComp> before = SettingsComps(lancer);
            if (before.Count != 1)
            {
                Log.Message("AUTOTEST info WVC: a vanilla lancer has " + before.Count + " settings comps, expected 1 (WVC changed its approach?); skipping the WVC checks");
                return;
            }
            Check(true, "a vanilla lancer has exactly one WVC settings comp");
            ThingComp original = before[0];
            FieldInfo allowShutdown = AccessTools.Field(original.GetType(), "allowShutdown");
            List<ThingComp> otherCompsBefore = lancer.AllComps.Where(c => c != original && !(c is CompOverseerSubject)).ToList();

            allowShutdown.SetValue(original, false);
            Check(ReferenceEquals(getSettings.Invoke(null, new object[] { lancer }), original), "WVC's cache is primed with the lancer's settings comp");

            bool ok = SentientMechConverter.TryConvert(lancer, showMessages: false);
            Check(ok, "the lancer converted with WVC active");
            if (!ok)
            {
                return;
            }
            Check(SettingsComps(lancer).Count == 0, "no WVC settings comp is left on the sentient pawn");
            Check(!lancer.AllComps.Any(c => c is CompOverseerSubject), "no overseer comp on the sentient pawn");
            Check(otherCompsBefore.All(c => lancer.AllComps.Contains(c)), "every other inherited comp instance is still on the pawn");

            Check(getSettings.Invoke(null, new object[] { lancer }) == null, "GetMechSettings returns null for the sentient pawn (cache cleared)");
            Check(!((HashSet<Pawn>)nonShutdown.GetValue(null)).Contains(lancer), "the sentient pawn is not in WVC's non shutdown set");

            Type tab = AccessTools.TypeByName(WorkModesCompat.WorkMechsTabName);
            if (tab == null)
            {
                Log.Message("AUTOTEST info WVC: the Mechs work tab is not loaded (no Odyssey part); skipping that check");
            }
            else
            {
                Pawn ordinary = spawn("Mech_Militor");
                object window = Activator.CreateInstance(tab);
                PropertyInfo pawns = AccessTools.Property(tab, WorkModesCompat.PawnsGetterName);
                var listed = ((IEnumerable<Pawn>)pawns.GetValue(window)).ToList();
                Check(!listed.Contains(lancer), "the Mechs work tab does not list the sentient pawn");
                Check(listed.Contains(ordinary), "the Mechs work tab still lists an ordinary colony mech");
                Check(SettingsComps(ordinary).Count == 1, "the ordinary mech keeps its WVC settings comp");
            }

            Pawn second = spawn("Mech_Militor");
            List<ThingComp> secondBefore = second.AllComps.ToList();
            ThingComp secondSettings = SettingsComps(second).FirstOrDefault();
            Check(secondSettings != null, "the second mech has a WVC settings comp");
            if (secondSettings != null)
            {
                allowShutdown.SetValue(secondSettings, false);
                getSettings.Invoke(null, new object[] { second });
                Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
                SentientMechConverter.ForceFailureForTesting = true;
                bool converted = SentientMechConverter.TryConvert(second, showMessages: false);
                SentientMechConverter.ForceFailureForTesting = false;
                Check(!converted && !SentientMechRegistry.IsSentient(second), "the forced failure rolled the second mech back");
                List<ThingComp> secondAfter = second.AllComps.ToList();
                Check(secondAfter.SequenceEqual(secondBefore), "the comps are the same instances in the same order after the rollback");
                Check(secondAfter.IndexOf(secondSettings) == secondBefore.IndexOf(secondSettings), "the WVC settings comp is back at its original index");
                Check(!(bool)allowShutdown.GetValue(secondSettings), "its allowShutdown setting is unchanged");
                Check(ReferenceEquals(getSettings.Invoke(null, new object[] { second }), secondSettings), "WVC finds the restored comp again");
            }
        }

        public static void CheckAfterLoad(Pawn sentient)
        {
            Check(sentient != null && SettingsComps(sentient).Count == 0, "the reloaded sentient pawn has no WVC settings comp");
        }
    }
}
#endif
