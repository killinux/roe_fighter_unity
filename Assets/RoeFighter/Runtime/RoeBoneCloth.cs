using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// Bone cloth: skirts, hair, chains and breasts that swing with the body.
    ///
    /// The method of Magica Cloth 2's BoneCloth, ported from the Blender add-on bone_cloth of the
    /// ripper_tpose repository (scripts/blender_addons/bone_cloth/core.py, simulate()), which bakes
    /// the same thing into MMD motions.  The animated pose is the baseline: each bone tail is a
    /// particle; every step
    ///   1. inertia: the particles ride the bone the chain hangs on (pelvis, head), except the share
    ///      of its movement the cloth "feels", and never above the speed limits;
    ///   2. Verlet integration with damping, a particle speed limit and gravity;
    ///   3. root to tip, a few iterations: angle restoration (turn a share back towards the animated
    ///      direction, most of that move not becoming speed), angle limit (root strict, tip loose),
    ///      colliders (capsules on the thighs, calves and hips; a bone segment collides as a capsule
    ///      so it cannot slip between the legs; as deep as the animation itself goes is allowed),
    ///      backstop (no point sinks behind its animated place towards the body), the floor.
    /// Bone lengths are rigid.  The result is written as bone rotations, blended by a weight.
    ///
    /// Chains are found by name (skirt / hair, braid, bangs / chains, waist ornaments / breasts);
    /// a bone with several simulated children is not simulated itself (its children hang on it).
    /// The ROE skirts are keyed by hand in the game's own clips; the fight blends this in under the
    /// motion-capture moves only.
    /// </summary>
    public class RoeBoneCloth
    {
        [Serializable]
        public class Settings
        {
            public float gravity = 5f, damping = 0.05f, radius = 0.02f;
            public float restore = 0.2f, attenuation = 0.8f;          // angle restoration and its velocity attenuation
            public float limitRoot = 25f, limitTip = 60f;               // angle limit, degrees
            public float inertia = 0.4f, moveLimit = 3f, turnLimit = 360f, particleLimit = 4f;
            public bool backstop = true;
            public float backstopRadius = 10f, backstopDistance = 0.02f;
            public bool edge = true;                                    // bone segments collide as capsules
            public float friction = 0.05f;
            public float pushAttenuation = 0.9f;                        // of a push out of a collider, the share that does not become speed
            public bool floor = true;
            public int iterations = 4;
        }

        // the add-on's presets (Magica Cloth 2 numbers are per 1/90 s step).  Skirts are stiffer than the
        // add-on's (restore 0.2, damping 0.05, limits 25/60, inertia 0.4): with those g04's long panels,
        // whose chains start right at the waist, swung 12 degrees rms standing still and 16-22 walking and
        // striking, jumping 14-39 degrees in a step (RoeFightProbe.SkirtSwing); now 5 / 6-9, 7-17.
        public static Settings Skirt() => Tuned(new Settings { restore = 0.35f, damping = 0.15f, limitRoot = 5f, limitTip = 35f, inertia = 0.3f }, SkirtTuning);

        /// <summary>For checks: "key=value,..." over the skirt preset (gravity, damping, restore, attenuation, limitRoot, limitTip, inertia, particleLimit).</summary>
        public static string SkirtTuning = "";

        static Settings Tuned(Settings s, string tuning)
        {
            foreach (var pair in (tuning ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    continue;
                switch (kv[0].Trim())
                {
                    case "gravity": s.gravity = v; break;
                    case "damping": s.damping = v; break;
                    case "restore": s.restore = v; break;
                    case "attenuation": s.attenuation = v; break;
                    case "limitRoot": s.limitRoot = v; break;
                    case "limitTip": s.limitTip = v; break;
                    case "inertia": s.inertia = v; break;
                    case "particleLimit": s.particleLimit = v; break;
                    case "pushAttenuation": s.pushAttenuation = v; break;
                }
            }
            return s;
        }
        public static Settings Hair() => new Settings
        {
            radius = 0.015f, restore = 0.3f, limitRoot = 15f, limitTip = 50f, inertia = 0.3f, moveLimit = 2f,
            particleLimit = 3f, backstopDistance = 0.01f, edge = false,
        };
        public static Settings Chain() => new Settings
        {
            radius = 0.012f, restore = 0.12f, limitRoot = 30f, limitTip = 80f, inertia = 0.5f, moveLimit = 3f,
            particleLimit = 4f, backstop = false, edge = false,
        };
        public static Settings Breast() => new Settings
        {
            gravity = 3f, radius = 0.03f, restore = 0.35f, attenuation = 0.5f, limitRoot = 6f, limitTip = 12f,
            inertia = 0.6f, moveLimit = 4f, turnLimit = 720f, particleLimit = 2f, backstop = false, edge = false, floor = false,
        };

        static readonly (string kind, Regex name)[] Kinds =
        {
            ("chain", new Regex(@"skirt_chain|^chain\d|chain_arm|dec_wrist", RegexOptions.IgnoreCase)),
            ("skirt", new Regex(@"skirt", RegexOptions.IgnoreCase)),
            ("hair", new Regex(@"hair|braid|bangs|hari", RegexOptions.IgnoreCase)),
            ("breast", new Regex(@"breast|chest_[lr]", RegexOptions.IgnoreCase)),
        };

        public class Capsule
        {
            public string name;
            public Transform a, b;
            public float ra, rb;
        }

        class ChainSet
        {
            public string name, kind;
            public Settings s;
            public Transform anchor;
            public Vector3 anchorAxis;          // the anchor's local axis that pointed up at rest: the body axis for the backstop
            public Transform[] bones;
            public int[] parent;
            public float[] length, limit;       // limit in radians, by depth
            public Vector3[] tailLocal;
            public Quaternion[] restLocal;
            public int[][] levels;
            public Capsule[] capsules;
            // state
            public bool ready;
            public Vector3[] x, xOld, hb, tb, hbPrev, tbPrev, head;
            public Quaternion[] rb, rot;
            public Vector3 anchorPos, anchorPosPrev;
            public Quaternion anchorRot, anchorRotPrev;
        }

        readonly List<ChainSet> chains = new List<ChainSet>();
        public readonly List<Capsule> capsules = new List<Capsule>();
        public float weight = 1f;
        public static bool NoColliders, NoBackstop;     // for checks (RoeFightProbe.SkirtSwing -roeVariant)
        public string Report { get; private set; }

        /// <summary>
        /// Finds the chains under a humanoid model in its current pose (the battle stance) and measures
        /// the leg, hip and torso colliders on its skin.  ``exclude``: bones something else drives.
        /// </summary>
        public RoeBoneCloth(Animator animator, ICollection<Transform> exclude)
        {
            var human = new Dictionary<Transform, HumanBodyBones>();
            foreach (HumanBodyBones hb in Enum.GetValues(typeof(HumanBodyBones)))
                if (hb != HumanBodyBones.LastBone && animator.GetBoneTransform(hb) != null)
                    human[animator.GetBoneTransform(hb)] = hb;
            var skin = SkinPoints(animator);

            // the cloth bones, by kind
            var kindOf = new Dictionary<Transform, string>();
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t == animator.transform || human.ContainsKey(t) || (exclude != null && exclude.Contains(t)) || t.GetComponent<Renderer>() != null)
                    continue;
                if (t.name.EndsWith("_ALL") || t.name.Contains("Dummy") || t.name.Contains("Nub") || t.name.Contains("nub"))
                    continue;
                foreach (var (kind, re) in Kinds)
                    if (re.IsMatch(t.name))
                    {
                        kindOf[t] = kind;
                        break;
                    }
            }
            // a bone with two or more cloth children of its kind hangs them, it is not simulated
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var t in kindOf.Keys.ToList())
                {
                    int kids = 0;
                    foreach (Transform c in t)
                        if (kindOf.TryGetValue(c, out var k) && k == kindOf[t])
                            kids++;
                    if (kids >= 2)
                    {
                        kindOf.Remove(t);
                        changed = true;
                    }
                }
            }
            // a skirt piece of one bone (g04's waist ornaments) is stiff: the game keeps it on the pelvis
            foreach (var t in kindOf.Keys.Where(t => kindOf[t] == "skirt").ToList())
            {
                bool hung = t.parent != null && kindOf.TryGetValue(t.parent, out var pk) && pk == "skirt";
                bool hangs = t.Cast<Transform>().Any(c => kindOf.TryGetValue(c, out var ck) && ck == "skirt");
                if (!hung && !hangs)
                    kindOf.Remove(t);
            }

            // colliders: thighs, calves, the hips; the torso for hair
            Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
            var legs = new List<Capsule>();
            foreach (var (up, low, foot, side) in new[]
            {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "left"),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "right"),
            })
            {
                if (B(up) == null || B(low) == null || B(foot) == null)
                    continue;
                legs.Add(Measure($"{side} thigh", B(up), B(low), skin, 70f, 0.07f, 0.05f));
                legs.Add(Measure($"{side} calf", B(low), B(foot), skin, 70f, 0.05f, 0.035f));
            }
            if (B(HumanBodyBones.LeftUpperLeg) != null && B(HumanBodyBones.RightUpperLeg) != null)
            {
                var hips = Measure("hips", B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.RightUpperLeg), skin, 60f, 0.09f, 0.09f);
                hips.ra = hips.rb = Mathf.Max(hips.ra, hips.rb);
                legs.Add(hips);
            }
            capsules.AddRange(legs);
            var neck = B(HumanBodyBones.Neck) != null ? B(HumanBodyBones.Neck) : B(HumanBodyBones.Head);
            var torso = B(HumanBodyBones.Hips) != null && neck != null ? Measure("torso", B(HumanBodyBones.Hips), neck, skin, 50f, 0.12f, 0.1f) : null;
            if (torso != null)
                capsules.Add(torso);

            // chain sets: one per kind and anchor
            foreach (var group in kindOf.Keys.GroupBy(t => (kindOf[t], Anchor(t, kindOf))))
            {
                var (kind, anchor) = group.Key;
                if (anchor == null)
                    continue;
                var bones = group.OrderBy(Depth).ToArray();
                var s = kind == "skirt" ? Skirt() : kind == "hair" ? Hair() : kind == "chain" ? Chain() : Breast();
                Transform owner = anchor;
                while (owner != null && !human.ContainsKey(owner))
                    owner = owner.parent;
                var lower = owner != null && (human[owner] == HumanBodyBones.Hips || human[owner] == HumanBodyBones.Spine);
                var cs = lower ? legs.ToArray()
                       : kind == "hair" && torso != null ? new[] { torso }
                       : new Capsule[0];
                // inertia and the backstop's body axis come from the body part the chain belongs to
                // (the pelvis for a skirt, even when its panels hang on a skirt bone)
                chains.Add(Build($"{kind} on {anchor.name}", kind, s, owner != null ? owner : anchor, bones, cs, skin));
            }
            Report = $"{chains.Count} chain sets, {chains.Sum(c => c.bones.Length)} bones: " +
                     string.Join(", ", chains.Select(c => $"{c.name} ({c.bones.Length})")) +
                     "; colliders " + string.Join(", ", capsules.Select(c => $"{c.name} r {c.ra:F3}/{c.rb:F3}"));
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t != null; t = t.parent)
                d++;
            return d;
        }

        static Transform Anchor(Transform t, Dictionary<Transform, string> kindOf)
        {
            var p = t.parent;
            while (p != null && kindOf.TryGetValue(p, out var k) && k == kindOf[t])
                p = p.parent;
            return p;
        }

        /// <summary>Every skin vertex in the current pose (world), with the bone it hangs on most.</summary>
        static List<(Vector3 p, Transform bone)> SkinPoints(Animator animator)
        {
            var points = new List<(Vector3, Transform)>();
            var mesh = new Mesh();
            foreach (var r in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null)
                    continue;
                r.BakeMesh(mesh, true);
                var v = mesh.vertices;
                var w = r.sharedMesh.boneWeights;
                var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                for (int i = 0; i < v.Length && i < w.Length; i++)
                    if (w[i].weight0 >= 0.5f && w[i].boneIndex0 < r.bones.Length && r.bones[w[i].boneIndex0] != null)
                        points.Add((m.MultiplyPoint3x4(v[i]), r.bones[w[i].boneIndex0]));
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            return points;
        }

        /// <summary>
        /// A capsule from bone a to bone b, as thick as the skin round it (a percentile of the distances of
        /// the vertices of every bone that lies on the segment: twist and knee helpers carry leg skin too).
        /// </summary>
        static Capsule Measure(string name, Transform a, Transform b, List<(Vector3 p, Transform bone)> skin, float pct, float r0, float r1)
        {
            Vector3 A = a.position, ab = b.position - a.position;
            float L2 = Mathf.Max(ab.sqrMagnitude, 1e-8f);
            var onSegment = new HashSet<Transform> { a };
            foreach (var (p, bone) in skin)
            {
                if (onSegment.Contains(bone))
                    continue;
                float t = Vector3.Dot(bone.position - A, ab) / L2;
                if (t >= -0.05f && t <= 0.95f && Vector3.Distance(bone.position, A + t * ab) <= 0.03f)
                    onSegment.Add(bone);
            }
            var halves = new[] { new List<float>(), new List<float>() };
            foreach (var (p, bone) in skin)
            {
                if (!onSegment.Contains(bone))
                    continue;
                float t = Mathf.Clamp01(Vector3.Dot(p - A, ab) / L2);
                float d = Vector3.Distance(p, A + t * ab);
                if (d < 0.3f)
                    halves[t < 0.5f ? 0 : 1].Add(d);
            }
            float Pct(List<float> list, float fallback)
            {
                if (list.Count < 8)
                    return fallback;
                list.Sort();
                return Mathf.Clamp(list[Mathf.Clamp((int)(list.Count * pct / 100f), 0, list.Count - 1)], 0.02f, 0.25f);
            }
            float ra = Pct(halves[0], halves[1].Count >= 8 ? -1f : r0), rb = Pct(halves[1], halves[0].Count >= 8 ? -1f : r1);
            if (ra < 0f) ra = rb;
            if (rb < 0f) rb = ra;
            return new Capsule { name = name, a = a, b = b, ra = ra, rb = rb };
        }

        ChainSet Build(string name, string kind, Settings s, Transform anchor, Transform[] bones, Capsule[] caps, List<(Vector3 p, Transform bone)> skin)
        {
            int n = bones.Length;
            var index = new Dictionary<Transform, int>();
            for (int i = 0; i < n; i++)
                index[bones[i]] = i;
            var c = new ChainSet
            {
                name = name, kind = kind, s = s, anchor = anchor, bones = bones, capsules = caps,
                parent = new int[n], length = new float[n], limit = new float[n], tailLocal = new Vector3[n], restLocal = new Quaternion[n],
                x = new Vector3[n], xOld = new Vector3[n], hb = new Vector3[n], tb = new Vector3[n], hbPrev = new Vector3[n],
                tbPrev = new Vector3[n], head = new Vector3[n], rb = new Quaternion[n], rot = new Quaternion[n],
            };
            // the anchor's axis that points up at rest
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            c.anchorAxis = axes.OrderByDescending(ax => Vector3.Dot(anchor.rotation * ax, Vector3.up)).First();
            var level = new int[n];
            for (int i = 0; i < n; i++)
            {
                var b = bones[i];
                c.parent[i] = b.parent != null && index.TryGetValue(b.parent, out int p) ? p : -1;
                level[i] = c.parent[i] < 0 ? 0 : level[c.parent[i]] + 1;
                c.restLocal[i] = b.localRotation;
                // the tail: the one simulated child; else where the bone's own skin reaches; else on along the parent
                Transform child = null;
                foreach (Transform k in b)
                    if (index.ContainsKey(k))
                        child = k;
                Vector3 tail;
                if (child != null)
                    tail = child.position;
                else
                {
                    var mine = skin.Where(v => v.bone == b).Select(v => v.p).ToList();
                    if (mine.Count >= 4)
                    {
                        var mean = mine.Aggregate(Vector3.zero, (acc, v) => acc + v) / mine.Count;
                        tail = b.position + (mean - b.position) * 1.6f;
                    }
                    else if (c.parent[i] >= 0)
                        tail = b.position + (b.position - bones[c.parent[i]].position).normalized * 0.6f * Vector3.Distance(b.position, bones[c.parent[i]].position);
                    else
                        tail = b.position + b.rotation * Vector3.right * 0.06f;
                }
                if ((tail - b.position).sqrMagnitude < 1e-6f)
                    tail = b.position + b.rotation * Vector3.right * 0.03f;
                c.tailLocal[i] = b.InverseTransformPoint(tail);
                c.length[i] = Vector3.Distance(b.position, tail);
            }
            // depth along its own chain: 0 at the root, 1 at the deepest tip below that root
            var deepest = (int[])level.Clone();
            for (int i = n - 1; i >= 0; i--)
                if (c.parent[i] >= 0)
                    deepest[c.parent[i]] = Mathf.Max(deepest[c.parent[i]], deepest[i]);
            var chainMax = (int[])deepest.Clone();
            for (int i = 0; i < n; i++)
                if (c.parent[i] >= 0)
                    chainMax[i] = chainMax[c.parent[i]];
            for (int i = 0; i < n; i++)
            {
                float depth = chainMax[i] > 0 ? (float)level[i] / chainMax[i] : 0f;
                c.limit[i] = Mathf.Deg2Rad * Mathf.Lerp(s.limitRoot, s.limitTip, depth);
            }
            c.levels = Enumerable.Range(0, n == 0 ? 0 : level.Max() + 1).Select(l => Enumerable.Range(0, n).Where(i => level[i] == l).ToArray()).ToArray();
            return c;
        }

        /// <summary>Before the animation writes the pose: bones no clip animates go back to their rest rotation.</summary>
        public void Rest()
        {
            foreach (var c in chains)
                for (int i = 0; i < c.bones.Length; i++)
                    c.bones[i].localRotation = c.restLocal[i];
        }

        /// <summary>
        /// For checks, after Step: every chain tip (a bone with no child in its set) of the given kind, how
        /// far its simulated tail is turned away from the animated one, seen from where its chain hangs
        /// (degrees), and the simulated direction.
        /// </summary>
        public IEnumerable<(string set, string tip, float angle, Vector3 dir, Vector3 animatedDir)> TipDeviations(string kind)
        {
            foreach (var c in chains.Where(c => c.kind == kind && c.ready))
            {
                var isParent = new HashSet<int>(c.parent.Where(p => p >= 0));
                for (int i = 0; i < c.bones.Length; i++)
                {
                    if (isParent.Contains(i))
                        continue;
                    int r = i;
                    while (c.parent[r] >= 0)
                        r = c.parent[r];
                    var animated = c.tb[i] - c.hb[r];
                    var simulated = c.x[i] - c.hb[r];
                    yield return (c.name, c.bones[i].name, Vector3.Angle(animated, simulated), simulated.normalized, animated.normalized);
                }
            }
        }

        /// <summary>Start again from the animated pose (a teleport, a new round).</summary>
        public void Reset()
        {
            foreach (var c in chains)
                c.ready = false;
        }

        /// <summary>One step of the fight (dt), on the pose the animation and the rig made; floor = world height.</summary>
        public void Step(float dt, float floor)
        {
            foreach (var c in chains)
            {
                ReadPose(c);
                if (!c.ready)
                {
                    Array.Copy(c.tb, c.x, c.x.Length);
                    Array.Copy(c.tb, c.xOld, c.x.Length);
                    Array.Copy(c.hb, c.hbPrev, c.x.Length);
                    Array.Copy(c.tb, c.tbPrev, c.x.Length);
                    Array.Copy(c.rb, c.rot, c.x.Length);
                    c.anchorPosPrev = c.anchorPos;
                    c.anchorRotPrev = c.anchorRot;
                    c.ready = true;
                }
                if (dt > 0f)
                {
                    int sub = Mathf.Max(1, Mathf.RoundToInt(dt * 120f));
                    for (int k = 0; k < sub; k++)
                        Simulate(c, dt / sub, (float)k / sub, (k + 1f) / sub, floor);
                    Array.Copy(c.hb, c.hbPrev, c.x.Length);
                    Array.Copy(c.tb, c.tbPrev, c.x.Length);
                    c.anchorPosPrev = c.anchorPos;
                    c.anchorRotPrev = c.anchorRot;
                }
                Write(c);
            }
        }

        static void ReadPose(ChainSet c)
        {
            for (int i = 0; i < c.bones.Length; i++)
            {
                var b = c.bones[i];
                c.hb[i] = b.position;
                c.tb[i] = b.TransformPoint(c.tailLocal[i]);
                c.rb[i] = b.rotation;
            }
            c.anchorPos = c.anchor.position;
            c.anchorRot = c.anchor.rotation;
        }

        /// <summary>One substep: the fight's step is split in ~120 Hz substeps; w / wPrev = where in the step it ends / starts.</summary>
        static void Simulate(ChainSet c, float dt, float wPrev, float w, float floor)
        {
            var s = c.s;
            int n = c.bones.Length;
            float step90 = dt * 90f;
            float damp = 1f - Mathf.Pow(1f - s.damping, step90);
            float restore = 1f - Mathf.Pow(1f - s.restore, step90);
            var gravity = Vector3.down * s.gravity;

            // the animated pose and the anchor in between the fight's steps
            var hb = new Vector3[n];
            var tb = new Vector3[n];
            var d0 = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                hb[i] = Vector3.Lerp(c.hbPrev[i], c.hb[i], w);
                tb[i] = Vector3.Lerp(c.tbPrev[i], c.tb[i], w);
                d0[i] = (tb[i] - hb[i]).normalized;
            }
            var cPos = Vector3.Lerp(c.anchorPosPrev, c.anchorPos, w);
            var cRot = Quaternion.Slerp(c.anchorRotPrev, c.anchorRot, w);
            var prevPos = Vector3.Lerp(c.anchorPosPrev, c.anchorPos, wPrev);
            var prevRot = Quaternion.Slerp(c.anchorRotPrev, c.anchorRot, wPrev);

            // 1. inertia: the particles ride the anchor except the share of its motion the cloth feels
            var move = cPos - prevPos;
            var turn = cRot * Quaternion.Inverse(prevRot);
            float speed = move.magnitude / dt;
            float feltT = speed > 1e-6f ? s.inertia * Mathf.Min(1f, s.moveLimit / speed) : s.inertia;
            turn.ToAngleAxis(out float angle, out _);
            if (angle > 180f)
                angle = 360f - angle;
            float feltR = angle > 1e-4f ? s.inertia * Mathf.Min(1f, s.turnLimit * dt / angle) : s.inertia;
            var carry = Quaternion.Slerp(Quaternion.identity, turn, 1f - feltR);
            var shift = move * (1f - feltT);
            for (int i = 0; i < n; i++)
            {
                c.x[i] = carry * (c.x[i] - prevPos) + prevPos + shift;
                c.xOld[i] = carry * (c.xOld[i] - prevPos) + prevPos + shift;
            }

            // 2. Verlet: damping, speed limit, gravity
            for (int i = 0; i < n; i++)
            {
                var v = (c.x[i] - c.xOld[i]) / dt * (1f - damp);
                float sp = v.magnitude;
                if (sp > s.particleLimit)
                    v *= s.particleLimit / sp;
                v += gravity * dt;
                c.xOld[i] = c.x[i];
                c.x[i] += v * dt;
            }

            // 3. constraints, root to tip
            var axis = cRot * c.anchorAxis;
            for (int it = 0; it < s.iterations; it++)
            {
                foreach (var lv in c.levels)
                {
                    foreach (int i in lv)
                    {
                        int p = c.parent[i];
                        // the parent's turn away from its animated pose carries this bone's baseline
                        var D = p < 0 ? Quaternion.identity : c.rot[p] * Quaternion.Inverse(c.rb[p]);
                        var h = p < 0 ? hb[i] : c.x[p];
                        var g = (D * d0[i]).normalized;
                        var dir = (c.x[i] - h).normalized;
                        float L = c.length[i];
                        if (it == 0)
                        {
                            // angle restoration, once a step; most of the move does not become speed
                            var d2 = Turn(dir, g, restore);
                            var nx = h + d2 * L;
                            c.xOld[i] += (nx - c.x[i]) * s.attenuation;
                            c.x[i] = nx;
                            dir = d2;
                        }
                        // angle limit
                        dir = Vector3.RotateTowards(g, dir, c.limit[i], 0f).normalized;
                        c.x[i] = h + dir * L;
                        // colliders: as deep as the animation itself goes is allowed
                        if (c.capsules.Length > 0 && !NoColliders)
                        {
                            bool hit;
                            Vector3 nx = s.edge ? PushEdge(h, c.x[i], s.radius, c.capsules, hb[i], tb[i], out hit)
                                                : PushPoint(c.x[i], s.radius, c.capsules, tb[i], out hit);
                            nx = h + (nx - h).normalized * L;
                            if (hit)
                            {
                                // the leg carries the cloth along, it does not bat it away: most of the push
                                // does not become speed (all of it did: g04's front panels popped 24-39
                                // degrees in a step when a thigh came through), then friction
                                c.xOld[i] += (nx - c.x[i]) * s.pushAttenuation;
                                c.xOld[i] += (nx - c.xOld[i]) * s.friction;
                            }
                            c.x[i] = nx;
                        }
                        // backstop: a big sphere behind the animated tail, on the side of the body's axis
                        if (s.backstop && !NoBackstop)
                        {
                            var rel = tb[i] - cPos;
                            var nrm = (rel - Vector3.Dot(rel, axis) * axis).normalized;
                            var centre = tb[i] - nrm * (s.backstopRadius + s.backstopDistance);
                            var dv = c.x[i] - centre;
                            float dl = dv.magnitude;
                            if (dl < s.backstopRadius && dl > 1e-6f)
                                c.x[i] = h + (centre + dv / dl * s.backstopRadius - h).normalized * L;
                        }
                        // the floor: the bone turns up just enough; into the floor the speed is stopped
                        if (s.floor)
                        {
                            float lo = floor + s.radius;
                            var d = (c.x[i] - h).normalized;
                            float zmin = Mathf.Clamp((lo - h.y) / Mathf.Max(L, 1e-6f), -1f, 1f);
                            if (d.y < zmin)
                            {
                                var hor = new Vector3(d.x, 0f, d.z);
                                if (hor.sqrMagnitude < 1e-8f)
                                {
                                    var rel = c.x[i] - cPos;
                                    hor = rel - Vector3.Dot(rel, axis) * axis;
                                    hor.y = 0f;
                                    if (hor.sqrMagnitude < 1e-8f)
                                        hor = Vector3.forward;
                                }
                                hor.Normalize();
                                var nd = hor * Mathf.Sqrt(Mathf.Max(0f, 1f - zmin * zmin)) + Vector3.up * zmin;
                                var nx = h + nd * L;
                                var vel = c.x[i] - c.xOld[i];
                                vel.y = Mathf.Max(vel.y, 0f);
                                vel.x *= 1f - s.friction;
                                vel.z *= 1f - s.friction;
                                c.x[i] = nx;
                                c.xOld[i] = nx - vel;
                            }
                        }
                        dir = (c.x[i] - h).normalized;
                        c.head[i] = h;
                        // the bone's frame: the animated one carried by the parent, swung onto the simulated direction
                        c.rot[i] = Quaternion.FromToRotation(g, dir) * D * c.rb[i];
                    }
                }
            }
        }

        static Vector3 Turn(Vector3 a, Vector3 b, float fraction)
        {
            float angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
            return angle < 1e-6f ? b : Vector3.RotateTowards(a, b, angle * fraction, 0f).normalized;
        }

        // ---- colliders

        static float PointDepth(Vector3 p, float r, Capsule cap, out Vector3 normal)
        {
            Vector3 a = cap.a.position, ab = cap.b.position - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            var d = p - (a + t * ab);
            float dist = d.magnitude;
            normal = dist > 1e-9f ? d / dist : Vector3.up;
            return Mathf.Lerp(cap.ra, cap.rb, t) + r - dist;
        }

        static Vector3 PushPoint(Vector3 p, float r, Capsule[] caps, Vector3 animated, out bool hit)
        {
            hit = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool any = false;
                foreach (var cap in caps)
                {
                    float allow = Mathf.Max(0f, PointDepth(animated, r, cap, out _));
                    float pen = PointDepth(p, r, cap, out var n) - allow;
                    if (pen > 0f)
                    {
                        p += n * pen;
                        any = true;
                    }
                }
                hit |= any;
                if (!any)
                    break;
            }
            return p;
        }

        /// <summary>Closest points of segments p0-p1 and a-b: parameter s on the first, t on the second.</summary>
        static void Closest(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, out float s, out float t)
        {
            Vector3 d1 = p1 - p0, d2 = b - a, r = p0 - a;
            float A = Vector3.Dot(d1, d1), E = Vector3.Dot(d2, d2), F = Vector3.Dot(d2, r), C = Vector3.Dot(d1, r), Bd = Vector3.Dot(d1, d2);
            float den = A * E - Bd * Bd;
            s = den > 1e-12f ? Mathf.Clamp01((Bd * F - C * E) / den) : 0f;
            float tt = (Bd * s + F) / Mathf.Max(E, 1e-12f);
            t = Mathf.Clamp01(tt);
            if (!Mathf.Approximately(t, tt))
                s = Mathf.Clamp01((Bd * t - C) / Mathf.Max(A, 1e-12f));
        }

        static float EdgeDepth(Vector3 head, Vector3 tail, float r, Capsule cap, out Vector3 normal, out float s)
        {
            Vector3 a = cap.a.position, b = cap.b.position;
            Closest(head, tail, a, b, out s, out float t);
            var d = head + s * (tail - head) - (a + t * (b - a));
            float dist = d.magnitude;
            normal = dist > 1e-9f ? d / dist : Vector3.up;
            return Mathf.Lerp(cap.ra, cap.rb, t) + r - dist;
        }

        /// <summary>
        /// A bone segment against the capsules: its tail moves so the closest point clears them.  The
        /// tail moves pen / s for a contact at s along the segment (the lever); near the head that was up
        /// to 5x the depth in one go and the panel jumped, so the lever stops at 3x and one push at
        /// MaxPush - the rest follows in the next iterations and substeps.
        /// </summary>
        static Vector3 PushEdge(Vector3 head, Vector3 tail, float r, Capsule[] caps, Vector3 animHead, Vector3 animTail, out bool hit)
        {
            hit = false;
            var move = Vector3.zero;
            foreach (var cap in caps)
            {
                float allow = Mathf.Max(0f, EdgeDepth(animHead, animTail, r, cap, out _, out _));
                float pen = EdgeDepth(head, tail, r, cap, out var n, out float s) - allow;
                if (pen > 0f && s > 0.05f)
                {
                    move += n * (pen / Mathf.Max(s, 0.35f));
                    hit = true;
                }
            }
            return tail + Vector3.ClampMagnitude(move, MaxPush);
        }

        const float MaxPush = 0.025f;

        /// <summary>Write the simulated rotations, blended with the animated ones by ``weight``.</summary>
        void Write(ChainSet c)
        {
            if (weight <= 0f)
                return;
            foreach (var lv in c.levels)
                foreach (int i in lv)
                    c.bones[i].rotation = weight >= 1f ? c.rot[i] : Quaternion.Slerp(c.bones[i].rotation, c.rot[i], weight);
        }
    }
}
