using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class JobGiver_Work_PawnCanUseWorkGiver
    {
        private static readonly FieldInfo CanBeDoneByMechs = AccessTools.Field(typeof(WorkGiverDef), nameof(WorkGiverDef.canBeDoneByMechs));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(SentientMechUtility), nameof(SentientMechUtility.CanBeDoneByMech));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == CanBeDoneByMechs)
                {
                    count++;
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, Replacement);
                }
            }
            if (count != 1)
            {
                throw new InvalidOperationException("[Mechanoid Sentience] JobGiver_Work.PawnCanUseWorkGiver: expected exactly one canBeDoneByMechs check but found " + count + ". The game code changed, review this transpiler.");
            }
        }
    }
}
