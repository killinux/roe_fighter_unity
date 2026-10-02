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

        /// <summary>
        /// The real fight logic, g04's super: every 10 steps, the skinning bones of her meshes that are
        /// lower than her feet (left behind on the floor while she jumps), and which meshes use them.
        /// </summary>
        public static void FloorBones()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            int who = int.Parse(RoeCapture.Arg("-roeFighter", "1"));
            string action = RoeCapture.Arg("-roeAction", "skill3");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            game.Setup();
            var g = game.f[who];
            g.meter = 100f;
            var a = g.rig.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
            var smrs = g.rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.transform.IsChildOf(a.transform)).ToArray();
            var sb = new System.Text.StringBuilder();
            bool started = false;
            for (int s = 0; s < 60 * 9; s++)
            {
                var inputs = new FighterInput[2];
                if (game.phase == FightGame.Phase.Fight && !started)
                {
                    if (action == "skill3") inputs[who].s3 = true;
                    else if (action == "skill2") inputs[who].s2 = true;
                    else inputs[who].s1 = true;
                    started = true;
                }
                game.Step(inputs);
                if (g.state != FightState.Special || s % 10 != 0)
                    continue;
                float floor = Mathf.Min(feet[0].position.y, feet[1].position.y) - 0.15f;
                sb.Append($"\n[ROE]   t={g.t:F2} hips y {hips.position.y:F2} feet y {feet[0].position.y:F2}/{feet[1].position.y:F2}");
                var low = new Dictionary<Transform, List<string>>();
                foreach (var r in smrs)
                    foreach (var b in r.bones)
                        if (b != null && b.position.y < floor)
                        {
                            if (!low.TryGetValue(b, out var users))
                                low[b] = users = new List<string>();
                            users.Add(r.name);
                        }
                foreach (var kv in low.OrderBy(kv => kv.Key.position.y).Take(12))
                    sb.Append($"\n[ROE]     low bone {RoeHumanoidClips.PathOf(kv.Key, a.transform)} y {kv.Key.position.y:F2} local {kv.Key.localPosition:F3} used by {string.Join(",", kv.Value.Distinct())}");
            }
            Debug.Log($"[ROE] floor bones of {g.rig.id} in {action}:" + sb);
        }

        /// <summary>
        /// The real fight logic, a fighter's skill up to a time (-roeTime 2.1): every mesh under the
        /// fighter skinned on the CPU (BakeMesh), the vertices that end up below the feet, the bones
        /// they hang from; then pictures of the legs: everything, our model only, the game's unit only.
        /// </summary>
        public static void Spikes()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            int who = int.Parse(RoeCapture.Arg("-roeFighter", "1"));
            string action = RoeCapture.Arg("-roeAction", "skill3");
            var times = RoeCapture.Arg("-roeTimes", "2.1").Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            string outDir = RoeCapture.Arg("-roeOut", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "_work", "probe_spikes"));
            System.IO.Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            game.hud.drawnByCamera = true;
            foreach (var rig in game.rigs)
                foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
            game.Setup();
            var g = game.f[who];
            g.meter = 100f;
            bool started = false;
            for (int s = 0; s < 60 * 12; s++)
            {
                var inputs = new FighterInput[2];
                if (game.phase == FightGame.Phase.Fight && !started)
                {
                    if (action == "skill3") inputs[who].s3 = true;
                    else if (action == "skill2") inputs[who].s2 = true;
                    else inputs[who].s1 = true;
                    started = true;
                }
                game.Step(inputs);
                if (started && g.state == FightState.Special && times.Length > 0 && g.t >= times[0])
                {
                    SpikesAt(game, g, action, outDir);
                    times = times.Skip(1).ToArray();
                    if (times.Length == 0)
                        break;
                }
            }
        }

        /// <summary>
        /// The recorded CPU match again (same seed, same steps as RoeFightScene.Record, nothing
        /// filmed) up to given steps (-roeSteps 5280,5340), then the Spikes check there, plus the
        /// game camera's own view and every bone of the fighter that is far from where its
        /// skinning expects it.
        /// </summary>
        public static void MatchAt()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            int who = int.Parse(RoeCapture.Arg("-roeFighter", "1"));
            int seed = int.Parse(RoeCapture.Arg("-roeSeed", "1"));
            var steps = RoeCapture.Arg("-roeSteps", "5280").Split(',').Select(int.Parse).ToList();
            string outDir = RoeCapture.Arg("-roeOut", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "_work", "probe_match"));
            System.IO.Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { true, true };
            game.seed = seed;
            game.hud.drawnByCamera = true;
            foreach (var rig in game.rigs)
                foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
            game.Setup();
            var g = game.f[who];
            var a = g.rig.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var history = new System.Text.StringBuilder();
            int last = steps.Max();
            for (int s = 0; s <= last; s++)
            {
                game.Step(null);
                game.UpdateCamera(FightGame.Dt);
                if (s % 30 == 0 && s >= steps.Min() - 600)
                    history.Append($"\n[ROE]   step {s}: {g.state} t={g.t:F2} clip {g.rig.Current}@{g.rig.CurrentTime:F2} hips y {hips.position.y:F2} left foot y {lf.position.y:F2}");
                if (!steps.Contains(s))
                    continue;
                string Size(FighterRig rig)
                {
                    var sizes = new List<string>();
                    foreach (var r in rig.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("wp_")))
                    {
                        var m = new Mesh();
                        r.BakeMesh(m, true);
                        m.RecalculateBounds();
                        sizes.Add($"{r.name} {m.bounds.size:F2} enabled {r.enabled}");
                        Object.DestroyImmediate(m);
                    }
                    return string.Join("; ", sizes);
                }
                Debug.Log($"[ROE] match at step {s} ({s / 60f:F2} s): {g.rig.id} {g.state} special {g.special?.name} t={g.t:F2} clip {g.rig.Current}@{g.rig.CurrentTime:F2}" + history +
                          string.Concat(game.f.Select(x => $"\n[ROE]   mixers {x.rig.id} {x.state} t={x.t:F2}: {x.rig.MixerWeights()}; weapon mesh {Size(x.rig)}")));
                history.Clear();
                game.hud.Refresh(game);
                Canvas.ForceUpdateCanvases();
                RoeCapture.Render(game.cam, 320, 180, System.IO.Path.Combine(outDir, "_warm.jpg"), 80);
                RoeCapture.Render(game.cam, 1280, 720, System.IO.Path.Combine(outDir, $"step{s}_game.jpg"), 90);
                SpikesAt(game, g, g.special?.name ?? g.state.ToString(), outDir);
            }
        }

        static void SpikesAt(FightGame game, Fighter g, string action, string outDir)
        {
            var a = g.rig.animator;
            var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
            float floor = Mathf.Min(feet[0].position.y, feet[1].position.y) - 0.2f;
            var sb = new System.Text.StringBuilder($"[ROE] spikes {g.rig.id} {action} t={g.t:F2} state {g.state}: hips y {a.GetBoneTransform(HumanBodyBones.Hips).position.y:F2}, feet y {feet[0].position.y:F2}/{feet[1].position.y:F2}, root y {g.rig.transform.position.y:F2}");
            var baked = new Mesh();
            foreach (var r in g.rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null)
                    continue;
                r.BakeMesh(baked, true);
                var v = baked.vertices;
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                var low = new List<int>();
                for (int i = 0; i < v.Length; i++)
                    if (m.MultiplyPoint3x4(v[i]).y < floor)
                        low.Add(i);
                bool ours = r.transform.IsChildOf(a.transform);
                sb.Append($"\n[ROE]   {(ours ? "model" : "unit ")} {r.name} enabled={r.enabled && r.gameObject.activeInHierarchy} verts {v.Length}, below the feet {low.Count}");
                if (low.Count == 0)
                    continue;
                var bw = r.sharedMesh.boneWeights;
                var bones = r.bones;
                string Name(int k) => k < bones.Length ? (bones[k] != null ? bones[k].name : "null") : $"#{k} out of {bones.Length}";
                var byBone = new Dictionary<string, int>();
                float lowest = float.MaxValue;
                foreach (int i in low)
                {
                    lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(v[i]).y);
                    if (i >= bw.Length)
                        continue;
                    var w = bw[i];
                    string key = $"{Name(w.boneIndex0)} {w.weight0:F2}" + (w.weight1 > 0 ? $" + {Name(w.boneIndex1)} {w.weight1:F2}" : "") +
                                 (w.weight2 > 0 ? $" + {Name(w.boneIndex2)} {w.weight2:F2}" : "") + (w.weight3 > 0 ? $" + {Name(w.boneIndex3)} {w.weight3:F2}" : "") +
                                 $" (sum {w.weight0 + w.weight1 + w.weight2 + w.weight3:F2})";
                    byBone[key] = byBone.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                sb.Append($", lowest y {lowest:F2}; root bone {(r.rootBone ? r.rootBone.name : "none")}");
                foreach (var kv in byBone.OrderByDescending(kv => kv.Value).Take(10))
                    sb.Append($"\n[ROE]     {kv.Value} verts: {kv.Key}");
            }
            Object.DestroyImmediate(baked);
            Debug.Log(sb.ToString());

            // pictures of the legs from the side, at the height of the feet
            var cam = game.cam;
            var camWas = (cam.transform.position, cam.transform.rotation);
            string tag = g.t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var mid = (feet[0].position + feet[1].position) * 0.5f;
            var side = Vector3.Cross(Vector3.up, g.Forward).normalized;
            cam.transform.position = mid + side * 4.5f + Vector3.down * 0.2f;
            cam.transform.LookAt(mid + Vector3.down * 0.4f);
            game.hud.gameObject.SetActive(false);
            var ourRenderers = a.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            var unitRenderers = g.rig.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !r.transform.IsChildOf(a.transform)).ToArray();
            RoeCapture.Render(cam, 320, 180, System.IO.Path.Combine(outDir, "_warm.jpg"), 80);
            RoeCapture.Render(cam, 960, 720, System.IO.Path.Combine(outDir, $"{tag}_all.jpg"), 90);
            foreach (var r in unitRenderers) r.enabled = false;
            RoeCapture.Render(cam, 960, 720, System.IO.Path.Combine(outDir, $"{tag}_model.jpg"), 90);
            foreach (var r in unitRenderers) r.enabled = true;
            foreach (var r in ourRenderers) r.enabled = false;
            RoeCapture.Render(cam, 960, 720, System.IO.Path.Combine(outDir, $"{tag}_unit.jpg"), 90);
            foreach (var r in ourRenderers) r.enabled = true;
            game.hud.gameObject.SetActive(true);
            cam.transform.SetPositionAndRotation(camWas.Item1, camWas.Item2);
            Debug.Log($"[ROE] spikes pictures t={tag} in {outDir}: {ourRenderers.Length} model renderers, {unitRenderers.Length} unit renderers ({string.Join(", ", unitRenderers.Select(r => r.name).Distinct().Take(20))})");
        }

        /// <summary>
        /// Close-ups of each fighter in each basic move, posed by the fight's own graph (all three
        /// layers), from the side and from the front: _work/probe_moves/&lt;id&gt;_&lt;view&gt;_&lt;clip&gt;_&lt;k&gt;.jpg
        /// (strikes at start, hit start, hit peak, hit end, recovery).  -roeClips guard,jab
        /// </summary>
        public static void MovesSheet()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "_work", "probe_moves"));
            var clipNames = RoeCapture.Arg("-roeClips", "guard,walk,walk_back,side_left,side_right,jab,cross,kick,slash,hurt").Split(',');
            int width = int.Parse(RoeCapture.Arg("-roeWidth", "400")), height = int.Parse(RoeCapture.Arg("-roeHeight", "560"));
            System.IO.Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            foreach (var rig in game.rigs)
                foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
            game.hud.gameObject.SetActive(false);
            var cam = game.cam;
            cam.fieldOfView = 30f;
            var facing = Quaternion.LookRotation(game.axis, Vector3.up);
            bool warmed = false;
            var sb = new System.Text.StringBuilder("[ROE] moves sheet:");
            foreach (var rig in game.rigs)
            {
                // the other one out of the way
                foreach (var other in game.rigs)
                    other.transform.position = other == rig ? game.centre : game.centre + Vector3.down * 50f;
                rig.transform.rotation = facing;
                rig.Init();
                foreach (var name in clipNames)
                {
                    if (!rig.Has(name))
                        continue;
                    var move = game.moves.FirstOrDefault(m => m.clip == name);
                    float len = rig.Length(name);
                    float[] times = move != null
                        ? new[] { 0f, move.hitStart, (move.hitStart + move.hitEnd) * 0.5f, move.hitEnd, Mathf.Min(len, move.length * move.recover) }
                        : Enumerable.Range(0, 5).Select(k => len * k / 5f).ToArray();
                    for (int k = 0; k < times.Length; k++)
                    {
                        rig.Init();
                        rig.Play(name, 1f, 0f);
                        for (float t = 0f; t + 1f / 120f < times[k]; t += 1f / 60f)
                            rig.Tick(1f / 60f);
                        rig.Tick(0f);
                        var a = rig.animator;
                        var hips = a.GetBoneTransform(HumanBodyBones.Hips).position;
                        var target = new Vector3(rig.transform.position.x, hips.y * 0.55f + 0.42f, rig.transform.position.z);
                        var fwd = rig.transform.forward;
                        var left = -rig.transform.right;
                        var views = new (string tag, Vector3 dir)[] { ("side", left), ("front", (fwd * 0.85f - left * 0.53f).normalized) };
                        foreach (var (tag, dir) in views)
                        {
                            cam.transform.position = target + dir * 4.1f + Vector3.up * 0.25f;
                            cam.transform.LookAt(target, Vector3.up);
                            if (!warmed)
                            {
                                RoeCapture.Render(cam, 200, 280, System.IO.Path.Combine(outDir, "_warm.jpg"), 80);
                                RoeCapture.Render(cam, 200, 280, System.IO.Path.Combine(outDir, "_warm.jpg"), 80);
                                warmed = true;
                            }
                            RoeCapture.Render(cam, width, height, System.IO.Path.Combine(outDir, $"{rig.id}_{tag}_{name}_{k}.jpg"), 90);
                        }
                        var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                        var rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
                        var lt = a.GetBoneTransform(HumanBodyBones.LeftToes);
                        var rt = a.GetBoneTransform(HumanBodyBones.RightToes);
                        sb.Append($"\n[ROE]   {rig.id} {name} t={times[k]:F2}: hips y {hips.y:F2}, feet y {lf.position.y:F2}/{rf.position.y:F2}, toes y {(lt ? lt.position.y : 0f):F2}/{(rt ? rt.position.y : 0f):F2}");
                    }
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Foot sliding in the real fight logic: each fighter walks forward, walks back and steps to
        /// each side for 3 s; while a foot is planted (its sole within 2 cm of the floor) it should
        /// not move.  Prints the body speed and the planted feet's mean speed per locomotion.
        /// </summary>
        public static void FootSlide()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            string pack = RoeCapture.Arg("-roeMotions", null);
            if (pack != null)
                game.motionPack = Mathf.Max(0, game.motionPacks.FindIndex(p => p.name == pack));
            var sb = new System.Text.StringBuilder($"[ROE] foot slide ({game.Pack?.name}):");
            var tests = new (string name, int x, int y)[] { ("forward", 1, 0), ("back", -1, 0), ("side up", 0, 1), ("side down", 0, -1) };
            for (int who = 0; who < 2; who++)
            {
                foreach (var test in tests)
                {
                    game.Setup();
                    while (game.phase != FightGame.Phase.Fight)
                        game.Step(new FighterInput[2]);
                    var me = game.f[who];
                    // the opponent far enough not to be walked into
                    me.foe.pos = me.pos + (me.foe.pos - me.pos).normalized * 6.5f;
                    me.foe.Place();
                    var a = me.rig.animator;
                    var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot),
                                       a.GetBoneTransform(HumanBodyBones.LeftToes), a.GetBoneTransform(HumanBodyBones.RightToes) };
                    // which screen direction is "forward" for this fighter
                    var camRight = game.cam.transform.right;
                    camRight.y = 0f;
                    int towards = Vector3.Dot(me.foe.pos - me.pos, camRight) >= 0f ? 1 : -1;
                    var floorY = a.transform.position.y;
                    var last = feet.Select(f => f.position).ToArray();
                    // the lowest of each bone in the first second = its planted height
                    var planted = feet.Select(f => float.MaxValue).ToArray();
                    // the contact: each step, the bone (ankle or ball of a foot) nearest its planted height;
                    // while it is within 2 cm of it and stays the same bone, it should not move
                    float slid = 0f, contactTime = 0f, travelled = 0f;
                    var drift = Vector3.zero;
                    int lastContact = -1;
                    var start = me.pos;
                    for (int s = 0; s < 180; s++)
                    {
                        var inputs = new FighterInput[2];
                        inputs[who] = new FighterInput { x = test.x * towards, y = test.y };
                        game.Step(inputs);
                        game.UpdateCamera(FightGame.Dt);
                        int contact = -1;
                        float lowest = float.MaxValue;
                        for (int k = 0; k < feet.Length; k++)
                        {
                            float h = feet[k].position.y - a.transform.position.y;
                            if (s < 60)
                                planted[k] = Mathf.Min(planted[k], h);
                            else if (h - planted[k] < lowest)
                            {
                                lowest = h - planted[k];
                                contact = k;
                            }
                        }
                        if (s >= 60 && lowest < 0.02f && contact == lastContact)
                        {
                            var d = feet[contact].position - last[contact];
                            d.y = 0f;
                            slid += d.magnitude;
                            drift += d;
                            contactTime += FightGame.Dt;
                        }
                        lastContact = lowest < 0.02f ? contact : -1;
                        for (int k = 0; k < feet.Length; k++)
                            last[k] = feet[k].position;
                    }
                    travelled = Vector3.Distance(Flat(start), Flat(me.pos));
                    var v = contactTime > 0f ? drift / contactTime : Vector3.zero;
                    sb.Append($"\n[ROE]   {me.rig.id} {test.name} (strides {me.rig.StrideReport()}): state {me.state} clip {me.rig.Current} rate {me.rig.CurrentRate:F2}, body {travelled / 3f:F2} m/s; " +
                              $"the contact slides {(contactTime > 0f ? slid / contactTime : 0f):F2} m/s ({contactTime / 2f * 100f:F0}% of the time), " +
                              $"drifting {Vector3.Dot(v, me.Forward):F2} m/s forward, {Vector3.Dot(v, Quaternion.Euler(0f, me.yaw, 0f) * Vector3.right):F2} m/s right");
                }
            }
            Debug.Log(sb.ToString());

            Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        }

        /// <summary>
        /// g04's fan over her battle stance (layer 0 of the graph, under the basic moves): the distance
        /// between the two outer ribs' tips and the fan's size, every 0.25 s of the stance clip.
        /// </summary>
        public static void Fan()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var rig = Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).First(r => r.id == "g04");
            rig.Init();
            rig.Play("guard", 1f, 0f);
            var bones = rig.animator.GetComponentsInChildren<Transform>(true);
            var left = bones.FirstOrDefault(b => b.name == "Left_fan_03");
            var right = bones.FirstOrDefault(b => b.name == "Right_fan_03");
            var root = bones.FirstOrDefault(b => b.name == "fan_01");
            var sb = new System.Text.StringBuilder($"[ROE] g04 fan over the stance ({rig.stance.name}, {rig.stance.length:F2} s):");
            for (int s = 0; s <= Mathf.CeilToInt(rig.stance.length * 60f) + 30; s++)
            {
                rig.Tick(1f / 60f);
                if (s % 15 == 0)
                    sb.Append($"\n[ROE]   t={s / 60f:F2}: outer ribs {Vector3.Distance(left.position, right.position):F2} m apart, fan scale {root.lossyScale.x:F2}");
            }
            Debug.Log(sb.ToString());

            // the same in the fight: side steps, then walking, then standing
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            game.Setup();
            while (game.phase != FightGame.Phase.Fight)
                game.Step(new FighterInput[2]);
            var g = game.f[1];
            g.foe.pos = g.pos + (g.foe.pos - g.pos).normalized * 6.5f;
            sb.Clear();
            sb.Append("[ROE] g04 fan in the fight:");
            for (int s = 0; s < 360; s++)
            {
                var inputs = new FighterInput[2];
                inputs[1] = s < 180 ? new FighterInput { y = 1 } : s < 300 ? new FighterInput { x = -1 } : default;
                game.Step(inputs);
                if (s % 15 == 0)
                    sb.Append($"\n[ROE]   step {s} {g.state} clip {g.rig.Current}: outer ribs {Vector3.Distance(left.position, right.position):F2} m apart; {g.rig.MixerWeights()}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// One cycle of a locomotion clip in place (-roeChar g04 -roeClip walk): the ankles' and balls'
        /// heights and positions along the body's forward axis every 1/30 s, at rate 1.
        /// </summary>
        public static void Cycle()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string id = RoeCapture.Arg("-roeChar", "g04");
            string clipName = RoeCapture.Arg("-roeClip", "walk");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var rig = Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).First(r => r.id == id);
            rig.Init();
            rig.Play(clipName, 1f, 0f);
            var a = rig.animator;
            var bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, HumanBodyBones.Hips }
                .Select(a.GetBoneTransform).ToArray();
            float len = rig.Length(clipName);
            var sb = new System.Text.StringBuilder($"[ROE] {id} {clipName} ({len:F2} s) in place, forward z / height y of: left ankle, left ball, right ankle, right ball, hips");
            for (int s = 0; s <= Mathf.CeilToInt(len * 30f) + 3; s++)
            {
                rig.Tick(s == 0 ? 0f : 1f / 30f);
                sb.Append($"\n[ROE]   t={rig.CurrentTime:F3}: " + string.Join("  ", bones.Select(b =>
                {
                    var p = rig.transform.InverseTransformPoint(b.position);
                    return $"{p.z,6:F3}/{p.y,5:F3}";
                })));
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// The bones a cloth simulation could move, per fighter: every non-human bone that is not a
        /// limb helper, grouped by the bone its chain hangs on and its name without the trailing number,
        /// with the chain depth, how much skin the group carries and whether the battle stance animates it.
        /// </summary>
        public static void ClothBones()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            foreach (var rig in Object.FindObjectsByType<FighterRig>(FindObjectsSortMode.None).OrderBy(r => r.id))
            {
                var a = rig.animator;
                var human = new HashSet<Transform>();
                foreach (HumanBodyBones hb in System.Enum.GetValues(typeof(HumanBodyBones)))
                    if (hb != HumanBodyBones.LastBone && a.GetBoneTransform(hb) != null)
                        human.Add(a.GetBoneTransform(hb));
                var helperRig = a.GetComponent<RoeHelperRig>();
                var helpers = new HashSet<Transform>(helperRig != null ? helperRig.drives.Select(d => d.helper) : Enumerable.Empty<Transform>());
                var skin = new Dictionary<Transform, int>();
                foreach (var r in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bw = r.sharedMesh.boneWeights;
                    foreach (var w in bw)
                        if (w.weight0 > 0.3f && r.bones[w.boneIndex0] != null)
                            skin[r.bones[w.boneIndex0]] = skin.TryGetValue(r.bones[w.boneIndex0], out int n) ? n + 1 : 1;
                }
                var animated = new HashSet<string>(AnimationUtility.GetCurveBindings(rig.stance).Select(b => b.path.Split('/').Last()));
                string Prefix(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"[._\-\s]*\d+$", "");
                var groups = new Dictionary<string, List<Transform>>();
                foreach (var t in a.GetComponentsInChildren<Transform>(true))
                {
                    if (t == a.transform || human.Contains(t) || helpers.Contains(t) || t.GetComponent<Renderer>() != null)
                        continue;
                    var top = t;
                    while (top.parent != null && !human.Contains(top.parent) && Prefix(top.parent.name) == Prefix(t.name))
                        top = top.parent;
                    string key = $"{Prefix(t.name)} (on {(top.parent != null ? top.parent.name : "-")})";
                    if (!groups.TryGetValue(key, out var list))
                        groups[key] = list = new List<Transform>();
                    list.Add(t);
                }
                var sb = new System.Text.StringBuilder($"[ROE] cloth candidates of {rig.id}:");
                foreach (var kv in groups.OrderByDescending(kv => kv.Value.Sum(t => skin.TryGetValue(t, out int n) ? n : 0)))
                {
                    int verts = kv.Value.Sum(t => skin.TryGetValue(t, out int n) ? n : 0);
                    if (verts == 0)
                        continue;
                    int keyed = kv.Value.Count(t => animated.Contains(t.name));
                    float span = kv.Value.Count > 1 ? kv.Value.Max(t => Vector3.Distance(t.position, kv.Value[0].position)) : 0f;
                    sb.Append($"\n[ROE]   {kv.Key}: {kv.Value.Count} bones, {verts} verts, {keyed} keyed in the stance, span {span:F2} m: " +
                              string.Join(" ", kv.Value.Take(8).Select(t => t.name)) + (kv.Value.Count > 8 ? " ..." : ""));
                }
                Debug.Log(sb.ToString());
            }
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
            /// <summary>
        /// Where the pelvis points in humanoid clips: each clip posed on a fighter at a few times, the
        /// pelvis bone's axes, the spine and thighs relative to world up, and the skirt's first bone.
        ///   -executeMethod RoeFighter.EditorTools.RoeFightProbe.PelvisCheck [-roeChars a08] [-roeClips path1,path2]
        /// </summary>
        public static void PelvisCheck()
        {
            var ids = RoeCapture.Arg("-roeChars", "a08").Split(',');
            var clips = RoeCapture.Arg("-roeClips", "Assets/RoeFighter/Generated/mocap/mc_guard.anim," +
                                       "Assets/RoeFighter/Generated/motionpacks/accad_male2/guard.anim," +
                                       "Assets/RoeFighter/Generated/motionpacks/accad_male2/walk.anim").Split(',');
            foreach (var id in ids)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id)));
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var a = go.GetComponent<Animator>();
                var pelvis = a.GetBoneTransform(HumanBodyBones.Hips);
                var spine = a.GetBoneTransform(HumanBodyBones.Spine);
                var thighL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var thighR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var kneeL = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                var kneeR = a.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                var skirt = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Skirt_L_00" || t.name == "Skirt_Back_01");
                string Dir(Vector3 v) => $"({v.x:+0.00;-0.00} {v.y:+0.00;-0.00} {v.z:+0.00;-0.00})";
                float Tilt(Vector3 v) => Vector3.Angle(v, Vector3.up);
                var sb = new System.Text.StringBuilder($"[ROE] pelvis check {id}:");
                foreach (var path in clips)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null)
                        continue;
                    foreach (float t in new[] { 0f, clip.length * 0.5f })
                    {
                        RoeCapture.Pose(go, clip, t);
                        var sp = spine.position - pelvis.position;
                        var hipsAxis = thighR.position - thighL.position;
                        sb.Append($"\n[ROE]   {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path))}/{clip.name} t={t:F2}: pelvis x{Dir(pelvis.right)} y{Dir(pelvis.up)} z{Dir(pelvis.forward)}; " +
                                  $"spine tilt {Tilt(sp):F0} deg, hip axis {Dir(hipsAxis.normalized)}, thighs L{Dir((kneeL.position - thighL.position).normalized)} R{Dir((kneeR.position - thighR.position).normalized)}" +
                                  (skirt != null && skirt.childCount > 0 ? $", {skirt.name} {Dir((skirt.GetChild(0).position - skirt.position).normalized)} local {skirt.localEulerAngles}" : ""));
                    }
                }
                RoeCapture.EndPosing();
                Object.DestroyImmediate(go);
                Debug.Log(sb.ToString());
            }
        }
    }
}
