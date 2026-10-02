using System.Collections.Generic;
using System.Linq;
using RoeFighter.Fight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>Checks on the saved fight scene.</summary>
    public static class RoeFightProbe
    {
        /// <summary>Lists what each fighter draws (renderer, enabled, materials/shaders).</summary>
        public static void Renderers()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            foreach (var rig in Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None))
                foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
                    Debug.Log($"[ROE] {rig.id} {RoeHumanoidClips.PathOf(r.transform, rig.transform)} enabled={r.enabled} active={r.gameObject.activeInHierarchy} " +
                              $"{r.GetType().Name} mats: {string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : $"{m.name}<{m.shader.name}>"))}");
        }

        /// <summary>
        /// The same clip, the same time: posed by sampling the clip alone, and by the fight's playable graph.
        /// Prints the bones that end up farthest apart.  -roeChar g04 -roeClip skill_03 -roeTimes 1,2,3
        /// </summary>
        public static void Pose()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string id = RoeCapture.Arg("-roeChar", "g04");
            string clipName = RoeCapture.Arg("-roeClip", "skill_03");
            var times = RoeCapture.Arg("-roeTimes", "0.5,1,1.5,2,2.5,3").Split(',').Select(float.Parse).ToArray();
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var rig = Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).First(r => r.id == id);
            var clip = rig.clips.First(c => c.name == clipName).clip;
            var bones = rig.animator.GetComponentsInChildren<Transform>(true);
            foreach (float time in times)
            {
                rig.Init();
                rig.Play(clipName, 1f, 0f);
                float t = 0f;
                const float dt = 1f / 60f;
                while (t + dt <= time)
                {
                    rig.Tick(dt);
                    t += dt;
                }
                var graphPose = bones.ToDictionary(b => b, b => b.position);
                RoeCapture.Pose(rig.animator.gameObject, clip, t);
                var worst = bones.Select(b => (b, d: Vector3.Distance(graphPose[b], b.position))).OrderByDescending(x => x.d).Take(8).ToList();
                var hips = rig.animator.GetBoneTransform(HumanBodyBones.Hips);
                Debug.Log($"[ROE] pose check {id} {clipName} t={t:F2}: hips graph {graphPose[hips]:F2} sampled {hips.position:F2}; farthest apart: " +
                          string.Join(", ", worst.Select(x => $"{x.b.name} {x.d:F2} m")));
                RoeCapture.EndPosing();
            }
        }
        /// <summary>Everything drawn while a skill runs (timeline-made objects included): -roeChar g04 -roeAction skill3 -roeTimes 1.5,2</summary>
        public static void SkillRenderers()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string id = RoeCapture.Arg("-roeChar", "g04");
            string action = RoeCapture.Arg("-roeAction", "skill3");
            string clipName = "skill_0" + action.Substring(action.Length - 1);
            var times = RoeCapture.Arg("-roeTimes", "1.5,2").Split(',').Select(float.Parse).ToArray();
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var rig = Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).First(r => r.id == id);
            rig.Init();
            rig.Play(clipName, 1f, 0f);
            float t = 0f;
            const float dt = 1f / 60f;
            foreach (float time in times)
            {
                while (t + dt <= time)
                {
                    t += dt;
                    rig.Tick(dt);
                    rig.ShowTimeline(action, t);
                }
                var hips = rig.animator.GetBoneTransform(HumanBodyBones.Hips);
                Debug.Log($"[ROE] skill renderers {id} {action} t={t:F2}: our hips {hips.position:F2}");
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || EditorUtility.IsPersistent(r) || r is ParticleSystemRenderer)
                        continue;
                    if (!r.transform.IsChildOf(rig.transform) && r.gameObject.scene != rig.gameObject.scene)
                        continue;
                    if (!r.transform.IsChildOf(rig.transform))
                        continue;
                    if (r.transform.IsChildOf(rig.animator.transform))
                        continue;
                    var smr = r as SkinnedMeshRenderer;
                    Debug.Log($"[ROE]   {RoeHumanoidClips.PathOf(r.transform, rig.transform)} {r.GetType().Name} bounds {r.bounds.center:F2} size {r.bounds.size:F2} " +
                              $"mats {string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "<" + m.shader.name + ">"))}" +
                              (smr != null && smr.rootBone != null ? $" rootBone {RoeHumanoidClips.PathOf(smr.rootBone, rig.transform)}" : ""));
                }
            }
        }
        /// <summary>
        /// The real fight logic, g04 starting her super as soon as the fight begins: hips and feet
        /// heights every 10 steps, and two rendered frames.  -roeOut dir
        /// </summary>
        public static void SuperInGame()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "_work", "probe_super"));
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            game.hud.drawnByCamera = true;
            foreach (var rig in game.rigs)
                foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
            game.Setup();
            var g = game.f[1];
            g.meter = 100f;
            var a = g.rig.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var sb = new System.Text.StringBuilder();
            bool started = false;
            for (int s = 0; s < 60 * 9; s++)
            {
                var inputs = new FighterInput[2];
                if (game.phase == FightGame.Phase.Fight && !started)
                {
                    inputs[1].s3 = true;
                    started = true;
                }
                game.Step(inputs);
                game.UpdateCamera(FightGame.Dt);
                if (g.state == FightState.Special && s % 10 == 0)
                    sb.Append($"\n[ROE]   step {s} special t={g.t:F2} rig clip {g.rig.Current}@{g.rig.CurrentTime:F2}: hips y {hips.position.y:F2} left foot y {lf.position.y:F2}");
                if (g.state == FightState.Special && (Mathf.Abs(g.t - 2.0f) < 0.009f || Mathf.Abs(g.t - 1.5f) < 0.009f))
                {
                    game.hud.Refresh(game);
                    Canvas.ForceUpdateCanvases();
                    RoeCapture.Render(game.cam, 320, 180, System.IO.Path.Combine(outDir, "_warm.jpg"), 80);
                    RoeCapture.Render(game.cam, 1280, 720, System.IO.Path.Combine(outDir, $"super_{g.t:F2}.jpg"), 90);
                }
            }
            Debug.Log("[ROE] super in game:" + sb);
        }

        /// <summary>Animators under each fighter and what the skill timelines are bound to.</summary>
        public static void Bindings()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            foreach (var rig in Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None))
            {
                foreach (var a in rig.GetComponentsInChildren<Animator>(true))
                    Debug.Log($"[ROE] {rig.id} animator at {RoeHumanoidClips.PathOf(a.transform, rig.transform)} enabled={a.enabled} avatar={(a.avatar ? a.avatar.name : "none")} controller={(a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "none")}");
                foreach (var nd in rig.directors.Where(d => d.action.StartsWith("skill")))
                {
                    var asset = nd.director.playableAsset as UnityEngine.Timeline.TimelineAsset;
                    if (asset == null)
                        continue;
                    foreach (var track in asset.GetOutputTracks())
                    {
                        if (!(track is UnityEngine.Timeline.AnimationTrack))
                            continue;
                        var bound = nd.director.GetGenericBinding(track);
                        string where = bound is Component c ? RoeHumanoidClips.PathOf(c.transform, rig.transform) : bound is GameObject g ? RoeHumanoidClips.PathOf(g.transform, rig.transform) : "nothing";
                        Debug.Log($"[ROE] {rig.id} {nd.action} track '{track.name}' bound to {bound?.GetType().Name} at {where}");
                    }
                }
            }
        }

        /// <summary>Leg length (hips to foot) and the largest bone scale over a whole clip, by the fight's graph.</summary>
        public static void Legs()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string id = RoeCapture.Arg("-roeChar", "g04");
            string clipName = RoeCapture.Arg("-roeClip", "skill_03");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var rig = Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).First(r => r.id == id);
            rig.Init();
            rig.Play(clipName, 1f, 0f);
            var a = rig.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            var bones = a.GetComponentsInChildren<Transform>(true);
            float len = rig.Length(clipName);
            const float dt = 1f / 60f;
            var sb = new System.Text.StringBuilder();
            for (float t = 0f; t < len; t += dt)
            {
                rig.Tick(dt);
                if (Mathf.RoundToInt(t / dt) % 15 != 0)
                    continue;
                float legL = Vector3.Distance(hips.position, lf.position), legR = Vector3.Distance(hips.position, rf.position);
                var big = bones.Select(b => (b, s: b.lossyScale.magnitude / Mathf.Sqrt(3f))).OrderByDescending(x => x.s).First();
                sb.Append($"\n[ROE]   t={t:F2} hips y {hips.position.y:F2} feet y {lf.position.y:F2}/{rf.position.y:F2} legs {legL:F2}/{legR:F2} m, " +
                          $"largest scale {big.b.name} {big.s:F2}, hips scale {hips.lossyScale.y:F2}");
            }
            Debug.Log($"[ROE] legs {id} {clipName} ({len:F2} s):" + sb);
        }
    }
}
