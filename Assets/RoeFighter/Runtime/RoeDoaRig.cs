using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// A Dead or Alive 6 character's physics, mapped onto her fighter prefab (DoaFighter.BuildPhysics, from her game data:
    /// tools/doa6_physics.py).  Everything is on her own bones, in metres; RoeDoaPhysics simulates it, and the bone cloth
    /// gets the kinds of her loose bones from here (their names are only numbers).
    ///   chains   bone chains (DOA6 NUNO4): ponytail strands, sash tails, cords - a particle per bone, the first one fixed
    ///   swings   swing bones (.swg): short hair strands with angle limits
    ///   softs    lattice soft bodies (SOFT): breasts (and hips in some outfits) - a node per lattice point, each with its
    ///            animated target skinned to body bones; the breast meshes are skinned to the nodes (8 per vertex), blended
    ///            back to plain skinning towards the body
    ///   colliders capsules and ellipsoids on bones, in groups; each chain / soft body collides with its own groups
    ///   twists   the game's twist helpers (RF_*): bones its rig script turns with a share of a limb bone's own twist
    ///            (the clips never key them; DriveHelpers, after each pose)
    /// </summary>
    public class RoeDoaRig : MonoBehaviour
    {
        [Serializable]
        public class Collider
        {
            public Transform bone;
            public int type;                // 5 capsule along local Y (a radius, b half-length), 6 ellipsoid (radii a b c)
            public float a, b, c;           // metres
            public Vector3 center;          // in the bone's frame
            public Quaternion rotation = Quaternion.identity;
        }

        [Serializable]
        public class Group
        {
            public string name;
            public int[] colliders;
        }

        [Serializable]
        public class Chain
        {
            public string name, kind;       // hair, ribbon, chain
            public Transform parent;
            public Transform[] bones;       // bones[0] stays on the parent
            public float[] restLength;      // metres, to the previous bone
            public float[] param;           // the game's 17 words: gravity/mass, flags, damping a, damping b, stiffness a b c, iterations, restore, friction
            public int[] groups;
        }

        [Serializable]
        public class Swing
        {
            public Transform bone;
            public int group;               // the strand
            public float[] param;           // f0-f3 angle limits (degrees), f13 damping
        }

        [Serializable]
        public class Soft
        {
            public string name, kind;       // breast, body
            public Transform parent;
            public Transform[] nodes;
            public int[] flags;             // 0x01 pinned (the chest wall), 0x20 interior, 0x40 surface
            public float[] wSelf;
            // the animated target of node i: sum over k in [targetStart[i], targetStart[i+1]) of targetWeight[k] * targetBone[k].TransformPoint(targetLocal[k])
            public int[] targetStart;
            public Transform[] targetBone;
            public Vector3[] targetLocal;
            public float[] targetWeight;
            public int[] springs;           // pairs of nodes: lattice edges, then face diagonals
            public float[] springRest;      // metres
            public int edgeCount;           // the first edgeCount springs are lattice edges
            public int[] hull;              // closed triangle hull over the surface nodes
            public float restVolume;        // m^3
            public float gravity, damping, stiffness, scale;
            /// <summary>Which way is down in the parent bone when she stands as modelled: the rest shape already hangs that way.</summary>
            public Vector3 restDown;
            public Vector3 perAxis;         // metres (the game's per-axis words, taken as displacement limits)
            public float[] coef;
            public int[] groups;
        }

        [Serializable]
        public class Attachment
        {
            public Transform bone;
            public int[] body;              // per ref
            public float[] weight;          // per ref
            public int[] nodes;             // 8 per ref
            public float[] nodeWeight;      // 8 per ref
            public Vector3 offset;          // rest position minus the interpolated rest position (world, at build)
        }

        [Serializable]
        public class Twist
        {
            public Transform bone;
            public Transform source;        // the limb bone whose twist it shares
            public Vector3 axis;            // the twist axis in the source's frame (towards the next limb bone)
            public Quaternion sourceRest;   // the source's local rotation at rest
            public Quaternion restToSource; // the helper's rotation in the source's frame at rest
            public float share;             // 0: none of the twist (the limb's swing only), 1: all of it
        }

        public string source;
        public List<Twist> twists = new List<Twist>();
        public List<Collider> colliders = new List<Collider>();
        public List<Group> groups = new List<Group>();
        public List<Chain> chains = new List<Chain>();
        public List<Swing> swings = new List<Swing>();
        public List<Soft> softs = new List<Soft>();
        public List<Attachment> attachments = new List<Attachment>();

        /// <summary>
        /// The twist helpers on the pose just made: each takes its limb bone's swing and its share of the limb's twist
        /// about the limb (the upper arm's twist split 0 / 1/2 / all from the shoulder to the elbow, the thigh's from
        /// the hip to the knee; the NoTwist ones none).  Left on their parents they turned with all of it, and the chest
        /// wall of her soft-body breasts, partly skinned to the shoulder end, turned away with every roll of the arm.
        /// </summary>
        public void DriveHelpers()
        {
            foreach (var t in twists)
            {
                if (t.bone == null || t.source == null)
                    continue;
                var r = Quaternion.Inverse(t.sourceRest) * t.source.localRotation;
                var along = Vector3.Project(new Vector3(r.x, r.y, r.z), t.axis);
                var twist = new Quaternion(along.x, along.y, along.z, r.w);
                float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
                twist = m > 1e-6f ? new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m) : Quaternion.identity;
                t.bone.rotation = t.source.rotation * Quaternion.Inverse(twist) * Quaternion.Slerp(Quaternion.identity, twist, t.share) * t.restToSource;
            }
        }

        /// <summary>Her loose bones by kind (for the bone cloth, which tells kinds by bone names otherwise).</summary>
        public Dictionary<Transform, string> KindsOf()
        {
            var kindOf = new Dictionary<Transform, string>();
            // a chain's first bone stays on its parent but turns towards the next one, like the rest
            foreach (var c in chains)
                foreach (var b in c.bones)
                    if (b != null)
                        kindOf[b] = c.kind;
            foreach (var s in swings)
                if (s.bone != null)
                    kindOf[s.bone] = "hair";
            // a breast for a bone solver: the pivot on the chest wall and the breast bone below it, swung as one
            foreach (var s in softs)
                if (s.parent != null && s.parent.parent != null && s.parent.parent.name.EndsWith("_pivot"))
                {
                    kindOf[s.parent.parent] = s.kind;
                    kindOf[s.parent] = s.kind;
                }
            return kindOf;
        }
    }
}
