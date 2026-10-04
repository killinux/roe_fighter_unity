using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// A Dead or Alive 6 character's physics onto her fighter prefab (user 10-04: "看看乳摇，头发，衣服物理是怎么做的，是否比目前的
    /// 物理做得更好" / "最好做成可插拔的设计").  Reads Assets/DOA/&lt;id&gt;/physics.json (tools/doa6_physics.py, from her game data) and
    ///   - maps G1M model space onto the prefab: x mirrored (G1M +X is her left, Unity's -X), cm -> m, hips onto the hips;
    ///     checked on every chain and swing bone (the log gives the worst miss);
    ///   - strings each bone chain into a parent chain (the game keeps the ponytail and sash bones as flat children of the
    ///     head / hips; same world pose, the skin does not change);
    ///   - makes a node per soft-body lattice point (under the soft body's bone), and a pivot on the chest wall for each
    ///     breast (the centroid of its pinned nodes; the breast bone hangs on it, so a bone solver can swing it as one);
    ///   - skins the breast meshes to the nodes: per vertex the 8 lattice nodes of its cell (trilinear weights) times
    ///     (1 - blend), the original bone weights times blend (DOA6 blends back to plain skinning towards the body).
    ///     The vertices are matched to the game's by position and UV; new meshes in Assets/DOA/&lt;id&gt;/meshes;
    ///   - puts it all in a RoeDoaRig component on the prefab (colliders on their bones, chains, swing bones, soft bodies
    ///     with each node's animated target, springs from the lattice, the hull for the volume, attachments).
    ///   -executeMethod RoeFighter.EditorTools.DoaPhysicsBuilder.Build [-roeDoa kas]
    /// </summary>
    public static class DoaPhysicsBuilder
    {
        /// <summary>
        /// How the seams ease into plain skinning: a vertex's blend b (the game's, 0 at the front of the breast, 1 where the
        /// mesh meets the body) taken as 1 - (1 - b)^SeamEase.  1 = the game's own; 2 (default) keeps the cups' lower edge
        /// closer to the corset under them - with the game's, a 1.5 cm bounce opened a gap there (the corset is no soft mesh).
        /// </summary>
        public static float SeamEase = 2f;

        // ---- physics.json, as tools/doa6_physics.py writes it
        [Serializable] class JCollider { public int type, bone; public float a, b, c; public float[] pos, rot; }
        [Serializable] class JGroup { public string name; public int[] colliders; }
        [Serializable] class JChain { public string name, kind; public int parent; public int[] bones; public float[] rest, restLen, t, @params; public string[] groups; public float length; }
        [Serializable] class JSwing { public int bone, parent, group; public float[] pos, @params; public string kind; }
        [Serializable] class JNode { public float[] pos; public int flags; public int[] bones; public float[] weights; public float wSelf; public int[] n6; }
        [Serializable] class JSoft { public string name, kind; public int parent; public float gravity, damping, stiffness, scale, restVolume; public float[] perAxis, coef, axis; public string[] groups; public JNode[] nodes; public int[] hull; }
        [Serializable] class JVertex { public float[] pos, uv, w; public int[] nodes; public float blend; }
        [Serializable] class JSoftMesh { public string mesh; public int body; public JVertex[] vertices; }
        [Serializable] class JRef { public int body; public float weight; public int[] nodes; public float[] w; }
        [Serializable] class JAttachment { public int bone; public JRef[] refs; }
        [Serializable] class JTwist { public int bone, source, toward; public float share; public string name; }
        [Serializable] class JData
        {
            public string[] source;
            public JCollider[] colliders;
            public JGroup[] groups;
            public JChain[] chains;
            public JSwing[] swings;
            public JSoft[] softs;
            public JSoftMesh[] softMeshes;
            public JAttachment[] attachments;
            public JTwist[] twists;
        }

        public static void Build()
        {
            foreach (var id in RoeCapture.Arg("-roeDoa", "kas").Split(','))
                Build(id.Trim());
            AssetDatabase.SaveAssets();
        }

        public static void Build(string id)
        {
            string jsonPath = $"{DoaFighter.Dir(id)}/physics.json";
            if (!File.Exists(jsonPath))
            {
                Debug.LogWarning($"[ROE] {id}: no {jsonPath} (tools/doa6_physics.py): no DOA6 physics");
                return;
            }
            var data = JsonUtility.FromJson<JData>(File.ReadAllText(jsonPath));
            string prefabPath = RoeHumanoid.FighterPath(id);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var notes = new List<string>();
            try
            {
                var byName = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (!byName.ContainsKey(t.name))
                        byName[t.name] = t;
                Transform Bone(int gid) => byName.TryGetValue($"bone_{gid}", out var t) ? t : null;
                var old = root.GetComponent<RoeDoaRig>();
                if (old != null)
                {
                    Debug.LogWarning($"[ROE] {id}: the prefab already has a RoeDoaRig - rebuild the prefab first (DoaFighter.Build), then this");
                    return;
                }
                var rig = root.AddComponent<RoeDoaRig>();
                rig.source = string.Join(" + ", data.source);

                // each bone's place in the bind pose, from the skinned meshes: the fighter prefab has its arms turned to a
                // T pose for the avatar (RoeHumanoid.EnforceTPose, up to 15 deg), the game's data is in the bind pose
                var bind = new Dictionary<Transform, Matrix4x4>();      // world -> bone, at bind
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bp = r.sharedMesh != null ? r.sharedMesh.bindposes : null;
                    if (bp == null || bp.Length != r.bones.Length)
                        continue;
                    for (int i = 0; i < bp.Length; i++)
                        if (r.bones[i] != null && !bind.ContainsKey(r.bones[i]))
                            bind[r.bones[i]] = bp[i] * r.transform.worldToLocalMatrix;
                }
                Vector3 BindLocal(Transform b, Vector3 world) => bind.TryGetValue(b, out var m) ? m.MultiplyPoint3x4(world) : b.InverseTransformPoint(world);
                Quaternion BindRotation(Transform b) => bind.TryGetValue(b, out var m) ? m.inverse.rotation : b.rotation;
                Vector3 BindPosition(Transform b) => bind.TryGetValue(b, out var m) ? m.inverse.MultiplyPoint3x4(Vector3.zero) : b.position;
                int moved = bind.Count(kv => Vector3.Distance(kv.Key.position, kv.Value.inverse.MultiplyPoint3x4(Vector3.zero)) > 0.001f);
                notes.Add($"bind pose of {bind.Count} bones from the meshes ({moved} stand elsewhere in the prefab: the T pose)");

                // G1M model space -> the prefab (root at the origin, unrotated): mirror x, cm -> m, the hips onto the hips
                var hips = Bone(2);
                var offset = hips.position;
                Vector3 M(float[] p) => new Vector3(-p[0], p[1], p[2]) * 0.01f + offset;
                Quaternion R(float[] q) => new Quaternion(q[0], -q[1], -q[2], q[3]);
                float worst = 0f;
                int checkedBones = 0;
                foreach (var ch in data.chains)
                    for (int i = 0; i < ch.bones.Length; i++)
                    {
                        var b = Bone(ch.bones[i]);
                        if (b == null)
                            continue;
                        worst = Mathf.Max(worst, Vector3.Distance(b.position, M(new[] { ch.rest[3 * i], ch.rest[3 * i + 1], ch.rest[3 * i + 2] })));
                        checkedBones++;
                    }
                foreach (var s in data.swings)
                    if (Bone(s.bone) is Transform b)
                    {
                        worst = Mathf.Max(worst, Vector3.Distance(b.position, M(s.pos)));
                        checkedBones++;
                    }
                notes.Add($"G1M -> prefab: x mirrored, cm -> m, hips at {offset}; {checkedBones} chain / swing bones within {worst * 1000f:F2} mm");
                if (worst > 0.005f)
                    Debug.LogWarning($"[ROE] {id}: the physics data misses her bones by up to {worst * 100f:F1} cm - check the mapping");

                // the twist helpers her game's rig script drives (RoeDoaRig.DriveHelpers)
                foreach (var tw in data.twists ?? new JTwist[0])
                {
                    var b = Bone(tw.bone);
                    var src = Bone(tw.source);
                    var toward = Bone(tw.toward);
                    if (b == null || src == null || toward == null)
                        continue;
                    var srcRot = BindRotation(src);
                    rig.twists.Add(new RoeDoaRig.Twist
                    {
                        bone = b, source = src, share = tw.share,
                        axis = (Quaternion.Inverse(srcRot) * (BindPosition(toward) - BindPosition(src))).normalized,
                        sourceRest = src.parent != null ? Quaternion.Inverse(BindRotation(src.parent)) * srcRot : srcRot,
                        restToSource = Quaternion.Inverse(srcRot) * BindRotation(b),
                    });
                }
                if (rig.twists.Count > 0)
                    notes.Add($"{rig.twists.Count} twist helpers: " + string.Join(", ", (data.twists ?? new JTwist[0]).Select(t => $"{t.name} {t.share:0.#}")));

                // colliders (those on bones she has), then the groups by their new numbers
                var colliderIndex = new Dictionary<int, int>();
                for (int i = 0; i < data.colliders.Length; i++)
                {
                    var c = data.colliders[i];
                    var b = Bone(c.bone);
                    if (b == null)
                        continue;
                    colliderIndex[i] = rig.colliders.Count;
                    rig.colliders.Add(new RoeDoaRig.Collider
                    {
                        bone = b, type = c.type, a = c.a * 0.01f, b = c.b * 0.01f, c = c.c * 0.01f,
                        center = BindLocal(b, M(c.pos)), rotation = Quaternion.Inverse(BindRotation(b)) * R(c.rot),
                    });
                }
                var groupIndex = new Dictionary<string, int>();
                foreach (var g in data.groups)
                {
                    groupIndex[g.name] = rig.groups.Count;
                    rig.groups.Add(new RoeDoaRig.Group { name = g.name, colliders = g.colliders.Where(colliderIndex.ContainsKey).Select(i => colliderIndex[i]).ToArray() });
                }
                int[] Groups(string[] names) => names.Where(groupIndex.ContainsKey).Select(n => groupIndex[n]).ToArray();

                // chains: strung into parent chains
                foreach (var ch in data.chains)
                {
                    var bones = ch.bones.Select(Bone).ToArray();
                    if (bones.Any(b => b == null))
                    {
                        notes.Add($"{ch.name}: bones missing, left out");
                        continue;
                    }
                    for (int i = 1; i < bones.Length; i++)
                        bones[i].SetParent(bones[i - 1], true);
                    rig.chains.Add(new RoeDoaRig.Chain
                    {
                        name = ch.name, kind = ch.kind, parent = Bone(ch.parent), bones = bones,
                        restLength = bones.Select((b, i) => i == 0 ? 0f : Vector3.Distance(bones[i - 1].position, b.position)).ToArray(),
                        param = ch.@params, groups = Groups(ch.groups),
                    });
                }
                // swing bones
                foreach (var s in data.swings)
                    if (Bone(s.bone) is Transform b)
                        rig.swings.Add(new RoeDoaRig.Swing { bone = b, group = s.group, param = s.@params });

                // soft bodies: a node per lattice point; a pivot on the chest wall for a breast
                var nodeLists = new List<Transform[]>();
                foreach (var s in data.softs)
                {
                    var parent = Bone(s.parent);
                    var wall = s.nodes.Where(n => (n.flags & 1) != 0).Select(n => M(n.pos)).ToList();
                    if (s.kind == "breast" && wall.Count > 0 && parent.parent != null)
                    {
                        var pivot = new GameObject($"{parent.name}_pivot").transform;
                        pivot.SetParent(parent.parent, false);
                        pivot.position = wall.Aggregate(Vector3.zero, (a, p) => a + p) / wall.Count;
                        pivot.rotation = parent.rotation;
                        parent.SetParent(pivot, true);
                    }
                    var nodes = new Transform[s.nodes.Length];
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var n = new GameObject($"{parent.name}_node{i:D3}").transform;
                        n.SetParent(parent, false);
                        n.position = M(s.nodes[i].pos);
                        n.rotation = Quaternion.identity;
                        nodes[i] = n;
                    }
                    nodeLists.Add(nodes);
                    var soft = new RoeDoaRig.Soft
                    {
                        name = s.name, kind = s.kind, parent = parent, nodes = nodes,
                        flags = s.nodes.Select(n => n.flags).ToArray(), wSelf = s.nodes.Select(n => n.wSelf).ToArray(),
                        gravity = s.gravity, damping = s.damping, stiffness = s.stiffness, scale = s.scale,
                        restDown = Quaternion.Inverse(parent.rotation) * Vector3.down,
                        perAxis = s.perAxis != null && s.perAxis.Length == 3 ? new Vector3(s.perAxis[0], s.perAxis[1], s.perAxis[2]) * 0.01f : Vector3.zero,
                        coef = s.coef, groups = Groups(s.groups), hull = s.hull,
                    };
                    // targets: each node skinned to body bones, as at rest
                    var start = new List<int>();
                    var tb = new List<Transform>();
                    var tl = new List<Vector3>();
                    var tw = new List<float>();
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        start.Add(tb.Count);
                        var n = s.nodes[i];
                        for (int k = 0; k < n.bones.Length; k++)
                        {
                            var b = Bone(n.bones[k]);
                            if (b == null || n.weights[k] <= 0f)
                                continue;
                            tb.Add(b);
                            tl.Add(BindLocal(b, nodes[i].position));
                            tw.Add(n.weights[k]);
                        }
                    }
                    start.Add(tb.Count);
                    soft.targetStart = start.ToArray();
                    soft.targetBone = tb.ToArray();
                    soft.targetLocal = tl.ToArray();
                    soft.targetWeight = tw.ToArray();
                    // springs: the lattice's edges (6 neighbours), then the diagonals of its faces
                    var pairs = new List<(int, int)>();
                    var seen = new HashSet<(int, int)>();
                    void Pair(int a, int b)
                    {
                        if (a < 0 || b < 0 || a == b)
                            return;
                        var key = a < b ? (a, b) : (b, a);
                        if (seen.Add(key))
                            pairs.Add(key);
                    }
                    for (int i = 0; i < nodes.Length; i++)
                        foreach (var j in s.nodes[i].n6)
                            Pair(i, j);
                    soft.edgeCount = pairs.Count;
                    for (int i = 0; i < nodes.Length; i++)
                        for (int a = 0; a < 6; a++)
                            for (int b = 0; b < 6; b++)
                            {
                                if (a / 2 == b / 2)
                                    continue;
                                int j = s.nodes[i].n6[a];
                                if (j >= 0)
                                    Pair(i, s.nodes[j].n6[b]);
                            }
                    soft.springs = pairs.SelectMany(p => new[] { p.Item1, p.Item2 }).ToArray();
                    soft.springRest = pairs.Select(p => Vector3.Distance(nodes[p.Item1].position, nodes[p.Item2].position)).ToArray();
                    soft.restVolume = Mathf.Abs(Volume(nodes.Select(n => n.position).ToArray(), s.hull));
                    rig.softs.Add(soft);
                    notes.Add($"{s.name} ({s.kind}): {nodes.Length} nodes ({soft.flags.Count(f => (f & 1) != 0)} pinned), {soft.edgeCount} edges + " +
                              $"{pairs.Count - soft.edgeCount} diagonals, hull {s.hull.Length / 3} triangles {soft.restVolume * 1e3f:F2} L, gravity {s.gravity}, " +
                              $"damping {s.damping}, stiffness {s.stiffness}, groups {string.Join(" ", s.groups)}");
                }

                // the breast meshes onto the nodes
                string meshDir = $"{DoaFighter.Dir(id)}/meshes";
                Directory.CreateDirectory(meshDir);
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name, r => r);
                foreach (var sm in data.softMeshes)
                {
                    if (!renderers.TryGetValue(sm.mesh, out var smr) || sm.body >= nodeLists.Count)
                    {
                        notes.Add($"{sm.mesh}: no renderer");
                        continue;
                    }
                    notes.Add(Rebind(smr, sm, nodeLists[sm.body], M, $"{meshDir}/{sm.mesh}_soft.asset"));
                }

                // bones placed from the soft body (the cleavage)
                foreach (var a in data.attachments)
                {
                    var b = Bone(a.bone);
                    if (b == null)
                        continue;
                    var att = new RoeDoaRig.Attachment
                    {
                        bone = b, body = a.refs.Select(r => r.body).ToArray(), weight = a.refs.Select(r => r.weight).ToArray(),
                        nodes = a.refs.SelectMany(r => r.nodes).ToArray(), nodeWeight = a.refs.SelectMany(r => r.w).ToArray(),
                    };
                    var at = Vector3.zero;
                    for (int r = 0; r < att.body.Length; r++)
                        for (int k = 0; k < 8; k++)
                            at += att.weight[r] * att.nodeWeight[8 * r + k] * nodeLists[att.body[r]][att.nodes[8 * r + k]].position;
                    att.offset = b.position - at;
                    rig.attachments.Add(att);
                    notes.Add($"bone_{a.bone} placed from {att.body.Length} soft-body cells (rest miss {att.offset.magnitude * 1000f:F1} mm)");
                }
                Debug.Log($"[ROE] {id}: DOA6 physics ({rig.source}): {rig.groups.Count} collider groups ({rig.colliders.Count} colliders), " +
                          $"{rig.chains.Count} chains ({string.Join(", ", rig.chains.Select(c => $"{c.kind} {c.bones.Length}"))}), {rig.swings.Count} swing bones, " +
                          $"{rig.softs.Count} soft bodies, {rig.attachments.Count} attachments\n[ROE]   " + string.Join("\n[ROE]   ", notes));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Signed volume of a closed triangle hull (m^3).</summary>
        public static float Volume(Vector3[] p, int[] tris)
        {
            float v = 0f;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                v += Vector3.Dot(p[tris[t]], Vector3.Cross(p[tris[t + 1]], p[tris[t + 2]])) / 6f;
            return v;
        }

        /// <summary>
        /// A soft-body mesh skinned to its lattice nodes: each vertex matched to the game's by position (and UV), then its
        /// weights = the 8 nodes of its cell * (1 - blend) + its own bone weights * blend.  Returns a line for the log.
        /// </summary>
        static string Rebind(SkinnedMeshRenderer smr, JSoftMesh sm, Transform[] nodes, Func<float[], Vector3> M, string assetPath)
        {
            var src = smr.sharedMesh;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_soft";
            var verts = src.vertices;
            var uvs = src.uv;
            // the game's vertices by place (1 mm cells)
            var cells = new Dictionary<Vector3Int, List<int>>();
            Vector3Int Key(Vector3 p) => Vector3Int.RoundToInt(p * 1000f);
            var world = sm.vertices.Select(v => M(v.pos)).ToArray();
            for (int i = 0; i < world.Length; i++)
            {
                var k = Key(world[i]);
                if (!cells.TryGetValue(k, out var l))
                    cells[k] = l = new List<int>();
                l.Add(i);
            }
            var toWorld = smr.transform.localToWorldMatrix;
            var match = new int[verts.Length];
            int matched = 0;
            float worstMiss = 0f;
            for (int v = 0; v < verts.Length; v++)
            {
                var p = toWorld.MultiplyPoint3x4(verts[v]);
                var k = Key(p);
                int best = -1;
                float bestScore = float.MaxValue;
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dz = -2; dz <= 2; dz++)
                            if (cells.TryGetValue(new Vector3Int(k.x + dx, k.y + dy, k.z + dz), out var l))
                                foreach (int j in l)
                                {
                                    float d = Vector3.Distance(p, world[j]);
                                    var juv = sm.vertices[j].uv;
                                    float du = uvs.Length > v ? Mathf.Min(Vector2.Distance(uvs[v], new Vector2(juv[0], juv[1])),
                                                                          Vector2.Distance(uvs[v], new Vector2(juv[0], 1f - juv[1]))) : 0f;
                                    float score = d + du * 0.01f;
                                    if (score < bestScore)
                                    {
                                        bestScore = score;
                                        best = j;
                                    }
                                }
                match[v] = best;
                if (best >= 0)
                {
                    matched++;
                    worstMiss = Mathf.Max(worstMiss, Vector3.Distance(p, world[best]));
                }
            }
            // new bones: the renderer's, then the nodes
            var bones = smr.bones.ToList();
            int first = bones.Count;
            bones.AddRange(nodes);
            var bind = src.bindposes.ToList();
            foreach (var n in nodes)
                bind.Add(n.worldToLocalMatrix * toWorld);
            var perVertex = src.GetBonesPerVertex();
            var all = src.GetAllBoneWeights();
            var counts = new List<byte>();
            var weights = new List<BoneWeight1>();
            int at = 0;
            for (int v = 0; v < verts.Length; v++)
            {
                int n = perVertex[v];
                var list = new List<BoneWeight1>();
                int j = match[v];
                float blend = j >= 0 ? 1f - Mathf.Pow(1f - Mathf.Clamp01(sm.vertices[j].blend), SeamEase) : 1f;
                for (int k = 0; k < n; k++)
                {
                    var bw = all[at + k];
                    if (bw.weight * blend > 1e-4f)
                        list.Add(new BoneWeight1 { boneIndex = bw.boneIndex, weight = bw.weight * blend });
                }
                at += n;
                if (j >= 0 && blend < 1f)
                {
                    var jv = sm.vertices[j];
                    for (int k = 0; k < 8; k++)
                        if (jv.w[k] * (1f - blend) > 1e-4f)
                        {
                            int idx = first + jv.nodes[k];
                            int same = list.FindIndex(x => x.boneIndex == idx);
                            if (same >= 0)
                                list[same] = new BoneWeight1 { boneIndex = idx, weight = list[same].weight + jv.w[k] * (1f - blend) };
                            else
                                list.Add(new BoneWeight1 { boneIndex = idx, weight = jv.w[k] * (1f - blend) });
                        }
                }
                float sum = list.Sum(x => x.weight);
                list = list.Select(x => new BoneWeight1 { boneIndex = x.boneIndex, weight = x.weight / sum }).OrderByDescending(x => x.weight).ToList();
                counts.Add((byte)list.Count);
                weights.AddRange(list);
            }
            var countArray = new NativeArray<byte>(counts.ToArray(), Allocator.Temp);
            var weightArray = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp);
            mesh.bindposes = bind.ToArray();
            mesh.SetBoneWeights(countArray, weightArray);
            countArray.Dispose();
            weightArray.Dispose();
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.quality = SkinQuality.Auto;
            return $"{smr.name}: {matched}/{verts.Length} vertices matched to the game's ({sm.vertices.Length}; worst {worstMiss * 1000f:F2} mm), " +
                   $"up to {counts.Max()} weights per vertex, {nodes.Length} nodes added, seams eased (SeamEase {SeamEase})";
        }
    }
}
