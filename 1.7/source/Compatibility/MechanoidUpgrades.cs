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
        private static bool? overseerInfixesSafe;
        private static Type upgradableCompType;

        public static string BindFailure { get; private set; }

        public static bool OverseerInfixesSafe
        {
            get
            {
                if (overseerInfixesSafe == null)
                {
                    overseerInfixesSafe = CheckOverseerInfixSites();
                }
                return overseerInfixesSafe.Value;
            }
        }

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

        private static readonly CompOverseerSubject SentientSubject = new CompOverseerSubject();

        public static CompOverseerSubject SubjectFor(CompOverseerSubject subject, Pawn pawn)
        {
            return SentientMechRegistry.IsSentient(pawn) ? SentientSubject : subject;
        }

        public static bool IsSentientSubject(CompOverseerSubject subject)
        {
            return ReferenceEquals(subject, SentientSubject);
        }

        public static int CountCalls(MethodBase method, Type declaringType, string name)
        {
            try
            {
                return PatchProcessor.GetOriginalInstructions(method).Count(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
                    && i.operand is MethodInfo called && called.DeclaringType == declaringType && called.Name == name);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static bool CheckOverseerInfixSites()
        {
            var problems = new List<string>();
            foreach (MethodBase method in new[] { upgraderCanAcceptPawn, upgraderTickInterval })
            {
                if (method == null)
                {
                    problems.Add("an upgrader method is missing");
                    continue;
                }
                int subjects = CountCalls(method, typeof(Pawn), "get_OverseerSubject");
                int states = CountCalls(method, typeof(CompOverseerSubject), "get_State");
                if (subjects != 1 || states != 1)
                {
                    problems.Add(method.Name + " reads the overseer subject " + subjects + " and its state " + states + " times (expected 1 and 1)");
                }
            }
            if (problems.Count > 0)
            {
                Log.Warning("[Mechanoid Sentience] The Mechanoid Upgrades upgrader could not be adapted (" + string.Join("; ", problems) + "). Sentient mechanoids cannot be put into the upgrader until this mod is updated.");
            }
            return problems.Count == 0;
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
