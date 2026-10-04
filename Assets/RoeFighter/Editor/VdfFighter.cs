using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// A Vindictus: Defying Fate character as a fighter (user 10-04: "再加入一个角色吧，vindictus里的fiona，用 PCF_005 这个版本，
    /// 加入进去，注意衣服和头发效果").  tools/vdf_fbx.py turns the assembled .blend of ripper_tpose scripts/vindictus into
    /// Assets/VDF/&lt;id&gt;/&lt;id&gt;.fbx (the game's UE5 skeleton, in metres) + materials.json, tools/vdf_textures.py makes the maps
    /// (textures/) and unity.json; this
    ///   - sets the texture importers (normal maps; the URP metallic / occlusion masks linear; the cut-out maps keep their
    ///     coverage down the mip chain, or hair and lashes thin out with distance),
    ///   - makes URP Lit materials from unity.json (opaque skin and eyes; the dress, shoes, tiara, hair, brows and lashes cut
    ///     out by their alpha and two-sided; the eye's shadow shell and tear film blended) and puts them on the model,
    ///   - saves the Generic prefab and the humanoid fighter (UE5 / MetaHuman body bones: RoeHumanoid.MapBones) the way the
    ///     DOA6 characters are saved (DoaFighter.SavePrefabs).
    /// Her loose bones (hair, skirt, feathers, breasts) are found by name by the cloth backends (RoeBoneCloth.Classify's
    /// Vindictus rules); her fight definition is tools/vdf/&lt;id&gt;.json (read by DoaFighter.Load like a DOA6 one).
    ///   -executeMethod RoeFighter.EditorTools.VdfFighter.Build [-roeVdf fio005]
    /// </summary>
    public static class VdfFighter
    {
        public static string Dir(string id) => $"Assets/VDF/{id}";

        [System.Serializable]
        class Sidecar
        {
            public string id, fbx;
            public List<Mat> materials = new List<Mat>();
        }

        [System.Serializable]
        class Mat
        {
            public string name, kind, albedo, normal, mask, surface, cull;
            public float[] color;
            public float smoothness, metallic, normalScale = 1f, cutoff = 0.5f;
        }

        [MenuItem("ROE Fighter/Build/Vindictus fighter (Fiona)")]
        public static void Build()
        {
            foreach (var id in RoeCapture.Arg("-roeVdf", "fio005").Split(','))
                Build(id.Trim());
            AssetDatabase.SaveAssets();
        }

        public static GameObject Build(string id)
        {
            string dir = Dir(id);
            string sidecarPath = $"{dir}/unity.json";
            if (!File.Exists(sidecarPath))
            {
                Debug.LogError($"[ROE] {id}: no {sidecarPath} - run tools/vdf_fbx.py and tools/vdf_textures.py first");
                return null;
            }
            var side = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidecarPath));
            string fbxPath = $"{dir}/{side.fbx}";
            AssetDatabase.Refresh();

            // textures
            var byFile = side.materials.SelectMany(m => new[] { (m.albedo, m, "albedo"), (m.normal, m, "normal"), (m.mask, m, "mask") })
                .Where(x => !string.IsNullOrEmpty(x.Item1)).GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First());
            int changed = 0;
            foreach (var kv in byFile)
            {
                var (file, m, role) = kv.Value;
                var ti = (TextureImporter)AssetImporter.GetAtPath($"{dir}/textures/{file}");
                if (ti == null)
                {
                    Debug.LogWarning($"[ROE] {id}: no texture {dir}/textures/{file}");
                    continue;
                }
                bool normal = role == "normal";
                bool linear = role != "albedo";
                bool cut = role == "albedo" && m.surface == "cutout";
                bool alpha = role == "albedo" && m.surface != "opaque";
                var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != want || ti.sRGBTexture == linear || ti.alphaIsTransparency != alpha || ti.mipMapsPreserveCoverage != cut ||
                    (cut && Mathf.Abs(ti.alphaTestReferenceValue - m.cutoff) > 1e-3f) || ti.maxTextureSize != 2048)
                {
                    ti.textureType = want;
                    ti.sRGBTexture = !linear;
                    ti.alphaIsTransparency = alpha;
                    ti.mipMapsPreserveCoverage = cut;
                    if (cut)
                        ti.alphaTestReferenceValue = m.cutoff;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                    changed++;
                }
            }

            // materials (URP Lit)
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string matDir = $"{dir}/materials";
            Directory.CreateDirectory(matDir);
            var made = new Dictionary<string, Material>();
            Texture2D Tex(string file) => string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/textures/{file}");
            foreach (var m in side.materials)
            {
                string matPath = $"{matDir}/{m.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                var c = m.color != null && m.color.Length >= 4 ? new Color(m.color[0], m.color[1], m.color[2], m.color[3]) : Color.white;
                mat.SetTexture("_BaseMap", Tex(m.albedo));
                mat.SetColor("_BaseColor", c);
                mat.SetTexture("_BumpMap", Tex(m.normal));
                mat.SetFloat("_BumpScale", m.normalScale);
                var mask = Tex(m.mask);
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.SetTexture("_OcclusionMap", mask);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                // with the mask, URP scales its alpha (smoothness) by _Smoothness and takes metallic from its red
                mat.SetFloat("_Smoothness", mask != null ? 1f : m.smoothness);
                mat.SetFloat("_Metallic", mask != null ? 1f : m.metallic);
                bool transparent = m.surface == "transparent";
                bool cut = m.surface == "cutout";
                mat.SetFloat("_Surface", transparent ? 1f : 0f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_BlendModePreserveSpecular", 0f);
                mat.SetFloat("_AlphaClip", cut ? 1f : 0f);
                mat.SetFloat("_Cutoff", m.cutoff);
                mat.SetFloat("_Cull", m.cull == "off" ? 0f : 2f);
                // the eye's shadow shell and tear film: no shadows of their own
                mat.SetShaderPassEnabled("ShadowCaster", !transparent);
                mat.renderQueue = -1;
                BaseShaderGUI.SetMaterialKeywords(mat);
                EditorUtility.SetDirty(mat);
                made[m.name] = mat;
            }

            // the model: our materials on it, readable meshes (the cloth solvers and the meter read vertices)
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            var remaps = mi.GetExternalObjectMap();
            bool reimport = mi.animationType != ModelImporterAnimationType.Generic || mi.optimizeGameObjects || mi.importAnimation ||
                            mi.importBlendShapes || !mi.isReadable || mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
                            made.Any(kv => !remaps.TryGetValue(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), out var o) || o != kv.Value);
            if (reimport)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.optimizeGameObjects = false;
                mi.importAnimation = false;
                mi.importBlendShapes = false;
                mi.isReadable = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var kv in made)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var missing = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(x => x != null && !made.ContainsValue(x)).Select(x => x.name).Distinct().ToList();
            if (missing.Count > 0)
                Debug.LogWarning($"[ROE] {id}: materials not from unity.json: {string.Join(", ", missing)}");
            if (!DoaFighter.SavePrefabs(id, model, "Vindictus", $"{made.Count} materials, {changed} texture importers set", heels: true))
                return null;
            AttachKawaii(id);
            AttachUeRig(id);
            AttachWeapons(id);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id));
        }

        [System.Serializable]
        class WeaponList
        {
            public List<Weapon> weapons = new List<Weapon>();
        }

        [System.Serializable]
        class Weapon
        {
            public string name, socket, psk, albedo, normal, mask;
        }

        /// <summary>
        /// Her sword and shield (tools/vdf_weapons.py: maps + Assets/VDF/&lt;id&gt;/weapons/weapons.json) on the fighter prefab:
        /// each mesh read from the game's .psk (UE Viewer writes it mirrored in Y like its animation keys: a point becomes
        /// (-x, y, z) cm in the socket bone's frame, RoeKawaiiPhysics.FromUe after the mirror; the triangles are wound the way
        /// their normals face), saved as a mesh asset, given a URP Lit material, and hung on its socket bone - weapon_r on the
        /// right hand, shield_l on the left forearm, both in her mesh's skeleton as in the game - with no offset: in the game
        /// the weapon's component sits on the socket.
        ///   -executeMethod RoeFighter.EditorTools.VdfFighter.Weapons [-roeVdf fio005]
        /// </summary>
        public static void Weapons()
        {
            foreach (var id in RoeCapture.Arg("-roeVdf", "fio005").Split(','))
                AttachWeapons(id.Trim());
            AssetDatabase.SaveAssets();
        }

        public static void AttachWeapons(string id)
        {
            string dir = $"{Dir(id)}/weapons";
            string listPath = $"{dir}/weapons.json";
            if (!File.Exists(listPath))
            {
                Debug.Log($"[ROE] {id}: no {listPath} - no weapons (tools/vdf_weapons.py)");
                return;
            }
            AssetDatabase.Refresh();
            var list = JsonUtility.FromJson<WeaponList>(File.ReadAllText(listPath));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string path = RoeHumanoid.FighterPath(id);
            var root = PrefabUtility.LoadPrefabContents(path);
            var notes = new List<string>();
            foreach (var w in list.weapons)
            {
                // maps
                foreach (var (file, role) in new[] { (w.albedo, "albedo"), (w.normal, "normal"), (w.mask, "mask") })
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{file}");
                    if (ti == null)
                        continue;
                    var type = role == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (ti.textureType != type || ti.sRGBTexture != (role == "albedo") || ti.maxTextureSize != 2048)
                    {
                        ti.textureType = type;
                        ti.sRGBTexture = role == "albedo";
                        ti.maxTextureSize = 2048;
                        ti.SaveAndReimport();
                    }
                }
                // material
                string matPath = $"{dir}/{w.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{w.mask}");
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{w.albedo}"));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{w.normal}"));
                mat.SetFloat("_BumpScale", 1f);
                mat.SetTexture("_MetallicGlossMap", maskTex);
                mat.SetTexture("_OcclusionMap", maskTex);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f);
                BaseShaderGUI.SetMaterialKeywords(mat);
                EditorUtility.SetDirty(mat);
                // mesh
                var mesh = ReadPsk(w.psk, w.name, out string meshNote);
                string meshPath = $"{dir}/{w.name}_mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                // on its socket
                var socket = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == w.socket);
                if (socket == null)
                {
                    notes.Add($"{w.name}: NO socket bone {w.socket} in her rig - left out");
                    continue;
                }
                var old = socket.Find(w.name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                var go = new GameObject(w.name);
                go.transform.SetParent(socket, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                // where it lands on her bind pose: the far end of the mesh from the socket
                var far = mesh.vertices.OrderByDescending(v => v.sqrMagnitude).First();
                // a sword: its blade (the mesh's origin is the guard) hits in her strikes of that hand
                if (w.name.Contains("sword"))
                {
                    var blade = go.AddComponent<RoeBlade>();
                    blade.hand = w.socket.EndsWith("_l") ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    blade.hilt = Vector3.zero;
                    blade.tip = far;
                }
                notes.Add($"{w.name} on {socket.parent.name}/{w.socket}: {meshNote}; farthest point {100f * far.magnitude:F0} cm from the socket, " +
                          $"at {go.transform.TransformPoint(far)} (socket at {socket.position})");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[ROE] {id}: weapons - {string.Join("; ", notes)}");
        }

        /// <summary>
        /// A rigid mesh from a UE Viewer .psk (PNTS, VTXW, FACE, VTXNORMS): a vertex per wedge, positions and normals in the
        /// socket bone's frame in metres, UVs flipped to Unity's origin, tangents recalculated.
        /// </summary>
        static Mesh ReadPsk(string path, string name, out string note)
        {
            var data = File.ReadAllBytes(path);
            var chunks = new Dictionary<string, (int at, int size, int count)>();
            for (int off = 0; off + 32 <= data.Length;)
            {
                string cid = System.Text.Encoding.ASCII.GetString(data, off, 20).Split('\0')[0];
                int size = System.BitConverter.ToInt32(data, off + 24), count = System.BitConverter.ToInt32(data, off + 28);
                chunks[cid] = (off + 32, size, count);
                off += 32 + size * count;
            }
            float F(int at) => System.BitConverter.ToSingle(data, at);
            Vector3 Unity(Vector3 v) => new Vector3(-v.x, v.y, v.z);       // mirrored in Y (ActorX) -> Unreal -> her bone frame
            var (pa, _, pc) = chunks["PNTS0000"];
            var points = new Vector3[pc];
            for (int i = 0; i < pc; i++)
                points[i] = Unity(new Vector3(F(pa + 12 * i), F(pa + 12 * i + 4), F(pa + 12 * i + 8))) * 0.01f;
            Vector3[] pointNormals = null;
            if (chunks.TryGetValue("VTXNORMS", out var nc))
            {
                pointNormals = new Vector3[nc.count];
                for (int i = 0; i < nc.count; i++)
                    pointNormals[i] = Unity(new Vector3(F(nc.at + 12 * i), F(nc.at + 12 * i + 4), F(nc.at + 12 * i + 8))).normalized;
            }
            bool wideWedges = !chunks.ContainsKey("VTXW0000");
            var (wa, ws, wc) = wideWedges ? chunks["VTXW3200"] : chunks["VTXW0000"];
            var vertices = new Vector3[wc];
            var normals = new Vector3[wc];
            var uvs = new Vector2[wc];
            for (int i = 0; i < wc; i++)
            {
                int at = wa + ws * i;
                int p = wideWedges ? System.BitConverter.ToInt32(data, at) : System.BitConverter.ToUInt16(data, at);
                vertices[i] = points[p];
                normals[i] = pointNormals != null ? pointNormals[p] : Vector3.up;
                uvs[i] = new Vector2(F(at + 4), 1f - F(at + 8));
            }
            bool wideFaces = !chunks.ContainsKey("FACE0000");
            var (fa, fs, fc) = wideFaces ? chunks["FACE3200"] : chunks["FACE0000"];
            var tris = new int[fc * 3];
            int agree = 0;
            for (int i = 0; i < fc; i++)
            {
                int at = fa + fs * i;
                for (int k = 0; k < 3; k++)
                    tris[3 * i + k] = wideFaces ? System.BitConverter.ToInt32(data, at + 4 * k) : System.BitConverter.ToUInt16(data, at + 2 * k);
                var a = vertices[tris[3 * i]];
                var face = Vector3.Cross(vertices[tris[3 * i + 1]] - a, vertices[tris[3 * i + 2]] - a);
                if (Vector3.Dot(face, normals[tris[3 * i]] + normals[tris[3 * i + 1]] + normals[tris[3 * i + 2]]) > 0f)
                    agree++;
            }
            // Unity's front faces: their normal is Cross(b - a, c - a); wound the other way round when most disagree
            bool flip = agree < fc - agree;
            if (flip)
                for (int i = 0; i < fc; i++)
                    (tris[3 * i + 1], tris[3 * i + 2]) = (tris[3 * i + 2], tris[3 * i + 1]);
            var mesh = new Mesh { name = name };
            if (wc > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            note = $"{pc} points, {wc} vertices, {fc} triangles ({(flip ? "wound round" : "as written")}: {Mathf.Max(agree, fc - agree)} of {fc} face the normals' way), " +
                   $"size {100f * mesh.bounds.size.x:F0} x {100f * mesh.bounds.size.y:F0} x {100f * mesh.bounds.size.z:F0} cm";
            return mesh;
        }

        /// <summary>RoeUeRig on the fighter prefab: her unmapped spine joints and twist bones follow every clip.</summary>
        static void AttachUeRig(string id)
        {
            string path = RoeHumanoid.FighterPath(id);
            var root = PrefabUtility.LoadPrefabContents(path);
            var rig = root.GetComponent<RoeUeRig>();
            if (rig == null)
                rig = root.AddComponent<RoeUeRig>();
            rig.Build();
            int twists = rig.TwistBones;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[ROE] {id}: RoeUeRig - spine_02 / spine_04 / neck_02 take {rig.spine02} / {rig.spine04} / {rig.neck02} of their bends back, {twists} twist bones");
        }

        /// <summary>
        /// Her game's KawaiiPhysics settings (Assets/VDF/&lt;id&gt;/kawaii.json, tools/vdf_kawaii.py) onto the fighter prefab
        /// (RoeKawaiiRig), and a check of where the game's capsules land on her (their offsets are in UE bone space,
        /// RoeKawaiiPhysics.FromUe): for each, how far its centre is from her skin and how much of its bone's skin it holds.
        /// </summary>
        static void AttachKawaii(string id)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Dir(id)}/kawaii.json");
            if (text == null)
            {
                Debug.Log($"[ROE] {id}: no kawaii.json - no KawaiiPhysics settings (tools/vdf_kawaii.py)");
                return;
            }
            string path = RoeHumanoid.FighterPath(id);
            var root = PrefabUtility.LoadPrefabContents(path);
            var rig = root.GetComponent<RoeKawaiiRig>();
            if (rig == null)
                rig = root.AddComponent<RoeKawaiiRig>();
            rig.data = text;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var physics = new RoeKawaiiPhysics(new RoeClothScope { animator = go.GetComponent<Animator>(), world = go.transform });
            var skin = RoeBoneCloth.SkinPoints(go.transform);
            var notes = new List<string>();
            foreach (var c in physics.Capsules().GroupBy(c => (c.bone, c.radius, c.half, c.centre)).Select(g => g.First()))
            {
                var bone = go.GetComponentsInChildren<Transform>(true).First(t => t.name == c.bone);
                // where the centre is against the bone: along it towards its biggest child, and off that line
                var child = bone.Cast<Transform>().OrderByDescending(t => t.GetComponentsInChildren<Transform>(true).Length).FirstOrDefault();
                var along = child != null && (child.position - bone.position).sqrMagnitude > 1e-8f ? (child.position - bone.position).normalized : Vector3.up;
                var rel = c.centre - bone.position;
                float a = Vector3.Dot(rel, along);
                float off = (rel - along * a).magnitude;
                float nearest = skin.Count > 0 ? skin.Min(p => Vector3.Distance(p.p, c.centre)) : -1f;
                float tilt = Vector3.Angle(c.axis, along);
                notes.Add($"{c.node}: {c.bone} r {100f * c.radius:F1} L {200f * c.half:F0} cm, centre {100f * a:F1} cm along to " +
                          $"{(child != null ? child.name : "-")} and {100f * off:F1} cm off it, axis {tilt:F0} deg to the bone, nearest skin {100f * nearest:F1} cm");
            }
            Object.DestroyImmediate(go);
            Debug.Log($"[ROE] {id}: {physics.Report}; capsules: " + string.Join("; ", notes));
        }
    }
}
