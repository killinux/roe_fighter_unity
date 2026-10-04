using System.Linq;
using RoeFighter.Fight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// A DOA6 character's soft bodies in the fight, by numbers (no pictures): she stands, walks, side-steps and strikes
    /// as in RoeClothDemo with the "doa6" physics setup, and at the given steps the log lists, per soft body, the nodes
    /// furthest off their animated targets (offset in her own frame, the target's bones, colliders the target is in,
    /// how far the node's lattice springs are stretched), and at the end the solver's report.
    ///   -executeMethod RoeFighter.EditorTools.DoaPhysicsProbe.Soft [-roeChar kas] [-roeCloth doa6] [-roeSteps 30,120,600] [-roeTop 6] [-roeSoftColliders 0]
    /// </summary>
    public static class DoaPhysicsProbe
    {
        public static void Soft()
        {
            string stage = RoeCapture.Arg("-roeStage", "e23_steel_s02");
            string id = RoeCapture.Arg("-roeChar", "kas");
            FighterRig.ClothBackend = RoeCapture.Arg("-roeCloth", "doa6");
            var at = RoeCapture.Arg("-roeSteps", "30,120,600").Split(',').Select(int.Parse).ToHashSet();
            int top = int.Parse(RoeCapture.Arg("-roeTop", "6"));
            RoeDoaPhysics.SoftColliders = RoeCapture.Arg("-roeSoftColliders", "1") != "0";
            EditorSceneManager.OpenScene(RoeFightScene.ScenePath(stage), OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<FightGame>();
            game.cpu = new[] { false, false };
            if (game.roster.Length > 0 && game.roster[game.pick[0]].id != id && game.roster[game.pick[1]].id != id)
                game.PickIds(null, id);
            game.Setup();
            while (game.phase != FightGame.Phase.Fight)
                game.Step(new FighterInput[2]);
            int who = game.f[0].rig.id == id ? 0 : 1;
            var me = game.f[who];
            me.foe.pos = me.pos + (me.foe.pos - me.pos).normalized * 5.5f;
            me.foe.Place();
            var doa = me.rig.ClothPart<RoeDoaPhysics>();
            var sb = new System.Text.StringBuilder($"[ROE] {id} soft bodies ({FighterRig.ClothBackend}):");
            if (doa == null)
            {
                Debug.Log(sb.Append(" no DOA6 physics on her").ToString());
                return;
            }
            // RoeClothDemo's script: guard, walk on and back, side steps, then A C D B
            var script = new (float from, float to, FighterInput input)[]
            {
                (0.0f, 1.0f, default), (1.0f, 2.6f, new FighterInput { x = 1 }), (2.6f, 3.8f, new FighterInput { x = -1 }),
                (3.8f, 5.3f, new FighterInput { y = 1 }), (5.3f, 6.8f, new FighterInput { y = -1 }),
                (7.0f, 7.0f, new FighterInput { a = true }), (7.8f, 7.8f, new FighterInput { c = true }),
                (8.8f, 8.8f, new FighterInput { d = true }), (10.2f, 10.2f, new FighterInput { b = true }),
            };
            int steps = at.Max();
            for (int s = 1; s <= steps; s++)
            {
                float t = (s - 1) / 60f;
                var input = default(FighterInput);
                foreach (var (from, to, inp) in script)
                    if (from == to ? Mathf.Abs(t - from) < 0.5f / 60f : t >= from && t < to)
                        input = inp;
                game.UpdateCamera(FightGame.Dt);
                var camRight = game.cam.transform.right;
                camRight.y = 0f;
                input.x *= Vector3.Dot(me.foe.pos - me.pos, camRight) >= 0f ? 1 : -1;
                var inputs = new FighterInput[2];
                inputs[who] = input;
                game.Step(inputs);
                if (at.Contains(s))
                    sb.Append($"\n[ROE]   step {s} ({me.rig.Current} {me.rig.CurrentTime:F2} s):\n[ROE]   " + doa.Probe(top).Replace("\n", "\n[ROE]   "));
            }
            sb.Append($"\n[ROE]   {doa.Report}");
            Debug.Log(sb.ToString());
        }
    }
}
