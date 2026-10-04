using System.Collections.Generic;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// What a Unity humanoid leaves out of an Unreal Engine 5 / MetaHuman body (Vindictus' Fiona), put back on every pose
    /// whatever clip made it (FighterRig: Rest before the animation is evaluated, Distribute after it, DriveTwists on the
    /// final pose after the legs' IK):
    ///   - spine and neck: a humanoid has three spine bones and one neck, MetaHuman five and two.  The humanoid maps
    ///     spine_01 / 03 / 05 and neck_01, so the bend of spine_02, spine_04 and neck_02 lands in the mapped bone above
    ///     each: her chest and head were 3.2 cm from the game's pose on average (12 cm at worst) in her own clips.  Each
    ///     unmapped joint takes back a share of the pair's bend (spine02, spine04, neck02), the upper bone keeping its turn;
    ///     the defaults are fitted on her 90 game clips by where they put her chest and head (1.3 cm then;
    ///     tools/vdf_research/spine_share_sim.py).
    ///   - twist bones: a humanoid does not move them.  As in the game, each turns by where it sits along its limb: the
    ///     forearm's and shin's with that share of the hand's / foot's roll about the limb (1/3 and 2/3 of the way from
    ///     the elbow: 1/3 and 2/3 of it - the game's clips have 0.35 and 0.72 in a sword swing), the upper arm's and
    ///     thigh's undoing the limb's own roll towards the shoulder / hip (1/3 of the way: -2/3 of it; the game's -0.71).
    ///     Left alone, her wrists twisted like a sweet wrapper when the sword hand rolled (160 degrees in her first attack).
    /// Bind rotations are taken when the component wakes (the prefab's bind pose).
    /// </summary>
    public class RoeUeRig : MonoBehaviour
    {
        [Tooltip("Share of the bend between spine_01 and spine_03 that spine_02 takes back (0: as the humanoid leaves it)")]
        [Range(0f, 1f)] public float spine02 = 0.8f;
        [Tooltip("Share of the bend between spine_03 and spine_05 that spine_04 takes back")]
        [Range(0f, 1f)] public float spine04 = 0.3f;
        [Tooltip("Share of the bend between neck_01 and the head that neck_02 takes back")]
        [Range(0f, 1f)] public float neck02 = 0f;
        [Tooltip("How much of their share of the roll the twist bones take (0: they stay with their limb)")]
        [Range(0f, 1f)] public float twist = 1f;

        class Pair
        {
            public Transform low, high;
            public Quaternion lowRest, highRest;
            public System.Func<float> share;
        }

        class Twist
        {
            public Transform bone, limb, driver;   // the twist bone, the limb bone it hangs on, the bone whose roll it shares
            public Quaternion boneRest, driverRest;
            public Vector3 axis;                    // along the limb, in the limb bone's frame
            public float share;                     // of the driver's roll (negative: undoes the limb's own roll)
            public bool own;                        // the driver is the limb itself (upper arm, thigh)
        }

        readonly List<Pair> pairs = new List<Pair>();
        readonly List<Twist> twists = new List<Twist>();
        bool built;

        public int TwistBones => twists.Count;

        void Awake() => Build();

        /// <summary>An Unreal / MetaHuman body: pelvis, spine_01..05, neck_01/02.</summary>
        public static bool IsUe(Transform root)
        {
            bool pelvis = false, spine = false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                pelvis |= t.name == "pelvis";
                spine |= t.name == "spine_02";
            }
            return pelvis && spine;
        }

        public void Build()
        {
            if (built)
                return;
            built = true;
            var byName = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name))
                    byName[t.name] = t;
            Transform Get(string n) => byName.TryGetValue(n, out var t) ? t : null;

            void AddPair(string low, string high, System.Func<float> share)
            {
                var l = Get(low);
                var h = Get(high);
                if (l != null && h != null && h.parent == l)
                    pairs.Add(new Pair { low = l, high = h, lowRest = l.localRotation, highRest = h.localRotation, share = share });
            }
            AddPair("spine_02", "spine_03", () => spine02);
            AddPair("spine_04", "spine_05", () => spine04);
            AddPair("neck_02", "head", () => neck02);

            // limb bone, the joint below it, the bone whose roll the twist bones share, own roll or the next joint's
            foreach (var s in new[] { "l", "r" })
            {
                AddLimb(Get($"upperarm_{s}"), Get($"lowerarm_{s}"), Get($"upperarm_{s}"), "upperarm_twist_", s, byName);
                AddLimb(Get($"lowerarm_{s}"), Get($"hand_{s}"), Get($"hand_{s}"), "lowerarm_twist_", s, byName);
                AddLimb(Get($"thigh_{s}"), Get($"calf_{s}"), Get($"thigh_{s}"), "thigh_twist_", s, byName);
                AddLimb(Get($"calf_{s}"), Get($"foot_{s}"), Get($"foot_{s}"), "calf_twist_", s, byName);
            }
        }

        void AddLimb(Transform limb, Transform next, Transform driver, string prefix, string side, Dictionary<string, Transform> byName)
        {
            if (limb == null || next == null || driver == null)
                return;
            var along = next.localPosition;
            float length = along.magnitude;
            if (length < 1e-4f)
                return;
            var axis = along / length;
            bool own = driver == limb;
            foreach (Transform child in limb)
            {
                if (!child.name.StartsWith(prefix) || !child.name.EndsWith("_" + side))
                    continue;
                // where it sits along the limb: 0 at the limb bone, 1 at the next joint
                float f = Mathf.Clamp01(Vector3.Dot(child.localPosition, axis) / length);
                twists.Add(new Twist
                {
                    bone = child, limb = limb, driver = driver, own = own, axis = axis,
                    boneRest = child.localRotation, driverRest = driver.localRotation,
                    share = own ? f - 1f : f,
                });
            }
        }

        /// <summary>Before the animation is evaluated: the unmapped spine and neck joints at rest (the humanoid expects them there).</summary>
        public void Rest()
        {
            foreach (var p in pairs)
                p.low.localRotation = p.lowRest;
        }

        /// <summary>After it: each pair's bend shared between its two joints, the upper bone's turn kept.</summary>
        public void Distribute()
        {
            foreach (var p in pairs)
            {
                float s = p.share();
                if (s <= 0f)
                    continue;
                var turn = p.low.localRotation * p.high.localRotation;
                var bend = Quaternion.Inverse(p.lowRest * p.highRest) * turn;            // in the upper bone's rest frame
                var lowBend = p.highRest * bend * Quaternion.Inverse(p.highRest);         // the same, in the lower one's
                p.low.localRotation = p.lowRest * Quaternion.Slerp(Quaternion.identity, lowBend, s);
                p.high.localRotation = p.highRest * Quaternion.Slerp(Quaternion.identity, bend, 1f - s);
            }
        }

        /// <summary>On the final pose: each twist bone turned by its share of the roll about its limb.</summary>
        public void DriveTwists()
        {
            if (twist <= 0f)
            {
                foreach (var t in twists)
                    t.bone.localRotation = t.boneRest;
                return;
            }
            foreach (var t in twists)
            {
                // the limb's own roll (in its own frame), or the next joint's (in the limb's frame)
                var delta = t.own ? Quaternion.Inverse(t.driverRest) * t.driver.localRotation
                                  : t.driver.localRotation * Quaternion.Inverse(t.driverRest);
                float roll = Roll(delta, t.axis);
                t.bone.localRotation = Quaternion.AngleAxis(roll * t.share * twist, t.axis) * t.boneRest;
            }
        }

        /// <summary>The twist of a rotation about an axis (swing-twist), degrees in -180..180.</summary>
        public static float Roll(Quaternion q, Vector3 axis)
        {
            var v = new Vector3(q.x, q.y, q.z);
            float along = Vector3.Dot(v, axis);
            float angle = 2f * Mathf.Atan2(along, q.w) * Mathf.Rad2Deg;
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            return angle;
        }
    }
}
