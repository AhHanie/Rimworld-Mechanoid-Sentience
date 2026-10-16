using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public static class MechanoidUpgradesCompat
    {
        public const string PackageId = "GoGaTio.MechanoidUpgrades";

        private const string UpgraderName = "MU.Building_MechUpgrader";
        private const string UpgradableCompName = "MU.CompUpgradableMechanoid";
        private const string UpgradeDefName = "MU.MechUpgradeDef";
        private const string TurretCompName = "MU.UpgradeCompTurretGun";
        private const string CarrierCompName = "MU.UpgradeComp_MechCarrier";
        private const string LaserDefenceCompName = "MU.HeavyWeaponry.UpgradeComp_LaserDefence";
        private const string ImproveBillName = "MU.Bill_Improve";
        private const string ScenPartName = "MU.ScenPart_Upgrades";

        private static bool? available;
        private static Type upgradableCompType;

        public static string BindFailure { get; private set; }

        public static bool UpgraderTickPatched { get; internal set; }

        public static bool IsAvailable
        {
            get
            {
                if (available == null)
                {
                    bool active = ModsConfig.IsActive(PackageId);
                    available = active;
                    if (active)
                    {
                        Bind();
                        if (BindFailure != null)
                        {
                            Log.Warning("[Mechanoid Sentience] Mechanoid Upgrades is active but part of its API changed (" + BindFailure + "). The matching sentient mechanoid support is disabled.");
                        }
                    }
                }
                return available.Value;
            }
        }

        public static MethodBase UpgraderCanAcceptPawn => IsAvailable ? upgraderCanAcceptPawn : null;
        public static MethodBase UpgraderTickInterval => IsAvailable ? upgraderTickInterval : null;
        public static MethodBase UpgradeDefCanAdd => IsAvailable ? upgradeDefCanAdd : null;
        public static MethodBase UpgradableGizmos => IsAvailable ? upgradableGizmos : null;
        public static MethodBase TurretCanShoot => IsAvailable ? turretCanShoot : null;
        public static MethodBase TurretGizmos => IsAvailable ? turretGizmos : null;
        public static MethodBase CarrierCanSpawn => IsAvailable ? carrierCanSpawn : null;
        public static MethodBase CarrierGizmos => IsAvailable ? carrierGizmos : null;
        public static MethodBase LaserDefenceGizmos => IsAvailable ? laserDefenceGizmos : null;
        public static MethodBase ImproveBillAllowed => IsAvailable ? improveBillAllowed : null;
        public static MethodBase ScenPartMechDefs => IsAvailable ? scenPartMechDefs : null;
        public static MethodBase UpgraderInspectString => IsAvailable ? upgraderInspectString : null;

        private static MethodBase upgraderCanAcceptPawn;
        private static MethodBase upgraderTickInterval;
        private static MethodBase upgradeDefCanAdd;
        private static MethodBase upgradableGizmos;
        private static MethodBase turretCanShoot;
        private static MethodBase turretGizmos;
        private static MethodBase carrierCanSpawn;
        private static MethodBase carrierGizmos;
        private static MethodBase laserDefenceGizmos;
        private static MethodBase improveBillAllowed;
        private static MethodBase scenPartMechDefs;
        private static MethodBase upgraderInspectString;

        public static void Bind()
        {
            var failures = new List<string>();
            Type upgrader = AccessTools.TypeByName(UpgraderName);
            upgradableCompType = AccessTools.TypeByName(UpgradableCompName);
            Type upgradeDef = AccessTools.TypeByName(UpgradeDefName);
            Type turret = AccessTools.TypeByName(TurretCompName);
            Type carrier = AccessTools.TypeByName(CarrierCompName);
            Type laser = AccessTools.TypeByName(LaserDefenceCompName);
            Type improve = AccessTools.TypeByName(ImproveBillName);
            Type scenPart = AccessTools.TypeByName(ScenPartName);

            if (upgradableCompType == null)
            {
                failures.Add(UpgradableCompName);
            }
            upgraderCanAcceptPawn = Resolve(failures, "Building_MechUpgrader.CanAcceptPawn(Pawn)", () => AccessTools.DeclaredMethod(upgrader, "CanAcceptPawn", new[] { typeof(Pawn) }));
            upgraderTickInterval = Resolve(failures, "Building_MechUpgrader.TickInterval(int)", () => AccessTools.DeclaredMethod(upgrader, "TickInterval", new[] { typeof(int) }));
            upgraderInspectString = Resolve(failures, "Building_MechUpgrader.GetInspectString()", () => AccessTools.DeclaredMethod(upgrader, "GetInspectString", Type.EmptyTypes));
            upgradeDefCanAdd = Resolve(failures, "MechUpgradeDef.CanAdd(ThingDef, List)", () => AccessTools.GetDeclaredMethods(upgradeDef).SingleOrDefault(m => m.Name == "CanAdd" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(ThingDef)));
            upgradableGizmos = Resolve(failures, "CompUpgradableMechanoid.CompGetGizmosExtra()", () => AccessTools.StateMachineMoveNext(AccessTools.DeclaredMethod(upgradableCompType, "CompGetGizmosExtra")));
            turretCanShoot = Resolve(failures, "UpgradeCompTurretGun.CanShoot", () => AccessTools.PropertyGetter(turret, "CanShoot"));
            turretGizmos = Resolve(failures, "UpgradeCompTurretGun.CompGetGizmosExtra()", () => AccessTools.StateMachineMoveNext(AccessTools.DeclaredMethod(turret, "CompGetGizmosExtra")));
            carrierCanSpawn = Resolve(failures, "UpgradeComp_MechCarrier.CanSpawn", () => AccessTools.PropertyGetter(carrier, "CanSpawn"));
            carrierGizmos = Resolve(failures, "UpgradeComp_MechCarrier.CompGetGizmosExtra()", () => AccessTools.StateMachineMoveNext(AccessTools.DeclaredMethod(carrier, "CompGetGizmosExtra")));
            laserDefenceGizmos = laser == null ? null : Resolve(failures, "UpgradeComp_LaserDefence.CompGetGizmosExtra()", () => AccessTools.StateMachineMoveNext(AccessTools.DeclaredMethod(laser, "CompGetGizmosExtra")));
            improveBillAllowed = Resolve(failures, "Bill_Improve.PawnAllowedToStartAnew(Pawn)", () => AccessTools.DeclaredMethod(improve, "PawnAllowedToStartAnew", new[] { typeof(Pawn) }));
            scenPartMechDefs = Resolve(failures, "ScenPart_Upgrades.MechDefs", () => AccessTools.PropertyGetter(scenPart, "MechDefs"));

            BindFailure = failures.Count == 0 ? null : string.Join(", ", failures) + " not found";
        }

        private static MethodBase Resolve(List<string> failures, string label, Func<MethodBase> resolve)
        {
            MethodBase method = null;
            try
            {
                method = resolve();
            }
            catch (Exception)
            {
            }
            if (method == null)
            {
                failures.Add(label);
            }
            return method;
        }

        public static bool HasUpgradableComp(Pawn pawn)
        {
            if (upgradableCompType == null)
            {
                return false;
            }
            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (upgradableCompType.IsInstanceOfType(comps[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static AcceptanceReport CanAcceptSentient(Building_Enterable upgrader, Pawn pawn)
        {
            if (!HasUpgradableComp(pawn) || !pawn.IsColonyMech)
            {
                return false;
            }
            if (upgrader.SelectedPawn != null && upgrader.SelectedPawn != pawn)
            {
                return false;
            }
            CompPowerTrader power = upgrader.GetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
            {
                return "CannotUseNoPower".Translate();
            }
            if (!UpgraderTickPatched)
            {
                return "MechanoidSentience.MuUpgrader.Unsupported".Translate();
            }
            return true;
        }

        public static bool OccupantLostOverseer(Pawn occupant)
        {
            if (SentientMechRegistry.IsSentient(occupant))
            {
                return false;
            }
            CompOverseerSubject subject = occupant.OverseerSubject;
            return subject == null || subject.State != OverseerSubjectState.Overseen;
        }

        private static readonly MethodInfo OccupantLostOverseerMethod = AccessTools.Method(typeof(MechanoidUpgradesCompat), nameof(OccupantLostOverseer));

        public static bool TryRewriteOverseerCheck(List<CodeInstruction> code, out string failure)
        {
            failure = null;
            var meaningful = new List<int>(code.Count);
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Nop)
                {
                    meaningful.Add(i);
                }
            }

            int matchAt = -1;
            for (int k = 0; k + 10 < meaningful.Count; k++)
            {
                if (MatchesOverseerCheck(code, meaningful, k))
                {
                    if (matchAt >= 0)
                    {
                        failure = "the overseer check occurs more than once";
                        return false;
                    }
                    matchAt = k;
                }
            }
            if (matchAt < 0)
            {
                failure = "the overseer check was not found";
                return false;
            }

            int first = meaningful[matchAt];
            int last = meaningful[matchAt + 10];
            var interiorLabels = new HashSet<Label>();
            for (int i = first + 1; i <= last; i++)
            {
                if (code[i].blocks.Count > 0)
                {
                    failure = "an exception block starts or ends inside the overseer check";
                    return false;
                }
                foreach (Label label in code[i].labels)
                {
                    interiorLabels.Add(label);
                }
            }
            if (interiorLabels.Count > 0)
            {
                for (int i = 0; i < code.Count; i++)
                {
                    if (i >= first && i <= last)
                    {
                        continue;
                    }
                    if (BranchesTo(code[i], interiorLabels))
                    {
                        failure = "a branch from outside jumps into the overseer check";
                        return false;
                    }
                }
            }

            var replacement = new CodeInstruction(OpCodes.Call, OccupantLostOverseerMethod);
            replacement.labels.AddRange(code[first].labels);
            replacement.blocks.AddRange(code[first].blocks);
            code.RemoveRange(first, last - first + 1);
            code.Insert(first, replacement);
            return true;
        }

        private static bool MatchesOverseerCheck(List<CodeInstruction> code, List<int> meaningful, int k)
        {
            CodeInstruction At(int offset) => code[meaningful[k + offset]];
            return IsCallTo(At(0), typeof(Pawn), "get_OverseerSubject")
                && At(1).opcode == OpCodes.Dup
                && (At(2).opcode == OpCodes.Brtrue || At(2).opcode == OpCodes.Brtrue_S)
                && At(3).opcode == OpCodes.Pop
                && At(4).opcode == OpCodes.Ldc_I4_1
                && (At(5).opcode == OpCodes.Br || At(5).opcode == OpCodes.Br_S)
                && IsCallTo(At(6), typeof(CompOverseerSubject), "get_State")
                && At(7).opcode == OpCodes.Ldc_I4_2
                && At(8).opcode == OpCodes.Ceq
                && At(9).opcode == OpCodes.Ldc_I4_0
                && At(10).opcode == OpCodes.Ceq;
        }

        private static bool IsCallTo(CodeInstruction instruction, Type declaringType, string name)
        {
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method && method.DeclaringType == declaringType && method.Name == name;
        }

        private static bool BranchesTo(CodeInstruction instruction, HashSet<Label> labels)
        {
            if (instruction.operand is Label label)
            {
                return labels.Contains(label);
            }
            if (instruction.operand is Label[] targets)
            {
                return targets.Any(labels.Contains);
            }
            return false;
        }

        public static ThingDef RestrictionRace(ThingDef race)
        {
            return SentientMechRegistry.OriginalRaceOf(race) ?? race;
        }

        public static string WithSentientWaitingText(string inspectString, Building_Enterable upgrader)
        {
            if (inspectString.NullOrEmpty())
            {
                return inspectString;
            }
            Pawn occupant = null;
            foreach (Thing held in upgrader.GetDirectlyHeldThings())
            {
                if (held is Pawn pawn)
                {
                    occupant = pawn;
                    break;
                }
            }
            if (occupant == null || !SentientMechRegistry.IsSentient(occupant))
            {
                return inspectString;
            }
            string original = "MU_Upgrader_WaitingForMechanitor".Translate().ToString();
            return original.NullOrEmpty() ? inspectString : inspectString.Replace(original, "MechanoidSentience.MuUpgrader.WaitingForPlayer".Translate().ToString());
        }

        public static void HideSentientKinds(List<PawnKindDef> kinds)
        {
            kinds?.RemoveAll(SentientMechRegistry.IsSentientKind);
        }
    }
}
