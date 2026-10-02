using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoeFighter.Fight;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Builds motion packs (MotionPack): the swappable basic moves of the fight.
    ///
    ///   BuildBandai   the Bandai Namco motion capture converted by RoeMocap (pack "bandai1").
    ///   Import        any folder of humanoid clips - Asset Store packs imported as Humanoid FBX, .anim
    ///                 files, other converted datasets - described by a small JSON file:
    ///   {
    ///     "name": "my_pack", "title": "Shown in the game", "source": "...", "license": "...",
    ///     "folder": "Assets/ThirdParty/SomePack",          // searched for AnimationClips (FBX sub-assets, .anim)
    ///     "humanoid": true,                                 // set the FBX files there to Humanoid first
    ///     "roles": { "guard": "Fight_Idle", "walk": "Walk_Fwd", "walk_back": "Walk_Bwd", "run": "Run" },
    ///     "strikes": [ { "button": "A", "name": "jab", "clip": "Punch_Jab", "speed": 1.0, "damage": 40 }, ... ]
    ///   }
    ///   Clips are found by name (case does not matter; "file.fbx:clip" picks one file).  Locomotion
    ///   clips are copied as loops with their travel taken out (the fight moves the body); every strike
    ///   is measured on a fighter model: which hand or foot hits, how far it reaches, when (the frames
    ///   it is within 85% of its farthest), how high.  Buttons not given in a strike get defaults
    ///   from light (A) to heavy (D).
    ///   -executeMethod RoeFighter.EditorTools.RoeMotionPacks.Import -roeSpec path\to\pack.json
    /// </summary>
    public static class RoeMotionPacks
    {
        public const string Dir = "Assets/RoeFighter/Generated/motionpacks";
        public static string PackPath(string name) => $"{Dir}/{name}.asset";

        /// <summary>All packs, the Bandai one first.</summary>
        public static List<MotionPack> All()
        {
            if (!Directory.Exists(Dir))
                return new List<MotionPack>();
            return AssetDatabase.FindAssets("t:MotionPack", new[] { Dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<MotionPack>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.name == "bandai1" ? 0 : 1).ThenBy(p => p.name).ToList();
        }

        // ---- the Bandai Namco motion capture (RoeMocap)

        [MenuItem("ROE Fighter/Motions/Build Bandai pack")]
        public static void BuildBandai()
        {
            var table = JsonUtility.FromJson<StrikeTable>(File.ReadAllText(RoeMocap.MovesPath));
            var pack = ScriptableObject.CreateInstance<MotionPack>();
            pack.title = "Bandai Namco motion dataset 1";
            pack.source = "Bandai Namco Research Motion Dataset 1 (https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset), converted by RoeMocap";
            pack.license = "CC BY-NC 4.0, Bandai Namco Research Inc.";
            foreach (var role in MotionPack.Roles)
                Add(pack, role, AssetDatabase.LoadAssetAtPath<AnimationClip>(RoeMocap.ClipPath(role)), true);
            Move Make(string name, string button, float speed, int damage, float hitstun, float blockstun, float push, Level level, float radius, float meter)
            {
                var s = table.moves.First(x => x.name == name);
                Add(pack, name, AssetDatabase.LoadAssetAtPath<AnimationClip>(RoeMocap.ClipPath(name)), false);
                return FromStrike(s, name, button, speed, damage, hitstun, blockstun, push, level, radius, meter);
            }
            pack.strikes = new List<Move>
            {
                Make("jab", "A", 1.7f, 40, 0.32f, 0.18f, 0.25f, Level.High, 0.15f, 5f),
                Make("slash", "B", 1.6f, 60, 0.40f, 0.22f, 0.35f, Level.Mid, 0.20f, 6f),
                Make("cross", "C", 1.5f, 80, 0.45f, 0.25f, 0.45f, Level.High, 0.17f, 7f),
                Make("kick", "D", 1.7f, 95, 0.50f, 0.28f, 0.60f, Level.Mid, 0.22f, 8f),
            };
            Save(pack, "bandai1");
        }

        [Serializable]
        class StrikeTable
        {
            public List<RoeMocap.StrikeInfo> moves = new List<RoeMocap.StrikeInfo>();
        }

        static void Add(MotionPack pack, string role, AnimationClip clip, bool loop)
        {
            if (clip == null)
            {
                if (role == "guard")
                    throw new Exception($"motion pack {pack.title}: no guard clip");
                return;
            }
            pack.clips.Add(new MotionPack.Clip { role = role, clip = clip, loop = loop });
        }

        static Move FromStrike(RoeMocap.StrikeInfo s, string name, string button, float speed, int damage, float hitstun, float blockstun,
                               float push, Level level, float radius, float meter)
        {
            return new Move
            {
                name = name, button = button, clip = name, speed = speed,
                bone = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s.bone),
                radius = radius, hitStart = s.hitStart, hitEnd = s.hitEnd, length = s.length,
                damage = damage, hitstun = hitstun, blockstun = blockstun, push = push, level = level,
                meterGain = meter, reach = s.reach,
                recover = 0.82f, cancel = Mathf.Clamp01(s.hitEnd / Mathf.Max(0.01f, s.length) + 0.05f),
            };
        }

        static MotionPack Save(MotionPack pack, string name)
        {
            Directory.CreateDirectory(Dir);
            string path = PackPath(name);
            pack.name = name;
            // an existing pack is updated in place, so scenes that hold it keep it
            var existing = AssetDatabase.LoadAssetAtPath<MotionPack>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(pack, existing);
                EditorUtility.SetDirty(existing);
                pack = existing;
            }
            else
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(pack, path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ROE] motion pack {path}: {pack.title}; roles {string.Join(" ", pack.clips.Select(c => c.role + (c.loop ? "(loop)" : "")))}; strikes " +
                      string.Join(", ", pack.strikes.Select(m => $"{m.button} {m.name} ({m.bone}, reach {m.reach:F2} m, hit {m.hitStart:F2}-{m.hitEnd:F2} of {m.length:F2} s)")));
            return pack;
        }

        // ---- any humanoid clips, from a JSON description

        [Serializable]
        public class Spec
        {
            public string name, title, source, license, folder;
            public bool humanoid = true;
            public float walkSpeed, backSpeed;  // m/s; 0: the fight's own (MotionPack)
            public RoeMocap.Source bvh;         // motion capture to convert first (its segments become the clips)
            public RoleMap roles = new RoleMap();
            public List<StrikeSpec> strikes = new List<StrikeSpec>();
        }

        [Serializable]
        public class RoleMap
        {
            public string guard, walk, walk_back, run;
        }

        [Serializable]
        public class StrikeSpec
        {
            public string button, name, clip, level;
            public float speed = 1f, hitstun, blockstun, push, radius, meter;
            public int damage;
            public bool knockdown;
        }

        // light to heavy: A jab-like ... D kick-like (damage, hitstun, blockstun, push, radius, meter)
        static readonly Dictionary<string, (int, float, float, float, float, float)> ButtonDefaults = new Dictionary<string, (int, float, float, float, float, float)>
        {
            { "A", (40, 0.32f, 0.18f, 0.25f, 0.15f, 5f) },
            { "B", (60, 0.40f, 0.22f, 0.35f, 0.18f, 6f) },
            { "C", (80, 0.45f, 0.25f, 0.45f, 0.18f, 7f) },
            { "D", (95, 0.50f, 0.28f, 0.60f, 0.22f, 8f) },
        };

        [MenuItem("ROE Fighter/Motions/Import pack from JSON")]
        public static void Import()
        {
            string specPath = RoeCapture.Arg("-roeSpec", null);
            if (specPath == null)
                specPath = EditorUtility.OpenFilePanel("Motion pack description", Application.dataPath, "json");
            if (string.IsNullOrEmpty(specPath))
                return;
            if (!Path.IsPathRooted(specPath))
                specPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), specPath);
            ImportSpec(JsonUtility.FromJson<Spec>(File.ReadAllText(specPath)));
        }

        public static MotionPack ImportSpec(Spec spec)
        {
            if (spec.bvh != null && spec.bvh.segments != null && spec.bvh.segments.Count > 0)
            {
                // BVH: each segment becomes a humanoid clip named after it, then on as for any clips
                spec.folder = $"{Dir}/{spec.name}/bvh";
                spec.humanoid = false;
                RoeMocap.ConvertAll(spec.bvh, n => $"{spec.folder}/{n}.anim");
            }
            if (spec.humanoid)
                MakeHumanoid(spec.folder);
            var found = FindClips(spec.folder);
            AnimationClip Find(string key)
            {
                if (string.IsNullOrEmpty(key))
                    return null;
                string file = null, clip = key;
                if (key.Contains(":"))
                {
                    file = key.Substring(0, key.IndexOf(':'));
                    clip = key.Substring(key.IndexOf(':') + 1);
                }
                var hit = found.FirstOrDefault(x => string.Equals(x.clip.name, clip, StringComparison.OrdinalIgnoreCase) &&
                                                    (file == null || Path.GetFileName(x.path).Equals(file, StringComparison.OrdinalIgnoreCase)));
                if (hit.clip == null)
                    Debug.LogWarning($"[ROE] motion pack {spec.name}: no clip '{key}' under {spec.folder}");
                return hit.clip;
            }

            string outDir = $"{Dir}/{spec.name}";
            Directory.CreateDirectory(outDir);
            var pack = ScriptableObject.CreateInstance<MotionPack>();
            pack.title = string.IsNullOrEmpty(spec.title) ? spec.name : spec.title;
            pack.source = spec.source;
            pack.license = spec.license;
            pack.walkSpeed = spec.walkSpeed;
            pack.backSpeed = spec.backSpeed;
            foreach (var (role, key) in new[] { ("guard", spec.roles.guard), ("walk", spec.roles.walk), ("walk_back", spec.roles.walk_back), ("run", spec.roles.run) })
            {
                var src = Find(key);
                if (src == null)
                    continue;
                Add(pack, role, Copy(src, $"{outDir}/{role}.anim", loop: true, inPlace: role != "guard"), true);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath("a08"));
            foreach (var s in spec.strikes)
            {
                var src = Find(s.clip);
                if (src == null)
                    continue;
                string name = string.IsNullOrEmpty(s.name) ? s.clip : s.name;
                var clip = Copy(src, $"{outDir}/{name}.anim", loop: false, inPlace: false);
                Add(pack, name, clip, false);
                var info = Measure(clip, model, name);
                var d = ButtonDefaults.TryGetValue(s.button, out var v) ? v : ButtonDefaults["B"];
                var level = string.IsNullOrEmpty(s.level) ? (info.height > 1.2f ? Level.High : info.height < 0.5f ? Level.Low : Level.Mid)
                                                           : (Level)Enum.Parse(typeof(Level), s.level, true);
                var move = FromStrike(info, name, s.button, s.speed > 0f ? s.speed : 1f, s.damage > 0 ? s.damage : d.Item1,
                                      s.hitstun > 0f ? s.hitstun : d.Item2, s.blockstun > 0f ? s.blockstun : d.Item3,
                                      s.push > 0f ? s.push : d.Item4, level, s.radius > 0f ? s.radius : d.Item5, s.meter > 0f ? s.meter : d.Item6);
                move.knockdown = s.knockdown;
                pack.strikes.Add(move);
            }
            return Save(pack, spec.name);
        }

        /// <summary>The FBX files of a folder imported as Humanoid (an avatar made from each model).</summary>
        static void MakeHumanoid(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.SaveAndReimport();
                }
            }
        }

        static List<(AnimationClip clip, string path)> FindClips(string folder)
        {
            var list = new List<(AnimationClip, string)>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                        list.Add((c, path));
            }
            return list;
        }

        /// <summary>
        /// A writable copy of a humanoid clip; loops get loop time on, and with inPlace their travel on
        /// the floor (RootT x/z from first to last key) is taken out - the fight moves the body itself.
        /// </summary>
        public static AnimationClip Copy(AnimationClip src, string path, bool loop, bool inPlace)
        {
            var clip = new AnimationClip { frameRate = src.frameRate };
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (inPlace && b.type == typeof(Animator) && (b.propertyName == "RootT.x" || b.propertyName == "RootT.z") && curve.length > 1)
                {
                    var keys = curve.keys;
                    float t0 = keys[0].time, t1 = keys[keys.Length - 1].time;
                    float v0 = keys[0].value, v1 = keys[keys.Length - 1].value;
                    float slope = (v1 - v0) / Mathf.Max(1e-4f, t1 - t0);
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k].value -= slope * (keys[k].time - t0);
                        keys[k].inTangent -= slope;
                        keys[k].outTangent -= slope;
                    }
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(src);
            settings.loopTime = loop;
            settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>
        /// A strike measured on a fighter model, the way RoeMocap measures the motion capture: the hand or
        /// foot that gets farthest in front of where the hips started, how far, how high, and the frames
        /// it is within 85% of that.
        /// </summary>
        public static RoeMocap.StrikeInfo Measure(AnimationClip clip, GameObject model, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = go.GetComponent<Animator>();
            var bones = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * 60f) + 1);
            var reach = new float[bones.Length, frames];
            var height = new float[bones.Length, frames];
            Vector3 hips0 = Vector3.zero;
            for (int f = 0; f < frames; f++)
            {
                RoeCapture.Pose(go, clip, Mathf.Min(clip.length, f / 60f));
                if (f == 0)
                    hips0 = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                for (int k = 0; k < bones.Length; k++)
                {
                    var p = animator.GetBoneTransform(bones[k]).position;
                    reach[k, f] = p.z - hips0.z;
                    height[k, f] = p.y;
                }
            }
            RoeCapture.EndPosing();
            UnityEngine.Object.DestroyImmediate(go);
            int best = 0, peak = 0;
            for (int k = 0; k < bones.Length; k++)
                for (int f = 0; f < frames; f++)
                    if (reach[k, f] > reach[best, peak])
                    {
                        best = k;
                        peak = f;
                    }
            float max = reach[best, peak];
            int a = peak, b = peak;
            while (a > 0 && reach[best, a - 1] >= 0.85f * max)
                a--;
            while (b < frames - 1 && reach[best, b + 1] >= 0.85f * max)
                b++;
            return new RoeMocap.StrikeInfo
            {
                name = name, bone = bones[best].ToString(), length = clip.length, reach = max, height = height[best, peak],
                hitStart = a / 60f, hitPeak = peak / 60f, hitEnd = b / 60f,
            };
        }
    }
}
