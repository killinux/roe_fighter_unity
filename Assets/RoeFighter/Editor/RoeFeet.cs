using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// How a fighter's feet stand, from the game's own standing clips (user 10-03: "有的角色是有高跟鞋的，有的角色是没有的，
    /// 得区分一下").  Every frame of the clips she stands in (idle_01 if it stands, idle_02, react_01, react_02) is posed on
    /// the fighter prefab; of the frames where a foot is planted (toe bone within 2.5 cm of the floor), the one a quarter of
    /// the way up from the lowest ankle is how she stands on it (the very lowest was a stray frame: g04's heel sank into the
    /// floor for a moment in react_01, her ankle at 9 cm).
    ///   heels   the shoe holds the ankle up even then (a08, g04: 13-15 cm): the fight keeps posing planted feet in the
    ///           battle stance's high-heeled angle, as before;
    ///   flat    the ankle comes down (g05 barefoot: 8 cm): the motion capture's feet - in the bind pose's angle, which
    ///           for ROE's bare feet is pointed 72 degrees like a high heel, so she stood on the tips of her toes - are
    ///           turned up by the difference to the flattest stand, and planted feet take that stand.
    /// -roeFeet g05=flat,b10=heels overrides the verdict.
    ///   -executeMethod RoeFighter.EditorTools.RoeFeet.Report [-roeChars a08,g04,b10,g05]
    /// </summary>
    public static class RoeFeet
    {
        /// <summary>Planted feet whose ankle stays at least this high (m) stand in heels.</summary>
        public const float HeelAnkle = 0.105f;
        /// <summary>A foot is planted when its toe bone is this close to the floor (m).</summary>
        public const float Planted = 0.025f;

        public class Result
        {
            public bool heels;
            public string note;
            // the flattest stand, per foot (left, right): the foot against the direction it points along the floor, the
            // foot's local axis that points ahead along the floor, the toe bone's local rotation, the bone heights
            public Quaternion[] rest = new Quaternion[2], toe = new Quaternion[2];
            public Vector3[] ahead = new Vector3[2];
            public float toeSole, ankleSole;
            // how far the bind pose's foot is pointed beyond the flattest stand (degrees), and the foot's local axis to turn it up about
            public float[] pitchFix = new float[2];
            public Vector3[] pitchAxis = new Vector3[2];
        }

        static float Pitch(Transform foot, Transform toe)
        {
            var d = toe.position - foot.position;
            return Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;     // + = toes below the ankle
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static Result Measure(string id)
        {
            var r = new Result();
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(id)));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var an = go.GetComponent<Animator>();
            var feet = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
            var toes = new[] { an.GetBoneTransform(HumanBodyBones.LeftToes), an.GetBoneTransform(HumanBodyBones.RightToes) };
            if (feet.Any(f => f == null) || toes.Any(t => t == null))
            {
                Object.DestroyImmediate(go);
                r.heels = true;
                r.note = "no foot or toe bones: as the stance has them";
                return r;
            }
            // the bind pose (the motion capture's feet keep its angle: the avatar's T-pose keeps the feet as they are)
            var bindPitch = new[] { Pitch(feet[0], toes[0]), Pitch(feet[1], toes[1]) };
            var bindLocal = feet.Select(f => f.localRotation).ToArray();
            var bindWorld = feet.Select(f => f.rotation).ToArray();

            var samples = new[] { new List<(float ankle, float toe, float pitch, string where, Quaternion rest, Vector3 ahead, Quaternion toeLocal)>(),
                                  new List<(float ankle, float toe, float pitch, string where, Quaternion rest, Vector3 ahead, Quaternion toeLocal)>() };
            foreach (var name in new[] { "idle_01", "idle_02", "react_01", "react_02" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RoeHumanoidClips.ClipPath(id, name));
                if (clip == null)
                    continue;
                for (float t = 0f; t <= clip.length + 1e-4f; t += 1f / 30f)
                {
                    RoeCapture.Pose(go, clip, t);
                    for (int k = 0; k < 2; k++)
                    {
                        float ankle = feet[k].position.y, toe = toes[k].position.y;
                        if (toe > Planted)
                            continue;
                        var ahead = Flat(toes[k].position - feet[k].position);
                        if (ahead.sqrMagnitude < 1e-6f)
                            continue;
                        ahead.Normalize();
                        samples[k].Add((ankle, toe, Pitch(feet[k], toes[k]), $"{name} {t:F2}s",
                                        Quaternion.Inverse(Quaternion.LookRotation(ahead, Vector3.up)) * feet[k].rotation,
                                        Quaternion.Inverse(feet[k].rotation) * ahead, toes[k].localRotation));
                    }
                }
            }
            RoeCapture.EndPosing();
            if (samples.Any(x => x.Count == 0))
            {
                Object.DestroyImmediate(go);
                r.heels = true;
                r.note = "no planted foot in the standing clips: as the stance has them";
                return r;
            }
            var best = samples.Select(x => x.OrderBy(v => v.ankle).ElementAt(x.Count / 4)).ToArray();
            float flattest = Mathf.Min(best[0].ankle, best[1].ankle);
            r.heels = flattest >= HeelAnkle;
            for (int k = 0; k < 2; k++)
            {
                r.rest[k] = best[k].rest;
                r.ahead[k] = best[k].ahead;
                r.toe[k] = best[k].toeLocal;
                // the bind pose's foot turned up about its own lateral axis (the ankle's hinge) by the difference in pitch
                feet[k].localRotation = bindLocal[k];
                var lateral = Vector3.Cross(Vector3.up, Flat(toes[k].position - feet[k].position).normalized);
                r.pitchAxis[k] = (Quaternion.Inverse(bindWorld[k]) * lateral).normalized;
                r.pitchFix[k] = Mathf.Max(0f, bindPitch[k] - best[k].pitch);
                // which way round lifts the toes
                var before = toes[k].position.y;
                feet[k].localRotation = bindLocal[k] * Quaternion.AngleAxis(r.pitchFix[k], r.pitchAxis[k]);
                if (toes[k].position.y < before)
                    r.pitchAxis[k] = -r.pitchAxis[k];
                feet[k].localRotation = bindLocal[k];
            }
            r.toeSole = (best[0].toe + best[1].toe) * 0.5f;
            r.ankleSole = (best[0].ankle + best[1].ankle) * 0.5f;
            r.note = $"{(r.heels ? "heels" : "flat")}: planted ankle {flattest * 100f:F1} cm (a quarter up the planted frames; " +
                     $"L {best[0].ankle * 100f:F1} cm pitch {best[0].pitch:F0} in {best[0].where} of {samples[0].Count}, lowest {samples[0].Min(v => v.ankle) * 100f:F1}, " +
                     $"R {best[1].ankle * 100f:F1} cm pitch {best[1].pitch:F0} in {best[1].where} of {samples[1].Count}, lowest {samples[1].Min(v => v.ankle) * 100f:F1}; " +
                     $"bind pose pitch {bindPitch[0]:F0}/{bindPitch[1]:F0}" + (r.heels ? "" : $", the motion capture's feet turned up {r.pitchFix[0]:F0}/{r.pitchFix[1]:F0} deg") + ")";
            Object.DestroyImmediate(go);
            return r;
        }

        [MenuItem("ROE Fighter/Fight/Feet kinds")]
        public static void Report()
        {
            var sb = new System.Text.StringBuilder("[ROE] feet:");
            foreach (var id in RoeCapture.Arg("-roeChars", "a08,g04,b10,g05").Split(','))
                sb.Append($"\n[ROE]   {id}: {Measure(id).note}");
            Debug.Log(sb.ToString());
        }
    }
}
