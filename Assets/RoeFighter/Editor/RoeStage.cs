using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// The game's own battle stages (imported by tools/import_stage.py into Assets/ROE/stages).
    ///   Open():   loads the stage scene and makes it usable here - game scripts that do not exist
    ///             in this project are stripped, the game's cameras are switched off, renderers
    ///             whose shader we have no replacement for (particles, decals) are hidden.
    ///   Stills(): the two fighters face each other on the stage, a few camera angles.
    /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeStage.Stills [-roeStage s01] [-roeOut dir]
    /// </summary>
    public static class RoeStage
    {
        public static string ScenePath(string stage)
        {
            var found = Directory.GetFiles($"Assets/ROE/stages/{stage}", "*.unity", SearchOption.AllDirectories);
            if (found.Length == 0)
                throw new FileNotFoundException($"No scene under Assets/ROE/stages/{stage}; run tools/import_stage.py {stage}");
            return found[0].Replace('\\', '/');
        }

        public class Info
        {
            public Bounds bounds;
            public List<Transform> formation = new List<Transform>();
            public Light sun;
            public Vector3 gameCameraPosition;
            public bool hasGameCamera;
            public string report;
        }

        public static Info Open(string stage)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath(stage), OpenSceneMode.Single);
            var info = new Info();
            var sb = new StringBuilder();
            int stripped = 0, hidden = 0, shown = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    stripped += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!info.hasGameCamera)
                {
                    info.gameCameraPosition = cam.transform.position;   // tells us from which side the game films the fight
                    info.hasGameCamera = true;
                    sb.Append($" gameCamera[{cam.name} pos {cam.transform.position} rot {cam.transform.rotation.eulerAngles} fov {cam.fieldOfView}]");
                }
                cam.gameObject.SetActive(false);
            }
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                listener.enabled = false;

            bool first = true;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool ok = r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported
                                                                                    && !m.shader.name.StartsWith("Hidden/")
                                                                                    && !m.shader.name.StartsWith("Pinkcore/"));
                if (!ok)
                {
                    r.enabled = false;
                    hidden++;
                    continue;
                }
                shown++;
                if (r is MeshRenderer)
                {
                    if (first) { info.bounds = r.bounds; first = false; }
                    else info.bounds.Encapsulate(r.bounds);
                }
            }

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                sb.Append($" light[{light.name} {light.type} i={light.intensity:F2} shadows={light.shadows} bake={light.lightmapBakeType}]");
                if (light.type == LightType.Directional && (info.sun == null || light.intensity > info.sun.intensity))
                    info.sun = light;
            }

            var formation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "FormationSetting");
            if (formation != null)
                info.formation = formation.GetComponentsInChildren<Transform>(true).Where(t => t != formation).ToList();

            info.report = $"stage {stage}: {shown} renderers shown, {hidden} hidden (no shader), {stripped} game scripts stripped, " +
                          $"bounds {info.bounds.min} .. {info.bounds.max}, lightmaps {LightmapSettings.lightmaps.Length}, " +
                          $"skybox {(RenderSettings.skybox ? RenderSettings.skybox.shader.name : "none")}, fog {RenderSettings.fog} " +
                          $"({RenderSettings.fogMode}, {RenderSettings.fogDensity}), ambient {RenderSettings.ambientMode}, " +
                          $"formation nodes {info.formation.Count};{sb}";
            return info;
        }

        [MenuItem("ROE Fighter/Check/Stage report")]
        public static void Report()
        {
            string stage = RoeCapture.Arg("-roeStage", "s01");
            var info = Open(stage);
            Debug.Log("[ROE] " + info.report);
            var sb = new StringBuilder("[ROE] formation nodes:");
            foreach (var t in info.formation.Take(60))
                sb.Append($"\n[ROE]   {RoeHumanoidClips.PathOf(t, null)} pos {t.position} rot {t.rotation.eulerAngles}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Where the fight takes place on a stage and from where it is filmed.</summary>
        public class Layout
        {
            public Vector3 centre;      // on the ground, between the fighters
            public Vector3 axis;        // unit vector from fighter 1 to fighter 2
            public Vector3 normal;      // unit vector from the fight line towards the camera
            public int occluders;
        }

        /// <summary>
        /// The game's battle layout gives the line the two sides stand on (FormationSetting).  A
        /// fighting-game camera looks at that line from the side, so of the two sides and of a few
        /// shifts along the line, take the one with the fewest stage props between camera and fighters.
        /// </summary>
        public static Layout FindLayout(Info info, float cameraDistance = 7f)
        {
            var layout = new Layout { axis = Vector3.forward };
            layout.centre = new Vector3(info.bounds.center.x, info.bounds.min.y, info.bounds.center.z);
            var centreNode = info.formation.FirstOrDefault(t => t.name == "Center");
            var p1 = info.formation.FirstOrDefault(t => t.name == "Anchor1" && t.parent.name == "1" && t.parent.parent.name == "Player1");
            var p2 = info.formation.FirstOrDefault(t => t.name == "Anchor1" && t.parent.name == "1" && t.parent.parent.name == "Player2");
            if (centreNode != null)
                layout.centre = centreNode.position;
            if (p1 != null && p2 != null && (p2.position - p1.position).sqrMagnitude > 0.01f)
            {
                layout.axis = Vector3.ProjectOnPlane(p2.position - p1.position, Vector3.up).normalized;
                if (centreNode == null)
                    layout.centre = (p1.position + p2.position) * 0.5f;
            }

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled).ToArray();
            var baseNormal = Vector3.Cross(Vector3.up, layout.axis).normalized;
            Vector3 bestCentre = layout.centre;
            Vector3 bestNormal = baseNormal;
            int best = int.MaxValue;
            float bestShift = 0f;
            foreach (float shift in new[] { 0f, 0.75f, -0.75f, 1.5f, -1.5f, 2.25f, -2.25f, 3f, -3f })
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    var c = layout.centre + layout.axis * shift;
                    var n = baseNormal * sign;
                    // the volume the camera looks through: 7 m of fight line, from just in front of the fighters to the camera
                    var corridor = new Bounds(c + Vector3.up * 1.3f, Vector3.zero);
                    foreach (float u in new[] { -3.5f, 3.5f })
                        foreach (float v in new[] { 0.9f, cameraDistance })
                            foreach (float h in new[] { 0.35f, 2.4f })
                                corridor.Encapsulate(c + layout.axis * u + n * v + Vector3.up * h);
                    int count = renderers.Count(r => r.bounds.Intersects(corridor));
                    if (count < best || (count == best && Mathf.Abs(shift) < Mathf.Abs(bestShift)))
                    {
                        best = count;
                        bestCentre = c;
                        bestNormal = n;
                        bestShift = shift;
                    }
                }
            }
            layout.centre = bestCentre;
            layout.normal = bestNormal;
            layout.occluders = best;
            return layout;
        }

        static void SampleAll(IList<GameObject> fighters, IList<AnimationClip> clips, IList<float> times)
        {
            // one batch for everybody: a new batch reverts whatever the previous one posed
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            for (int i = 0; i < fighters.Count; i++)
                if (clips[i] != null)
                    AnimationMode.SampleAnimationClip(fighters[i], clips[i], times[i]);
            AnimationMode.EndSampling();
        }

        /// <summary>
        /// A choreographed exchange on the stage, filmed like a fighting game: the two fighters take
        /// turns playing their skills, the other one flinches when the attacker is closest.  This is
        /// NOT gameplay (no hit detection, no input) - it shows how characters, stage and camera will look.
        /// Writes frames + timeline.json for tools/make_video.py.
        /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeStage.Duel [-roeStage s01] [-roeOut dir] [-roeSize 1920x1080]
        /// </summary>
        [MenuItem("ROE Fighter/Showcase/Stage duel video frames")]
        public static void Duel()
        {
            string stage = RoeCapture.Arg("-roeStage", "s01");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "duel"));
            var size = RoeCapture.Arg("-roeSize", "1920x1080").Split('x');
            int width = int.Parse(size[0]), height = int.Parse(size[1]);
            const int fps = 60;
            float gap = float.Parse(RoeCapture.Arg("-roeGap", "3.4"), System.Globalization.CultureInfo.InvariantCulture);

            ShaderUtil.allowAsyncCompilation = false;
            var info = Open(stage);
            var layout = FindLayout(info);
            Debug.Log("[ROE] " + info.report);
            Debug.Log($"[ROE] duel layout: centre {layout.centre}, axis {layout.axis}, camera side {layout.normal}, {layout.occluders} props in the way");

            var manifest = RoeManifest.Load();
            var chars = manifest.WithModels.Take(2).ToArray();
            var fighters = new List<GameObject>();
            foreach (var c in chars)
                fighters.Add((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoeFighterBuilder.PrefabPath(c.id))));
            var idles = chars.Select(c => c.LoadClip("idle_01")).ToArray();
            var hurts = chars.Select(c => c.LoadClip("hurt")).ToArray();
            SampleAll(fighters, idles, new[] { 0f, 0f });
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                fighters[i].transform.position = layout.centre + layout.axis * (gap * 0.5f * side);
                var forward = RoeShowcase.Forward(fighters[i]);
                fighters[i].transform.rotation = Quaternion.FromToRotation(forward, -layout.axis * side) * fighters[i].transform.rotation;
            }
            var hips = fighters.Select(f => RoeShowcase.FindBone(f, "Bip001")).ToArray();

            // ---- schedule: per frame, which clip and time each fighter shows
            var clipOf = new[] { new List<AnimationClip>(), new List<AnimationClip>() };
            var timeOf = new[] { new List<float>(), new List<float>() };
            var timeline = new List<string>();
            int total = 0;
            void Idle(int frames)
            {
                for (int f = 0; f < frames; f++)
                    for (int i = 0; i < 2; i++)
                    {
                        clipOf[i].Add(idles[i]);
                        timeOf[i].Add(((total + f) / (float)fps) % idles[i].length);
                    }
                total += frames;
            }
            void Attack(int attacker, string skill)
            {
                var clip = chars[attacker].LoadClip(skill);
                if (clip == null)
                    return;
                int victim = 1 - attacker;
                int frames = Mathf.RoundToInt(clip.length * fps);
                // the moment of impact: when the attacker's hips are furthest from where they started
                int hit = 0;
                float far = 0f;
                Vector3 start = Vector3.zero;
                for (int f = 0; f < frames; f += 2)
                {
                    var clips = new AnimationClip[2];
                    clips[attacker] = clip;
                    clips[victim] = idles[victim];
                    SampleAll(fighters, clips, new[] { f / (float)fps, f / (float)fps });
                    if (f == 0) start = hips[attacker].position;
                    float d = Vector3.ProjectOnPlane(hips[attacker].position - start, Vector3.up).magnitude;
                    if (d > far) { far = d; hit = f; }
                }
                int hurtFrames = hurts[victim] != null ? Mathf.RoundToInt(hurts[victim].length * fps) : 0;
                timeline.Add($"{{\"id\":\"{chars[attacker].id}\",\"clip\":\"{skill}\",\"frame\":{total},\"frames\":{frames},\"fps\":{fps}}}");
                timeline.Add($"{{\"id\":\"{chars[attacker].id}\",\"clip\":\"{skill}_hit\",\"frame\":{total + hit},\"frames\":1,\"fps\":{fps}}}");
                for (int f = 0; f < frames; f++)
                {
                    clipOf[attacker].Add(clip);
                    timeOf[attacker].Add(f / (float)fps);
                    if (hurtFrames > 0 && f >= hit && f < hit + hurtFrames)
                    {
                        clipOf[victim].Add(hurts[victim]);
                        timeOf[victim].Add((f - hit) / (float)fps);
                    }
                    else
                    {
                        clipOf[victim].Add(idles[victim]);
                        timeOf[victim].Add(((total + f) / (float)fps) % idles[victim].length);
                    }
                }
                total += frames;
                Debug.Log($"[ROE] duel: {chars[attacker].id} {skill} {frames} frames, impact at frame {hit} ({far:F2} m lunge)");
            }

            Idle(75);
            foreach (var skill in new[] { "skill_01", "skill_02", "skill_03" })
            {
                Attack(0, skill);
                Idle(30);
                Attack(1, skill);
                Idle(30);
            }
            Idle(45);
            timeline.Add($"{{\"id\":\"_total\",\"clip\":\"\",\"frame\":0,\"frames\":{total},\"fps\":{fps}}}");

            // ---- camera
            var camGo = new GameObject("Duel Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            const float fov = 30f;
            cam.fieldOfView = fov;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            var volGo = new GameObject("Duel Volume");
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RoeProjectSetup.SettingsDir + "/RoeStudioVolume.asset");

            // pass 1: where everybody's bones are, frame by frame, measured along the fight line and in height
            var bones = fighters.SelectMany(f => f.GetComponentsInChildren<SkinnedMeshRenderer>(true)).SelectMany(r => r.bones)
                .Where(b => b != null).Distinct().ToArray();
            var mid = new float[total];
            var midY = new float[total];
            var dist = new float[total];
            float tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float aspect = width / (float)height;
            for (int f = 0; f < total; f++)
            {
                SampleAll(fighters, new[] { clipOf[0][f], clipOf[1][f] }, new[] { timeOf[0][f], timeOf[1][f] });
                float lo = float.MaxValue, hi = float.MinValue, top = 0f;
                foreach (var b in bones)
                {
                    float u = Vector3.Dot(b.position - layout.centre, layout.axis);
                    lo = Mathf.Min(lo, u);
                    hi = Mathf.Max(hi, u);
                    top = Mathf.Max(top, b.position.y - layout.centre.y);
                }
                mid[f] = (lo + hi) * 0.5f;
                float halfWidth = (hi - lo) * 0.5f + 0.7f;
                float halfHeight = Mathf.Max(1.25f, (top + 0.45f) * 0.5f);
                midY[f] = halfHeight - 0.12f;
                dist[f] = Mathf.Max(halfWidth / aspect, halfHeight) / tanHalf;
            }
            int r = Mathf.RoundToInt(fps * 0.45f);
            mid = RoeShowcase.Smooth(RoeShowcase.Smooth(mid, r), r);
            midY = RoeShowcase.Smooth(RoeShowcase.Smooth(midY, r), r);
            dist = RoeShowcase.Smooth(RoeShowcase.Smooth(RoeShowcase.SlidingMax(dist, r), r), r);

            // pass 2: render
            string warm = Path.Combine(outDir, "_warmup.jpg");
            for (int f = 0; f < total; f++)
            {
                SampleAll(fighters, new[] { clipOf[0][f], clipOf[1][f] }, new[] { timeOf[0][f], timeOf[1][f] });
                var target = layout.centre + layout.axis * mid[f] + Vector3.up * midY[f];
                cam.transform.position = target + layout.normal * dist[f] + Vector3.up * 0.25f;
                cam.transform.LookAt(target, Vector3.up);
                if (f == 0)
                {
                    RoeCapture.Render(cam, 320, 180, warm, 80);
                    RoeCapture.Render(cam, 320, 180, warm, 80);
                }
                RoeCapture.Render(cam, width, height, Path.Combine(outDir, "frames", $"{f:D5}.jpg"), 95);
            }
            RoeCapture.EndPosing();
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "timeline.json"), "[" + string.Join(", ", timeline) + "]");
            Debug.Log($"[ROE] duel: {total} frames written to {outDir}");
        }

        [MenuItem("ROE Fighter/Showcase/Stage stills")]
        public static void Stills()
        {
            string stage = RoeCapture.Arg("-roeStage", "s01");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "stage"));
            ShaderUtil.allowAsyncCompilation = false;
            var info = Open(stage);
            Debug.Log("[ROE] " + info.report);

            // where the fight happens: the middle of the formation nodes if the scene has them, else the middle of the stage
            Vector3 centre = info.formation.Count > 0
                ? info.formation.Aggregate(Vector3.zero, (a, t) => a + t.position) / info.formation.Count
                : new Vector3(info.bounds.center.x, info.bounds.min.y, info.bounds.center.z);
            Vector3 axis = Vector3.right;       // the line the two fighters stand on
            // The game's battle layout: FormationSetting/Center, and one anchor per side for a 1-vs-1 fight.
            var centreNode = info.formation.FirstOrDefault(t => t.name == "Center");
            var p1 = info.formation.FirstOrDefault(t => t.name == "Anchor1" && t.parent.name == "1" && t.parent.parent.name == "Player1");
            var p2 = info.formation.FirstOrDefault(t => t.name == "Anchor1" && t.parent.name == "1" && t.parent.parent.name == "Player2");
            if (centreNode != null)
                centre = centreNode.position;
            if (p1 != null && p2 != null && (p2.position - p1.position).sqrMagnitude > 0.01f)
            {
                axis = Vector3.ProjectOnPlane(p2.position - p1.position, Vector3.up).normalized;
                if (centreNode == null)
                    centre = (p1.position + p2.position) * 0.5f;
            }

            var manifest = RoeManifest.Load();
            var ids = manifest.WithModels.Select(c => c.id).Take(2).ToArray();
            var fighters = new List<GameObject>();
            for (int i = 0; i < ids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeFighterBuilder.PrefabPath(ids[i]));
                fighters.Add((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            }
            // one sampling batch for both: a new batch reverts whatever the previous one posed
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            for (int i = 0; i < ids.Length; i++)
            {
                var idle = manifest.Find(ids[i]).LoadClip("idle_01");
                if (idle != null)
                    AnimationMode.SampleAnimationClip(fighters[i], idle, 0.5f);
            }
            AnimationMode.EndSampling();
            for (int i = 0; i < ids.Length; i++)
            {
                var go = fighters[i];
                float side = i == 0 ? -1f : 1f;
                go.transform.position = centre + axis * (1.5f * side);
                // turn the character so that its own forward points at the opponent
                var forward = RoeShowcase.Forward(go);
                go.transform.rotation = Quaternion.FromToRotation(forward, -axis * side) * go.transform.rotation;
            }

            // camera + post processing
            var camGo = new GameObject("Stage Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            var volGo = new GameObject("Stage Volume");
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RoeProjectSetup.SettingsDir + "/RoeStudioVolume.asset");

            Vector3 look = centre + Vector3.up * 1.0f;
            Vector3 normal = Vector3.Cross(Vector3.up, axis).normalized;
            if (info.hasGameCamera && Vector3.Dot(info.gameCameraPosition - centre, normal) < 0f)
                normal = -normal;       // film from the side the game films from: the stage is dressed for that view
            void Shot(string name, Vector3 offset, float fov, Vector3 target, int w, int h)
            {
                cam.transform.position = centre + offset;
                cam.transform.LookAt(target, Vector3.up);
                cam.fieldOfView = fov;
                RoeCapture.Render(cam, w, h, Path.Combine(outDir, name));
            }

            string warm = "_warmup.png";
            Shot(warm, normal * 6f + Vector3.up * 1.3f, 30f, look, 320, 180);
            Shot(warm, normal * 6f + Vector3.up * 1.3f, 30f, look, 320, 180);
            Shot($"{stage}_1_fight_side.png", normal * 6.2f + Vector3.up * 1.25f, 30f, look, 1920, 1080);
            Shot($"{stage}_2_fight_other_side.png", -normal * 6.2f + Vector3.up * 1.25f, 30f, look, 1920, 1080);
            Shot($"{stage}_3_wide.png", normal * 14f + axis * 6f + Vector3.up * 5f, 40f, look, 1920, 1080);
            Shot($"{stage}_4_close.png", normal * 3.2f - axis * 2.2f + Vector3.up * 1.45f, 32f, centre - axis * 1.5f + Vector3.up * 1.2f, 1920, 1080);
            RoeCapture.EndPosing();
            Debug.Log($"[ROE] stage stills written to {outDir}; fight centre {centre}, axis {axis}");
        }
    }
}
