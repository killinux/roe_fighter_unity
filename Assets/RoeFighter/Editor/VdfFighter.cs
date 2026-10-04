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
            return AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id));
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
