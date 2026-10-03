using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RoeFighter.Fight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Builds the playable fight scene: one of the game's battle stages, every fighter of the roster (the
    /// select screen picks two; the others wait put away), the fight logic, camera and HUD.  Saved as
    /// Assets/RoeFighter/Scenes/Fight_&lt;stage&gt;.unity (press Play).
    ///   Build   -executeMethod RoeFighter.EditorTools.RoeFightScene.Build [-roeStage e23_steel_s02] [-roeRoster a08,g04,b10,g05]
    ///           [-roeP1 a08] [-roeP2 g04]   (the two picked when the scene starts)
    ///   Record  -executeMethod RoeFighter.EditorTools.RoeFightScene.Record [-roeStage ...] [-roeOut dir] [-roeSeconds 90]
    ///           [-roeSize 1280x720] [-roeSeed 1] [-roeP1 b10] [-roeP2 g05]   CPU against CPU, frames + timeline.json for tools/make_video.py
    ///   Player  -executeMethod RoeFighter.EditorTools.RoeFightScene.BuildPlayer [-roeStage ...] [-roeOut dir]
    /// </summary>
    public static class RoeFightScene
    {
        public const string SceneDir = "Assets/RoeFighter/Scenes";
        static string ProjectDir => Path.GetDirectoryName(Application.dataPath);

        public static string ScenePath(string stage) => $"{SceneDir}/Fight_{stage}.unity";

        static readonly string[] GameClips = { "hurt", "die", "rip", "idle_02", "react_01", "react_02", "skill_01", "skill_02", "skill_03" };
        static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
        {
            { "a08", "INASE" }, { "g04", "LUF" }, { "b10", "KART" }, { "g05", "GODDESS LUF" },
        };
        // a word on each suit for the select screen
        static readonly Dictionary<string, string> Outfits = new Dictionary<string, string>
        {
            { "a08", "Valkyrie" }, { "g04", "Porcelain" }, { "b10", "Agent" }, { "g05", "Goddess" },
        };

        /// <summary>Who the scene holds (user 10-03: "b10，G05也把nude补全，加入战斗").</summary>
        public const string DefaultRoster = "a08,g04,b10,g05";

        public static string PortraitPath(string id) => $"{RoeFighterBuilder.OutDir}/{id}/{id}_portrait.png";
        static readonly HashSet<string> Loops = new HashSet<string> { "guard", "walk", "walk_back", "side_left", "side_right", "run", "rip", "idle_02" };

        [MenuItem("ROE Fighter/Fight/Build fight scene")]
        public static void Build()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string p1 = RoeCapture.Arg("-roeP1", "a08"), p2 = RoeCapture.Arg("-roeP2", "g04");
            BuildScene(stage, p1, p2);
        }

        /// <summary>The fighters the scene holds: -roeRoster, else DefaultRoster; the two picked are always in it.</summary>
        static List<string> Roster(string p1, string p2)
        {
            var ids = RoeCapture.Arg("-roeRoster", DefaultRoster).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            foreach (var id in new[] { p1, p2 })
                if (!ids.Contains(id))
                    ids.Add(id);
            return ids.Where(id => File.Exists(RoeHumanoid.FighterPath(id)) && File.Exists(RoeSkillSheet.PathOf(id))).ToList();
        }

        public static string BuildScene(string stage, string p1, string p2)
        {
            ShaderUtil.allowAsyncCompilation = false;
            var ids = Roster(p1, p2);
            // the select screen's cards: rendered in the studio first (a scene of their own)
            Portraits(ids);
            var info = RoeStage.Open(stage);
            Debug.Log("[ROE] " + info.report);
            var sight = new RoeStage.Sight();
            var layout = RoeStage.FindLayout(info, sight, false);
            var (centre, radius) = SurveyArena(stage, layout.centre) ?? BestArena(sight, layout.centre);
            sight.Dispose();
            Debug.Log($"[ROE] fight scene {stage}: arena centre {centre} (fight line centre {layout.centre}), free radius {radius:F1} m, " +
                      $"fighters along {layout.axis}, camera side {layout.normal}");

            var manifest = RoeManifest.Load();
            var roster = ids.Select(id => MakeFighter(manifest.characters.First(c => c.id == id))).ToArray();
            int pick1 = Mathf.Max(0, ids.IndexOf(p1)), pick2 = Mathf.Max(0, ids.IndexOf(p2));
            var rigs = new[] { roster[pick1], roster[pick2] };
            // the ones not in the match wait put away (the select screen brings them out)
            foreach (var r in roster)
                r.gameObject.SetActive(rigs.Contains(r));

            // camera + post-processing
            var camGo = new GameObject("Fight Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = 32f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;       // the refraction effects read the picture behind them
            camGo.transform.position = centre + layout.normal * 6f + Vector3.up * 1.5f;
            camGo.transform.LookAt(centre + Vector3.up, Vector3.up);
            var volGo = new GameObject("Fight Volume");
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RoeProjectSetup.SettingsDir + "/RoeStudioVolume.asset");

            var hudGo = new GameObject("Fight HUD", typeof(RectTransform));
            var hud = hudGo.AddComponent<FightHud>();

            var gameGo = new GameObject("Fight");
            var game = gameGo.AddComponent<FightGame>();
            game.rigs = rigs;
            game.roster = roster;
            game.pick = new[] { pick1, pick2 };
            game.centre = centre;
            game.axis = layout.axis;
            game.arenaRadius = Mathf.Clamp(radius, 3f, 12f);
            game.cam = cam;
            game.hud = hud;
            // the basic moves: every motion pack built so far; the fight starts with -roeMotions (default bandai1)
            if (!File.Exists(RoeMotionPacks.PackPath("bandai1")))
                RoeMotionPacks.BuildBandai();
            game.motionPacks = RoeMotionPacks.All();
            string wanted = RoeCapture.Arg("-roeMotions", "bandai1");
            game.motionPack = Mathf.Max(0, game.motionPacks.FindIndex(p => p.name == wanted));
            foreach (var rig in roster)
                rig.UseMotions(game.Pack);
            game.moves = game.Pack.CopyStrikes();
            game.specials = new List<Special>
            {
                new Special { name = "skill1", clip = "skill_01", action = "skill1", damage = 150, range = 6f },
                new Special { name = "skill2", clip = "skill_02", action = "skill2", damage = 190, range = 6f },
                new Special { name = "skill3", clip = "skill_03", action = "skill3", damage = 360, meterCost = 100f, range = 7f },
            };
            (game.effects, game.hitEffect) = Effects(roster);
            foreach (var m in game.moves)
                m.hitSound = rigs[0].sounds.Any(s => s.name == "hit") ? "hit" : null;
            // the stage's own battle music
            var musicPath = Directory.Exists($"Assets/ROE/stages/{stage}")
                ? Directory.GetFiles($"Assets/ROE/stages/{stage}", "*.ogg", SearchOption.AllDirectories)
                    .Select(p => p.Replace('\\', '/')).OrderBy(p => Path.GetFileName(p).ToLowerInvariant().Contains("battle") ? 0 : 1).FirstOrDefault()
                : null;
            game.music = musicPath != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath) : null;

            Directory.CreateDirectory(SceneDir);
            var scene = SceneManager_Active();
            string path = ScenePath(stage);
            EditorSceneManager.SaveScene(scene, path, false);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            Debug.Log($"[ROE] fight scene saved: {path} (roster {string.Join(" ", ids)}, picked {p1} {p2}; {game.moves.Count} strikes, {game.specials.Count} specials, {game.effects.Count} effect prefabs, hit spark {game.hitEffect}, music {(musicPath ?? "none")}; " +
                      $"motion packs {string.Join(", ", game.motionPacks.Select(p => p.name))}, starting with {game.Pack.name})");
            return path;
        }

        static UnityEngine.SceneManagement.Scene SceneManager_Active() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        /// <summary>
        /// A picture per fighter for the select screen's card: upper body and face, standing, in the studio
        /// (Generated/&lt;id&gt;/&lt;id&gt;_portrait.png).  Opens a scene of its own: call it before the stage.
        /// </summary>
        public static void Portraits(IEnumerable<string> ids)
        {
            ShaderUtil.allowAsyncCompilation = false;
            var studio = RoeStudio.Build();
            foreach (var id in ids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id));
                if (prefab == null)
                    continue;
                var stance = RoeHumanoidClips.Standing(id, out _);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (stance != null)
                    RoeCapture.Pose(go, stance, 0f);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
                var forward = RoeShowcase.Forward(go);
                var head = RoeShowcase.FindBone(go, "Bip001 Head");
                var chest = RoeShowcase.FindBone(go, "Bip001 Spine2") ?? RoeShowcase.FindBone(go, "Bip001 Spine1");
                var target = head != null && chest != null ? Vector3.Lerp(chest.position, head.position, 0.75f) : RoeShowcase.WorldBounds(go).center;
                studio.LightFrom(forward);
                // no depth of field: the studio's volume profile is the fight scene's too (a focus left in it blurred the match)
                studio.Aim(target, forward, 14f, 3f, 1.45f, 24f);
                string file = Path.Combine(ProjectDir, PortraitPath(id));
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                RoeCapture.Render(studio.camera, 120, 160, file);
                RoeCapture.Render(studio.camera, 120, 160, file);
                RoeCapture.Render(studio.camera, 360, 480, file);
                RoeCapture.EndPosing();
                Object.DestroyImmediate(go);
                AssetDatabase.ImportAsset(PortraitPath(id), ImportAssetOptions.ForceUpdate);
            }
        }

        [Serializable]
        class SurveyEntry
        {
            public string scene;
            public float[] centre;
            public float r_best;
            public float[] best_offset;
        }

        /// <summary>
        /// The arena from tools/stage_survey.py (every stage mesh's triangles in a top-down grid: the
        /// most reliable measure, it also sees thin fences).  Null if the stage was not surveyed.
        /// </summary>
        static (Vector3, float)? SurveyArena(string stage, Vector3 fightCentre)
        {
            string dir = Path.Combine(ProjectDir, "_work", "stage_survey");
            string names = Path.Combine(dir, "stages.json"), survey = Path.Combine(dir, "survey.json");
            if (!File.Exists(names) || !File.Exists(survey))
                return null;
            var scene = MiniJson(File.ReadAllText(names), stage);
            if (scene == null)
                return null;
            string text = File.ReadAllText(survey);
            int at = text.IndexOf($"\"{scene}\": {{", StringComparison.Ordinal);
            if (at < 0)
                return null;
            int end = text.IndexOf('}', at);
            var entry = JsonUtility.FromJson<SurveyEntry>(text.Substring(text.IndexOf('{', at), end - text.IndexOf('{', at) + 1));
            var c = new Vector3(entry.centre[0] + entry.best_offset[0], fightCentre.y, entry.centre[2] + entry.best_offset[1]);
            return (c, entry.r_best);
        }

        static string MiniJson(string text, string key)
        {
            int at = text.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (at < 0)
                return null;
            int q1 = text.IndexOf('"', text.IndexOf(':', at) + 1);
            int q2 = text.IndexOf('"', q1 + 1);
            return text.Substring(q1 + 1, q2 - q1 - 1);
        }

        /// <summary>The largest circle of free, flat floor near the fight centre (ray tests on the stage meshes).</summary>
        static (Vector3 centre, float radius) BestArena(RoeStage.Sight sight, Vector3 start)
        {
            float Radius(Vector3 c, int directions, float max)
            {
                var radii = new List<float>();
                for (int k = 0; k < directions; k++)
                {
                    var dir = Quaternion.Euler(0f, k * 360f / directions, 0f) * Vector3.forward;
                    float free = 0f;
                    for (float d = 0.25f; d <= max; d += 0.25f)
                    {
                        var p = c + dir * d;
                        if (!sight.Ground(p, 2.2f, out float y) || Mathf.Abs(y - c.y) > 0.2f)
                            break;
                        // fences and railings: thin bars the downward rays slip past
                        if (sight.Blocked(c + Vector3.up * 1.0f, p + Vector3.up * 1.0f) || sight.Blocked(c + Vector3.up * 0.5f, p + Vector3.up * 0.5f))
                            break;
                        free = d;
                    }
                    radii.Add(free);
                }
                radii.Sort();
                return radii[Mathf.Min(2, radii.Count - 1)];    // two thin posts do not count
            }
            var best = (centre: start, radius: Radius(start, 24, 12f));
            for (float x = -3f; x <= 3.01f; x += 0.5f)
                for (float z = -3f; z <= 3.01f; z += 0.5f)
                {
                    var c = start + new Vector3(x, 0f, z);
                    if (!sight.Ground(c, 2.2f, out float y) || Mathf.Abs(y - start.y) > 0.2f)
                        continue;
                    c.y = y;
                    float r = Radius(c, 16, 12f);
                    if (r > best.radius + 0.25f)
                        best = (c, Radius(c, 24, 12f));
                }
            return best;
        }

        /// <summary>One fighter: root with the rig component, the game's unit prefab (skills, effects), our humanoid model.</summary>
        static FighterRig MakeFighter(RoeManifest.Character c)
        {
            var sheet = RoeSkillSheet.FromJson(File.ReadAllText(RoeSkillSheet.PathOf(c.id)));
            var root = new GameObject($"Fighter {c.id}");
            var rig = root.AddComponent<FighterRig>();
            rig.id = c.id;
            rig.displayName = DisplayNames.TryGetValue(c.id, out var shown) ? shown : c.id.ToUpperInvariant();
            rig.outfit = Outfits.TryGetValue(c.id, out var outfit) ? outfit : "";
            rig.portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitPath(c.id));

            // the game's unit: timelines + effect anchors on a hidden low-detail body
            var unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeUnit.RigPath(sheet));
            var unit = (GameObject)PrefabUtility.InstantiatePrefab(unitPrefab);
            PrefabUtility.UnpackPrefabInstance(unit, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            unit.name = sheet.unit;
            unit.transform.SetParent(root.transform, false);
            unit.transform.localPosition = Vector3.zero;
            unit.transform.localRotation = Quaternion.identity;
            int stripped = 0;
            foreach (var t in unit.GetComponentsInChildren<Transform>(true))
                stripped += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var s in unit.GetComponentsInChildren<AudioSource>(true))
                s.enabled = false;
            foreach (var l in unit.GetComponentsInChildren<AudioListener>(true))
                Object.DestroyImmediate(l);
            foreach (var cam in unit.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            var ghost = unit.transform.Find(sheet.model);
            string[] characterShaders = { "ROE/Character", "ROE/Skin", "ROE/Hair", "ROE/Eye", "ROE/Eyebrow" };
            int hidden = 0;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterials.Any(m => m != null && m.shader != null && characterShaders.Contains(m.shader.name)))
                {
                    r.enabled = false;
                    hidden++;
                }
            foreach (var a in unit.GetComponentsInChildren<Animator>(true))
            {
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.applyRootMotion = false;
            }
            foreach (var d in unit.GetComponentsInChildren<PlayableDirector>(true))
            {
                d.playOnAwake = false;
                d.timeUpdateMode = DirectorUpdateMode.Manual;
                rig.directors.Add(new FighterRig.NamedDirector { action = d.gameObject.name.ToLowerInvariant(), director = d });
            }
            rig.unitRoot = unit.transform;

            // our model where the game's body stands
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(c.id)));
            model.transform.SetParent(ghost.parent, false);
            model.transform.localPosition = ghost.localPosition;
            model.transform.localRotation = ghost.localRotation;
            model.transform.localScale = ghost.localScale;
            rig.animator = model.GetComponent<Animator>();
            rig.animator.runtimeAnimatorController = null;
            // the outfit's pieces that can come off (爆衣): each its own renderer (Editor/Burst/<id>.json)
            rig.burst = RoeBurstBuilder.Apply(model, c.id);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.forceMatrixRecalculationPerRender = false;

            // clips: the game's own here; the basic moves come from a motion pack (BuildScene, FightGame.Setup).  The
            // stance is the battle stance, or a showcase clip that stands when that one floats (g05 sits in the air)
            rig.stance = RoeHumanoidClips.Standing(c.id, out var standing);
            foreach (var name in GameClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RoeHumanoidClips.ClipPath(c.id, name));
                if (clip != null)
                    rig.clips.Add(new FighterRig.NamedClip { name = name, clip = clip, loop = Loops.Contains(name), game = true });
            }
            rig.skillSheetJson = AssetDatabase.LoadAssetAtPath<TextAsset>(RoeSkillSheet.PathOf(c.id));

            // weapons only show during the game's own clips (skills, intro, victory): in the basic moves a08's
            // sword would swing around the hips, and g04 throws her punches and kicks without the giant fan
            // (user 10-02: "小招的时候就不显示扇子了，大招再用扇子")
            rig.weaponRenderers = model.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterials.Any(m => m != null && m.name.StartsWith("wp_"))).ToArray();
            // her own strikes instead of the motion pack's (user 10-03: g04 strikes like Mai Shiranui, from DOA6;
            // -roeOwnStrikes g04=doa6_mai,a08=...), and the size of her weapon (user 10-03: a smaller fan; -roeWeaponScale g04=0.7)
            if (Table("-roeOwnStrikes", "g04=doa6_mai,g05=doa6_mai").TryGetValue(c.id, out var own))
                rig.strikePack = OwnStrikes(own);
            if (Table("-roeWeaponScale", "g04=0.7").TryGetValue(c.id, out var size))
                rig.weaponScale = float.Parse(size, CultureInfo.InvariantCulture);
            // barefoot (-roeBarefoot g05=1): flat feet under the motion capture, the soles from the bind pose
            string feetNote = "";
            if (Table("-roeBarefoot", "g05=1").TryGetValue(c.id, out var bare) && bare != "0")
            {
                rig.barefoot = true;
                var an = model.GetComponent<Animator>();
                var toes = new[] { an.GetBoneTransform(HumanBodyBones.LeftToes), an.GetBoneTransform(HumanBodyBones.RightToes) };
                var feet = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
                if (toes.All(t => t != null) && feet.All(t => t != null))
                {
                    // the prefab's human bones stand in the bind pose, its feet on the floor (the model's origin)
                    float floor = model.transform.position.y;
                    rig.barefootSoles = new Vector2(toes.Average(t => t.position.y) - floor, feet.Average(t => t.position.y) - floor);
                    float pitch = feet.Average(f => Vector3.Angle(toes[System.Array.IndexOf(feet, f)].position - f.position,
                        Vector3.ProjectOnPlane(toes[System.Array.IndexOf(feet, f)].position - f.position, Vector3.up)));
                    feetNote = $", barefoot (soles from the bind pose: toe {rig.barefootSoles.x:F3} m, ankle {rig.barefootSoles.y:F3} m, foot pitch {pitch:F0} deg)";
                }
            }

            // sounds the skills use, by clip name; the game's voice lines in Japanese
            var names = new HashSet<string>();
            foreach (var a in sheet.actions)
            {
                foreach (var s in a.sounds)
                    names.Add(s.clip);
                foreach (var v in a.voices)
                    foreach (var vc in v.clips.Where(x => x.language == "2" || x.language == "1"))
                        names.Add(vc.clip);
            }
            foreach (var name in names.Where(n => !string.IsNullOrEmpty(n)))
            {
                var path = FindAudio(c, name);
                if (path != null)
                    rig.sounds.Add(new FighterRig.NamedSound { name = name, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
            }
            // strikes sound like the shortest of the skills' hit sounds (the first one found was a heavy one)
            var hit = c.sfx.Select(p => (p, n: Path.GetFileNameWithoutExtension(p).ToLowerInvariant()))
                .Where(x => x.n.Contains("hit") || x.n.Contains("hurt"))
                .OrderBy(x => AssetDatabase.LoadAssetAtPath<AudioClip>(x.p) is AudioClip ac ? ac.length : 99f)
                .FirstOrDefault();
            if (hit.p != null)
                rig.sounds.Add(new FighterRig.NamedSound { name = "hit", clip = AssetDatabase.LoadAssetAtPath<AudioClip>(hit.p) });
            rig.audioSource = root.AddComponent<AudioSource>();
            rig.audioSource.playOnAwake = false;
            rig.audioSource.spatialBlend = 0f;

            Debug.Log($"[ROE] fighter {c.id}: {standing}; unit {sheet.unit} ({stripped} game scripts stripped, {hidden} ghost renderers hidden, " +
                      $"{rig.directors.Count} timelines: {string.Join(" ", rig.directors.Select(d => d.action))}), " +
                      $"{rig.clips.Count} clips, {rig.weaponRenderers.Length} weapon renderers, {rig.sounds.Count} sounds, " +
                      $"clothes burst {(rig.burst != null ? rig.burst.Report() : "none")}, " +
                      $"own strikes {(rig.strikePack != null ? $"{rig.strikePack.name} ({string.Join(" ", rig.strikePack.strikes.Select(m => $"{m.button} {m.name}"))})" : "none")}, " +
                      $"weapon size {rig.weaponScale:F2}{feetNote} " +
                      $"(hit sound {(hit.p != null ? Path.GetFileName(hit.p) : "none")}; sfx: {string.Join(" ", c.sfx.Take(12).Select(Path.GetFileNameWithoutExtension))})");
            return rig;
        }

        /// <summary>"g04=doa6_mai,a08=x" (the argument, else the fallback) as a table.</summary>
        static Dictionary<string, string> Table(string arg, string fallback) =>
            RoeCapture.Arg(arg, fallback).Split(',').Select(p => p.Split('='))
                .Where(p => p.Length == 2 && p[0].Trim().Length > 0 && p[1].Trim().Length > 0)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        /// <summary>A strike pack by name, imported from tools/motionpacks/&lt;name&gt;.json if it was not built yet.</summary>
        static MotionPack OwnStrikes(string name)
        {
            var pack = RoeMotionPacks.Load(name);
            string spec = Path.Combine(ProjectDir, "tools", "motionpacks", name + ".json");
            if (pack == null && File.Exists(spec))
                pack = RoeMotionPacks.ImportSpec(JsonUtility.FromJson<RoeMotionPacks.Spec>(File.ReadAllText(spec)));
            if (pack == null)
                Debug.LogWarning($"[ROE] own strikes '{name}': no pack and no tools/motionpacks/{name}.json");
            return pack;
        }

        static string FindAudio(RoeManifest.Character c, string clipName)
        {
            var path = c.sfx.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == clipName);
            if (path != null)
                return path;
            foreach (var guid in AssetDatabase.FindAssets($"{clipName} t:AudioClip", new[] { "Assets/ROE" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == clipName)
                    return p;
            }
            return null;
        }

        /// <summary>Every effect the skills spawn on a target, and a small one for the strikes.</summary>
        static (List<FightGame.NamedPrefab> list, string spark) Effects(FighterRig[] rigs)
        {
            var list = new List<FightGame.NamedPrefab>();
            foreach (var rig in rigs)
            {
                var sheet = RoeSkillSheet.FromJson(rig.skillSheetJson.text);
                foreach (var a in sheet.actions)
                    foreach (var s in a.spawns)
                    {
                        if (list.Any(x => x.name == s.prefab))
                            continue;
                        foreach (var guid in AssetDatabase.FindAssets($"{s.prefab} t:Prefab", new[] { "Assets/ROE/skills" }))
                        {
                            var p = AssetDatabase.GUIDToAssetPath(guid);
                            if (Path.GetFileNameWithoutExtension(p) == s.prefab)
                            {
                                list.Add(new FightGame.NamedPrefab { name = s.prefab, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p) });
                                break;
                            }
                        }
                    }
            }
            // the smallest hit effect of skill 1 serves as the spark for strikes
            var spark = list.Where(x => x.name.ToLowerInvariant().Contains("hit"))
                .OrderBy(x => x.prefab.GetComponentsInChildren<ParticleSystem>(true).Length).FirstOrDefault();
            return (list, spark?.name);
        }

        // ---- filming a CPU match

        [MenuItem("ROE Fighter/Fight/Record CPU match")]
        public static void Record()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(ProjectDir, "_work", "fight"));
            float seconds = float.Parse(RoeCapture.Arg("-roeSeconds", "100"), CultureInfo.InvariantCulture);
            var size = RoeCapture.Arg("-roeSize", "1280x720").Split('x');
            int width = int.Parse(size[0]), height = int.Parse(size[1]);
            int seed = int.Parse(RoeCapture.Arg("-roeSeed", "1"));
            int every = int.Parse(RoeCapture.Arg("-roeEvery", "2"));      // 60 steps per second, every 2nd filmed = 30 fps
            RoeClothesBurst.Enabled = RoeCapture.Arg("-roeBurst", "1") != "0";

            ShaderUtil.allowAsyncCompilation = false;
            string path = ScenePath(stage);
            if (!File.Exists(path))
                BuildScene(stage, RoeCapture.Arg("-roeP1", "a08"), RoeCapture.Arg("-roeP2", "g04"));
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.PickIds(RoeCapture.Arg("-roeP1", null), RoeCapture.Arg("-roeP2", null));
            game.cpu = new[] { true, true };
            game.seed = seed;
            game.hud.drawnByCamera = true;
            foreach (var rig in game.roster.Length > 0 ? game.roster : game.rigs)
                foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;     // many renders inside one editor update
            string pack = RoeCapture.Arg("-roeMotions", null);
            if (pack != null)
                game.motionPack = Mathf.Max(0, game.motionPacks.FindIndex(p => p.name == pack));
            game.Setup();

            string frameDir = Path.Combine(outDir, "frames");
            if (Directory.Exists(frameDir))
                Directory.Delete(frameDir, true);
            Directory.CreateDirectory(frameDir);
            string warm = Path.Combine(outDir, "_warmup.jpg");
            int steps = Mathf.RoundToInt(seconds * 60f), shot = 0, endAfter = -1;
            var states = new StringBuilder();
            for (int s = 0; s < steps; s++)
            {
                game.Step(null);
                game.UpdateCamera(FightGame.Dt);
                if (s % every != 0)
                    continue;
                game.hud.Refresh(game);
                Canvas.ForceUpdateCanvases();
                if (shot == 0)
                {
                    RoeCapture.Render(game.cam, 320, 180, warm, 80);
                    RoeCapture.Render(game.cam, 320, 180, warm, 80);
                }
                RoeCapture.Render(game.cam, width, height, Path.Combine(frameDir, $"{shot:D5}.jpg"), 92);
                shot++;
                if (s % 60 == 0)
                    states.Append($"\n[ROE]   t={s / 60f,5:F1}s {game.phase} {game.message} | " +
                                  string.Join(" | ", game.f.Select(x => $"{x.rig.id} {x.state} hp {x.hp} meter {x.meter:F0} at {x.pos.x:F1},{x.pos.z:F1}")));
                if (game.phase == FightGame.Phase.MatchOver && endAfter < 0)
                    endAfter = s + 240;
                if (endAfter >= 0 && s >= endAfter)
                    break;
            }
            // sounds for make_video.py: frames of the 30 fps video
            var json = new StringBuilder();
            var lines = new List<string>();
            foreach (var e in game.soundLog)
            {
                var rig = game.rigs.FirstOrDefault(r => r.id == e.fighter);
                var clip = rig?.sounds.FirstOrDefault(x => x.name == e.name)?.clip;
                if (clip == null)
                    continue;
                lines.Add($"{{\"path\":\"{AssetDatabase.GetAssetPath(clip)}\",\"frame\":{e.frame / every},\"volume\":{e.volume.ToString("F2", CultureInfo.InvariantCulture)}}}");
            }
            string music = game.music != null
                ? $",\"music\":{{\"path\":\"{AssetDatabase.GetAssetPath(game.music)}\",\"volume\":{game.musicVolume.ToString("F2", CultureInfo.InvariantCulture)}}}"
                : "";
            json.Append($"{{\"fps\":{60 / every},\"frames\":{shot}{music},\"sounds\":[\n  {string.Join(",\n  ", lines)}\n]}}\n");
            File.WriteAllText(Path.Combine(outDir, "timeline.json"), json.ToString());
            Debug.Log($"[ROE] fight recorded: {shot} frames ({shot * every / 60f:F1} s), {lines.Count} sounds, rounds won {game.wins[0]}-{game.wins[1]}, " +
                      $"to {outDir}{states}");
        }

        // ---- filming the select screen

        /// <summary>
        /// The select screen filmed (batch mode has no keys: the cards are moved by script the way the keys move
        /// them): 1P goes from INASE over LUF to KART and confirms, picks GODDESS LUF for the computer, confirms, and
        /// the match begins.  Frames and timeline.json (music) for tools/make_video.py.
        ///   -executeMethod RoeFighter.EditorTools.RoeFightScene.SelectDemo [-roeOut dir] [-roeSize 1280x720]
        /// </summary>
        [MenuItem("ROE Fighter/Fight/Film the select screen")]
        public static void SelectDemo()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(ProjectDir, "_work", "select_demo"));
            var size = RoeCapture.Arg("-roeSize", "1280x720").Split('x');
            int width = int.Parse(size[0]), height = int.Parse(size[1]);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.hud.drawnByCamera = true;
            game.hud.showHelp = true;
            void Recalc()
            {
                foreach (var rig in game.roster.Concat(game.rigs))
                    foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        smr.forceMatrixRecalculationPerRender = true;
            }
            Recalc();
            game.cpu = new[] { false, true };
            game.PickIds("a08", "g04");
            game.EnterSelect();
            string frameDir = Path.Combine(outDir, "frames");
            if (Directory.Exists(frameDir))
                Directory.Delete(frameDir, true);
            Directory.CreateDirectory(frameDir);
            int shot = 0;
            var log = new StringBuilder("[ROE] select demo:");
            void Film(float seconds, string what)
            {
                log.Append($"\n[ROE]   {shot / 30f,5:F1}s {what}");
                int steps = Mathf.RoundToInt(seconds * 60f);
                for (int s = 0; s < steps; s++)
                {
                    game.Step(null);
                    game.UpdateCamera(FightGame.Dt);
                    if (s % 2 != 0)
                        continue;
                    game.hud.Refresh(game);
                    Canvas.ForceUpdateCanvases();
                    if (shot == 0)
                    {
                        RoeCapture.Render(game.cam, 320, 180, Path.Combine(outDir, "_warm.jpg"), 80);
                        RoeCapture.Render(game.cam, 320, 180, Path.Combine(outDir, "_warm.jpg"), 80);
                    }
                    RoeCapture.Render(game.cam, width, height, Path.Combine(frameDir, $"{shot:D5}.jpg"), 92);
                    shot++;
                }
            }
            void Move(int side, string id)
            {
                var before = new[] { game.pick[0], game.pick[1] };
                game.pick[side] = Array.FindIndex(game.roster, r => r.id == id);
                game.PicksChanged(before);
                Recalc();
            }
            Film(1.6f, "the select screen: 1P on INASE, the computer on LUF");
            Move(0, "g04");
            Film(0.9f, "1P moves to LUF (both on the same card: a mirror match would get a copy)");
            Move(0, "b10");
            Film(1.2f, "1P moves to KART");
            game.picked[0] = true;
            game.choosing = 1;
            Film(1.0f, "1P confirms; now the computer's fighter");
            Move(1, "a08");
            Film(0.8f, "the computer's card to INASE");
            Move(1, "g05");
            Film(1.2f, "and to GODDESS LUF");
            game.picked[1] = true;
            Film(0.8f, "confirmed: READY");
            game.BeginMatch();
            Film(3.2f, "the match begins");
            string music = game.music != null
                ? $",\"music\":{{\"path\":\"{AssetDatabase.GetAssetPath(game.music)}\",\"volume\":{game.musicVolume.ToString("F2", CultureInfo.InvariantCulture)}}}"
                : "";
            File.WriteAllText(Path.Combine(outDir, "timeline.json"), $"{{\"fps\":30,\"frames\":{shot}{music},\"sounds\":[]}}\n");
            Debug.Log(log.Append($"\n[ROE]   {shot} frames to {frameDir}").ToString());
        }

        // ---- a Windows build to play

        [MenuItem("ROE Fighter/Fight/Build Windows player")]
        public static void BuildPlayer()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(ProjectDir, "out", "ROEFighter"));
            string path = ScenePath(stage);
            if (!File.Exists(path))
                BuildScene(stage, "a08", "g04");
            PlayerSettings.productName = "ROE Fighter";
            PlayerSettings.companyName = "ROE Fighter";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { path },
                locationPathName = Path.Combine(outDir, "ROEFighter.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[ROE] player build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, " +
                      $"{report.summary.totalTime.TotalMinutes:F1} min, errors {report.summary.totalErrors}, to {options.locationPathName}");
        }
    }
}
