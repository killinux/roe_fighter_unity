using System;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// Weapons a fighter holds in her hands under motion capture (a08's greatsword; user 10-06: "给inase拿着长剑的找一些普通攻击").
    /// Her game's battle stance holds the sword rigid in one hand, but the sword's bones are no humanoid bones: in the humanoid
    /// model they hang on the pelvis, and under the motion capture's moves the sword stayed where the stance left it (so it
    /// was shown only during her game's own clips).  Measured from that stance by RoeWeaponGrip (editor): the bone that
    /// carries each weapon, in the frame of the hand that holds it.  FighterRig.Holds puts them there each step when her own
    /// strikes are made for it (MotionPack.grip); RoeMotionPacks.Measure does the same while it measures such strikes.
    /// </summary>
    public class RoeGrips : MonoBehaviour
    {
        [Serializable]
        public class Grip
        {
            public Transform bone;
            public HumanBodyBones hand;
            public Vector3 position;                 // in the hand's frame, unscaled: hand.position + hand.rotation * position
            public Quaternion rotation = Quaternion.identity;
        }

        public Grip[] grips = new Grip[0];

        /// <summary>Each grip's bone where its hand holds it, blended by weight from where the clips put it.</summary>
        public void Apply(Animator animator, float weight = 1f)
        {
            foreach (var g in grips)
            {
                var hand = g?.bone != null ? animator.GetBoneTransform(g.hand) : null;
                if (hand == null)
                    continue;
                var pos = hand.position + hand.rotation * g.position;
                var rot = hand.rotation * g.rotation;
                if (weight >= 1f)
                    g.bone.SetPositionAndRotation(pos, rot);
                else
                    g.bone.SetPositionAndRotation(Vector3.Lerp(g.bone.position, pos, weight), Quaternion.Slerp(g.bone.rotation, rot, weight));
            }
        }
    }
}
