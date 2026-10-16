using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Mechanoid_Sentience
{
    [HarmonyPatch]
    public static class Building_MechUpgrader_TickInterval
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return MechanoidUpgradesCompat.UpgraderTickInterval != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return MechanoidUpgradesCompat.UpgraderTickInterval;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            bool rewritten;
            string failure;
            try
            {
                rewritten = MechanoidUpgradesCompat.TryRewriteOverseerCheck(code, out failure);
            }
            catch (System.Exception e)
            {
                rewritten = false;
                failure = e.GetType().Name + ": " + e.Message;
            }
            MechanoidUpgradesCompat.UpgraderTickPatched = rewritten;
            if (!rewritten)
            {
                Verse.Log.Warning("[Mechanoid Sentience] The Mechanoid Upgrades upgrader could not be adapted (" + failure + ", " + code.Count + " instructions). Sentient mechanoids cannot be put into the upgrader until this mod is updated.");
            }
            return code;
        }
    }
}
