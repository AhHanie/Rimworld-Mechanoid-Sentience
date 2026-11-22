#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static Mechanoid_Sentience.DmsLadyAutoTest;
using static Mechanoid_Sentience.MechSentienceAutoTest;

namespace Mechanoid_Sentience
{
    internal static class RockyAutoTest
    {
        private const string RockyKindName = "USAC_Mech_Rocky";
        private static readonly Rot4[] Facings = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
        private static RenderSnapshot[] ordinaryRender;
        private static Vector3?[] ordinaryHelmetOffsets;
        private static int rockyId;
        private static int pantsId;
        private static int dusterId;
        private static string pawnName;
        private static (int Id, int HitPoints, bool Locked)[] savedApparel;

        internal static void Run(Func<string, Pawn> spawn)
        {
            PawnKindDef originalKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(RockyKindName);
            if (originalKind == null)
            {
                Log.Message("AUTOTEST info Ascension's Rocky is unavailable, Rocky checks skipped");
                return;
            }
            if (!SentientMechRegistry.TryGetSentient(originalKind.race, originalKind, out ThingDef sentientRace, out PawnKindDef sentientKind))
            {
                Fail("Rocky has no sentient defs");
                return;
            }

            Pawn rocky = spawn(RockyKindName);
            DressRocky(rocky);
            ordinaryRender = Facings.Select(facing => Measure(rocky, facing)).ToArray();
            ordinaryHelmetOffsets = Facings.Select(facing => HelmetOffset(rocky, facing)).ToArray();
            Check(ordinaryHelmetOffsets.All(offset => offset.HasValue), "ordinary Rocky has a helmet render transform in every facing");
            CheckAppearance(rocky, "ordinary Rocky");
            Check(DeadMansSwitchCompat.CanWearApparel(rocky), "ordinary Rocky has apparel support");
            Check(rocky.AllComps.All(c => !DeadMansSwitchCompat.IsOwnershipComp(CompSignature.Of(c.props))), "ordinary Rocky has no DMS ownership comps");
            if (!CheckConversion(rocky, sentientRace, sentientKind, rollback: false))
            {
                return;
            }
            CheckAppearance(rocky, "awakened Rocky");
            CheckRender(rocky, "awakened Rocky", checkHelmet: true);
            Check(DeadMansSwitchCompat.CanWearApparel(rocky), "awakened Rocky has apparel support");
            rockyId = rocky.thingIDNumber;
            CheckWearMenu(rocky);

            Pawn rolledBack = spawn(RockyKindName);
            DressRocky(rolledBack);
            Measure(rolledBack);
            CheckConversion(rolledBack, originalKind.race, originalKind, rollback: true);
            CheckRender(rolledBack, "rolled back Rocky", checkHelmet: true);

            Pawn direct = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            CheckAppearance(direct, "directly generated Rocky");
            CheckRender(direct, "directly generated Rocky");
            CheckStarter(sentientRace);

            foreach (string kindName in new[] { "Mech_Militor", "USAC_Mech_Paraman" })
            {
                Pawn control = spawn(kindName);
                Apparel shirt = SpawnApparel(control, "Apparel_CollarShirt");
                Check(SentientMechConverter.TryConvert(control, showMessages: false) && !DeadMansSwitchCompat.CanWearApparel(control)
                    && !ApparelUtility.HasPartsToWear(control, shirt.def) && !EquipmentUtility.CanEquip(shirt, control),
                    kindName + " still rejects clothing after awakening");
                shirt.Destroy();
            }
        }

        private static void CheckStarter(ThingDef sentientRace)
        {
            PawnKindDef variant = PawnKindDef.Named(RockyKindName + "_AR");
            if (!SentientMechRegistry.TryGetSentient(variant.race, variant, out ThingDef variantRace, out PawnKindDef variantKind) || variantRace != sentientRace)
            {
                Fail("Rocky AR has no sentient kind for Rocky's race");
                return;
            }
            GameInitData initData = Current.Game.InitData;
            Pawn starter = null;
            try
            {
                Current.Game.InitData = new GameInitData();
                if (!SentientMechStartingPawnFactory.TryCreate(variantKind, out starter))
                {
                    Fail("Rocky AR starter could not be created");
                    return;
                }
                Check(!starter.Spawned && starter.kindDef == variantKind && DeadMansSwitchCompat.CanWearApparel(starter), "Rocky AR starter keeps its kind and shares Rocky's apparel support");
                CheckAppearance(starter, "Rocky AR starter");
                CheckRender(starter, "Rocky AR starter");
            }
            finally
            {
                Current.Game.InitData = initData;
                if (starter != null)
                {
                    Find.WorldPawns.PassToWorld(starter, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                }
            }
        }

        private static void DressRocky(Pawn pawn)
        {
            pawn.apparel.DestroyAll();
            Apparel shirt = Dress(pawn, locked: true, "Apparel_CollarShirt");
            Apparel helmet = Dress(pawn, locked: true, "Apparel_SimpleHelmet");
            Check(shirt?.def.defName == "Apparel_CollarShirt" && helmet?.def.defName == "Apparel_SimpleHelmet"
                && pawn.apparel.WornApparel.Contains(shirt) && pawn.apparel.WornApparel.Contains(helmet), "ordinary Rocky wears a shirt and helmet");
        }

        private static bool CheckConversion(Pawn pawn, ThingDef expectedRace, PawnKindDef expectedKind, bool rollback)
        {
            string what = rollback ? "rolled back Rocky" : "awakened Rocky";
            var worn = pawn.apparel.WornApparel.ToList();
            var ids = worn.Select(a => a.ThingID).ToList();
            var hitPoints = worn.Select(a => a.HitPoints).ToList();
            var lockStates = worn.Select(pawn.apparel.IsLocked).ToArray();
            ThingComp comp = ApparelComp(pawn);
            int compCount = pawn.AllComps.Count;
            if (rollback)
            {
                Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            }
            bool converted;
            try
            {
                SentientMechConverter.ForceFailureForTesting = rollback;
                converted = SentientMechConverter.TryConvert(pawn, showMessages: false);
            }
            finally
            {
                SentientMechConverter.ForceFailureForTesting = false;
            }
            Check(converted != rollback && pawn.Spawned && pawn.def == expectedRace && pawn.kindDef == expectedKind, what + " has the expected race and kind");
            if (rollback)
            {
                Check(pawn.AllComps.Count == compCount, what + " regains the original comp count");
            }
            Check(comp != null && ReferenceEquals(ApparelComp(pawn), comp)
                && pawn.AllComps.Count(c => DeadMansSwitchCompat.IsMechApparelComp(CompSignature.Of(c.props))) == 1,
                what + " has the same single apparel comp");
            CheckWorn(pawn, worn, ids, hitPoints, worn.FirstOrDefault(), lockStates.FirstOrDefault(), what);
            Check(worn.Select(pawn.apparel.IsLocked).SequenceEqual(lockStates), what + " keeps every apparel lock");
            return converted;
        }

        private static void CheckAppearance(Pawn pawn, string what)
        {
            Check(pawn.story?.bodyType == BodyTypeDefOf.Male && pawn.story.headType?.defName == "Male_AverageNormal", what + " uses Rocky's body and head types");
            foreach (Apparel apparel in pawn.apparel.WornApparel.Where(a => a.def.defName == "Apparel_CollarShirt" || a.def.defName == "Apparel_SimpleHelmet"))
            {
                string expected = apparel.WornGraphicPath + (apparel.def.defName == "Apparel_CollarShirt" ? "_Male" : "");
                Check(ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, pawn.story.bodyType, forStatue: false, out ApparelGraphicRecord record)
                    && record.graphic.path == expected, what + " resolves the worn graphics for " + apparel.def.defName);
            }
            Check(PortraitsCache.Get(pawn, new Vector2(128f, 128f), Rot4.South) != null, what + " renders a portrait");
        }

        private static void CheckRender(Pawn pawn, string what, bool checkHelmet = false)
        {
            for (int i = 0; i < Facings.Length; i++)
            {
                Rot4 facing = Facings[i];
                Compare(ordinaryRender[i], Measure(pawn, facing), what + " facing " + facing);
                if (checkHelmet)
                {
                    Vector3? offset = HelmetOffset(pawn, facing);
                    Check(offset.HasValue && ordinaryHelmetOffsets[i].HasValue
                        && (offset.Value - ordinaryHelmetOffsets[i].Value).sqrMagnitude < 0.000001f,
                        what + " keeps the helmet position in facing " + facing);
                }
            }
        }

        private static Vector3? HelmetOffset(Pawn pawn, Rot4 facing)
        {
            PawnRenderTree tree = pawn.Drawer.renderer.renderTree;
            if (!tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.ApparelHead, out PawnRenderNode headgearRoot))
            {
                return null;
            }
            PawnRenderNode helmet = headgearRoot.children?.FirstOrDefault(node => node.apparel?.def.defName == "Apparel_SimpleHelmet");
            PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
            parms.facing = facing;
            parms.matrix = Matrix4x4.identity;
            return helmet != null && tree.TryGetMatrix(helmet, parms, out Matrix4x4 matrix)
                ? matrix.MultiplyPoint3x4(Vector3.zero) : null;
        }

        private static Apparel SpawnApparel(Pawn pawn, string defName)
        {
            ThingDef def = ThingDef.Named(defName);
            var apparel = (Apparel)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            GenSpawn.Spawn(apparel, pawn.Position, pawn.Map);
            return apparel;
        }

        private static FloatMenuOption WearOption(Pawn pawn, Apparel apparel)
        {
            var context = new FloatMenuContext(new() { pawn }, apparel.DrawPos, pawn.Map);
            var provider = new FloatMenuOptionProvider_Wear();
            return provider.SelectedPawnValid(pawn, context) && provider.Applies(context) && provider.TargetThingValid(apparel, context)
                ? provider.GetOptionsFor(apparel, context).SingleOrDefault() : null;
        }

        private static void CheckWearMenu(Pawn pawn)
        {
            Apparel helmet = SpawnApparel(pawn, "Apparel_SimpleHelmet");
            Check(WearOption(pawn, helmet) is { Disabled: true } locked && locked.Label.Contains("WouldReplaceLockedApparel".Translate().CapitalizeFirst()),
                "Rocky's Wear menu preserves the locked helmet denial");
            helmet.Destroy();
            Apparel childPants = SpawnApparel(pawn, "Apparel_KidPants");
            Check(ApparelUtility.HasPartsToWear(pawn, childPants.def)
                && !EquipmentUtility.CanEquip(childPants, pawn, out string reason) && !string.IsNullOrEmpty(reason)
                && WearOption(pawn, childPants) is { Disabled: true } wrongAge && wrongAge.Label.Contains(reason),
                "Rocky's Wear menu preserves the base developmental stage denial");
            childPants.Destroy();

            Apparel pants = SpawnApparel(pawn, "Apparel_Pants");
            pantsId = pants.thingIDNumber;
            List<BodyPartGroupDef> groups = pants.def.apparel.bodyPartGroups;
            try
            {
                pants.def.apparel.bodyPartGroups = new List<BodyPartGroupDef> { new BodyPartGroupDef { defName = "MS_RockyTestMissingGroup" } };
                Check(WearOption(pawn, pants) is { Disabled: true } incompatible
                    && incompatible.Label.Contains("CannotWearBecauseOfMissingBodyParts".Translate().CapitalizeFirst()),
                    "Rocky's Wear menu preserves the incompatible body parts denial");
            }
            finally
            {
                pants.def.apparel.bodyPartGroups = groups;
            }
            pawn.drafter.Drafted = true;
            FloatMenuOption option = WearOption(pawn, pants);
            Check(option is { Disabled: false }, "drafted Rocky has an enabled Wear option for compatible pants");
            option?.action?.Invoke();
        }

        internal static void MarkLegacyAppearance(Map map)
        {
            Pawn pawn = FindRocky(map);
            if (pawn == null)
            {
                return;
            }
            Check(pawn.apparel.WornApparel.Any(a => a.thingIDNumber == pantsId), "Rocky finished the drafted Wear job");
            pawnName = pawn.Name.ToString();
            savedApparel = ApparelState(pawn);
            pawn.story.bodyType = DefDatabase<BodyTypeDef>.GetNamed(SentientDefGenerator.BodyTypeName);
            pawn.story.headType = HeadTypeDefOf.Skull;
        }

        internal static void VerifyAfterLoad(Map map, bool firstLoad)
        {
            if (savedApparel == null)
            {
                return;
            }
            Pawn pawn = FindRocky(map);
            Check(pawn != null && SentientMechRegistry.IsSentient(pawn) && pawn.Name.ToString() == pawnName, "Rocky keeps its identity after reload");
            if (pawn == null)
            {
                return;
            }
            string what = firstLoad ? "Rocky loaded from a legacy save" : "Rocky reloaded after repair";
            Check(ApparelState(pawn).SequenceEqual(savedApparel), what + " keeps the saved apparel, hit points and locks");
            CheckAppearance(pawn, what);
            CheckRender(pawn, what, checkHelmet: true);
            Check(!SentientMechGenerator.RepairAppearance(pawn), what + " needs no further appearance repair");
            if (!firstLoad)
            {
                return;
            }
            pawn.drafter.Drafted = false;
            Apparel duster = SpawnApparel(pawn, "Apparel_Duster");
            dusterId = duster.thingIDNumber;
            CheckOutfitOptimizer(pawn, duster);
            FloatMenuOption option = WearOption(pawn, duster);
            Check(option is { Disabled: false }, "undrafted Rocky has an enabled Wear option after loading a legacy save");
            option?.action?.Invoke();
        }

        private static void CheckOutfitOptimizer(Pawn pawn, Apparel upgrade)
        {
            Zone zone = pawn.Map.zoneManager.ZoneAt(upgrade.Position);
            if (zone == null)
            {
                zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, pawn.Map.zoneManager);
                pawn.Map.zoneManager.RegisterZone(zone);
                zone.AddCell(upgrade.Position);
            }
            pawn.mindState.nextApparelOptimizeTick = 0;
            var optimizer = AccessTools.Method("Fortified.HumanlikeMechApparelUtility:TryMakeJob_OptimizeApparel");
            var job = optimizer?.Invoke(null, new object[] { pawn }) as Job;
            Check(job?.def == JobDefOf.Wear && job.targetA.Thing == upgrade, "Fortified's apparel optimizer selects the compatible duster");
        }

        internal static bool PrepareSecondLoad(Map map)
        {
            Pawn pawn = FindRocky(map);
            if (pawn == null)
            {
                return false;
            }
            Check(pawn.apparel.WornApparel.Any(a => a.thingIDNumber == dusterId), "Rocky finished the undrafted Wear job after reload");
            savedApparel = ApparelState(pawn);
            return true;
        }

        private static Pawn FindRocky(Map map)
        {
            return rockyId == 0 ? null : map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == rockyId);
        }

        private static (int Id, int HitPoints, bool Locked)[] ApparelState(Pawn pawn)
        {
            return pawn.apparel.WornApparel.OrderBy(a => a.thingIDNumber)
                .Select(a => (a.thingIDNumber, a.HitPoints, pawn.apparel.IsLocked(a))).ToArray();
        }
    }
}
#endif
