using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// The skirt's animation pose under motion capture: the skirt follows the legs.
    ///
    /// The game keys its skirts by hand in every clip, following the legs; motion capture has no skirt,
    /// so the battle stance's skirt stayed pinned to the pelvis and every step put a thigh through it.
    /// Magica Cloth 2 asks for an animation pose that follows the legs (an artist's skinning or keys, or
    /// its Custom Skinning) with the simulation laid over it (docs/magica-cloth-2.md); this is that.
    ///
    /// Each skirt bone's tail is skinned to six drivers - the hips' heading (hanging), the pelvis, both
    /// thighs and both calves - with weights RoeHelperFit.FitSkirt fitted to the game's own skirt keys
    /// (linear blend skinning of the tail point, weights on the simplex).  At run time the rest of both
    /// the drivers and the skirt is the game's battle stance, as the fit has it: each driver carries the
    /// bones weighted to it from where it is in the stance to where it is now (RestOnStance).  Each bone is
    /// turned (swing only, its roll stays) to point from its skinned head to its skinned tail; the bone
    /// cloth swings from there.
    ///
    /// The first version (10-02, RestOnStance false) put the drivers' rest in the motion-capture guard
    /// instead, so that in the guard the skirt hung exactly as in the stance against the hips' heading.
    /// But the stance's pelvis is tilted (the right hip ~23 degrees up; 25-31 degrees off the guard's
    /// pelvis in all, RoeFightProbe.SkirtRests) while the guard's is about level, and a panel the game
    /// keys on the pelvis has to tilt with it: g04's back panel, which lies on her right buttock in the
    /// game, stood 3.4 cm off it in the guard (RoeFightProbe.ButtGap; 0.6 with the stance rest), and
    /// a08's skirt sat 3.5-3.7 cm into her legs in the guard and the steps (0.3-0.6 with the stance
    /// rest, RoeFightProbe.SkirtDepth, Bandai moves).
    /// </summary>
    public class RoeSkirtRig : MonoBehaviour
    {
        public const int Drivers = 6;
        public static readonly string[] DriverNames = { "heading", "pelvis", "L thigh", "R thigh", "L calf", "R calf" };

        [Serializable]
        public class Joint
        {
            public Transform bone;
            public int parent = -1;              // the joint of the bone's parent, if that is a skirt bone too
            public Vector3 tailLocal;            // the point the weights place, in the bone's frame
            public float[] weights = new float[Drivers];
            public float fitError, rigidError;   // degrees (RMS of the bone direction) on clips the fit did not see
        }

        public List<Joint> joints = new List<Joint>();          // parents before children
        public Transform hips, leftThigh, rightThigh, leftCalf, rightCalf;
        [TextArea] public string fitReport;

        /// <summary>
        /// The drivers' rest: the game's battle stance (true to the fit: a bone the game keys on the pelvis
        /// keeps its place on the pelvis - g04's back panel lies on the buttocks) or the motion-capture guard
        /// (the guard shows the stance's skirt as it hangs against the hips' heading, whatever the pelvis).
        /// </summary>
        public static bool RestOnStance = true;

        // the rests, measured on this body by FighterRig.Init
        Vector3[] stanceTail, stanceHead;                        // in the heading frame
        Vector3[] stancePos = new Vector3[Drivers];
        Quaternion[] stanceRot = new Quaternion[Drivers];
        Vector3[] guardPos;
        Vector4[] guardRotSum;
        Quaternion[] guardRot;
        int guardSamples;
        Vector3[] predicted, aimedFrom;
        Matrix4x4[] carry = new Matrix4x4[Drivers];

        public bool Ready => stanceTail != null && guardRot != null && joints.Count > 0;

        Transform DriverBone(int k) => k switch { 1 => hips, 2 => leftThigh, 3 => rightThigh, 4 => leftCalf, 5 => rightCalf, _ => null };

        /// <summary>In the battle stance: where each tail is against the hips' heading.</summary>
        public void CaptureStance(Vector3 headingPos, Quaternion headingRot)
        {
            stanceTail = new Vector3[joints.Count];
            stanceHead = new Vector3[joints.Count];
            var inv = Quaternion.Inverse(headingRot);
            for (int k = 1; k < Drivers; k++)
            {
                var b = DriverBone(k);
                stancePos[k] = b != null ? inv * (b.position - headingPos) : Vector3.zero;
                stanceRot[k] = b != null ? inv * b.rotation : Quaternion.identity;
            }
            for (int j = 0; j < joints.Count; j++)
                if (joints[j].bone != null)
                {
                    stanceTail[j] = inv * (joints[j].bone.TransformPoint(joints[j].tailLocal) - headingPos);
                    stanceHead[j] = inv * (joints[j].bone.position - headingPos);
                }
        }

        public void BeginGuard()
        {
            guardPos = new Vector3[Drivers];
            guardRotSum = new Vector4[Drivers];
            guardRot = null;
            guardSamples = 0;
        }

        /// <summary>One pose of the guard: each driver against the hips' heading.</summary>
        public void AddGuard(Vector3 headingPos, Quaternion headingRot)
        {
            var inv = Quaternion.Inverse(headingRot);
            for (int k = 1; k < Drivers; k++)
            {
                var b = DriverBone(k);
                if (b == null)
                    continue;
                guardPos[k] += inv * (b.position - headingPos);
                var q = inv * b.rotation;
                var first = guardRotSum[k];
                float s = first.sqrMagnitude > 0f && Vector4.Dot(first, new Vector4(q.x, q.y, q.z, q.w)) < 0f ? -1f : 1f;
                guardRotSum[k] += new Vector4(q.x, q.y, q.z, q.w) * s;
            }
            guardSamples++;
        }

        public void EndGuard()
        {
            if (guardSamples == 0)
                return;
            guardRot = new Quaternion[Drivers];
            for (int k = 0; k < Drivers; k++)
            {
                guardPos[k] /= guardSamples;
                var v = guardRotSum[k].sqrMagnitude > 1e-12f ? guardRotSum[k].normalized : new Vector4(0f, 0f, 0f, 1f);
                guardRot[k] = new Quaternion(v.x, v.y, v.z, v.w);
            }
        }

        /// <summary>
        /// Where the weights put each tail, and where each bone's head is aimed from, with the drivers' rest
        /// in the stance or in the guard.
        /// </summary>
        public void Predict(Vector3 headingPos, Quaternion headingRot, bool onStance, Vector3[] tails, Vector3[] heads)
        {
            // each driver's move from its rest, as a matrix from the stance's heading frame to the world
            carry[0] = Matrix4x4.TRS(headingPos, headingRot, Vector3.one);
            for (int k = 1; k < Drivers; k++)
            {
                var b = DriverBone(k);
                carry[k] = b == null ? carry[0]
                    : Matrix4x4.TRS(b.position, b.rotation, Vector3.one) *
                      (onStance ? Matrix4x4.TRS(stancePos[k], stanceRot[k], Vector3.one) : Matrix4x4.TRS(guardPos[k], guardRot[k], Vector3.one)).inverse;
            }
            for (int j = 0; j < joints.Count; j++)
            {
                var p = Vector3.zero;
                var w = joints[j].weights;
                for (int k = 0; k < Drivers; k++)
                    if (w[k] > 0f)
                        p += w[k] * carry[k].MultiplyPoint3x4(stanceTail[j]);
                tails[j] = p;
            }
            // a chain's first bone: from where the pelvis carries its stance head (the head itself sits where
            // the pelvis puts it, which is elsewhere when the pelvis tilts unlike the stance's)
            for (int j = 0; j < joints.Count; j++)
                heads[j] = joints[j].parent >= 0 ? tails[joints[j].parent] : carry[1].MultiplyPoint3x4(stanceHead[j]);
        }

        /// <summary>The drivers' rest, stance against guard: degrees and cm per driver (for the probes).</summary>
        public string RestReport()
        {
            if (!Ready)
                return "not ready";
            var parts = new List<string>();
            for (int k = 1; k < Drivers; k++)
                parts.Add($"{DriverNames[k]} {Quaternion.Angle(stanceRot[k], guardRot[k]):F0} deg {(stancePos[k] - guardPos[k]).magnitude * 100f:F1} cm");
            return string.Join(", ", parts);
        }

        /// <summary>Turn the skirt bones to their skinned pose; weight 0 leaves them as they are.</summary>
        public void Apply(Vector3 headingPos, Quaternion headingRot, float weight)
        {
            if (!Ready || weight <= 0f)
                return;
            if (predicted == null || predicted.Length != joints.Count)
            {
                predicted = new Vector3[joints.Count];
                aimedFrom = new Vector3[joints.Count];
            }
            Predict(headingPos, headingRot, RestOnStance, predicted, aimedFrom);
            for (int j = 0; j < joints.Count; j++)
            {
                var jt = joints[j];
                if (jt.bone == null)
                    continue;
                var target = predicted[j] - aimedFrom[j];
                var current = jt.bone.rotation * jt.tailLocal;
                if (target.sqrMagnitude < 1e-10f || current.sqrMagnitude < 1e-10f)
                    continue;
                var turned = Quaternion.FromToRotation(current, target) * jt.bone.rotation;
                jt.bone.rotation = weight >= 1f ? turned : Quaternion.Slerp(jt.bone.rotation, turned, weight);
            }
        }
    }
}
