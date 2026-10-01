using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Plays one action of a unit through the game's own timeline in the studio and writes a few
    /// stills: the quick check that animation, effect prefabs and effect shaders work together.
    /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeSkillTest.Run [-roeChar a08] [-roeAction skill1]
    ///             [-roeTimes 0.5,0.9,1.5] [-roeOut dir] [-roeYaw 55] [-roeDistance 7]
    /// The timeline is stepped at 60 fps from 0 to each time, as it would play, so particles are where they should be.
    /// </summary>
    public static class RoeSkillTest
    {
        [MenuItem("ROE Fighter/Check/Skill stills")]
        public static void Run()
        {
            string id = RoeCapture.Arg("-roeChar", "a08");
            string action = RoeCapture.Arg("-roeAction", "skill1");
            var times = RoeCapture.Arg("-roeTimes", "0.5,0.9,1.2,1.6,2.2,2.8,3.0,3.4").Split(',')
                .Select(t => float.Parse(t, CultureInfo.InvariantCulture)).OrderBy(t => t).ToArray();
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "skilltest"));
            float yaw = float.Parse(RoeCapture.Arg("-roeYaw", "55"), CultureInfo.InvariantCulture);
            float distance = float.Parse(RoeCapture.Arg("-roeDistance", "7"), CultureInfo.InvariantCulture);

            ShaderUtil.allowAsyncCompilation = false;
            var studio = RoeStudio.Build();
            studio.cameraData.requiresColorTexture = true;
            var unit = RoeUnit.Create(RoeManifest.Load().Find(id));
            studio.LightFrom(unit.rig.transform.forward);

            const int fps = 60;
            int next = 0;
            int last = Mathf.RoundToInt(times[times.Length - 1] * fps);
            string warm = Path.Combine(outDir, "_warmup.png");
            for (int f = 0; f <= last; f++)
            {
                unit.Show(action, f / (float)fps);
                if (f == 0)
                {
                    studio.Aim(new Vector3(0f, 1.2f, 1.0f), unit.rig.transform.forward, yaw, 8f, distance, 34f);
                    RoeCapture.Render(studio.camera, 320, 180, warm);
                    RoeCapture.Render(studio.camera, 320, 180, warm);
                }
                if (next < times.Length && f == Mathf.RoundToInt(times[next] * fps))
                {
                    int alive = unit.rig.GetComponentsInChildren<ParticleSystem>(false).Sum(p => p.particleCount);
                    int systems = unit.rig.GetComponentsInChildren<ParticleSystem>(false).Length;
                    RoeCapture.Render(studio.camera, 1280, 720, Path.Combine(outDir, $"{id}_{next + 1}_{action}_{times[next]:F2}.png"));
                    Debug.Log($"[ROE] {id} {action} at {times[next]:F2} s: {systems} active particle systems, {alive} particles");
                    next++;
                }
            }
            unit.Stop();
            Debug.Log($"[ROE] skill stills written to {outDir}");
        }
    }
}
