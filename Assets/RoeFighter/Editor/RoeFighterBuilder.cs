using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Builds one clean character prefab per ROE outfit from what the game ships:
    ///   - the HD model prefab (skeleton, skinned meshes, Generic avatar; placeholder materials),
    ///   - the real materials, read per renderer from the game's "meta" showcase prefab,
    ///   - weapons that the HD prefab lacks, taken from the battle (LD) prefab, which has them
    ///     skinned to the same bone names.
    /// Result: Assets/RoeFighter/Generated/&lt;id&gt;/&lt;id&gt;.prefab
    /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeFighterBuilder.BuildAll
    /// </summary>
    public static class RoeFighterBuilder
    {
        public const string OutDir = "Assets/RoeFighter/Generated";

        public static string PrefabPath(string id) => $"{OutDir}/{id}/{id}.prefab";

        [MenuItem("ROE Fighter/Build/Character prefabs")]
        public static void BuildAll()
        {
            var manifest = RoeManifest.Load();
            foreach (var c in manifest.WithModels)
                Build(c);
            AssetDatabase.SaveAssets();
        }

        public static GameObject Build(RoeManifest.Character c)
        {
            var hd = AssetDatabase.LoadAssetAtPath<GameObject>(c.hdPrefab);
            if (hd == null)
            {
                Debug.LogError($"[ROE] {c.id}: HD prefab not found at {c.hdPrefab}");
                return null;
            }
            var go = Object.Instantiate(hd);
            go.name = c.id;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int assigned = AssignMaterials(c, go);
            int weapons = AddMissingWeapons(c, go);

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // skill clips carry the body metres away from the prefab root
                smr.updateWhenOffscreen = true;
                smr.skinnedMotionVectors = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Directory.CreateDirectory($"{OutDir}/{c.id}");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(c.id));
            Object.DestroyImmediate(go);

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var placeholders = renderers.SelectMany(r => r.sharedMaterials)
                .Count(m => m == null || m.name.StartsWith("default_material"));
            Debug.Log($"[ROE] built {PrefabPath(c.id)}: {renderers.Length} renderers, {assigned} material slots from the game, " +
                      $"{weapons} weapon meshes added, {placeholders} slots still on a placeholder, " +
                      $"{prefab.GetComponentsInChildren<Transform>(true).Length} transforms");
            return prefab;
        }

        static int AssignMaterials(RoeManifest.Character c, GameObject go)
        {
            var meta = string.IsNullOrEmpty(c.metaPrefab) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(c.metaPrefab);
            var source = new Dictionary<string, Renderer>();
            if (meta != null)
                foreach (var r in meta.GetComponentsInChildren<Renderer>(true))
                    if (!source.ContainsKey(r.name))
                        source[r.name] = r;

            int assigned = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (source.TryGetValue(r.name, out var src) && src.sharedMaterials.Length == r.sharedMaterials.Length)
                {
                    r.sharedMaterials = src.sharedMaterials;
                    assigned += r.sharedMaterials.Length;
                }
                else if (r.name.StartsWith("wp_"))
                {
                    var mat = FindWeaponMaterial(c, r.name);
                    if (mat != null)
                    {
                        r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                        assigned += r.sharedMaterials.Length;
                    }
                }
                else
                {
                    Debug.LogWarning($"[ROE] {c.id}: no material source for renderer '{r.name}'");
                }
            }
            return assigned;
        }

        static int AddMissingWeapons(RoeManifest.Character c, GameObject go)
        {
            if (string.IsNullOrEmpty(c.ldPrefab))
                return 0;
            var ld = AssetDatabase.LoadAssetAtPath<GameObject>(c.ldPrefab);
            if (ld == null)
                return 0;

            var have = new HashSet<string>(go.GetComponentsInChildren<Renderer>(true).Select(r => r.name));
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name))
                    bones[t.name] = t;

            int added = 0;
            foreach (var src in ld.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!src.name.StartsWith("wp_") || have.Contains(src.name) || src.sharedMesh == null)
                    continue;
                var mapped = new Transform[src.bones.Length];
                bool ok = true;
                for (int i = 0; i < mapped.Length; i++)
                {
                    if (src.bones[i] == null || !bones.TryGetValue(src.bones[i].name, out mapped[i]))
                    {
                        Debug.LogWarning($"[ROE] {c.id}: weapon '{src.name}' needs bone '{(src.bones[i] ? src.bones[i].name : "null")}' which the HD rig lacks");
                        ok = false;
                        break;
                    }
                }
                if (!ok)
                    continue;

                var child = new GameObject(src.name);
                child.transform.SetParent(go.transform, false);
                var smr = child.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = src.sharedMesh;
                smr.bones = mapped;
                if (src.rootBone != null && bones.TryGetValue(src.rootBone.name, out var root))
                    smr.rootBone = root;
                smr.localBounds = src.localBounds;
                var mat = FindWeaponMaterial(c, src.name);
                smr.sharedMaterials = Enumerable.Repeat(mat, src.sharedMaterials.Length).ToArray();
                added++;
            }
            return added;
        }

        /// <summary>wp_a08_l (outfit a08) -> material "wp_a_08_hd".</summary>
        static Material FindWeaponMaterial(RoeManifest.Character c, string rendererName)
        {
            string name = $"wp_{c.Family}_{c.id.Substring(1)}_hd";
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material", new[] { "Assets/ROE" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && mat.name == name)
                    return mat;
            }
            Debug.LogWarning($"[ROE] {c.id}: weapon material '{name}' not found for '{rendererName}'");
            return null;
        }
    }
}
