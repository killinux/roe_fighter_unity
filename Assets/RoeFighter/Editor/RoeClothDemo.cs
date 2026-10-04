using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RoeFighter.Fight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Side-by-side demos: each fighter plays the same scripted moves in the real fight logic
    /// (guard, walk on and back, side steps both ways, then buttons A, C, D, B), once per variant,
    /// filmed by a camera that follows her from the front.
    ///   Run    the cloth backends (RoeClothBackends): one take each -> _work/cloth_demo/&lt;id&gt;_&lt;backend&gt;
    ///   Packs  the motion packs: one take per pack       -> _work/pack_demo/&lt;id&gt;_&lt;pack&gt;
    ///   Rests  the skirt's animation pose (RoeSkirtRig): rest_guard (fitted, rest in the guard), rest_stance
    ///          (fitted, rest in the stance), drape (hangs on the body) -> _work/rest_demo/&lt;id&gt;_&lt;variant&gt;
    /// tools/cloth_demo_video.py puts the takes side by side.  -roeView backright (or backleft) films the
    /// hips up close from behind and to that side instead of the whole fighter from the front; -roeView chest
    /// the chest up close from the front (breasts).
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Run [-roeCloths legacy,magica_style] [-roeChars g04,a08] [-roeSize 960x720] [-roeView front|backright|backleft|chest]
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Packs [-roePacks bandai1,cmu1] [-roeChars g04,a08]
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Rests [-roeView backright] [-roeChars g04,a08] [-roeRests rest_stance,drape]
    /// </summary>
    public static class RoeClothDemo
    {
        // (from, to, input) in seconds after the fight starts; presses last one step
        static readonly (float from, float to, FighterInput input, string what)[] Script =
        {
            (0.0f, 1.0f, default, "guard"),
            (1.0f, 2.6f, new FighterInput { x = 1 }, "walk"),
            (2.6f, 3.8f, new FighterInput { x = -1 }, "back"),
            (3.8f, 5.3f, new FighterInput { y = 1 }, "side step"),
            (5.3f, 6.8f, new FighterInput { y = -1 }, "side step"),
            (7.0f, 7.0f, new FighterInput { a = true }, "A"),
            (7.8f, 7.8f, new FighterInput { c = true }, "C"),
            (8.8f, 8.8f, new FighterInput { d = true }, "D"),
            (10.2f, 10.2f, new FighterInput { b = true }, "B"),
        };
        const float Length = 11.6f;

        [MenuItem("ROE Fighter/Fight/Bone cloth demo")]
        public static void Run()
        {
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "cloth_demo"));
            Film(outDir, RoeCapture.Arg("-roeCloths", "legacy,magica_style").Split(','), (game, variant) =>
            {
                FighterRig.ClothBackend = variant;
                foreach (var rig in game.rigs)
                    rig.useCloth = true;
            }, (game, variant, me) => $"{me.rig.ClothTitle}; {me.rig.ClothReport}");
        }

        [MenuItem("ROE Fighter/Fight/Skirt rest demo")]
        public static void Rests()
        {
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "rest_demo"));
            string backend = RoeCapture.Arg("-roeCloth", "magica_style");
            Film(outDir, RoeCapture.Arg("-roeRests", "rest_stance,drape").Split(','), (game, variant) =>
            {
                RoeSkirtRig.RestOnStance = variant != "rest_guard";
                RoeSkirtRig.Drape = variant == "drape" ? 1f : 0f;
                FighterRig.ClothBackend = backend;
                foreach (var rig in game.rigs)
                    rig.useCloth = true;
            }, (game, variant, me) => $"skirt rest in the {(RoeSkirtRig.RestOnStance ? "game's stance" : "motion-capture guard")}, drape {RoeSkirtRig.Drape}; {me.rig.ClothTitle}");
            RoeSkirtRig.RestOnStance = true;
            RoeSkirtRig.Drape = 1f;
        }

        /// <summary>A fighter's own strikes (FighterRig.strikePack, g04: Mai Shiranui's) against the motion pack's: takes "pack" and "own".</summary>
        [MenuItem("ROE Fighter/Fight/Own strikes demo")]
        public static void OwnStrikes()
        {
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "strike_demo"));
            var own = new Dictionary<string, MotionPack>();
            Film(outDir, new[] { "pack", "own" }, (game, variant) =>
            {
                foreach (var rig in game.roster.Length > 0 ? game.roster : game.rigs)
                {
                    if (rig.strikePack != null)
                        own[rig.id] = rig.strikePack;
                    rig.strikePack = variant == "own" && own.TryGetValue(rig.id, out var p) ? p : null;
                }
            }, (game, variant, me) => me.rig.strikePack != null ? $"own strikes: {me.rig.strikePack.title}" : $"the motion pack's strikes: {game.Pack.title}");
            // as the scene has it again
            foreach (var game in Object.FindObjectsByType<FightGame>(FindObjectsSortMode.None))
                foreach (var rig in game.roster.Length > 0 ? game.roster : game.rigs)
                    if (own.TryGetValue(rig.id, out var p))
                        rig.strikePack = p;
        }

        [MenuItem("ROE Fighter/Fight/Motion pack demo")]
        public static void Packs()
        {
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "pack_demo"));
            var packs = RoeCapture.Arg("-roePacks", null);
            Film(outDir, packs?.Split(','), (game, variant) =>
            {
                int i = game.motionPacks.FindIndex(p => p.name == variant);
                if (i < 0)
                    throw new Exception($"no motion pack {variant} in the scene (has {string.Join(", ", game.motionPacks.Select(p => p.name))})");
                game.motionPack = i;
            }, (game, variant, me) => $"{game.Pack.title}; " + string.Join(", ", game.moves.Select(m => $"{m.button} {m.name}")));
        }

        /// <summary>
        /// Films every fighter once per variant.  Each take writes its frames to &lt;out&gt;/&lt;id&gt;_&lt;variant&gt;
        /// and a line to takes.txt (id, variant, what the variant is, the moves on A-D).
        /// </summary>
        static void Film(string outDir, string[] variants, Action<FightGame, string> apply, Func<FightGame, string, Fighter, string> describe)
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            var ids = RoeCapture.Arg("-roeChars", "g04,a08").Split(',');
            var size = RoeCapture.Arg("-roeSize", "960x720").Split('x');
            string view = RoeCapture.Arg("-roeView", "front");
            int width = int.Parse(size[0]), height = int.Parse(size[1]);
            ShaderUtil.allowAsyncCompilation = false;
            var log = new System.Text.StringBuilder("[ROE] demo:");
            var takes = new List<string>();
            // the scene is opened once: opened again in the same editor session, URP's post-processing
            // lost its volume components (assertion "SetupColorLut colorAdjustments cannot be null") and
            // every frame came out black
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            variants ??= Object.FindFirstObjectByType<FightGame>().motionPacks.Select(p => p.name).ToArray();
            Directory.CreateDirectory(outDir);
            foreach (var id in ids)
                foreach (var variant in variants)
                {
                    var game = Object.FindFirstObjectByType<FightGame>();
                    game.cpu = new[] { false, false };
                    game.hud.gameObject.SetActive(false);
                    string pack = RoeCapture.Arg("-roeMotions", null);
                    if (pack != null)
                        game.motionPack = Mathf.Max(0, game.motionPacks.FindIndex(p => p.name == pack));
                    // she fights the scene's other pick (or takes 2P's place when she is not picked)
                    if (game.roster.Length > 0 && game.roster[game.pick[0]].id != id && game.roster[game.pick[1]].id != id)
                        game.PickIds(null, id);
                    apply(game, variant);
                    game.Setup();
                    // skinned anew for every render (no player loop here): set on the two Setup picked - set before it,
                    // a fighter picked only now (the first take of one the scene did not pick) kept the skinning of
                    // her first frame, the body left behind while the unskinned cloth surfaces moved on
                    foreach (var rig in game.rigs)
                        foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            smr.forceMatrixRecalculationPerRender = true;
                    while (game.phase != FightGame.Phase.Fight)
                        game.Step(new FighterInput[2]);
                    int who = game.f[0].rig.id == id ? 0 : 1;
                    var me = game.f[who];
                    // the other one stands well away
                    me.foe.pos = me.pos + (me.foe.pos - me.pos).normalized * 5.5f;
                    me.foe.Place();
                    string about = describe(game, variant, me);
                    // the strikes she uses: her own (strikePack) or the motion pack's
                    takes.Add($"{id}\t{variant}\t{about}\t" + string.Join("|", (me.moves ?? game.moves).Select(m => $"{m.button}={m.name}")));
                    if (about.Length > 0)
                        log.Append($"\n[ROE]   {id} {variant}: {about}");

                    string dir = Path.Combine(outDir, $"{id}_{variant}");
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    var cam = game.cam;
                    bool close = view.StartsWith("back"), chest = view == "chest";
                    float sideways = view == "backleft" ? -1f : 1f;
                    var hips = me.rig.animator.GetBoneTransform(HumanBodyBones.Hips);
                    var neck = me.rig.animator.GetBoneTransform(HumanBodyBones.Neck) ?? me.rig.animator.GetBoneTransform(HumanBodyBones.Head);
                    var shoulders = new[] { me.rig.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), me.rig.animator.GetBoneTransform(HumanBodyBones.RightUpperArm) };
                    // the way her chest faces (a side-on stance turns it away from her fighting direction)
                    Vector3 ChestAhead()
                    {
                        var across = shoulders[1].position - shoulders[0].position;
                        across.y = 0f;
                        return across.sqrMagnitude > 1e-6f ? Vector3.Cross(across.normalized, Vector3.up) : me.Forward;
                    }
                    Vector3 Aim() => close ? new Vector3(me.pos.x, hips.position.y - 0.12f, me.pos.z)
                                   : chest ? neck.position - Vector3.up * 0.18f
                                   : me.pos + Vector3.up * 0.95f;
                    cam.fieldOfView = close || chest ? 28f : 30f;
                    var look = Aim();
                    var chestAhead = chest ? ChestAhead() : Vector3.zero;
                    int steps = Mathf.RoundToInt(Length * 60f), shot = 0;
                    for (int s = 0; s < steps; s++)
                    {
                        float t = s / 60f;
                        var input = default(FighterInput);
                        foreach (var (from, to, inp, _) in Script)
                            if (from == to ? Mathf.Abs(t - from) < 0.5f / 60f : t >= from && t < to)
                                input = inp;
                        // left / right on the screen: towards the opponent
                        var camRight = cam.transform.right;
                        camRight.y = 0f;
                        input.x *= Vector3.Dot(me.foe.pos - me.pos, camRight) >= 0f ? 1 : -1;
                        var inputs = new FighterInput[2];
                        inputs[who] = input;
                        game.Step(inputs);
                        // the camera: in front of her, a little to the side, following softly (or behind her
                        // and to one side, at the hips)
                        if (chest)
                            chestAhead = Vector3.Slerp(chestAhead, ChestAhead(), 0.1f).normalized;
                        var fwd = chest ? chestAhead : me.Forward;
                        var side = Vector3.Cross(Vector3.up, fwd).normalized;
                        look = Vector3.Lerp(look, Aim(), s == 0 ? 1f : chest ? 0.25f : 0.08f);
                        cam.transform.position = close
                            ? look + (-fwd * 0.8f + side * (0.6f * sideways)).normalized * 1.9f + Vector3.up * 0.1f
                            : chest ? look + (fwd * 0.9f - side * 0.35f).normalized * 1.15f + Vector3.up * 0.05f
                            : look + (fwd * 0.8f - side * 0.6f).normalized * 3.6f + Vector3.up * 0.2f;
                        cam.transform.LookAt(look, Vector3.up);
                        if (s % 2 != 0)
                            continue;
                        if (shot == 0)
                        {
                            RoeCapture.Render(cam, 240, 180, Path.Combine(outDir, "_warm.jpg"), 80);
                            RoeCapture.Render(cam, 240, 180, Path.Combine(outDir, "_warm.jpg"), 80);
                        }
                        RoeCapture.Render(cam, width, height, Path.Combine(dir, $"{shot:D5}.jpg"), 90);
                        shot++;
                    }
                    log.Append($"\n[ROE]   {id} {variant}: {shot} frames to {dir}");
                    // what the take changed (a solver that reports how far it moved)
                    string after = describe(game, variant, me);
                    if (after != about)
                        log.Append($"\n[ROE]   {id} {variant} after the take: {after}");
                }
            File.WriteAllText(Path.Combine(outDir, "script.txt"), string.Join("\n", Script.Select(x =>
                $"{x.from.ToString("F1", CultureInfo.InvariantCulture)} {x.what}")));
            File.WriteAllText(Path.Combine(outDir, "takes.txt"), string.Join("\n", takes));
            Debug.Log(log.ToString());
        }
    }
}
