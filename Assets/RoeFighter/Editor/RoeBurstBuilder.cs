using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// 爆衣 (clothes burst) step 1, the editor half: which pieces of an outfit can come off, and in which
    /// stage.  The rules are a table per character, Assets/RoeFighter/Editor/Burst/&lt;id&gt;.json:
    ///   outfit   the renderers (and submesh, -1 all) that hold the outfit;
    ///   groups   in order, the first that matches a piece decides: name, stage (1 first, 0 stays on),
    ///            cloth (drops and lies down) or armour (flies), together (else one piece per side), and the
    ///            conditions - bone (regex on the bone that carries most of the piece's weight), anyBone (bones
    ///            that together carry at least anyShare, default 0.1), renderer, min/maxTriangles,
    ///            lowestBelow/Above (m above the floor of its lowest vertex in the bind pose).
    /// A piece is a connected part of the outfit (triangles that share a vertex position, welded at 0.1 mm).
    ///
    /// Apply(model, id) splits a fighter: the pieces of each group and side become a SkinnedMeshRenderer of
    /// their own on the same bones, materials and bounds (so nothing looks different), the rest of the
    /// outfit stays on the original renderer with a mesh that lacks them; the meshes are saved in
    /// Generated/&lt;id&gt;/burst (rewritten in place, so scenes keep their links).  It adds RoeClothesBurst with
    /// the list.  The fight scene calls it for each fighter (RoeFightScene.MakeFighter).
    ///   Check    -executeMethod RoeFighter.EditorTools.RoeBurstBuilder.Check [-roeChars a08,g04] [-roeOut dir]
    ///            pictures in the stance: each stage with the game's materials, and the groups in colours
    ///            (out/clothes_burst/unity; tools/burst_sheet.py puts them on one sheet).
    /// </summary>
    public static class RoeBurstBuilder
    {
        public const string RulesDir = "Assets/RoeFighter/Editor/Burst";
        public static string RulesPath(string id) => $"{RulesDir}/{id}.json";
        public static string OutDir(string id) => $"{RoeFighterBuilder.OutDir}/{id}/burst";
        static string ProjectDir => Path.GetDirectoryName(Application.dataPath);

        [Serializable]
        public class Rules
        {
            public string id, note;
            public Outfit[] outfit;
            public Group[] groups;
            public Nude nude;                       // the family's nude body in place of the suit's skin (RoeNudeBody)
        }

        [Serializable]
        public class Nude
        {
            public string prefab, renderer;         // the nude base and its body renderer
            public int submesh;                     // the body (the base's other submeshes are the head, eyes, lashes)
            public string material;
            public string attachTo;                 // the suit renderer it goes next to (its space, bones and settings)
            public Outfit[] replace;                // the suit's skin it replaces (renderer + submesh)
        }

        [Serializable]
        public class Outfit
        {
            public string renderer;
            public int submesh = -1;
        }

        [Serializable]
        public class Group
        {
            public string name, title;     // title: shown to people (captions, the check picture); the name if empty
            public int stage;
            public bool cloth, together;
            public string renderer, bone, anyBone;
            public float anyShare;
            public int minTriangles, maxTriangles;
            public float lowestBelow, lowestAbove;
        }

        /// <summary>One connected piece of the outfit.</summary>
        public class Part
        {
            public SkinnedMeshRenderer renderer;
            public readonly Dictionary<int, List<int>> triangles = new Dictionary<int, List<int>>();   // submesh -> triangle numbers in it
            public string bone;                                  // carries most of its weight
            public Dictionary<string, float> share;              // bone -> share of its weight
            public Vector3 centre;                               // model space, bind pose
            public float lowest, highest;                        // m above the floor
            public string side;                                  // L, M, R: her left, the middle, her right
            public Group group;                                  // null: no rule matched (stays on)
            public int Triangles => triangles.Values.Sum(l => l.Count);
        }

        public class Analysis
        {
            public Rules rules;
            public readonly List<Part> parts = new List<Part>();
            public float floor;
            public Vector3 pelvis, right;
        }

        public static Rules Load(string id)
        {
            string path = Path.Combine(ProjectDir, RulesPath(id));
            if (!File.Exists(path))
                return null;
            var rules = JsonUtility.FromJson<Rules>(File.ReadAllText(path));
            if (rules.outfit == null || rules.groups == null)
                throw new Exception($"{path}: needs \"outfit\" and \"groups\"");
            return rules;
        }

        static Matrix4x4 ToModel(SkinnedMeshRenderer r, Transform root) => root.worldToLocalMatrix * r.transform.localToWorldMatrix;

        static bool IsWeapon(Renderer r) => r.sharedMaterials.Any(m => m != null && m.name.StartsWith("wp_"));

        /// <summary>The outfit's pieces and the group each falls into (bind pose, model space: the renderers where the prefab has them).</summary>
        public static Analysis Analyse(GameObject model, Rules rules)
        {
            var a = new Analysis { rules = rules };
            var root = model.transform;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && !IsWeapon(r)).ToArray();
            // the floor: the lowest vertex of the body (the soles)
            a.floor = float.PositiveInfinity;
            foreach (var r in renderers)
            {
                var m = ToModel(r, root);
                foreach (var v in r.sharedMesh.vertices)
                    a.floor = Mathf.Min(a.floor, m.MultiplyPoint3x4(v).y);
            }
            // the pelvis, and which way is her right (from the thighs), from the bind poses
            Vector3? BindBone(string name)
            {
                foreach (var r in renderers)
                {
                    int i = Array.FindIndex(r.bones, b => b != null && b.name == name);
                    var poses = r.sharedMesh.bindposes;
                    if (i >= 0 && i < poses.Length)
                        return ToModel(r, root).MultiplyPoint3x4(poses[i].inverse.MultiplyPoint3x4(Vector3.zero));
                }
                return null;
            }
            var pelvis = BindBone("Bip001 Pelvis");
            var leftThigh = BindBone("Bip001 L Thigh");
            var rightThigh = BindBone("Bip001 R Thigh");
            a.pelvis = pelvis ?? Vector3.up * (a.floor + 0.95f);
            a.right = leftThigh != null && rightThigh != null ? Vector3.ProjectOnPlane(rightThigh.Value - leftThigh.Value, Vector3.up).normalized : Vector3.right;

            foreach (var o in rules.outfit)
            {
                var r = renderers.FirstOrDefault(x => x.name == o.renderer);
                if (r == null)
                {
                    Debug.LogWarning($"[ROE] burst {rules.id}: no renderer '{o.renderer}'");
                    continue;
                }
                var mesh = r.sharedMesh;
                var toModel = ToModel(r, root);
                var at = mesh.vertices.Select(v => toModel.MultiplyPoint3x4(v)).ToArray();
                // welded by position: the same point of a seam is one vertex
                var weld = new int[at.Length];
                var ids = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < at.Length; i++)
                {
                    var key = Vector3Int.RoundToInt(at[i] * 10000f);
                    if (!ids.TryGetValue(key, out int id))
                        ids[key] = id = ids.Count;
                    weld[i] = id;
                }
                var parent = Enumerable.Range(0, ids.Count).ToArray();
                int Find(int x)
                {
                    while (parent[x] != x)
                        x = parent[x] = parent[parent[x]];
                    return x;
                }
                var subs = o.submesh >= 0 ? new[] { o.submesh } : Enumerable.Range(0, mesh.subMeshCount).ToArray();
                var tris = subs.ToDictionary(s => s, s => mesh.GetTriangles(s));
                foreach (var t in tris.Values)
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        int ra = Find(weld[t[k]]), rb = Find(weld[t[k + 1]]), rc = Find(weld[t[k + 2]]);
                        parent[ra] = rb;
                        parent[Find(rc)] = Find(rb);
                    }
                var byRoot = new Dictionary<int, Part>();
                foreach (var kv in tris)
                    for (int k = 0; k + 2 < kv.Value.Length; k += 3)
                    {
                        int root_ = Find(weld[kv.Value[k]]);
                        if (!byRoot.TryGetValue(root_, out var part))
                            byRoot[root_] = part = new Part { renderer = r };
                        if (!part.triangles.TryGetValue(kv.Key, out var list))
                            part.triangles[kv.Key] = list = new List<int>();
                        list.Add(k / 3);
                    }
                // each piece: the bones that carry it, where it is
                var perVertex = mesh.GetBonesPerVertex();
                var weights = mesh.GetAllBoneWeights();
                var start = new int[mesh.vertexCount + 1];
                for (int i = 0; i < mesh.vertexCount && i < perVertex.Length; i++)
                    start[i + 1] = start[i] + perVertex[i];
                var bones = r.bones;
                foreach (var part in byRoot.Values)
                {
                    var used = new HashSet<int>();
                    foreach (var kv in part.triangles)
                        foreach (int t in kv.Value)
                        {
                            used.Add(tris[kv.Key][t * 3]);
                            used.Add(tris[kv.Key][t * 3 + 1]);
                            used.Add(tris[kv.Key][t * 3 + 2]);
                        }
                    var sum = new Dictionary<string, float>();
                    var centre = Vector3.zero;
                    float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                    foreach (int v in used)
                    {
                        centre += at[v];
                        lo = Mathf.Min(lo, at[v].y);
                        hi = Mathf.Max(hi, at[v].y);
                        if (v >= perVertex.Length)
                            continue;
                        for (int j = start[v]; j < start[v + 1]; j++)
                        {
                            var w = weights[j];
                            string name = w.boneIndex < bones.Length && bones[w.boneIndex] != null ? bones[w.boneIndex].name : $"#{w.boneIndex}";
                            sum[name] = (sum.TryGetValue(name, out float s) ? s : 0f) + w.weight;
                        }
                    }
                    float total = Mathf.Max(1e-6f, sum.Values.Sum());
                    part.share = sum.ToDictionary(kv => kv.Key, kv => kv.Value / total);
                    part.bone = sum.Count > 0 ? sum.OrderByDescending(kv => kv.Value).First().Key : "";
                    part.centre = centre / Mathf.Max(1, used.Count);
                    part.lowest = lo - a.floor;
                    part.highest = hi - a.floor;
                    float across = Vector3.Dot(part.centre - a.pelvis, a.right);
                    part.side = across > 0.03f ? "R" : across < -0.03f ? "L" : "M";
                    part.group = rules.groups.FirstOrDefault(g => Matches(g, part));
                    a.parts.Add(part);
                }
            }
            return a;
        }

        static bool Matches(Group g, Part p)
        {
            if (!string.IsNullOrEmpty(g.renderer) && g.renderer != p.renderer.name)
                return false;
            if (!string.IsNullOrEmpty(g.bone) && !Regex.IsMatch(p.bone, g.bone))
                return false;
            if (!string.IsNullOrEmpty(g.anyBone) && p.share.Where(kv => Regex.IsMatch(kv.Key, g.anyBone)).Sum(kv => kv.Value) < (g.anyShare > 0f ? g.anyShare : 0.1f))
                return false;
            if (g.minTriangles > 0 && p.Triangles < g.minTriangles)
                return false;
            if (g.maxTriangles > 0 && p.Triangles > g.maxTriangles)
                return false;
            if (g.lowestBelow > 0f && p.lowest >= g.lowestBelow)
                return false;
            if (g.lowestAbove > 0f && p.lowest <= g.lowestAbove)
                return false;
            return true;
        }

        /// <summary>
        /// Split a fighter's outfit by the rules (see the class comment).  Null when the character has no
        /// rules.  Run it on a fresh instance of the fighter prefab.
        /// </summary>
        public static RoeClothesBurst Apply(GameObject model, string id)
        {
            var rules = Load(id);
            if (rules == null)
                return null;
            var a = Analyse(model, rules);
            string dir = OutDir(id);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.Combine(ProjectDir, dir));
                AssetDatabase.Refresh();
            }
            var burst = model.GetComponent<RoeClothesBurst>();
            if (burst == null)
                burst = model.AddComponent<RoeClothesBurst>();
            burst.pieces.Clear();
            var log = new StringBuilder($"[ROE] burst {id}: floor {a.floor:F3}, pelvis {a.pelvis}, right {a.right}; {a.parts.Count} pieces in the outfit");
            // the family's nude body under the outfit, in place of the suit's skin (before anything is split)
            var nude = RoeNudeBody.Build(model, id, rules, a, dir, log);
            var replaced = new Dictionary<SkinnedMeshRenderer, HashSet<int>>();      // renderer -> submeshes gone whole
            if (nude != null)
                foreach (var s in rules.nude.replace ?? new Outfit[0])
                {
                    var r = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.name == s.renderer);
                    if (r == null)
                        continue;
                    if (!replaced.TryGetValue(r, out var set))
                        replaced[r] = set = new HashSet<int>();
                    if (s.submesh >= 0)
                        set.Add(s.submesh);
                    else
                        set.UnionWith(Enumerable.Range(0, r.sharedMesh.subMeshCount));
                }

            // the pieces of one group on one side of one renderer come off together
            var units = a.parts.Where(p => p.group != null && p.group.stage > 0)
                .GroupBy(p => (renderer: p.renderer, group: p.group, side: p.group.together ? "" : p.side))
                .OrderBy(g => g.Key.group.stage).ThenBy(g => Array.IndexOf(rules.groups, g.Key.group)).ThenBy(g => g.Key.side)
                .ToList();
            var taken = new Dictionary<SkinnedMeshRenderer, List<Part>>();
            foreach (var u in units)
            {
                var r = u.Key.renderer;
                var src = r.sharedMesh;
                string name = u.Key.group.name + (u.Key.side.Length > 0 ? " " + u.Key.side : "");
                var subs = u.SelectMany(p => p.triangles.Keys).Distinct().OrderBy(s => s).ToArray();
                var lists = subs.Select(s =>
                {
                    var t = src.GetTriangles(s);
                    var list = new List<int>();
                    foreach (var p in u)
                        if (p.triangles.TryGetValue(s, out var nums))
                            foreach (int n in nums)
                                list.AddRange(new[] { t[n * 3], t[n * 3 + 1], t[n * 3 + 2] });
                    return list;
                }).ToArray();
                string piecePath = $"{dir}/{Safe(r.name)}__{Safe(name)}.asset";
                var mesh = Commit(Subset(src, lists, MeshAsset(piecePath)), piecePath);
                var mats = r.sharedMaterials;
                var smr = AddRenderer(r, "burst " + name, mesh, subs.Select(s => mats[Mathf.Min(s, mats.Length - 1)]).ToArray());
                int count = lists.Sum(l => l.Count) / 3;
                burst.pieces.Add(new RoeClothesBurst.Piece
                {
                    name = name, group = u.Key.group.name, title = string.IsNullOrEmpty(u.Key.group.title) ? u.Key.group.name : u.Key.group.title,
                    stage = u.Key.group.stage, cloth = u.Key.group.cloth, renderer = smr, triangles = count,
                });
                if (!taken.TryGetValue(r, out var list_))
                    taken[r] = list_ = new List<Part>();
                list_.AddRange(u);
                log.Append($"\n[ROE]   stage {u.Key.group.stage} {(u.Key.group.cloth ? "cloth " : "armour")} {name,-22} {u.Count(),4} pieces {count,6} triangles  " +
                           $"bones {string.Join(" ", u.Select(p => p.bone).Distinct().Take(6))}");
            }
            // what stays on each renderer (the suit's skin the nude body replaces goes as well)
            foreach (var r in replaced.Keys)
                if (!taken.ContainsKey(r))
                    taken[r] = new List<Part>();
            foreach (var kv in taken)
            {
                var r = kv.Key;
                var src = r.sharedMesh;
                var gone = new Dictionary<int, HashSet<int>>();
                foreach (var p in kv.Value)
                    foreach (var t in p.triangles)
                    {
                        if (!gone.TryGetValue(t.Key, out var set))
                            gone[t.Key] = set = new HashSet<int>();
                        set.UnionWith(t.Value);
                    }
                var lists = Enumerable.Range(0, src.subMeshCount).Select(s =>
                {
                    var t = src.GetTriangles(s);
                    var list = new List<int>(t.Length);
                    gone.TryGetValue(s, out var set);
                    if (replaced.TryGetValue(r, out var whole) && whole.Contains(s))
                        return list;
                    for (int n = 0; n * 3 + 2 < t.Length; n++)
                        if (set == null || !set.Contains(n))
                            list.AddRange(new[] { t[n * 3], t[n * 3 + 1], t[n * 3 + 2] });
                    return list;
                }).ToArray();
                int before = src.triangles.Length / 3, after = lists.Sum(l => l.Count) / 3;
                string staysPath = $"{dir}/{Safe(r.name)}__stays.asset";
                r.sharedMesh = Commit(Subset(src, lists, MeshAsset(staysPath)), staysPath);
                log.Append($"\n[ROE]   {r.name}: {before} -> {after} triangles stay on it ({src.name} -> {r.sharedMesh.name})");
            }
            // what stays on, and pieces no rule took
            foreach (var g in a.parts.Where(p => p.group != null && p.group.stage <= 0).GroupBy(p => p.group.name))
                log.Append($"\n[ROE]   stays on (rule): {g.Key}, {g.Count()} pieces, {g.Sum(p => p.Triangles)} triangles");
            var free = a.parts.Where(p => p.group == null).ToList();
            if (free.Count > 0)
                log.Append($"\n[ROE]   stays on (no rule): {free.Count} pieces, {free.Sum(p => p.Triangles)} triangles - " +
                           string.Join(", ", free.GroupBy(p => p.bone).OrderByDescending(g => g.Sum(p => p.Triangles))
                               .Select(g => $"{g.Key} x{g.Count()} ({g.Sum(p => p.Triangles)})")));
            burst.stages = burst.pieces.Count > 0 ? burst.pieces.Max(p => p.stage) : 0;
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            return burst;
        }

        internal static string Safe(string s) => Regex.Replace(s, @"[^A-Za-z0-9_\-]+", "_");

        /// <summary>
        /// Some triangles of a mesh as a mesh of their own: only the vertices they use, every vertex
        /// stream, every bone influence, the same bind poses; one submesh per list.
        /// </summary>
        static Mesh Subset(Mesh src, List<int>[] indices, Mesh m)
        {
            var map = new Dictionary<int, int>();
            var order = new List<int>();
            foreach (var list in indices)
                foreach (int i in list)
                    if (!map.ContainsKey(i))
                    {
                        map[i] = order.Count;
                        order.Add(i);
                    }
            T[] Pick<T>(T[] all)
            {
                var r = new T[order.Count];
                for (int k = 0; k < r.Length; k++)
                    r[k] = all[order[k]];
                return r;
            }
            m.indexFormat = order.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.vertices = Pick(src.vertices);
            if (src.HasVertexAttribute(VertexAttribute.Normal))
                m.normals = Pick(src.normals);
            if (src.HasVertexAttribute(VertexAttribute.Tangent))
                m.tangents = Pick(src.tangents);
            if (src.HasVertexAttribute(VertexAttribute.Color))
                m.colors32 = Pick(src.colors32);
            for (int ch = 0; ch < 8; ch++)
            {
                var attr = VertexAttribute.TexCoord0 + ch;
                if (!src.HasVertexAttribute(attr))
                    continue;
                int dim = src.GetVertexAttributeDimension(attr);
                if (dim <= 2)
                {
                    var l = new List<Vector2>();
                    src.GetUVs(ch, l);
                    m.SetUVs(ch, Pick(l.ToArray()));
                }
                else if (dim == 3)
                {
                    var l = new List<Vector3>();
                    src.GetUVs(ch, l);
                    m.SetUVs(ch, Pick(l.ToArray()));
                }
                else
                {
                    var l = new List<Vector4>();
                    src.GetUVs(ch, l);
                    m.SetUVs(ch, Pick(l.ToArray()));
                }
            }
            var perVertex = src.GetBonesPerVertex();
            if (perVertex.Length == src.vertexCount)
            {
                var weights = src.GetAllBoneWeights();
                var start = new int[src.vertexCount + 1];
                for (int i = 0; i < src.vertexCount; i++)
                    start[i + 1] = start[i] + perVertex[i];
                int total = order.Sum(i => (int)perVertex[i]);
                var counts = new NativeArray<byte>(order.Count, Allocator.Temp);
                var picked = new NativeArray<BoneWeight1>(total, Allocator.Temp);
                int at = 0;
                for (int k = 0; k < order.Count; k++)
                {
                    int i = order[k];
                    counts[k] = perVertex[i];
                    for (int j = start[i]; j < start[i + 1]; j++)
                        picked[at++] = weights[j];
                }
                m.bindposes = src.bindposes;
                m.SetBoneWeights(counts, picked);
                counts.Dispose();
                picked.Dispose();
            }
            if (src.blendShapeCount > 0)
                Debug.LogWarning($"[ROE] burst: {src.name} has {src.blendShapeCount} blend shapes; the split meshes leave them out");
            m.subMeshCount = indices.Length;
            for (int s = 0; s < indices.Length; s++)
                m.SetTriangles(indices[s].Select(i => map[i]).ToArray(), s, false);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// The mesh asset at a path, emptied to be filled anew - or a new mesh, saved by Commit.  An existing asset is
        /// rewritten in place (scenes that use it keep the link) through the mesh API: copying another mesh over it
        /// (CopySerialized) left its drawable copy stale, and renderers drew nothing until the editor restarted.
        /// </summary>
        internal static Mesh MeshAsset(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
                return new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            mesh.Clear(false);
            mesh.name = Path.GetFileNameWithoutExtension(path);
            return mesh;
        }

        internal static Mesh Commit(Mesh mesh, string path)
        {
            if (AssetDatabase.Contains(mesh))
                EditorUtility.SetDirty(mesh);
            else
                AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>A renderer next to the original, on its bones, with its settings.</summary>
        static SkinnedMeshRenderer AddRenderer(SkinnedMeshRenderer src, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name) { layer = src.gameObject.layer };
            go.transform.SetParent(src.transform.parent, false);
            go.transform.localPosition = src.transform.localPosition;
            go.transform.localRotation = src.transform.localRotation;
            go.transform.localScale = src.transform.localScale;
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = mesh;
            r.bones = src.bones;
            r.rootBone = src.rootBone;
            r.sharedMaterials = materials;
            r.localBounds = src.localBounds;
            r.updateWhenOffscreen = src.updateWhenOffscreen;
            r.quality = src.quality;
            r.skinnedMotionVectors = src.skinnedMotionVectors;
            r.shadowCastingMode = src.shadowCastingMode;
            r.receiveShadows = src.receiveShadows;
            r.lightProbeUsage = src.lightProbeUsage;
            r.reflectionProbeUsage = src.reflectionProbeUsage;
            r.probeAnchor = src.probeAnchor;
            r.forceMatrixRecalculationPerRender = src.forceMatrixRecalculationPerRender;
            r.allowOcclusionWhenDynamic = src.allowOcclusionWhenDynamic;
            r.renderingLayerMask = src.renderingLayerMask;
            return r;
        }

        // ---- check pictures

        static readonly Color[] Palette =
        {
            new Color(0.90f, 0.20f, 0.25f), new Color(0.20f, 0.55f, 0.95f), new Color(0.95f, 0.75f, 0.10f), new Color(0.25f, 0.75f, 0.35f),
            new Color(0.65f, 0.30f, 0.85f), new Color(0.95f, 0.50f, 0.15f), new Color(0.15f, 0.80f, 0.80f), new Color(0.95f, 0.45f, 0.75f),
            new Color(0.55f, 0.40f, 0.20f), new Color(0.60f, 0.85f, 0.20f), new Color(0.10f, 0.35f, 0.55f), new Color(0.80f, 0.80f, 0.80f),
        };

        [MenuItem("ROE Fighter/Burst/Check pictures")]
        public static void Check()
        {
            var ids = RoeCapture.Arg("-roeChars", "a08,g04").Split(',');
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(ProjectDir, "out", "clothes_burst", "unity"));
            Directory.CreateDirectory(outDir);
            ShaderUtil.allowAsyncCompilation = false;
            var studio = RoeStudio.Build();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var id in ids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if (Load(id) == null)
                {
                    Debug.LogWarning($"[ROE] burst {id}: no rules at {RulesPath(id)}");
                    Object.DestroyImmediate(go);
                    continue;
                }
                var stance = AssetDatabase.LoadAssetAtPath<AnimationClip>(RoeHumanoidClips.ClipPath(id, "idle_01"));
                if (stance != null)
                    RoeCapture.Pose(go, stance, 0f);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
                var forward = RoeShowcase.Forward(go);
                var bounds = RoeShowcase.WorldBounds(go);
                float height = bounds.max.y - go.transform.position.y;
                var body = new Vector3(bounds.center.x, go.transform.position.y + height * 0.5f, bounds.center.z);
                studio.LightFrom(forward);
                studio.SetFocus(0f, 0f, 0f);
                void Shot(string file, float yaw)
                {
                    studio.Aim(body, forward, yaw, 6f, height * 2.6f, 26f);
                    RoeCapture.Render(studio.camera, 900, 1300, Path.Combine(outDir, file));
                }
                // the first frames with new materials come out with placeholder shading
                studio.Aim(body, forward, 18f, 6f, height * 2.6f, 26f);
                RoeCapture.Render(studio.camera, 200, 200, Path.Combine(outDir, "_warm.png"));
                RoeCapture.Render(studio.camera, 200, 200, Path.Combine(outDir, "_warm.png"));
                // as the prefab has it, then split: the same picture (tools/burst_sheet.py compares them)
                Shot($"{id}_unsplit_front.png", 15f);
                Shot($"{id}_unsplit_back.png", 165f);
                // split out of the posing (a renderer that gets another mesh while posed draws nothing), then posed again
                RoeCapture.EndPosing();
                var burst = Apply(go, id);
                if (stance != null)
                    RoeCapture.Pose(go, stance, 0f);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
                for (int stage = 0; stage <= burst.stages; stage++)
                {
                    foreach (var p in burst.pieces)
                        p.renderer.enabled = p.stage > stage;
                    Shot($"{id}_stage{stage}_front.png", 15f);
                    Shot($"{id}_stage{stage}_back.png", 165f);
                }
                // the groups in colours on a grey body
                foreach (var p in burst.pieces)
                    p.renderer.enabled = true;
                var grey = new Material(lit);
                grey.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.64f));
                var colours = new Dictionary<string, Material>();
                var legend = new StringBuilder();
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var piece = burst.pieces.FirstOrDefault(p => p.renderer == r);
                    if (piece == null)
                    {
                        r.sharedMaterials = Enumerable.Repeat(grey, r.sharedMaterials.Length).ToArray();
                        continue;
                    }
                    if (!colours.TryGetValue(piece.group, out var mat))
                    {
                        var c = Palette[colours.Count % Palette.Length];
                        mat = new Material(lit);
                        mat.SetColor("_BaseColor", c);
                        mat.SetFloat("_Smoothness", 0.3f);
                        colours[piece.group] = mat;
                        legend.Append($"{piece.title}\t#{ColorUtility.ToHtmlStringRGB(c)}\t{piece.stage}\t{(piece.cloth ? "cloth" : "armour")}\t" +
                                      $"{burst.pieces.Where(x => x.group == piece.group).Sum(x => x.triangles)}\n");
                    }
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                }
                File.WriteAllText(Path.Combine(outDir, $"{id}_legend.txt"), legend.ToString());
                Shot($"{id}_groups_front.png", 15f);
                Shot($"{id}_groups_back.png", 165f);
                RoeCapture.EndPosing();
                Debug.Log($"[ROE] burst {id}: {burst.Report()}");
                Object.DestroyImmediate(go);
            }
            Debug.Log($"[ROE] burst check pictures in {outDir}");
        }
    }
}
