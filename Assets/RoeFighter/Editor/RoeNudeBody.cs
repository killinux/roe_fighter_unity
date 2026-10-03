using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// 爆衣 step 2, the whole body under the outfit (user 10-03: "做个inase结合nude的模型，让爆衣都爆掉"): the game
    /// deleted the skin under the battle suits, so pieces with nothing under them could not come off.  The family's
    /// nude base (a01 for Inase, g01 for Luf; the game's own body for the H scenes, Assets/ROE/&lt;family&gt;01) lies on the
    /// suit's skin within a millimetre (docs/clothes-burst.md section 2), so it replaces the suit's skin as a whole -
    /// no seam where the two meet, the same skin everywhere.
    ///
    /// Its own skeleton has dozens of bones the suit's lacks, so it is not bound by bone names: every vertex takes
    /// the bone weights of what lies on it in the bind pose (weight transfer) - the suit's skin within 1.5 cm, else,
    /// where the game cut the skin away, whatever is nearest: the tight piece over it (bra armour, shin guard, glove).
    /// The body then moves exactly like the skin and armour it replaces.  Triangles that lie only under pieces that
    /// never come off (g04's shoes: her feet stand at another heel angle than the base's) are left out, so nothing
    /// pokes through them.
    ///
    /// Rules: the "nude" block of Editor/Burst/&lt;id&gt;.json (prefab, renderer, submesh, material of the base; the suit
    /// renderer it goes next to; the suit's skin it replaces).  Called by RoeBurstBuilder.Apply before the outfit is
    /// split.
    /// </summary>
    public static class RoeNudeBody
    {
        public const float SkinReach = 0.015f;       // the suit's skin this close is what the body copies
        const float Cell = 0.02f;

        class Grid
        {
            readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
            readonly List<Vector3> points;

            public Grid(List<Vector3> points, IEnumerable<int> which)
            {
                this.points = points;
                foreach (int i in which)
                {
                    var key = Key(points[i]);
                    if (!cells.TryGetValue(key, out var list))
                        cells[key] = list = new List<int>();
                    list.Add(i);
                }
            }

            static Vector3Int Key(Vector3 p) => Vector3Int.FloorToInt(p / Cell);

            /// <summary>Points within radius, with their distance.</summary>
            public void Near(Vector3 p, float radius, List<(int i, float d)> into)
            {
                into.Clear();
                int k = Mathf.CeilToInt(radius / Cell);
                var c = Key(p);
                float r2 = radius * radius;
                for (int x = -k; x <= k; x++)
                    for (int y = -k; y <= k; y++)
                        for (int z = -k; z <= k; z++)
                            if (cells.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out var list))
                                foreach (int i in list)
                                {
                                    float d2 = (points[i] - p).sqrMagnitude;
                                    if (d2 <= r2)
                                        into.Add((i, Mathf.Sqrt(d2)));
                                }
            }
        }

        /// <summary>
        /// The nude body as a renderer next to rules.nude.attachTo, on the suit's bones.  Null when the character has
        /// no "nude" block or something is missing (logged).
        /// </summary>
        public static SkinnedMeshRenderer Build(GameObject model, string id, RoeBurstBuilder.Rules rules, RoeBurstBuilder.Analysis analysis,
                                                string dir, StringBuilder log)
        {
            var n = rules.nude;
            if (n == null || string.IsNullOrEmpty(n.prefab))
                return null;
            var root = model.transform;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).ToArray();
            var target = renderers.FirstOrDefault(r => r.name == n.attachTo);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(n.prefab);
            var nudeRenderer = prefab != null ? prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == n.renderer) : null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(n.material);
            if (target == null || nudeRenderer == null || nudeRenderer.sharedMesh == null || material == null)
            {
                Debug.LogWarning($"[ROE] nude body {id}: missing - suit renderer '{n.attachTo}' {(target != null)}, base prefab {n.prefab} {(prefab != null)}, " +
                                 $"its renderer '{n.renderer}' {(nudeRenderer != null)}, material {n.material} {(material != null)}");
                return null;
            }
            var mesh = nudeRenderer.sharedMesh;
            Matrix4x4 ToModel(Transform t) => root.worldToLocalMatrix * t.localToWorldMatrix;
            var nudeToModel = prefab.transform.worldToLocalMatrix * nudeRenderer.transform.localToWorldMatrix;
            var modelToTarget = ToModel(target.transform).inverse;

            // ---- what the body copies its weights from: the suit's skin it replaces, and the outfit
            var points = new List<Vector3>();
            var skin = new List<bool>();
            var permanent = new List<bool>();
            var tight = new List<bool>();             // outfit points of pieces that lie on the body
            var influence = new List<(Transform bone, float weight)[]>();
            var bindpose = new Dictionary<Transform, Matrix4x4>();
            void Add(SkinnedMeshRenderer r, IEnumerable<int> vertices, bool isSkin, Func<int, bool> stays, Func<int, bool> lies = null)
            {
                var m = r.sharedMesh;
                var v = m.vertices;
                var perVertex = m.GetBonesPerVertex();
                var all = m.GetAllBoneWeights();
                var start = new int[m.vertexCount + 1];
                for (int i = 0; i < m.vertexCount && i < perVertex.Length; i++)
                    start[i + 1] = start[i] + perVertex[i];
                var poses = m.bindposes;
                var toTargetSpace = r.transform.worldToLocalMatrix * target.transform.localToWorldMatrix;
                var bones = r.bones;
                for (int b = 0; b < bones.Length && b < poses.Length; b++)
                    if (bones[b] != null && !bindpose.ContainsKey(bones[b]))
                        bindpose[bones[b]] = poses[b] * toTargetSpace;
                var toModel = ToModel(r.transform);
                foreach (int i in vertices)
                {
                    if (i >= perVertex.Length)
                        continue;
                    var list = new List<(Transform, float)>();
                    for (int j = start[i]; j < start[i + 1]; j++)
                        if (all[j].boneIndex < bones.Length && bones[all[j].boneIndex] != null && all[j].weight > 0f)
                            list.Add((bones[all[j].boneIndex], all[j].weight));
                    if (list.Count == 0)
                        continue;
                    points.Add(toModel.MultiplyPoint3x4(v[i]));
                    skin.Add(isSkin);
                    permanent.Add(stays(i));
                    tight.Add(lies != null && lies(i));
                    influence.Add(list.ToArray());
                }
            }
            foreach (var s in n.replace ?? new RoeBurstBuilder.Outfit[0])
            {
                var r = renderers.FirstOrDefault(x => x.name == s.renderer);
                if (r == null)
                    continue;
                var subs = s.submesh >= 0 ? new[] { s.submesh } : Enumerable.Range(0, r.sharedMesh.subMeshCount).ToArray();
                Add(r, subs.SelectMany(k => r.sharedMesh.GetIndices(k)).Distinct(), true, _ => false);
            }
            // ---- the body's own vertices (the base's body submesh)
            var tris = mesh.GetTriangles(n.submesh);
            var used = tris.Distinct().OrderBy(i => i).ToList();
            var nv = mesh.vertices;
            var position = new Dictionary<int, Vector3>();
            foreach (int i in used)
                position[i] = nudeToModel.MultiplyPoint3x4(nv[i]);
            var bodyPoints = used.Select(i => position[i]).ToList();
            var bodyGrid = new Grid(bodyPoints, Enumerable.Range(0, bodyPoints.Count));
            var near = new List<(int i, float d)>();
            float ToBody(Vector3 q, float limit)
            {
                bodyGrid.Near(q, limit, near);
                return near.Count > 0 ? near.Min(x => x.d) : limit;
            }

            // the outfit: a piece is tight when it lies on the body (median distance of its vertices at most 2.5 cm) -
            // armour, bra, gloves, shoes; skirt panels, chains and ribbons hang away from it and give no weights
            int tightParts = 0, looseParts = 0;
            foreach (var g in analysis.parts.GroupBy(x => x.renderer))
            {
                var r = g.Key;
                var stays = new Dictionary<int, bool>();
                var onBody = new Dictionary<int, bool>();
                var toModel = ToModel(r.transform);
                var rv = r.sharedMesh.vertices;
                foreach (var part in g)
                {
                    bool forever = part.group == null || part.group.stage <= 0;
                    var verts = new HashSet<int>();
                    foreach (var kv in part.triangles)
                    {
                        var t = r.sharedMesh.GetTriangles(kv.Key);
                        foreach (int tri in kv.Value)
                            for (int c = 0; c < 3; c++)
                                verts.Add(t[tri * 3 + c]);
                    }
                    var dist = verts.Select(v => ToBody(toModel.MultiplyPoint3x4(rv[v]), 0.05f)).OrderBy(x => x).ToList();
                    bool lies = dist.Count > 0 && dist[dist.Count / 2] <= 0.025f;
                    if (lies)
                        tightParts++;
                    else
                        looseParts++;
                    foreach (int v in verts)
                    {
                        stays[v] = forever;
                        onBody[v] = lies;
                    }
                }
                Add(r, stays.Keys, false, i => stays[i], i => onBody[i]);
            }
            int skinCount = skin.Count(x => x);
            var all = Enumerable.Range(0, points.Count).ToList();
            var skinGrid = new Grid(points, all.Where(i => skin[i]));
            var tightGrid = new Grid(points, all.Where(i => !skin[i] && tight[i]));
            var foreverGrid = new Grid(points, all.Where(i => !skin[i] && permanent[i]));
            var allGrid = new Grid(points, all);

            // ---- each vertex takes the weights of what lies on it: the suit's skin within 1.5 cm; else a tight piece
            // clearly nearer than any skin (where the game cut the skin away); else the nearest skin
            var weightsOf = new Dictionary<int, (Transform bone, float weight)[]>();
            var under = new HashSet<int>();         // only under pieces that never come off
            int fromSkin = 0, fromOutfit = 0, lost = 0;
            var skinGap = new List<float>();
            (float d, List<(int i, float d)> list) Nearest(Grid grid, Vector3 q, float radius, float band)
            {
                grid.Near(q, radius, near);
                if (near.Count == 0)
                    return (float.PositiveInfinity, null);
                float d0 = near.Min(x => x.d);
                return (d0, near.Where(x => x.d <= d0 + band).OrderBy(x => x.d).Take(8).ToList());
            }
            foreach (int i in used)
            {
                var p = position[i];
                List<(int i, float d)> pick = null;
                var (ds, onSkin) = Nearest(skinGrid, p, 0.08f, 0.005f);
                if (ds <= SkinReach)
                {
                    skinGap.Add(ds);
                    pick = onSkin;
                    fromSkin++;
                }
                else
                {
                    var (dt, onTight) = Nearest(tightGrid, p, 0.08f, 0.01f);
                    if (onTight != null && dt < ds - 0.005f)
                    {
                        pick = onTight;
                        fromOutfit++;
                    }
                    else if (onSkin != null)
                    {
                        pick = onSkin;
                        fromSkin++;
                    }
                    else
                        foreach (float radius in new[] { 0.16f, 0.32f })
                        {
                            var (_, any) = Nearest(allGrid, p, radius, 0.01f);
                            if (any == null)
                                continue;
                            pick = any;
                            fromOutfit++;
                            break;
                        }
                    // under a piece that never comes off (g04's shoes): left out, it would only poke through
                    foreverGrid.Near(p, 0.025f, near);
                    if (near.Count > 0)
                        under.Add(i);
                }
                if (pick == null)
                {
                    lost++;
                    continue;
                }
                var sum = new Dictionary<Transform, float>();
                foreach (var (k, d) in pick)
                {
                    float w = 1f / ((d + 0.002f) * (d + 0.002f));
                    foreach (var (bone, weight) in influence[k])
                        sum[bone] = (sum.TryGetValue(bone, out float s) ? s : 0f) + w * weight;
                }
                var top = sum.OrderByDescending(kv => kv.Value).Take(4).ToArray();
                float total = top.Sum(kv => kv.Value);
                weightsOf[i] = top.Select(kv => (kv.Key, kv.Value / total)).ToArray();
            }

            // ---- triangles: all but those only under pieces that never come off (and any with a vertex left without weights)
            var keep = new List<int>();
            int dropped = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (!weightsOf.ContainsKey(a) || !weightsOf.ContainsKey(b) || !weightsOf.ContainsKey(c) ||
                    (under.Contains(a) && under.Contains(b) && under.Contains(c)))
                {
                    dropped++;
                    continue;
                }
                keep.Add(a);
                keep.Add(b);
                keep.Add(c);
            }

            // ---- the mesh, in the suit renderer's space, on the suit's bones
            var order = keep.Distinct().ToList();
            var map = new Dictionary<int, int>();
            for (int k = 0; k < order.Count; k++)
                map[order[k]] = k;
            var toTarget = modelToTarget * nudeToModel;
            string meshPath = $"{dir}/nude_{RoeBurstBuilder.Safe(n.renderer)}.asset";
            var outMesh = RoeBurstBuilder.MeshAsset(meshPath);
            outMesh.indexFormat = order.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            outMesh.vertices = order.Select(i => toTarget.MultiplyPoint3x4(nv[i])).ToArray();
            if (mesh.HasVertexAttribute(VertexAttribute.Normal))
            {
                var nn = mesh.normals;
                outMesh.normals = order.Select(i => toTarget.MultiplyVector(nn[i]).normalized).ToArray();
            }
            if (mesh.HasVertexAttribute(VertexAttribute.Tangent))
            {
                var tt = mesh.tangents;
                outMesh.tangents = order.Select(i =>
                {
                    var d = toTarget.MultiplyVector(new Vector3(tt[i].x, tt[i].y, tt[i].z)).normalized;
                    return new Vector4(d.x, d.y, d.z, tt[i].w);
                }).ToArray();
            }
            if (mesh.HasVertexAttribute(VertexAttribute.Color))
            {
                var cc = mesh.colors32;
                outMesh.colors32 = order.Select(i => cc[i]).ToArray();
            }
            for (int ch = 0; ch < 8; ch++)
            {
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0 + ch))
                    continue;
                var l = new List<Vector4>();
                mesh.GetUVs(ch, l);
                int dim = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0 + ch);
                if (dim <= 2)
                    outMesh.SetUVs(ch, order.Select(i => (Vector2)l[i]).ToList());
                else if (dim == 3)
                    outMesh.SetUVs(ch, order.Select(i => (Vector3)l[i]).ToList());
                else
                    outMesh.SetUVs(ch, order.Select(i => l[i]).ToList());
            }
            var bones = weightsOf.Values.SelectMany(w => w.Select(x => x.bone)).Distinct().ToList();
            var boneIndex = new Dictionary<Transform, int>();
            for (int k = 0; k < bones.Count; k++)
                boneIndex[bones[k]] = k;
            var counts = new NativeArray<byte>(order.Count, Allocator.Temp);
            var flat = new List<BoneWeight1>();
            for (int k = 0; k < order.Count; k++)
            {
                var w = weightsOf[order[k]];
                counts[k] = (byte)w.Length;
                foreach (var (bone, weight) in w)
                    flat.Add(new BoneWeight1 { boneIndex = boneIndex[bone], weight = weight });
            }
            var weights = new NativeArray<BoneWeight1>(flat.ToArray(), Allocator.Temp);
            outMesh.bindposes = bones.Select(b => bindpose[b]).ToArray();
            outMesh.SetBoneWeights(counts, weights);
            counts.Dispose();
            weights.Dispose();
            outMesh.SetTriangles(keep.Select(i => map[i]).ToArray(), 0, false);
            outMesh.RecalculateBounds();
            var asset = RoeBurstBuilder.Commit(outMesh, meshPath);

            var go = new GameObject("nude body") { layer = target.gameObject.layer };
            go.transform.SetParent(target.transform.parent, false);
            go.transform.localPosition = target.transform.localPosition;
            go.transform.localRotation = target.transform.localRotation;
            go.transform.localScale = target.transform.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = asset;
            smr.bones = bones.ToArray();
            smr.rootBone = target.rootBone;
            smr.sharedMaterials = new[] { material };
            smr.localBounds = target.localBounds;
            smr.updateWhenOffscreen = target.updateWhenOffscreen;
            smr.quality = target.quality;
            smr.skinnedMotionVectors = target.skinnedMotionVectors;
            smr.shadowCastingMode = target.shadowCastingMode;
            smr.receiveShadows = target.receiveShadows;
            smr.lightProbeUsage = target.lightProbeUsage;
            smr.reflectionProbeUsage = target.reflectionProbeUsage;
            smr.probeAnchor = target.probeAnchor;
            smr.forceMatrixRecalculationPerRender = target.forceMatrixRecalculationPerRender;
            smr.allowOcclusionWhenDynamic = target.allowOcclusionWhenDynamic;
            smr.renderingLayerMask = target.renderingLayerMask;

            skinGap.Sort();
            float Pct(float q) => skinGap.Count > 0 ? skinGap[Mathf.Clamp(Mathf.RoundToInt(q * (skinGap.Count - 1)), 0, skinGap.Count - 1)] * 1000f : -1f;
            log.Append($"\n[ROE]   nude body from {Path.GetFileName(n.prefab)} {n.renderer}[{n.submesh}]: {used.Count} vertices, " +
                       $"{fromSkin} take the suit's skin (gap median {Pct(0.5f):F2} mm, p95 {Pct(0.95f):F2} mm), {fromOutfit} the outfit over them, " +
                       $"{lost} nothing; {dropped} of {tris.Length / 3} triangles left out (under pieces that never come off); " +
                       $"outfit pieces on the body {tightParts}, hanging away {looseParts}; " +
                       $"{bones.Count} bones; {skinCount} skin + {points.Count - skinCount} outfit points; material {material.name} ({material.shader.name})");
            return smr;
        }
    }
}
