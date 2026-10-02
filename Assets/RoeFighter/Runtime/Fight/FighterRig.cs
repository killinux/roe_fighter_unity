using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace RoeFighter.Fight
{
    /// <summary>
    /// One fighter's body: the high-detail humanoid model and the game's own unit rig for skills.
    ///
    ///   model    our humanoid character (Generated/&lt;id&gt;/&lt;id&gt;_fighter.prefab).  A playable graph
    ///            drives it in three layers:
    ///              0  the game's battle stance, always: it carries the hair, fan, skirt and weapon curves;
    ///              1  the basic moves from motion capture, crossfaded among themselves - humanoid
    ///                 curves only, so the other bones keep the stance's values (a closed fan);
    ///              2  the game's own clips converted to humanoid (hurt, fall, skills, poses), which
    ///                 move every bone; this layer fades in over the others while one of them plays.
    ///            (One mixer for both kinds would blend the bones the mocap does not move towards
    ///            the bind pose: the fan opened wide during every punch.)
    ///   unit     the game's unit prefab (gameplay_prefab_*), cleaned up in the editor.  Its
    ///            timelines animate a hidden low-detail copy of the character that carries the
    ///            effect anchors; during a skill the timeline runs in step with the model, so
    ///            the game's effects appear where the game puts them.
    /// Nothing advances by itself: the fight calls Tick(dt) once per simulation step.
    /// </summary>
    public class FighterRig : MonoBehaviour
    {
        [Serializable]
        public class NamedClip
        {
            public string name;
            public AnimationClip clip;
            public bool loop;
            public bool game;       // the game's clip: moves every bone (layer 2); else motion capture (layer 1)
        }

        [Serializable]
        public class NamedDirector
        {
            public string action;
            public PlayableDirector director;
        }

        [Serializable]
        public class NamedSound
        {
            public string name;
            public AudioClip clip;
        }

        public string id;
        public string displayName;
        public Animator animator;
        public AnimationClip stance;                 // layer 0: the game's battle stance
        public List<NamedClip> clips = new List<NamedClip>();
        public List<NamedDirector> directors = new List<NamedDirector>();
        public List<NamedSound> sounds = new List<NamedSound>();
        public TextAsset skillSheetJson;
        public Renderer[] weaponRenderers;           // shown only during skills (a08's sword)
        public AudioSource audioSource;
        public Transform unitRoot;

        [NonSerialized] public RoeSkillSheet sheet;
        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable mocapMixer, gameMixer;
        AnimationClipPlayable stancePlayable;
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        AnimationClipPlayable[] playables;
        int[] slot;                         // input of the clip in its mixer
        float[] weight, target, rate;
        float gameWeight, gameTarget;
        int current = -1;
        float fade = 0.12f;
        float stanceTime;
        PlayableDirector activeDirector;
        bool weaponsShown = true;
        RoeHelperRig helpers;
        Transform hips;
        Transform[] toes, feet;
        bool grounded;
        float toeSole, ankleSole, groundOffset;

        public bool Ready => graph.IsValid();

        /// <summary>
        /// The basic moves from a motion pack: every clip that is not the game's own is replaced by the
        /// pack's (named by role).  Takes effect at the next Init().
        /// </summary>
        public void UseMotions(MotionPack pack)
        {
            if (pack == null)
                return;
            clips.RemoveAll(c => !c.game);
            int at = 0;
            foreach (var c in pack.clips.Where(c => c.clip != null))
                clips.Insert(at++, new NamedClip { name = c.role, clip = c.clip, loop = c.loop });
            motions = pack;
        }

        [NonSerialized] public MotionPack motions;
        public string Current => current >= 0 ? clips[current].name : "";
        public float CurrentTime => current >= 0 ? (float)playables[current].GetTime() : 0f;
        public float CurrentRate => current >= 0 ? rate[current] : 0f;

        public void Init()
        {
            if (graph.IsValid())
                graph.Destroy();
            if (skillSheetJson != null)
                sheet = RoeSkillSheet.FromJson(skillSheetJson.text);
            // The Animator takes its default values from the bones as they are now: put the model in
            // its battle stance first, so bones no clip animates rest there (closed fan, not the bind pose).
            if (stance != null)
                stance.SampleAnimation(animator.gameObject, 0f);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // the soles: how high the toe and ankle bones are when the game's stance stands on the floor
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            toes = new[] { animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes) };
            feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            grounded = stance != null && toes.All(t => t != null) && feet.All(t => t != null);
            if (grounded)
            {
                float floor = animator.transform.position.y;
                toeSole = toes.Average(t => t.position.y) - floor;
                ankleSole = feet.Average(t => t.position.y) - floor;
            }
            helpers = animator.GetComponent<RoeHelperRig>();
            // skirts, hair, chains and breasts: found and measured in the stance pose
            boneCloth = useCloth ? new RoeBoneCloth(animator, helpers != null ? helpers.drives.Select(d => d.helper).ToList() : null) : null;
            if (boneCloth != null)
                Debug.Log($"[ROE] {id} bone cloth: {boneCloth.Report}");
            animator.Rebind();
            legs = null;
            var legBones = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                                   HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }.Select(animator.GetBoneTransform).ToArray();
            if (legBones.All(b => b != null))
                legs = new[]
                {
                    new Leg { upper = legBones[0], lower = legBones[1], foot = legBones[2] },
                    new Leg { upper = legBones[3], lower = legBones[4], foot = legBones[5] },
                };
            stepping = legsPlanted = false;
            stepWeight = 0f;
            nextLeg = -1;
            lockingFeet = false;
            lockWeight = 0f;
            System.Array.Clear(contactDown, 0, contactDown.Length);
            stanceTime = 0f;

            graph = PlayableGraph.Create($"{id} fighter");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "animation", animator);
            layers = AnimationLayerMixerPlayable.Create(graph, 3);
            output.SetSourcePlayable(layers);
            stancePlayable = AnimationClipPlayable.Create(graph, stance);
            stancePlayable.SetApplyFootIK(false);
            graph.Connect(stancePlayable, 0, layers, 0);
            layers.SetInputWeight(0, 1f);

            int mocapCount = clips.Count(c => !c.game), gameCount = clips.Count(c => c.game);
            mocapMixer = AnimationMixerPlayable.Create(graph, mocapCount);
            gameMixer = AnimationMixerPlayable.Create(graph, gameCount);
            graph.Connect(mocapMixer, 0, layers, 1);
            graph.Connect(gameMixer, 0, layers, 2);
            layers.SetInputWeight(1, 1f);
            layers.SetInputWeight(2, 0f);
            playables = new AnimationClipPlayable[clips.Count];
            slot = new int[clips.Count];
            weight = new float[clips.Count];
            target = new float[clips.Count];
            rate = new float[clips.Count];
            index.Clear();
            int nextMocap = 0, nextGame = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                index[clips[i].name] = i;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i].clip);
                playables[i].SetApplyFootIK(false);
                playables[i].Pause();       // time is set by Tick, not by the graph
                slot[i] = clips[i].game ? nextGame++ : nextMocap++;
                graph.Connect(playables[i], 0, Mixer(i), slot[i]);
                Mixer(i).SetInputWeight(slot[i], 0f);
                rate[i] = 1f;
            }
            // the mocap layer always holds a pose (all weights zero would be the bind pose)
            if (index.TryGetValue("guard", out int guard))
                weight[guard] = target[guard] = 1f;
            gameWeight = gameTarget = 0f;
            ApplyWeights();
            foreach (var d in directors)
            {
                if (d.director == null)
                    continue;
                d.director.playOnAwake = false;
                d.director.timeUpdateMode = DirectorUpdateMode.Manual;
            }
            current = -1;
            ShowWeapons(false);

            strideSpeeds.Clear();
            foreach (var name in new[] { "walk", "walk_back", "run" })
                if (index.ContainsKey(name))
                    strideSpeeds[name] = MeasureStride(name);
            // back to the guard, as if nothing had played
            for (int i = 0; i < clips.Count; i++)
            {
                weight[i] = target[i] = 0f;
                rate[i] = 1f;
                playables[i].SetTime(0);
            }
            if (index.TryGetValue("guard", out guard))
                weight[guard] = target[guard] = 1f;
            gameWeight = gameTarget = 0f;
            ApplyWeights();
            current = -1;
            stanceTime = 0f;
            boneCloth?.Reset();
        }

        readonly Dictionary<string, float> strideSpeeds = new Dictionary<string, float>();

        /// <summary>
        /// How fast a planted foot travels back under the body in a locomotion clip played in place
        /// at rate 1 (m/s).  The fight moves the body at its own speed; playing the clip at
        /// speed / this keeps the planted foot still on the floor.  Measured on this body in Init.
        /// </summary>
        public float StrideSpeed(string clip) => strideSpeeds.TryGetValue(clip, out var v) && v > 0.05f ? v : 1f;

        float MeasureStride(string clip)
        {
            if (toes == null || toes.Any(b => b == null))
                return 0f;
            const float dt = 1f / 30f;
            Play(clip, 1f, 0f);
            float length = Length(clip);
            var heights = new List<Vector2>[] { new List<Vector2>(), new List<Vector2>() };   // per ball: (height, forward)
            for (float t = 0f; t < length; t += dt)
            {
                Tick(t == 0f ? 0f : dt);
                for (int k = 0; k < 2; k++)
                {
                    var p = transform.InverseTransformPoint(toes[k].position);
                    heights[k].Add(new Vector2(p.y, p.z));
                }
            }
            // the ball of a foot is planted within 1 cm of its lowest point (these heels stand on it)
            float sum = 0f;
            int n = 0;
            for (int k = 0; k < 2; k++)
            {
                float floor = heights[k].Min(h => h.x) + 0.01f;
                for (int f = 1; f < heights[k].Count; f++)
                    if (heights[k][f].x < floor && heights[k][f - 1].x < floor)
                    {
                        sum += (heights[k][f].y - heights[k][f - 1].y) / dt;
                        n++;
                    }
            }
            return n > 0 ? Mathf.Abs(sum / n) : 0f;
        }

        /// <summary>The stride speeds measured in Init, for the log.</summary>
        public string StrideReport() => string.Join(", ", strideSpeeds.Select(kv => $"{kv.Key} {kv.Value:F2} m/s"));

        void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        AnimationMixerPlayable Mixer(int clip) => clips[clip].game ? gameMixer : mocapMixer;

        public bool Has(string clip) => index.ContainsKey(clip);

        public float Length(string clip) => index.TryGetValue(clip, out int i) && clips[i].clip != null ? clips[i].clip.length : 0f;

        /// <summary>Crossfade to a clip.  restart = from its start; otherwise it keeps its own time.</summary>
        public void Play(string clip, float speed = 1f, float fadeSeconds = 0.12f, bool restart = true, float startTime = 0f)
        {
            if (!index.TryGetValue(clip, out int i))
                return;
            fade = Mathf.Max(0.0001f, fadeSeconds);
            if (restart || current != i)
                playables[i].SetTime(startTime);
            rate[i] = speed;
            bool game = clips[i].game;
            // a crossfade among the clips of the same layer; the other layer keeps its pose underneath
            for (int k = 0; k < target.Length; k++)
                if (clips[k].game == game)
                    target[k] = k == i ? 1f : 0f;
            gameTarget = game ? 1f : 0f;
            if (fadeSeconds <= 0f)
            {
                for (int k = 0; k < weight.Length; k++)
                    weight[k] = target[k];
                gameWeight = gameTarget;
                ApplyWeights();
            }
            current = i;
        }

        public void SetSpeed(float speed)
        {
            if (current >= 0)
                rate[current] = speed;
        }

        public void SetTime(float time)
        {
            if (current >= 0)
                playables[current].SetTime(time);
        }

        /// <summary>Advance the clips and evaluate the pose (and a running skill timeline).</summary>
        public void Tick(float dt)
        {
            if (!graph.IsValid())
                return;
            stanceTime += dt;
            if (stance != null)
                stancePlayable.SetTime(stanceTime % Mathf.Max(0.01f, stance.length));
            float step = dt / fade;
            gameWeight = Mathf.MoveTowards(gameWeight, gameTarget, step);
            for (int i = 0; i < playables.Length; i++)
            {
                if (weight[i] <= 0f && target[i] <= 0f)
                    continue;
                weight[i] = Mathf.MoveTowards(weight[i], target[i], step);
                double t = playables[i].GetTime() + dt * rate[i];
                var c = clips[i];
                if (c.loop && c.clip.length > 0f)
                    t %= c.clip.length;
                else
                    t = Math.Min(t, c.clip.length);
                playables[i].SetTime(t);
            }
            ApplyWeights();
            boneCloth?.Rest();                // cloth bones no clip animates start from their rest pose again
            graph.Evaluate(0f);
            StepLinger(dt);
            float mocap = 1f - gameWeight;
            // motion capture on the floor: retargeted onto these long-legged, high-heeled bodies it
            // floats 11-18 cm; the lowest sole goes down to the floor (the game's clips stay as they are)
            if (grounded && mocap > 0f)
            {
                float lowest = float.MaxValue;
                for (int i = 0; i < 2; i++)
                    lowest = Mathf.Min(lowest, Mathf.Min(toes[i].position.y - toeSole, feet[i].position.y - ankleSole));
                groundOffset = animator.transform.position.y - lowest;
                hips.position += Vector3.up * (groundOffset * mocap);
            }
            TrackContact();                 // on the animated pose, before the legs are bent to locked feet
            StepLegs(dt, mocap);
            LockFeet(dt, mocap);
            // the limb helpers: the game's clips animate them; under motion capture they follow the limbs
            if (helpers != null)
                helpers.Apply(mocap);
            // bone cloth on the finished pose; the game's own clips keep their hand-keyed skirts and hair
            if (boneCloth != null)
            {
                boneCloth.weight = mocap * clothWeight;
                boneCloth.Step(dt, transform.position.y);
            }
        }

        // ---- bone cloth (RoeBoneCloth): skirts, hair, chains, breasts

        public bool useCloth = true;
        [Range(0f, 1f)] public float clothWeight = 1f;
        RoeBoneCloth boneCloth;

        /// <summary>The cloth starts again from the animated pose (after a teleport: a new round).</summary>
        public void ResetCloth() => boneCloth?.Reset();

        public string ClothReport => boneCloth?.Report ?? "off";

        // ---- walking: a foot the clip puts down stays where it landed (two-bone IK)

        bool lockingFeet;
        float lockWeight;

        /// <summary>While walking: feet the clip puts down are locked where they landed.</summary>
        public void LockFeet(bool on) => lockingFeet = on;

        void LockFeet(float dt, float mocap)
        {
            if (legs == null || !grounded)
                return;
            if (dt > 0f)
                lockWeight = Mathf.MoveTowards(lockWeight, lockingFeet ? 1f : 0f, dt / 0.1f);
            for (int i = 0; i < 2; i++)
            {
                var l = legs[i];
                var ankle = l.foot.position;
                var local = transform.InverseTransformPoint(ankle);
                var ball = transform.InverseTransformPoint(toes[i].position);
                bool down = lockWeight > 0f && mocap > 0f && (local.y < ankleSole + 0.015f || ball.y < toeSole + 0.012f);
                if (down && !l.locked)
                {
                    l.locked = true;
                    l.lockAt = ankle;
                }
                else if (!down)
                    l.locked = false;
                if (l.locked && Flat(l.lockAt - ankle).magnitude > 0.25f)   // the body went on too far: let go
                    l.locked = false;
                if (dt > 0f)
                    l.lockBlend = Mathf.MoveTowards(l.lockBlend, l.locked ? 1f : 0f, dt / 0.08f);
                if (l.lockBlend <= 0f)
                    continue;
                var at = new Vector3(l.lockAt.x, ankle.y, l.lockAt.z);
                TwoBoneIK(l.upper, l.lower, l.foot, Vector3.Lerp(ankle, at, l.lockBlend * lockWeight * mocap), transform.forward);
            }
        }

        // ---- the planted foot, for walking: the body goes where the foot pushes it

        readonly Vector3[] lastContact = new Vector3[4];
        readonly bool[] contactDown = new bool[4];

        /// <summary>How far the planted parts of the feet (balls, heels) moved under the body in the last Tick (in the fighter's frame, flat).</summary>
        public Vector3 ContactDelta { get; private set; }
        public bool HasContact { get; private set; }

        void TrackContact()
        {
            HasContact = false;
            ContactDelta = Vector3.zero;
            if (!grounded)
                return;
            var sum = Vector3.zero;
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                // 0, 1 the balls of the feet; 2, 3 the ankles (a heel coming down)
                var bone = k < 2 ? toes[k] : feet[k - 2];
                var p = transform.InverseTransformPoint(bone.position);
                bool down = p.y < (k < 2 ? toeSole : ankleSole) + 0.012f;
                if (down && contactDown[k])
                {
                    sum += p - lastContact[k];
                    n++;
                }
                contactDown[k] = down;
                lastContact[k] = p;
            }
            if (n > 0)
            {
                HasContact = true;
                ContactDelta = Flat(sum / n);
            }
        }

        // ---- side steps: procedural legs
        //
        // The motion capture has no step to the side while facing forward, and a made-up clip cannot
        // keep pace with the speed the fight moves the body at: the feet slid.  While the fighter
        // steps sideways the legs step by themselves.  The upper body keeps the guard; a planted foot
        // stays where it landed; when it lags behind its place under the body it swings in an arc to
        // a point ahead (lead foot first, then the other), and two-bone IK bends the leg to the foot.

        class Leg
        {
            public Transform upper, lower, foot;
            public Vector3 planted, from, to;
            public bool swinging;
            public float t;
            public bool locked;            // walking: the foot is held where it landed
            public Vector3 lockAt;
            public float lockBlend;
        }

        Leg[] legs;
        bool stepping, legsPlanted;
        Vector3 stepVelocity;
        float stepWeight;
        int nextLeg = -1;
        public const float SwingTime = 0.17f, SwingLift = 0.07f;

        /// <summary>The fighter steps sideways at this velocity (zero: it does not).</summary>
        public void SideStep(Vector3 velocity)
        {
            stepping = velocity.sqrMagnitude > 1e-4f;
            if (stepping)
                stepVelocity = velocity;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void StepLegs(float dt, float mocap)
        {
            if (legs == null)
                return;
            if (dt > 0f)
                stepWeight = Mathf.MoveTowards(stepWeight, stepping ? 1f : 0f, dt / (stepping ? 0.06f : 0.15f));
            if (stepWeight <= 0f || mocap <= 0f)
            {
                legsPlanted = false;
                nextLeg = -1;
                return;
            }
            var home = new[] { legs[0].foot.position, legs[1].foot.position };     // where the animation puts the ankles
            if (!legsPlanted)
            {
                for (int i = 0; i < 2; i++)
                {
                    legs[i].planted = home[i];
                    legs[i].swinging = false;
                }
                legsPlanted = true;
            }
            foreach (var l in legs)
            {
                if (!l.swinging)
                    continue;
                l.t += dt / SwingTime;
                if (l.t >= 1f)
                {
                    l.swinging = false;
                    l.planted = l.to;
                }
            }
            if (stepping && !legs[0].swinging && !legs[1].swinging)
            {
                var dir = stepVelocity.normalized;
                var centre = (home[0] + home[1]) * 0.5f;
                if (nextLeg < 0)
                    nextLeg = Vector3.Dot(home[0] - centre, dir) >= Vector3.Dot(home[1] - centre, dir) ? 0 : 1;
                var l = legs[nextLeg];
                if (Flat(home[nextLeg] - l.planted).magnitude > stepVelocity.magnitude * SwingTime * 0.5f)
                {
                    l.swinging = true;
                    l.t = 0f;
                    l.from = l.planted;
                    // under its place in the middle of the next stance
                    l.to = home[nextLeg] + Flat(stepVelocity) * (SwingTime * 1.5f);
                    nextLeg = 1 - nextLeg;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var l = legs[i];
                var p = l.planted;
                if (l.swinging)
                    p = Vector3.Lerp(l.from, l.to, Mathf.SmoothStep(0f, 1f, l.t)) + Vector3.up * (SwingLift * Mathf.Sin(Mathf.PI * l.t));
                TwoBoneIK(l.upper, l.lower, l.foot, Vector3.Lerp(home[i], p, stepWeight * mocap), transform.forward);
            }
        }

        /// <summary>Bend upper and lower bone so that the end bone reaches the target; the end keeps its world rotation.</summary>
        static void TwoBoneIK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bendHint)
        {
            var endRotation = end.rotation;
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude;
            var toTarget = target - a;
            if (la < 1e-4f || lb < 1e-4f || toTarget.sqrMagnitude < 1e-8f)
                return;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            var u = toTarget.normalized;
            var pole = (b - a) - Vector3.Dot(b - a, u) * u;          // the knee keeps its side
            if (pole.sqrMagnitude < 1e-6f)
                pole = bendHint - Vector3.Dot(bendHint, u) * u;
            pole.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            var knee = a + la * (cosA * u + Mathf.Sqrt(1f - cosA * cosA) * pole);
            upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, a + u * d - lower.position) * lower.rotation;
            end.rotation = endRotation;
        }

        /// <summary>Vertical shift that put the motion capture on the floor in the last step (for checks).</summary>
        public float GroundOffset => groundOffset;

        /// <summary>
        /// The mixers' input weights from the clip weights, every clip every time, normalized per
        /// mixer.  A humanoid mixer adds its inputs up as they are: two clips at weight 1 put the
        /// hips twice as high and every helper bone twice as far from its parent (a stale weight
        /// left by an instant switch did that from the second round on: g04 floated with spikes
        /// for shins), and weights that sum to less than 1 pull the pose towards the T-pose.
        /// </summary>
        void ApplyWeights()
        {
            float mocapSum = 0f, gameSum = 0f;
            for (int i = 0; i < weight.Length; i++)
            {
                if (clips[i].game)
                    gameSum += weight[i];
                else
                    mocapSum += weight[i];
            }
            for (int i = 0; i < weight.Length; i++)
            {
                float sum = clips[i].game ? gameSum : mocapSum;
                Mixer(i).SetInputWeight(slot[i], sum > 1e-5f ? weight[i] / sum : 0f);
            }
            layers.SetInputWeight(2, gameWeight);
        }

        /// <summary>The clips the mixers actually blend (input weight), for checks.</summary>
        public string MixerWeights()
        {
            if (!graph.IsValid())
                return "no graph";
            var parts = new List<string>();
            for (int i = 0; i < clips.Count; i++)
            {
                float w = Mixer(i).GetInputWeight(slot[i]);
                if (w > 0f || weight[i] > 0f || target[i] > 0f)
                    parts.Add($"{clips[i].name} {w:F2} (weight {weight[i]:F2} target {target[i]:F2})");
            }
            return $"game layer {layers.GetInputWeight(2):F2}: " + string.Join(", ", parts);
        }

        // ---- skills: the game's timeline in step with the model

        bool lingering;
        float lingerTime;

        /// <summary>
        /// The skill is over for the body, but its timeline plays on to its end by itself (its
        /// effects fade out instead of vanishing); a new skill or ShowTimeline(null) stops it.
        /// </summary>
        public void LingerTimeline(float time)
        {
            if (activeDirector == null)
                return;
            lingering = true;
            lingerTime = time;
        }

        void StepLinger(float dt)
        {
            if (!lingering || activeDirector == null)
                return;
            lingerTime += dt;
            if (lingerTime >= activeDirector.duration)
            {
                activeDirector.Stop();
                activeDirector = null;
                lingering = false;
                return;
            }
            activeDirector.time = lingerTime;
            activeDirector.Evaluate();
        }

        /// <summary>Start (or keep showing) a skill's timeline at a time; null stops it.</summary>
        public void ShowTimeline(string action, float time)
        {
            lingering = false;
            var director = action == null ? null : directors.FirstOrDefault(d => d.action == action)?.director;
            if (director != activeDirector)
            {
                if (activeDirector != null)
                    activeDirector.Stop();
                activeDirector = director;
                if (director != null)
                    director.RebuildGraph();
            }
            if (director == null)
                return;
            director.time = Mathf.Clamp(time, 0f, (float)director.duration);
            director.Evaluate();
        }

        public void ShowWeapons(bool show)
        {
            if (show == weaponsShown || weaponRenderers == null)
                return;
            foreach (var r in weaponRenderers)
                if (r != null)
                    r.enabled = show;
            weaponsShown = show;
        }

        public Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        public void Sound(string name, float volume = 1f)
        {
            if (string.IsNullOrEmpty(name) || audioSource == null || !Application.isPlaying)
                return;
            var s = sounds.FirstOrDefault(x => x.name == name);
            if (s?.clip != null)
                audioSource.PlayOneShot(s.clip, volume);
        }

        /// <summary>The anchor the game uses for effects on this character ("center", "hurt", ...).</summary>
        public Transform Anchor(string name)
        {
            var node = sheet?.anchors.FirstOrDefault(a => a.name == name)?.node;
            var t = node == null || string.IsNullOrEmpty(node.path) || unitRoot == null ? null : unitRoot.Find(node.path);
            return t != null ? t : transform;
        }
    }
}
