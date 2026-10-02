#if MAGICACLOTH2 && ROE_MAGICA
// Magica Cloth 2 as a cloth backend (F4), for when the package is installed.  A DRAFT written from the
// public manual (runtime construction, ClothSerializeData, MagicaCapsuleCollider - see
// docs/magica-cloth-2.md) before the package was here: it compiles only with the package's own define
// MAGICACLOTH2 and ROE_MAGICA, which is added by hand (Project Settings > Player > Scripting Define
// Symbols) once the package is in, the first time this is built and tried.
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// The same cloth as RoeBoneCloth's Magica-style backend, built as MagicaCloth components at run time:
    /// one BoneCloth per piece of cloth (chains linked across become one Sequential Non Loop mesh, the
    /// rest Line), our settings turned into Magica's parameters, our leg / hip / torso capsules as
    /// MagicaCapsuleColliders.  The skirt's skinned pose (RoeSkirtRig) is on the bones before Magica
    /// reads them, so it is Magica's animation pose; animationPoseRatio 1 restores towards it.
    ///
    /// Magica runs on Unity's player loop with its own 90 Hz clock: in play mode (the game, the exe) it
    /// moves; the editor's batch checks and videos step the fight by hand and it does not.
    /// </summary>
    public class RoeMagicaCloth : IRoeCloth
    {
        readonly List<MagicaCloth> cloths = new List<MagicaCloth>();
        float weight = 1f;

        public string Report { get; }

        public float Weight
        {
            get => weight;
            set
            {
                if (Mathf.Abs(value - weight) < 0.01f && value != 0f && value != 1f)
                    return;
                weight = value;
                foreach (var c in cloths)
                {
                    c.SerializeData.blendWeight = value;
                    c.SetParameterChange();
                }
            }
        }

        public RoeMagicaCloth(Animator animator, Transform world, ICollection<Transform> exclude)
        {
            // the pieces and colliders exactly as our Magica-style solver finds and measures them
            var ours = new RoeBoneCloth(animator, world, exclude, legacy: false);
            var colliders = new Dictionary<RoeBoneCloth.Capsule, MagicaCapsuleCollider>();
            foreach (var cap in ours.capsules)
            {
                var go = new GameObject($"Magica {cap.name}");
                go.transform.SetParent(cap.a, false);
                var axis = cap.b.position - cap.a.position;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                var col = go.AddComponent<MagicaCapsuleCollider>();
                col.direction = MagicaCapsuleCollider.Direction.Y;
                col.alignedOnCenter = false;          // from the bone's head along the axis
                col.radiusSeparation = true;
                col.SetSize(cap.ra, cap.rb, axis.magnitude);
                colliders[cap] = col;
            }
            var parts = new List<string>();
            foreach (var piece in ours.Pieces())
            {
                var go = new GameObject($"Magica {piece.kind} {piece.roots[0].name}");
                go.transform.SetParent(animator.transform, false);
                var cloth = go.AddComponent<MagicaCloth>();
                var sd = cloth.SerializeData;
                var s = piece.settings;
                sd.clothType = ClothProcess.ClothType.BoneCloth;
                sd.rootBones.AddRange(piece.roots);
                sd.connectionMode = piece.roots.Count > 1 && piece.mesh
                    ? RenderSetupData.BoneConnectionMode.SequentialNonLoopMesh
                    : RenderSetupData.BoneConnectionMode.Line;
                sd.updateMode = ClothUpdateMode.Normal;
                sd.animationPoseRatio = 1f;           // restore towards the pose the animation (and RoeSkirtRig) made
                sd.rotationalInterpolation = 1f;
                sd.rootRotation = 1f;
                sd.gravity = s.gravity;
                sd.damping.SetValue(s.damping);
                sd.radius.SetValue(s.radius);
                sd.angleRestorationConstraint.useAngleRestoration = true;
                sd.angleRestorationConstraint.stiffness.SetValue(s.restore);
                sd.angleRestorationConstraint.velocityAttenuation = s.attenuation;
                sd.angleLimitConstraint.useAngleLimit = true;
                sd.angleLimitConstraint.limitAngle.SetValue(s.limitTip, s.limitRoot / Mathf.Max(1f, s.limitTip), 1f, true);
                sd.inertiaConstraint.worldInertia = s.worldInertia;
                sd.inertiaConstraint.movementSpeedLimit.SetValue(true, s.worldMoveLimit);
                sd.inertiaConstraint.rotationSpeedLimit.SetValue(true, s.worldTurnLimit);
                sd.inertiaConstraint.localInertia = s.inertia;
                sd.inertiaConstraint.localMovementSpeedLimit.SetValue(true, s.moveLimit);
                sd.inertiaConstraint.localRotationSpeedLimit.SetValue(true, s.turnLimit);
                sd.inertiaConstraint.depthInertia = s.depthInertia;
                sd.inertiaConstraint.particleSpeedLimit.SetValue(true, s.particleLimit);
                sd.tetherConstraint.distanceCompression = s.tether;
                sd.distanceConstraint.stiffness.SetValue(s.linkStiffness);
                sd.motionConstraint.useMaxDistance = s.maxDistanceTip > 0f;
                if (s.maxDistanceTip > 0f)
                    sd.motionConstraint.maxDistance.SetValue(s.maxDistanceTip, s.maxDistanceRoot / s.maxDistanceTip, 1f, true);
                sd.motionConstraint.useBackstop = s.backstop;
                sd.motionConstraint.backstopRadius = s.backstopRadius;
                sd.motionConstraint.backstopDistance.SetValue(s.backstopDistance);
                if (s.backstop)
                {
                    // ours: the body's side of each point, out from the bone the chain belongs to
                    sd.normalAlignmentSetting.alignmentMode = NormalAlignmentSettings.AlignmentMode.Transform;
                    sd.normalAlignmentSetting.adjustmentTransform = piece.owner;
                }
                sd.colliderCollisionConstraint.mode = s.edge ? ColliderCollisionConstraint.Mode.Edge : ColliderCollisionConstraint.Mode.Point;
                sd.colliderCollisionConstraint.friction = Mathf.Min(0.5f, s.friction);
                foreach (var cap in piece.capsules)
                    if (colliders.TryGetValue(cap, out var col))
                        sd.colliderCollisionConstraint.colliderList.Add(col);
                cloth.BuildAndRun();
                cloths.Add(cloth);
                parts.Add($"{piece.kind} {string.Join("+", piece.roots.Select(r => r.name))} ({sd.connectionMode})");
            }
            Report = $"Magica Cloth 2: {cloths.Count} BoneCloths: {string.Join(", ", parts)}; colliders {colliders.Count}";
        }

        // Magica keeps the bones' own pose and puts it back itself
        public void Rest() { }

        // Magica steps itself on the player loop (after LateUpdate)
        public void Step(float dt, float floor) { }

        public void Reset()
        {
            foreach (var c in cloths)
                c.ResetCloth();
        }
    }
}
#endif
