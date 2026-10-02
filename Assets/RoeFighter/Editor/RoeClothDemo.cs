using System.Globalization;
using System.IO;
using System.Linq;
using RoeFighter.Fight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// The bone cloth, shown: each fighter plays the same scripted moves twice in the real fight
    /// logic (guard, walk on and back, side steps both ways, jab, straight, kick, slash), once
    /// without and once with RoeBoneCloth, filmed by a camera that follows her from the front.
    /// Frames go to _work/cloth_demo/&lt;id&gt;_off and _on; tools/cloth_demo_video.py puts them side by side.
    ///   -executeMethod RoeFighter.EditorTools.RoeClothDemo.Run [-roeChars g04,a08] [-roeSize 960x720]
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
            (7.0f, 7.0f, new FighterInput { a = true }, "jab"),
            (7.8f, 7.8f, new FighterInput { c = true }, "straight"),
            (8.8f, 8.8f, new FighterInput { d = true }, "kick"),
            (10.2f, 10.2f, new FighterInput { b = true }, "slash"),
        };
        const float Length = 11.6f;

        [MenuItem("ROE Fighter/Fight/Bone cloth demo")]
        public static void Run()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "cloth_demo"));
            var ids = RoeCapture.Arg("-roeChars", "g04,a08").Split(',');
            var size = RoeCapture.Arg("-roeSize", "960x720").Split('x');
            int width = int.Parse(size[0]), height = int.Parse(size[1]);
            ShaderUtil.allowAsyncCompilation = false;
            var log = new System.Text.StringBuilder("[ROE] bone cloth demo:");
            // the scene is opened once: opened again in the same editor session, URP's post-processing
            // lost its volume components (assertion "SetupColorLut colorAdjustments cannot be null") and
            // every frame came out black
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            foreach (var id in ids)
                foreach (bool on in new[] { false, true })
                {
                    var game = Object.FindFirstObjectByType<FightGame>();
                    game.cpu = new[] { false, false };
                    game.hud.gameObject.SetActive(false);
                    foreach (var rig in game.rigs)
                    {
                        rig.useCloth = on;
                        foreach (var smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            smr.forceMatrixRecalculationPerRender = true;
                    }
                    game.Setup();
                    while (game.phase != FightGame.Phase.Fight)
                        game.Step(new FighterInput[2]);
                    int who = game.f[0].rig.id == id ? 0 : 1;
                    var me = game.f[who];
                    // the other one stands well away
                    me.foe.pos = me.pos + (me.foe.pos - me.pos).normalized * 5.5f;
                    me.foe.Place();
                    if (on)
                        log.Append($"\n[ROE]   {id}: {me.rig.ClothReport}");

                    string dir = Path.Combine(outDir, $"{id}_{(on ? "on" : "off")}");
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
                    log.Append($"\n[ROE]   {id} cloth {(on ? "on" : "off")}: {shot} frames to {dir}");
                }
            File.WriteAllText(Path.Combine(outDir, "script.txt"), string.Join("\n", Script.Select(x =>
                $"{x.from.ToString("F1", CultureInfo.InvariantCulture)} {x.what}")));
            Debug.Log(log.ToString());
        }
    }
}
