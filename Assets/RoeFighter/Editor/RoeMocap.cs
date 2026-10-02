using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Motion capture -> Unity humanoid clips for the fighters' basic moves (walking, side steps,
    /// punches, kicks, the guard).  The game has none of these; its characters only have their
    /// battle stance, three skills, a hit reaction and a fall.
    ///
    /// Source: Bandai Namco Research Motion Dataset 1 (Bandai Namco Research Inc., CC BY-NC 4.0),
    /// BVH at 30 fps, downloaded by tools/fetch_mocap.py into _work/mocap/bandai1.
    ///
    /// The BVH skeleton is rebuilt as GameObjects (BVH is right-handed: x is mirrored, cm -> m).
    /// Its rest pose has every bone along its local x, so the avatar's T-pose is made from a calm
    /// standing frame: the hips are turned to face +Z, then every bone is swung (no twist) to the
    /// T-pose direction - spine up, arms out to the side, legs down, toes forward.  Each frame of a
    /// segment is then read back as a HumanPose (muscles + body position) and written as a humanoid
    /// clip, which any humanoid fighter can play.
    /// Loops (walks, side steps, run) are cut where the pose repeats best and play in place; the
    /// strikes keep their own small travel.
    /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeMocap.ImportAll
    /// </summary>
    public static class RoeMocap
    {
        public const string OutDir = "Assets/RoeFighter/Generated/mocap";
        const float Scale = 0.01f;
        const string ReferenceFile = "dataset-1_walk_normal_001.bvh";

        static string SourceDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "mocap", "bandai1");

        public static string ClipPath(string name) => $"{OutDir}/mc_{name}.anim";
        public static string MovesPath => $"{OutDir}/mocap_strikes.json";

        /// <summary>A piece of a BVH file that becomes one clip.  Frame numbers from tools/bvh_preview.py.</summary>
        public class Segment
        {
            public string name, file;
            public int from, to;            // frame range; for loops the range the cycle is searched in
            public bool loop;
            public int minCycle, maxCycle;  // loop length limits in frames
            public string limb;             // strikes: the BVH joint that hits; its reach is turned to +Z
            public string alignWith;        // use the turn of another segment of the same file
        }

        public static readonly Segment[] Segments =
        {
            new Segment { name = "guard", file = "punch_normal_001", from = 0, to = 24, loop = true, minCycle = 12, maxCycle = 22, alignWith = "jab" },
            new Segment { name = "jab", file = "punch_normal_001", from = 18, to = 44, limb = "Hand_L" },
            new Segment { name = "cross", file = "punch_normal_002", from = 40, to = 74, limb = "Hand_R" },
            new Segment { name = "kick", file = "kick_normal_001", from = 38, to = 96, limb = "Foot_R" },
            new Segment { name = "slash", file = "slash_normal_001", from = 56, to = 100, limb = "Hand_R" },
            new Segment { name = "walk", file = "walk_feminine_001", from = 40, to = 240, loop = true, minCycle = 26, maxCycle = 46 },
            new Segment { name = "walk_back", file = "walk-back_feminine_001", from = 40, to = 280, loop = true, minCycle = 26, maxCycle = 50 },
            new Segment { name = "run", file = "run_feminine_001", from = 20, to = 110, loop = true, minCycle = 14, maxCycle = 30 },
        };

        // Unity humanoid bone -> BVH joint
        static readonly (string human, string joint)[] HumanMap =
        {
            ("Hips", "Hips"), ("Spine", "Spine"), ("Chest", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
            ("LeftShoulder", "Shoulder_L"), ("LeftUpperArm", "UpperArm_L"), ("LeftLowerArm", "LowerArm_L"), ("LeftHand", "Hand_L"),
            ("RightShoulder", "Shoulder_R"), ("RightUpperArm", "UpperArm_R"), ("RightLowerArm", "LowerArm_R"), ("RightHand", "Hand_R"),
            ("LeftUpperLeg", "UpperLeg_L"), ("LeftLowerLeg", "LowerLeg_L"), ("LeftFoot", "Foot_L"), ("LeftToes", "Toes_L"),
            ("RightUpperLeg", "UpperLeg_R"), ("RightLowerLeg", "LowerLeg_R"), ("RightFoot", "Foot_R"), ("RightToes", "Toes_R"),
        };

        class Joint
        {
            public string name;
            public int parent = -1;
            public Vector3 offset;
            public string[] channels = new string[0];
            public int firstChannel;
            public Transform transform;
        }

        class Bvh
        {
            public readonly List<Joint> joints = new List<Joint>();
            public float[][] frames;
            public float frameTime;
            public Joint this[string name] => joints.First(j => j.name == name);
        }

        static Bvh Parse(string path)
        {
            var bvh = new Bvh();
            var stack = new Stack<int>();
            var lines = File.ReadAllLines(path);
            int channelCount = 0, i = 0;
            for (; i < lines.Length; i++)
            {
                var tok = lines[i].Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length == 0)
                    continue;
                switch (tok[0])
                {
                    case "ROOT":
                    case "JOINT":
                        bvh.joints.Add(new Joint { name = tok[1], parent = stack.Count > 0 ? stack.Peek() : -1 });
                        stack.Push(bvh.joints.Count - 1);
                        break;
                    case "End":
                        bvh.joints.Add(new Joint { name = bvh.joints[stack.Peek()].name + "_end", parent = stack.Peek() });
                        stack.Push(bvh.joints.Count - 1);
                        break;
                    case "OFFSET":
                        bvh.joints[stack.Peek()].offset = new Vector3(F(tok[1]), F(tok[2]), F(tok[3]));
                        break;
                    case "CHANNELS":
                        var j = bvh.joints[stack.Peek()];
                        j.channels = tok.Skip(2).ToArray();
                        j.firstChannel = channelCount;
                        channelCount += j.channels.Length;
                        break;
                    case "}":
                        stack.Pop();
                        break;
                }
                if (tok[0] == "MOTION")
                    break;
            }
            int frames = int.Parse(lines[i + 1].Split(':')[1].Trim());
            bvh.frameTime = F(lines[i + 2].Split(':')[1].Trim());
            bvh.frames = new float[frames][];
            for (int f = 0; f < frames; f++)
                bvh.frames[f] = lines[i + 3 + f].Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Select(F).ToArray();
            return bvh;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        // BVH is right-handed: mirror x.  A rotation about x keeps its angle, about y and z it flips.
        static Vector3 Position(float x, float y, float z) => new Vector3(-x, y, z) * Scale;

        static Quaternion Rotation(char axis, float degrees)
        {
            switch (axis)
            {
                case 'X': return Quaternion.AngleAxis(degrees, Vector3.right);
                case 'Y': return Quaternion.AngleAxis(-degrees, Vector3.up);
                default: return Quaternion.AngleAxis(-degrees, Vector3.forward);
            }
        }

        /// <summary>The skeleton as GameObjects under root/align; end sites without length are left out.</summary>
        static (GameObject root, Transform align) Build(Bvh bvh)
        {
            var root = new GameObject("mocap_actor");
            var align = new GameObject("align").transform;
            align.SetParent(root.transform, false);
            foreach (var j in bvh.joints)
            {
                if (j.name.EndsWith("_end") && j.offset.sqrMagnitude < 1e-6f)
                    continue;
                var t = new GameObject(j.name).transform;
                t.SetParent(j.parent >= 0 ? bvh.joints[j.parent].transform : align, false);
                t.localPosition = Position(j.offset.x, j.offset.y, j.offset.z);
                j.transform = t;
            }
            return (root, align);
        }

        static void Apply(Bvh bvh, int frame)
        {
            var values = bvh.frames[Mathf.Clamp(frame, 0, bvh.frames.Length - 1)];
            foreach (var j in bvh.joints)
            {
                if (j.transform == null || j.channels.Length == 0)
                    continue;
                var p = new Vector3(j.offset.x, j.offset.y, j.offset.z);
                var q = Quaternion.identity;
                for (int c = 0; c < j.channels.Length; c++)
                {
                    float v = values[j.firstChannel + c];
                    string ch = j.channels[c];
                    if (ch.EndsWith("position"))
                        p["XYZ".IndexOf(ch[0])] = v;
                    else
                        q *= Rotation(ch[0], v);
                }
                j.transform.localPosition = Position(p.x, p.y, p.z);
                j.transform.localRotation = q;
            }
        }

        static Vector3 Facing(Bvh bvh)
        {
            var right = bvh["UpperLeg_R"].transform.position - bvh["UpperLeg_L"].transform.position;
            var forward = Vector3.Cross(right, Vector3.up);
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
        }

        static void Swing(Transform bone, Transform child, Vector3 direction)
        {
            var current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-10f)
                return;
            bone.rotation = Quaternion.FromToRotation(current.normalized, direction.normalized) * bone.rotation;
        }

        /// <summary>T-pose from a calm standing frame, and the avatar built on it.</summary>
        static Avatar BuildAvatar(Bvh bvh, GameObject root, Transform align)
        {
            Apply(bvh, 0);
            var hips = bvh["Hips"].transform;
            // hips upright, facing +Z, above the origin
            var up = (bvh["Spine"].transform.position - hips.position).normalized;
            var right = (bvh["UpperLeg_R"].transform.position - bvh["UpperLeg_L"].transform.position).normalized;
            var current = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            hips.rotation = Quaternion.Inverse(current) * hips.rotation;
            hips.position = new Vector3(0f, hips.position.y, 0f);
            (string bone, string child, Vector3 dir)[] swings =
            {
                ("Spine", "Chest", Vector3.up), ("Chest", "Neck", Vector3.up), ("Neck", "Head", Vector3.up),
                ("Shoulder_L", "UpperArm_L", Vector3.left), ("UpperArm_L", "LowerArm_L", Vector3.left), ("LowerArm_L", "Hand_L", Vector3.left),
                ("Shoulder_R", "UpperArm_R", Vector3.right), ("UpperArm_R", "LowerArm_R", Vector3.right), ("LowerArm_R", "Hand_R", Vector3.right),
                ("UpperLeg_L", "LowerLeg_L", Vector3.down), ("LowerLeg_L", "Foot_L", Vector3.down), ("Foot_L", "Toes_L", Vector3.forward),
                ("UpperLeg_R", "LowerLeg_R", Vector3.down), ("LowerLeg_R", "Foot_R", Vector3.down), ("Foot_R", "Toes_R", Vector3.forward),
            };
            foreach (var (bone, child, dir) in swings)
                Swing(bvh[bone].transform, bvh[child].transform, dir);
            // feet on the floor
            float lowest = Mathf.Min(bvh["Toes_L"].transform.position.y, bvh["Toes_R"].transform.position.y);
            hips.position -= new Vector3(0f, lowest, 0f);

            var human = HumanMap.Select(m =>
            {
                var hb = new HumanBone { humanName = m.human, boneName = m.joint };
                hb.limit.useDefaultValues = true;
                return hb;
            }).ToArray();
            var skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
            {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
            }).ToArray();
            var description = new HumanDescription
            {
                human = human,
                skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = "mocap_actor_avatar";
            return avatar;
        }

        static string MuscleAttribute(string traitName)
        {
            var m = Regex.Match(traitName, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
            return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : traitName;
        }

        /// <summary>Pose features for finding where a cycle repeats: limbs relative to the hips, heading removed.</summary>
        static float[] Features(Bvh bvh)
        {
            var hips = bvh["Hips"].transform;
            var heading = Quaternion.Inverse(Quaternion.LookRotation(Facing(bvh)));
            var list = new List<float> { hips.position.y };
            foreach (var name in new[] { "Hand_L", "Hand_R", "Foot_L", "Foot_R", "Head", "LowerLeg_L", "LowerLeg_R", "LowerArm_L", "LowerArm_R" })
            {
                var p = heading * (bvh[name].transform.position - hips.position);
                list.Add(p.x);
                list.Add(p.y);
                list.Add(p.z);
            }
            return list.ToArray();
        }

        static (int a, int b, float cost) FindCycle(Bvh bvh, Segment s)
        {
            int lo = Mathf.Max(0, s.from), hi = Mathf.Min(bvh.frames.Length - 1, s.to);
            var feats = new float[hi - lo + 2][];
            for (int f = lo; f <= hi + 1; f++)
            {
                Apply(bvh, f);
                feats[f - lo] = Features(bvh);
            }
            float Dist(int a, int b)
            {
                float d = 0f;
                var fa = feats[a - lo];
                var fb = feats[b - lo];
                var na = feats[Mathf.Min(a + 1, hi + 1) - lo];
                var nb = feats[Mathf.Min(b + 1, hi + 1) - lo];
                for (int k = 0; k < fa.Length; k++)
                {
                    float p = fa[k] - fb[k];
                    float v = (na[k] - fa[k]) - (nb[k] - fb[k]);
                    d += p * p + 4f * v * v;
                }
                return d;
            }
            var best = (a: lo, b: Mathf.Min(hi, lo + s.maxCycle), cost: float.MaxValue);
            for (int a = lo; a <= hi - s.minCycle; a++)
                for (int b = a + s.minCycle; b <= Mathf.Min(hi, a + s.maxCycle); b++)
                {
                    float c = Dist(a, b);
                    if (c < best.cost)
                        best = (a, b, c);
                }
            return best;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Which way is "towards the opponent" in a segment (yaw, degrees): for a strike the
        /// direction of its farthest reach, otherwise the mean facing of the hips.
        /// </summary>
        static float Heading(Bvh bvh, Segment s, int a, int b)
        {
            if (!string.IsNullOrEmpty(s.limb))
            {
                var best = Vector3.zero;
                for (int f = a; f <= b; f++)
                {
                    Apply(bvh, f);
                    var d = bvh[s.limb].transform.position - bvh["Hips"].transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > best.sqrMagnitude)
                        best = d;
                }
                return Yaw(best);
            }
            var sum = Vector3.zero;
            for (int f = a; f <= b; f++)
            {
                Apply(bvh, f);
                sum += Facing(bvh);
            }
            return Yaw(sum);
        }

        /// <summary>A strike as the fight sees it (times in seconds of the clip at its own speed).</summary>
        [System.Serializable]
        public class StrikeInfo
        {
            public string name, bone;           // humanoid bone that hits
            public float length, reach, height; // reach: farthest the bone gets towards the opponent, from where the hips started
            public float hitStart, hitPeak, hitEnd;
        }

        [System.Serializable]
        class StrikeTable
        {
            public List<StrikeInfo> moves = new List<StrikeInfo>();
        }

        static readonly Dictionary<string, float> Headings = new Dictionary<string, float>();
        static readonly List<StrikeInfo> Strikes = new List<StrikeInfo>();

        static AnimationClip Convert(Bvh bvh, Segment s, Avatar avatar, GameObject root, Transform align, out string report)
        {
            int a = s.from, b = Mathf.Min(s.to, bvh.frames.Length - 1);
            float cost = 0f;
            if (s.loop)
                (a, b, cost) = FindCycle(bvh, s);

            // start of the segment: hips above the origin, the opponent towards +Z
            align.localPosition = Vector3.zero;
            align.localRotation = Quaternion.identity;
            float heading = !string.IsNullOrEmpty(s.alignWith) && Headings.TryGetValue(s.alignWith, out float other) ? other : Heading(bvh, s, a, b);
            Headings[s.name] = heading;
            Apply(bvh, a);
            var turn = Quaternion.Euler(0f, -heading, 0f);
            var start = bvh["Hips"].transform.position;
            align.localRotation = turn;
            align.localPosition = -(turn * new Vector3(start.x, 0f, start.z));
            StrikeInfo strike = null;
            if (!string.IsNullOrEmpty(s.limb))
            {
                var bone = HumanMap.First(m => m.joint == s.limb).human;
                var reach = new List<(float time, float dist, float height)>();
                for (int f = a; f <= b; f++)
                {
                    Apply(bvh, f);
                    var p = bvh[s.limb].transform.position;
                    reach.Add(((f - a) * bvh.frameTime, p.z, p.y));   // forward reach towards the opponent
                }
                var peak = reach.OrderByDescending(r => r.dist).First();
                // active while the bone is within 15 % of its farthest reach around the peak
                float limit = peak.dist * 0.85f;
                int ip = reach.IndexOf(peak), i0 = ip, i1 = ip;
                while (i0 > 0 && reach[i0 - 1].dist >= limit)
                    i0--;
                while (i1 < reach.Count - 1 && reach[i1 + 1].dist >= limit)
                    i1++;
                strike = new StrikeInfo
                {
                    name = s.name, bone = bone, length = (b - a) * bvh.frameTime, reach = peak.dist, height = peak.height,
                    hitStart = reach[i0].time, hitPeak = peak.time, hitEnd = reach[i1].time,
                };
                Strikes.RemoveAll(x => x.name == s.name);
                Strikes.Add(strike);
                Apply(bvh, a);
            }

            var handler = new HumanPoseHandler(avatar, root.transform);
            var pose = new HumanPose();
            int muscles = HumanTrait.MuscleCount;
            var keys = new List<Keyframe>[muscles + 7];
            for (int k = 0; k < keys.Length; k++)
                keys[k] = new List<Keyframe>();
            var lastQ = Quaternion.identity;
            var hips0 = Vector3.zero;
            var hips1 = Vector3.zero;
            for (int f = a; f <= b; f++)
            {
                Apply(bvh, f);
                if (f == a)
                    hips0 = bvh["Hips"].transform.position;
                if (f == b)
                    hips1 = bvh["Hips"].transform.position;
                handler.GetHumanPose(ref pose);
                float time = (f - a) * bvh.frameTime;
                for (int m = 0; m < muscles; m++)
                    keys[m].Add(new Keyframe(time, pose.muscles[m]));
                var q = pose.bodyRotation;
                if (f > a && Quaternion.Dot(q, lastQ) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                lastQ = q;
                float[] rootValues = { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z, q.x, q.y, q.z, q.w };
                for (int k = 0; k < 7; k++)
                    keys[muscles + k].Add(new Keyframe(time, rootValues[k]));
            }
            handler.Dispose();

            // Loops walk on the spot: the steady travel is taken out of the body position (the fight
            // moves the fighter), only the sway stays.  Strikes keep their small lunge.
            var travelXZ = Vector2.zero;
            if (s.loop)
            {
                var tx = keys[muscles + 0];
                var tz = keys[muscles + 2];
                float x0 = tx[0].value, z0 = tz[0].value;
                float duration = Mathf.Max(1e-4f, tx[tx.Count - 1].time);
                travelXZ = new Vector2(tx[tx.Count - 1].value - x0, tz[tz.Count - 1].value - z0);
                for (int i = 0; i < tx.Count; i++)
                {
                    float u = tx[i].time / duration;
                    tx[i] = new Keyframe(tx[i].time, tx[i].value - x0 - travelXZ.x * u);
                    tz[i] = new Keyframe(tz[i].time, tz[i].value - z0 - travelXZ.y * u);
                }
            }

            var clip = new AnimationClip { name = "mc_" + s.name, frameRate = Mathf.Round(1f / bvh.frameTime) };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int k = 0; k < keys.Length; k++)
            {
                string attribute = k < muscles ? MuscleAttribute(HumanTrait.MuscleName[k]) : rootNames[k - muscles];
                var curve = new AnimationCurve(keys[k].ToArray());
                for (int i = 0; i < curve.length; i++)
                    curve.SmoothTangents(i, 0f);
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), attribute));
                curves.Add(curve);
            }
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = s.loop;
            settings.loopBlend = false;
            settings.loopBlendOrientation = true;      // the body keeps the clip's heading
            settings.keepOriginalOrientation = true;
            settings.loopBlendPositionY = true;        // and its height
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionXZ = true;       // body position as in the clip (loops: on the spot)
            settings.keepOriginalPositionXZ = true;
            settings.heightFromFeet = false;
            settings.mirror = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string assetPath = ClipPath(s.name);
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(clip, assetPath);
            var travel = hips1 - hips0;
            float seconds = (b - a) * bvh.frameTime;
            report = $"mc_{s.name}: {s.file} frames {a}..{b} ({seconds:F2} s), heading {heading:F0} deg{(s.loop ? $", cycle match {cost:F4}" : "")}, " +
                     $"hips travel ({travel.x:F2}, {travel.y:F2}, {travel.z:F2}) m = {(seconds > 0 ? new Vector2(travel.x, travel.z).magnitude / seconds : 0):F2} m/s" +
                     (s.loop ? $", taken out of the clip: ({travelXZ.x:F2}, {travelXZ.y:F2}) m" : "") +
                     (strike != null ? $"; strike {strike.bone} reach {strike.reach:F2} m at height {strike.height:F2} m, " +
                                       $"active {strike.hitStart:F2}-{strike.hitEnd:F2} s (peak {strike.hitPeak:F2})" : "");
            return clip;
        }

        /// <summary>
        /// Check pictures: every fighter in every mocap clip, a few evenly spaced frames each, seen
        /// from the side the strikes go to.
        /// Batch mode: -executeMethod RoeFighter.EditorTools.RoeMocap.Preview [-roeOut dir] [-roeClips jab,kick] [-roeShots 6]
        /// </summary>
        [MenuItem("ROE Fighter/Mocap/Preview stills")]
        public static void Preview()
        {
            string outDir = RoeCapture.Arg("-roeOut", Path.Combine(Path.GetDirectoryName(Application.dataPath), "_work", "mocap", "preview"));
            int shots = int.Parse(RoeCapture.Arg("-roeShots", "6"));
            var names = RoeCapture.Arg("-roeClips", string.Join(",", Segments.Select(s => s.name))).Split(',');
            ShaderUtil.allowAsyncCompilation = false;
            var studio = RoeStudio.Build();
            foreach (var c in RoeManifest.Load().WithModels)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeHumanoid.FighterPath(c.id));
                if (prefab == null)
                    continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var bones = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(b => b != null).Distinct().ToArray();
                bool warmed = false;
                foreach (var name in names)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                    if (clip == null)
                        continue;
                    // seen from the fighter's left, a little in front: punches and kicks go to the right of the picture
                    var view = Quaternion.Euler(0f, -70f, 0f) * Vector3.forward;
                    studio.LightFrom(view);
                    for (int i = 0; i < shots; i++)
                    {
                        float time = clip.length * i / Mathf.Max(1, shots - 1);
                        RoeCapture.Pose(go, clip, time);
                        studio.Aim(new Vector3(0f, 0.9f, 0.3f), view, 8f, 0f, 1.25f / Mathf.Tan(13.5f * Mathf.Deg2Rad), 27f);
                        if (!warmed)
                        {
                            RoeCapture.Render(studio.camera, 256, 256, Path.Combine(outDir, "_warmup.jpg"), 80);
                            RoeCapture.Render(studio.camera, 256, 256, Path.Combine(outDir, "_warmup.jpg"), 80);
                            warmed = true;
                        }
                        RoeCapture.Render(studio.camera, 480, 600, Path.Combine(outDir, $"{c.id}_{name}_{i}.jpg"), 88);
                    }
                }
                RoeCapture.EndPosing();
                Object.DestroyImmediate(go);
                Debug.Log($"[ROE] mocap preview: {c.id} done");
            }
        }

        /// <summary>
        /// Side steps, made here: the mocap has no step to the side while facing forward (its
        /// "walk-left" turns and walks).  The guard pose stays; the legs shuffle: the leading leg
        /// lifts and steps out, then the trailing leg lifts and closes in, while the fight moves the
        /// fighter sideways.  Angles in degrees, turned into muscle values with Unity's default ranges.
        /// </summary>
        static void BuildSideSteps()
        {
            var guard = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("guard"));
            if (guard == null)
                return;
            var bindings = AnimationUtility.GetCurveBindings(guard);
            var baseValues = bindings.ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(guard, b).Evaluate(0f));
            int Muscle(string name) => System.Array.IndexOf(HumanTrait.MuscleName, name);
            float Delta(string muscle, float degrees)
            {
                int i = Muscle(muscle);
                float range = degrees >= 0f ? HumanTrait.GetMuscleDefaultMax(i) : -HumanTrait.GetMuscleDefaultMin(i);
                return degrees / Mathf.Max(1f, range);
            }
            const int frames = 16;              // one shuffle: 0.53 s at 30 fps
            const float fps = 30f;
            foreach (var (name, lead, trail, side) in new[] { ("side_right", "Right", "Left", 1f), ("side_left", "Left", "Right", -1f) })
            {
                var keys = bindings.ToDictionary(b => b.propertyName, b => new List<Keyframe>());
                for (int k = 0; k <= frames; k++)
                {
                    float u = k / (float)frames;
                    float first = u < 0.5f ? u / 0.5f : 1f;
                    float second = u < 0.5f ? 0f : (u - 0.5f) / 0.5f;
                    float spread = u < 0.5f ? Mathf.SmoothStep(0f, 1f, first) : 1f - Mathf.SmoothStep(0f, 1f, second);
                    float liftLead = u < 0.5f ? Mathf.Sin(Mathf.PI * first) : 0f;
                    float liftTrail = u < 0.5f ? 0f : Mathf.Sin(Mathf.PI * second);
                    var v = new Dictionary<string, float>(baseValues);
                    void Add(string attribute, float delta)
                    {
                        if (v.ContainsKey(attribute))
                            v[attribute] += delta;
                    }
                    // both legs open while the leading foot travels, close while the trailing one follows
                    Add($"{lead} Upper Leg In-Out", Delta($"{lead} Upper Leg In-Out", 13f * spread));
                    Add($"{trail} Upper Leg In-Out", Delta($"{trail} Upper Leg In-Out", 13f * spread));
                    Add($"{lead} Upper Leg Front-Back", Delta($"{lead} Upper Leg Front-Back", -14f * liftLead));
                    Add($"{lead} Lower Leg Stretch", Delta($"{lead} Lower Leg Stretch", -22f * liftLead));
                    Add($"{trail} Upper Leg Front-Back", Delta($"{trail} Upper Leg Front-Back", -12f * liftTrail));
                    Add($"{trail} Lower Leg Stretch", Delta($"{trail} Lower Leg Stretch", -20f * liftTrail));
                    Add("Spine Left-Right", Delta("Spine Left-Right", 3f * side));
                    Add("RootT.y", -0.012f * (liftLead + liftTrail));
                    foreach (var pair in v)
                        keys[pair.Key].Add(new Keyframe(k / fps, pair.Value));
                }
                var clip = new AnimationClip { name = "mc_" + name, frameRate = fps };
                var curves = bindings.Select(b =>
                {
                    var c = new AnimationCurve(keys[b.propertyName].ToArray());
                    for (int i = 0; i < c.length; i++)
                        c.SmoothTangents(i, 0f);
                    return c;
                }).ToArray();
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                var settings = AnimationUtility.GetAnimationClipSettings(guard);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.DeleteAsset(ClipPath(name));
                AssetDatabase.CreateAsset(clip, ClipPath(name));
                Debug.Log($"[ROE] mc_{name}: made from the guard pose, {frames / fps:F2} s per shuffle");
            }
        }

        [MenuItem("ROE Fighter/Mocap/Import basic moves")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(OutDir);
            var reference = Parse(Path.Combine(SourceDir, ReferenceFile));
            var (refRoot, refAlign) = Build(reference);
            var avatar = BuildAvatar(reference, refRoot, refAlign);
            Debug.Log($"[ROE] mocap avatar: valid={avatar.isValid} human={avatar.isHuman}, " +
                      $"{reference.joints.Count(j => j.transform != null)} joints, height {reference["Head"].transform.position.y:F2} m (head)");
            string avatarPath = $"{OutDir}/mocap_actor_avatar.asset";
            AssetDatabase.DeleteAsset(avatarPath);
            AssetDatabase.CreateAsset(avatar, avatarPath);
            Object.DestroyImmediate(refRoot);
            if (!avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[ROE] mocap avatar is not a valid humanoid");
                return;
            }

            Headings.Clear();
            Strikes.Clear();
            foreach (var s in Segments.OrderBy(x => string.IsNullOrEmpty(x.alignWith) ? 0 : 1))
            {
                string path = Path.Combine(SourceDir, $"dataset-1_{s.file}.bvh");
                if (!File.Exists(path))
                {
                    Debug.LogError($"[ROE] mocap: {path} missing - run tools/fetch_mocap.py");
                    continue;
                }
                var bvh = Parse(path);
                var (root, align) = Build(bvh);
                // The avatar was built on the reference file's skeleton; every file of the dataset has
                // the same joints and offsets, so the T-pose (bind) values carry over by name.
                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                Convert(bvh, s, avatar, root, align, out string report);
                Debug.Log("[ROE] " + report);
                Object.DestroyImmediate(root);
            }
            BuildSideSteps();
            var table = new StrikeTable();
            table.moves.AddRange(Strikes.OrderBy(x => x.name));
            File.WriteAllText(MovesPath, JsonUtility.ToJson(table, true));
            AssetDatabase.ImportAsset(MovesPath);
            Debug.Log($"[ROE] mocap strikes: {MovesPath} ({table.moves.Count} moves)");
            AssetDatabase.SaveAssets();
        }
    }
}
