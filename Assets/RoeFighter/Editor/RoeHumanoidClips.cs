using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Converts the game's Generic clips (one transform curve per bone) into Humanoid clips for
    /// the fighter prefab (see RoeHumanoid):
    ///   - the body becomes muscle curves + root curves.  The original clip is sampled on the
    ///     game's rig, the pose is copied bone by bone onto the fighter rig (whose hierarchy may
    ///     differ), and HumanPoseHandler reads the muscles from there;
    ///   - everything that is not a human bone (hair, skirt, breasts, twist helpers, weapons, face)
    ///     keeps its original transform curves, re-addressed to the fighter rig's paths;
    ///   - the nodes above the hips (Root, Bip001) lose their curves, because a humanoid Animator
    ///     places the hips itself; their other children (weapon attach points) are re-expressed
    ///     relative to the now static parent so that they end up in the same place.
    /// Verify() plays the result back next to the original and reports the differences.
    /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeHumanoidClips.ConvertAll
    /// </summary>
    public static class RoeHumanoidClips
    {
        const int Fps = 60;

        public static string ClipPath(string id, string clip) => $"{RoeFighterBuilder.OutDir}/{id}/humanoid/{id}_{clip}.anim";

        /// <summary>"Left Index 1 Stretched" (HumanTrait) -> "LeftHand.Index.1 Stretched" (curve attribute).</summary>
        static string MuscleAttribute(string traitName)
        {
            var m = Regex.Match(traitName, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
            return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : traitName;
        }

        public static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Dictionary<string, Transform> ByName(GameObject go)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != go.transform && !d.ContainsKey(t.name))
                    d[t.name] = t;
            return d;
        }

        /// <summary>Copies the pose of one rig onto another with the same bone names (parents first).</summary>
        public class PoseCopier
        {
            readonly Transform[] from, to;

            public PoseCopier(GameObject source, GameObject target, ICollection<Transform> skip = null)
            {
                var src = ByName(source);
                var a = new List<Transform>();
                var b = new List<Transform>();
                foreach (var t in target.GetComponentsInChildren<Transform>(true))   // depth first: parents come first
                {
                    if (t == target.transform || !src.TryGetValue(t.name, out var s) || (skip != null && skip.Contains(t)))
                        continue;
                    a.Add(s);
                    b.Add(t);
                }
                from = a.ToArray();
                to = b.ToArray();
            }

            public void Copy()
            {
                for (int i = 0; i < from.Length; i++)
                {
                    to[i].localScale = from[i].localScale;
                    to[i].SetPositionAndRotation(from[i].position, from[i].rotation);
                }
            }
        }

        class Keys
        {
            public readonly List<Keyframe>[] curves;
            public Keys(int count)
            {
                curves = new List<Keyframe>[count];
                for (int i = 0; i < count; i++)
                    curves[i] = new List<Keyframe>();
            }
        }

        static AnimationCurve Curve(List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        /// <param name="original">instance of the Generic prefab (the game's hierarchy)</param>
        /// <param name="fighter">instance of the fighter prefab (humanoid avatar, normalized hierarchy)</param>
        /// <param name="fighterPrefab">the fighter prefab asset, for bind-pose values</param>
        public static AnimationClip Convert(string id, string clipName, AnimationClip source,
                                            GameObject original, GameObject fighter, GameObject fighterPrefab)
        {
            var root = fighter.transform;
            var avatar = fighter.GetComponent<Animator>().avatar;
            var map = RoeHumanoid.MapBones(fighter);
            var humanTransforms = new HashSet<Transform>(map.Values);
            var hips = map["Hips"];
            // Nodes above the hips stay in their bind pose (a humanoid Animator places the hips itself).
            // Every non-human node whose parent is one of them, or whose parent differs from the game's
            // rig (weapons moved under a hand), gets new curves: its local values on the fighter rig
            // once the original pose has been copied over.
            var dropped = RoeHumanoid.AboveHips(fighter, map);
            var copier = new PoseCopier(original, fighter, dropped);
            var originalByName = ByName(original);
            var rebake = new List<Transform>();
            foreach (var t in fighter.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || humanTransforms.Contains(t) || dropped.Contains(t))
                    continue;
                if (!originalByName.TryGetValue(t.name, out var o) || o.parent == null)
                    continue;
                if (dropped.Contains(t.parent) || o.parent.name != t.parent.name)
                    rebake.Add(t);
            }
            var bindScale = map.ToDictionary(p => p.Value, p => fighterPrefab.transform.Find(PathOf(p.Value, root)).localScale);

            var handler = new HumanPoseHandler(avatar, root);
            var pose = new HumanPose();
            int muscles = HumanTrait.MuscleCount;
            int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * Fps));

            var muscleKeys = new Keys(muscles);
            var rootKeys = new Keys(7);
            var rebakeKeys = rebake.ToDictionary(r => r, r => new Keys(10));
            var lastRootQ = Quaternion.identity;
            var lastRebakeQ = rebake.ToDictionary(r => r, r => Quaternion.identity);
            float worstScale = 0f;
            string worstScaleBone = "";

            for (int f = 0; f <= frames; f++)
            {
                float time = Mathf.Min(source.length, f / (float)Fps);
                RoeCapture.Pose(original, source, time);
                copier.Copy();
                handler.GetHumanPose(ref pose);

                for (int i = 0; i < muscles; i++)
                    muscleKeys.curves[i].Add(new Keyframe(time, pose.muscles[i]));

                var q = pose.bodyRotation;
                if (f > 0 && Quaternion.Dot(q, lastRootQ) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                lastRootQ = q;
                float[] rootValues = { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z, q.x, q.y, q.z, q.w };
                for (int i = 0; i < 7; i++)
                    rootKeys.curves[i].Add(new Keyframe(time, rootValues[i]));

                foreach (var r in rebake)
                {
                    Vector3 p = r.localPosition;
                    var rq = r.localRotation;
                    if (f > 0 && Quaternion.Dot(rq, lastRebakeQ[r]) < 0f)
                        rq = new Quaternion(-rq.x, -rq.y, -rq.z, -rq.w);
                    lastRebakeQ[r] = rq;
                    var s = r.localScale;
                    float[] v = { p.x, p.y, p.z, rq.x, rq.y, rq.z, rq.w, s.x, s.y, s.z };
                    for (int i = 0; i < 10; i++)
                        rebakeKeys[r].curves[i].Add(new Keyframe(time, v[i]));
                }

                foreach (var pair in bindScale)
                {
                    float d = (pair.Key.localScale - pair.Value).magnitude;
                    if (d > worstScale)
                    {
                        worstScale = d;
                        worstScaleBone = pair.Key.name;
                    }
                }
            }
            handler.Dispose();

            var clip = new AnimationClip { name = $"{id}_{clipName}", frameRate = Fps };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();

            for (int i = 0; i < muscles; i++)
            {
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), MuscleAttribute(HumanTrait.MuscleName[i])));
                curves.Add(Curve(muscleKeys.curves[i]));
            }
            string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < 7; i++)
            {
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), rootNames[i]));
                curves.Add(Curve(rootKeys.curves[i]));
            }

            // Original transform curves of everything that is not a human bone and not above the hips,
            // addressed by bone name: the fighter rig's paths can differ from the game's.
            var fighterByName = ByName(fighter);
            var skip = new HashSet<Transform>(humanTransforms);
            skip.UnionWith(dropped);
            skip.UnionWith(rebake);
            int kept = 0, replaced = 0, missing = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform))
                {
                    replaced++;     // the game's own root-motion floats of the Generic rig
                    continue;
                }
                string boneName = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
                if (!fighterByName.TryGetValue(boneName, out var target))
                {
                    missing++;
                    continue;
                }
                if (skip.Contains(target))
                {
                    replaced++;
                    continue;
                }
                var b = binding;
                b.path = PathOf(target, root);
                bindings.Add(b);
                curves.Add(AnimationUtility.GetEditorCurve(source, binding));
                kept++;
            }

            string[] trs =
            {
                "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
            };
            foreach (var r in rebake)
            {
                string path = PathOf(r, root);
                for (int i = 0; i < 10; i++)
                {
                    bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), trs[i]));
                    curves.Add(Curve(rebakeKeys[r].curves[i]));
                }
            }

            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();

            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopBlend = false;
            // keep the motion inside the pose, as the game plays these clips: the object stays, the body travels
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.heightFromFeet = false;
            settings.mirror = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string assetPath = ClipPath(id, clipName);
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(clip, assetPath);
            Debug.Log($"[ROE] {id} {clipName}: {assetPath} human={clip.humanMotion}; {frames + 1} keys x {muscles} muscles, " +
                      $"{kept} curves kept, {replaced} replaced, {missing} without a target, {rebake.Count} nodes re-based" +
                      (worstScale > 0.01f ? $"; WARNING {worstScaleBone} is scaled by the clip ({worstScale:F2}) and loses that" : ""));
            return clip;
        }

        /// <summary>
        /// Plays the humanoid clip on a fighter instance and compares every skinned bone's position
        /// with the original Generic playback on the game's rig.
        /// </summary>
        public static string Verify(GameObject original, AnimationClip source, GameObject fighter, AnimationClip converted)
        {
            var target = ByName(fighter);
            var a = original.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones)
                .Where(b => b != null && target.ContainsKey(b.name)).Distinct().ToArray();
            var b2 = a.Select(t => target[t.name]).ToArray();

            int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * Fps));
            float worst = 0f, sum = 0f;
            int count = 0;
            string worstBone = "";
            float worstTime = 0f;
            for (int f = 0; f <= frames; f += 2)
            {
                float time = Mathf.Min(source.length, f / (float)Fps);
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(original, source, time);
                AnimationMode.SampleAnimationClip(fighter, converted, time);
                AnimationMode.EndSampling();
                for (int i = 0; i < a.Length; i++)
                {
                    float d = Vector3.Distance(a[i].position - original.transform.position, b2[i].position - fighter.transform.position);
                    sum += d;
                    count++;
                    if (d > worst)
                    {
                        worst = d;
                        worstBone = a[i].name;
                        worstTime = time;
                    }
                }
            }
            return $"mean {sum / count * 100f:F2} cm, worst {worst * 100f:F1} cm ({worstBone} at {worstTime:F2} s) over {a.Length} bones";
        }

        [MenuItem("ROE Fighter/Build/Humanoid fighters and clips")]
        public static void ConvertAll()
        {
            var manifest = RoeManifest.Load();
            foreach (var c in manifest.WithModels)
            {
                var fighterPrefab = RoeHumanoid.BuildFighter(c);    // always rebuild: the clips depend on the avatar
                if (fighterPrefab == null)
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeFighterBuilder.PrefabPath(c.id));
                var original = Object.Instantiate(prefab);
                original.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var worker = Object.Instantiate(fighterPrefab);     // receives the copied poses
                worker.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var player = Object.Instantiate(fighterPrefab);     // plays the converted clips
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                if (!AnimationMode.InAnimationMode())
                    AnimationMode.StartAnimationMode();
                foreach (var entry in c.clips)
                {
                    var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.path);
                    if (source == null)
                        continue;
                    var converted = Convert(c.id, entry.name, source, original, worker, fighterPrefab);
                    Debug.Log($"[ROE] {c.id} {entry.name}: playback difference {Verify(original, source, player, converted)}");
                }
                RoeCapture.EndPosing();
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(worker);
                Object.DestroyImmediate(player);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
