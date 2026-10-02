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
    ///   Run    the bone cloth: without and with RoeBoneCloth -> _work/cloth_demo/&lt;id&gt;_off, _on
    ///   Packs  the motion packs: one take per pack       -> _work/pack_demo/&lt;id&gt;_&lt;pack&gt;
    /// tools/cloth_demo_video.py puts the takes side by side.
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Run [-roeChars g04,a08] [-roeSize 960x720]
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Packs [-roePacks bandai1,cmu1] [-roeChars g04,a08]
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
            Film(outDir, new[] { "off", "on" }, (game, variant) =>
            {
                foreach (var rig in game.rigs)
                    rig.useCloth = variant == "on";
            }, (game, variant, me) => variant == "on" ? me.rig.ClothReport : "");
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
                    foreach (var rig in game.rigs)
                        foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            smr.forceMatrixRecalculationPerRender = true;
                    string pack = RoeCapture.Arg("-roeMotions", null);
                    if (pack != null)
                        game.motionPack = Mathf.Max(0, game.motionPacks.FindIndex(p => p.name == pack));
                    apply(game, variant);
                    game.Setup();
                    while (game.phase != FightGame.Phase.Fight)
                        game.Step(new FighterInput[2]);
                    int who = game.f[0].rig.id == id ? 0 : 1;
                    var me = game.f[who];
                    // the other one stands well away
                    me.foe.pos = me.pos + (me.foe.pos - me.pos).normalized * 5.5f;
                    me.foe.Place();
                    string about = describe(game, variant, me);
                    takes.Add($"{id}\t{variant}\t{about}\t" + string.Join("|", game.moves.Select(m => $"{m.button}={m.name}")));
                    if (about.Length > 0)
                        log.Append($"\n[ROE]   {id} {variant}: {about}");

                    string dir = Path.Combine(outDir, $"{id}_{variant}");
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    var cam = game.cam;
                    cam.fieldOfView = 30f;
                    var look = me.pos + Vector3.up * 0.95f;
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
                        // the camera: in front of her, a little to the side, following softly
                        var fwd = me.Forward;
                        var side = Vector3.Cross(Vector3.up, fwd).normalized;
                        look = Vector3.Lerp(look, me.pos + Vector3.up * 0.95f, s == 0 ? 1f : 0.08f);
                        cam.transform.position = look + (fwd * 0.8f - side * 0.6f).normalized * 3.6f + Vector3.up * 0.2f;
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
                }
            File.WriteAllText(Path.Combine(outDir, "script.txt"), string.Join("\n", Script.Select(x =>
                $"{x.from.ToString("F1", CultureInfo.InvariantCulture)} {x.what}")));
            File.WriteAllText(Path.Combine(outDir, "takes.txt"), string.Join("\n", takes));
            Debug.Log(log.ToString());
        }
    }
}
