#if DEBUG
using System;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class DmsLadyAutoTest
    {
        private const string LadyKindName = "DMS_Mech_Lady";

        private sealed class RenderSnapshot
        {
            public string Text;
            public string RenderTree;
            public float BodySize;
            public Vector2 KindDrawSize;
            public Vector2 BodyWorldSize;
            public Vector2 HeadWorldSize;
            public Vector3 HeadOffset;
            public bool BodyNode;
            public bool HeadNode;
        }

        internal static void Run(Func<string, Pawn> spawn)
        {
            PawnKindDef ladyKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(LadyKindName);
            if (ladyKind == null || DeadMansSwitchCompat.KindOfOriginal(ladyKind.race) != DmsRaceKind.HumanlikeAutomatroid)
            {
                Log.Message("AUTOTEST info DMS Lady not present, Lady checks skipped");
                return;
            }
            try
            {
                CheckConversion(spawn);
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Lady conversion checks threw " + e);
            }
            try
            {
                CheckDirectGeneration(ladyKind);
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("Lady direct generation checks threw " + e);
            }
        }

        private static void CheckConversion(Func<string, Pawn> spawn)
        {
            Pawn lady = spawn(LadyKindName);
            RenderSnapshot before = Measure(lady);
            Log.Message("AUTOTEST info Lady before conversion " + before.Text);
            Check(before.BodyNode && before.HeadNode, "ordinary Lady has body and head render nodes");

            int ownershipBefore = lady.AllComps.Count(c => DeadMansSwitchCompat.IsOwnershipComp(CompSignature.Of(c.props)));
            Check(SentientMechConverter.TryConvert(lady, showMessages: false), "Lady converted");
            Check(ownershipBefore == 3 && lady.AllComps.Count(c => DeadMansSwitchCompat.IsOwnershipComp(CompSignature.Of(c.props))) == 0, "Lady lost her three ownership comps (had " + ownershipBefore + ")");

            RenderSnapshot after = Measure(lady);
            Log.Message("AUTOTEST info Lady after conversion " + after.Text);
            Compare(before, after, "converted Lady");

            Pawn drawn = spawn(LadyKindName);
            Measure(drawn);
            Check(SentientMechConverter.TryConvert(drawn, showMessages: false), "already drawn Lady converted");
            Compare(before, Measure(drawn), "converted Lady that had already been drawn");

            Pawn rolledBack = spawn(LadyKindName);
            int comps = rolledBack.AllComps.Count;
            Log.Message("AUTOTEST info the next error line from SentientMechConverter (forced failure) is expected");
            SentientMechConverter.ForceFailureForTesting = true;
            bool converted = SentientMechConverter.TryConvert(rolledBack, showMessages: false);
            SentientMechConverter.ForceFailureForTesting = false;
            Check(!converted && !SentientMechRegistry.IsSentient(rolledBack) && rolledBack.AllComps.Count == comps, "forced failure restores the Lady and her comps");
            Compare(before, Measure(rolledBack), "rolled back Lady");
        }

        private static void Compare(RenderSnapshot before, RenderSnapshot after, string what)
        {
            Check(after.BodyNode && after.HeadNode, what + " has body and head render nodes");
            Check(after.RenderTree == before.RenderTree, what + " keeps render tree " + before.RenderTree + " (now " + after.RenderTree + ")");
            Check(Approximately(after.KindDrawSize, before.KindDrawSize), what + " keeps the kind body draw size " + before.KindDrawSize + " (now " + after.KindDrawSize + ")");
            Check(Approximately(after.BodyWorldSize, before.BodyWorldSize), what + " keeps the rendered body size " + before.BodyWorldSize + " (now " + after.BodyWorldSize + ")");
            Check(Approximately(after.HeadWorldSize, before.HeadWorldSize), what + " keeps the rendered head size " + before.HeadWorldSize + " (now " + after.HeadWorldSize + ")");
            Check(Mathf.Approximately(after.HeadOffset.x, before.HeadOffset.x) && Mathf.Approximately(after.HeadOffset.z, before.HeadOffset.z), what + " keeps the head offset " + before.HeadOffset + " (now " + after.HeadOffset + ")");
            Check(Mathf.Approximately(after.BodySize, before.BodySize), what + " keeps the BodySize stat " + before.BodySize + " (now " + after.BodySize + ")");
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private static void CheckDirectGeneration(PawnKindDef ladyKind)
        {
            if (!SentientMechRegistry.TryGetSentient(ladyKind.race, ladyKind, out _, out PawnKindDef sentientKind))
            {
                MechSentienceAutoTest.Fail("no sentient kind for " + LadyKindName);
                return;
            }
            Pawn generated = null;
            try
            {
                generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(sentientKind, Faction.OfPlayer, forceGenerateNewPawn: true));
            }
            catch (Exception e)
            {
                MechSentienceAutoTest.Fail("generating a sentient Lady threw " + e);
            }
            if (generated == null)
            {
                return;
            }
            Check(generated.story?.bodyType != null && generated.story.headType != null && generated.story.hairDef != null, "directly generated sentient Lady has body type, head type and hair (body=" + generated.story?.bodyType?.defName + ", head=" + generated.story?.headType?.defName + ", hair=" + generated.story?.hairDef?.defName + ")");
            Check(generated.style != null && !HasGraphic(generated.style.FaceTattoo) && !HasGraphic(generated.style.BodyTattoo), "directly generated sentient Lady has no graphic tattoos");

            Log.Message("AUTOTEST info the sentient Lady portrait must not log errors");
            RenderTexture portrait = PortraitsCache.Get(generated, new Vector2(128f, 128f), Rot4.South);
            Check(portrait != null, "directly generated sentient Lady renders a portrait");
            RenderSnapshot snapshot = Measure(generated);
            Log.Message("AUTOTEST info directly generated sentient Lady " + snapshot.Text);
            Check(snapshot.BodyNode && snapshot.HeadNode, "directly generated sentient Lady has body and head render nodes");

            HeadTypeDef head = generated.story.headType;
            Check(!SentientMechGenerator.RepairAppearance(generated), "repairing a complete sentient Lady changes nothing");
            generated.story.headType = null;
            TattooDef graphicTattoo = ModsConfig.IdeologyActive ? DefDatabase<TattooDef>.AllDefs.FirstOrDefault(t => !t.noGraphic && t.tattooType == TattooType.Face) : null;
            if (graphicTattoo != null)
            {
                generated.style.FaceTattoo = graphicTattoo;
            }
            Check(SentientMechGenerator.RepairAppearance(generated), "repair fixes a Lady with a missing head type" + (graphicTattoo != null ? " and a graphic tattoo" : ""));
            Check(generated.story.headType != null && generated.story.headType == head && !HasGraphic(generated.style.FaceTattoo), "repair restores the head type and removes the graphic tattoo");
            generated.Drawer.renderer.renderTree.SetDirty();
            Check(Measure(generated).HeadNode, "repaired sentient Lady rebuilds her render tree");

            Map map = Find.CurrentMap;
            GenSpawn.Spawn(generated, CellFinder.RandomClosewalkCellNear(map.mapPawns.FreeColonists.First().Position, map, 6), map);
            Check(generated.Spawned, "directly generated sentient Lady spawns");
            Check(Measure(generated).BodyNode, "spawned directly generated sentient Lady keeps its render tree");
        }

        private static bool HasGraphic(TattooDef tattoo)
        {
            return tattoo != null && !tattoo.noGraphic;
        }

        private static RenderSnapshot Measure(Pawn pawn)
        {
            var result = new RenderSnapshot { RenderTree = pawn.RaceProps.renderTree?.defName ?? "none", BodySize = pawn.BodySize };
            var text = new StringBuilder();
            PawnKindLifeStage stage = pawn.ageTracker.CurKindLifeStage;
            GraphicData bodyData = stage?.bodyGraphicData;
            result.KindDrawSize = bodyData == null ? Vector2.zero : bodyData.drawSize;
            text.Append("def=").Append(pawn.def.defName)
                .Append(" kind=").Append(pawn.kindDef.defName)
                .Append(" renderTree=").Append(result.RenderTree)
                .Append(" baseBodySize=").Append(pawn.RaceProps.baseBodySize)
                .Append(" BodySize=").Append(result.BodySize)
                .Append(" stageIndex=").Append(pawn.ageTracker.CurLifeStageIndex)
                .Append(" lifeStage=").Append(pawn.ageTracker.CurLifeStage?.defName)
                .Append(" bodyType=").Append(pawn.story?.bodyType?.defName)
                .Append(" bodyTypeHeadOffset=").Append(pawn.story?.bodyType?.headOffset)
                .Append(" headType=").Append(pawn.story?.headType?.defName)
                .Append(" kindBody=").Append(bodyData?.texPath).Append("@").Append(result.KindDrawSize)
                .Append(" lifeStageBodyWidth=").Append(pawn.ageTracker.CurLifeStage?.bodyWidth)
                .Append(" lifeStageHeadFactor=").Append(pawn.ageTracker.CurLifeStage?.headSizeFactor)
                .Append(" lifeStageBodySizeFactor=").Append(pawn.ageTracker.CurLifeStage?.bodySizeFactor);

            PawnRenderTree tree = pawn.Drawer.renderer.renderTree;
            tree.EnsureInitialized(PawnRenderFlags.None);
            PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
            parms.pawn = pawn;
            parms.facing = Rot4.South;
            parms.matrix = Matrix4x4.identity;
            if (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body, out PawnRenderNode body))
            {
                result.BodyNode = true;
                result.BodyWorldSize = WorldSize(tree, body, parms, text, "body");
            }
            if (tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out PawnRenderNode head))
            {
                result.HeadNode = true;
                result.HeadWorldSize = WorldSize(tree, head, parms, text, "head");
                head.GetTransform(parms, out Vector3 offset, out _, out _, out _);
                result.HeadOffset = offset;
                text.Append(" headNodeOffset=").Append(offset);
            }
            result.Text = text.ToString();
            return result;
        }

        private static Vector2 WorldSize(PawnRenderTree tree, PawnRenderNode node, PawnDrawParms parms, StringBuilder text, string label)
        {
            Mesh mesh = node.GetMesh(parms);
            Vector2 meshSize = mesh == null ? Vector2.zero : new Vector2(mesh.bounds.size.x, mesh.bounds.size.z);
            Vector2 size = Vector2.zero;
            if (tree.TryGetMatrix(node, parms, out Matrix4x4 matrix))
            {
                Vector3 scale = matrix.lossyScale;
                size = new Vector2(meshSize.x * scale.x, meshSize.y * scale.z);
            }
            Graphic graphic = node.PrimaryGraphic;
            text.Append(" ").Append(label).Append("[class=").Append(node.GetType().Name)
                .Append(" graphic=").Append(graphic?.path).Append("@").Append(graphic?.drawSize)
                .Append(" mesh=").Append(meshSize)
                .Append(" propsDrawSize=").Append(node.Props.drawSize)
                .Append(" drawDataScale=").Append(node.Props.drawData?.ScaleFor(parms.pawn))
                .Append(" workerScale=").Append(node.Worker.ScaleFor(node, parms))
                .Append(" world=").Append(size).Append("]");
            return size;
        }

        private static void Check(bool condition, string what)
        {
            MechSentienceAutoTest.Check(condition, what);
        }
    }
}
#endif
