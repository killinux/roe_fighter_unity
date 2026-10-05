#if MAGICACLOTH2
// Magica Cloth 2 as a cloth backend (F4), when the package is installed in Assets/MagicaCloth2 (gitignored; it adds
// the define MAGICACLOTH2 to the project itself when it loads).  First written from the public manual (runtime
// construction, ClothSerializeData, MagicaCapsuleCollider - docs/magica-cloth-2.md), fitted to v2.18.2 on 10-04.
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// The same cloth as RoeBoneCloth's Magica-style backend, built as MagicaCloth components at run time:
    /// one BoneCloth per piece of cloth (chains linked across become one Sequential Non Loop mesh, a ring a
    /// Sequential Loop mesh, the rest Line), our settings turned into Magica's parameters, our leg / hip / torso
    /// capsules as MagicaCapsuleColliders.  The skirt's skinned pose (RoeSkirtRig) is on the bones before Magica
    /// reads them, so it is Magica's animation pose; animationPoseRatio 1 restores towards it.
    ///
    /// The outfit's pieces of cloth that are meshes of their own (the 爆衣 split, RoeClothesBurst: a08's long skirt
    /// panels and back flap) are MeshCloths instead (user 10-04: "之前riseoferos的布料都不完美，尽量修正一个"): Magica
    /// simulates the mesh itself, thinned to a few centimetres (its reduction), and writes the vertices back.  Every
    /// vertex is fixed or moves by its skin weights: on the bone chains that carry the piece (which then are no
    /// BoneCloth) it moves, on the hip armour or the pelvis it is fixed.  The chains keep their animation pose
    /// (RoeSkirtRig), which places the mesh before Magica moves it.  Its parameters start from Magica's own skirt
    /// preset (MeshSettings); BoneCloths stay on our numbers.
    ///
    /// Magica runs on Unity's player loop with its own 90 Hz clock: in play mode (the game, the exe) it
    /// moves; the editor's batch checks and videos step the fight by hand and it does not (RoeClothPlayDemo
    /// films it in play mode).
    ///
    /// With presets (the "magica_full" setup, user 10-05: "inase完全用magic cloth 2"): every piece takes the parameters of
    /// Magica's own preset for its kind, as the plugin ships them (MC2_Preset_*.json, RoeMagicaPresets; ImportJson keeps
    /// the set-up - renderers, bones, connection, colliders - and imports the parameters): a skirt MC2_Preset_Skirt (the
    /// MeshCloth panels too), hair MC2_Preset_FrontHair / ShortHair / LongHair by where and how long, breasts a BoneSpring
    /// on MC2_Preset_MiddleSpring (Magica's spring mode, made for them), chains, ribbons and the rest MC2_Preset_Accessory.
    /// The set-up ImportJson leaves alone (clothType, connection, reduction, colliders, animationPoseRatio) is ours:
    /// animationPoseRatio 1 - the cloth restores towards the animation's pose (the preset files say 0, the pose the cloth
    /// was built in, here the bind pose: see InBindPose).
    /// </summary>
    public class RoeMagicaCloth : IRoeCloth, System.IDisposable
    {
        /// <summary>
        /// A MeshCloth's numbers.  They start from Magica Cloth 2's own skirt preset (MC2_Preset_Skirt.json, v2.18.2: gravity 5,
        /// damping 0.1, particle radius 2 cm, distance stiffness 1 falling to 0.5 at the tip, bending 1, angle restoration 0.2
        /// falling to a fifth, angle limit 60 degrees, world and local inertia 1, centrifugal 0.1, colliders against points) and
        /// were measured into these on a08's long panels (RoeClothPlayDemo's meter, 10-04; docs/magica-cloth-2.md): the
        /// preset's light silk floated round her legs and wrapped them in kicks, so - the distance as stiff at the tip as at
        /// the root, colliders against the edges, damping 0.2, restoration 0.4 falling to 0.2, angle limit 35, real gravity,
        /// half the inertia and no centrifugal pull: a heavier cloth; a 3 cm particle radius and a coarser thinned mesh
        /// (6 % of the piece's size, not 3) keep it out of the legs and stop it stretching and turning over.
        /// "Root" / "Tip" pairs are the two ends of Magica's depth curve (0 at the fixed vertices, 1 the farthest).
        /// </summary>
        public class MeshSettings
        {
            public bool on = true;                                      // false: the bone chains as BoneCloth, as before
            public float reduction = 0.06f, shapeReduction = 0.065f;    // Magica's simple / shape distance: share of the piece's size (preset 0)
            public float poseRatio = 1f;                                // animationPoseRatio: 1 = the pose the animation (RoeSkirtRig) gives; below 1 blends towards the bind pose (InBindPose)
            public float moveWeight = 0.5f;                             // a vertex moves when at least this much of its weight is on the chains
            public float gravity = 9.8f, damping = 0.2f, radius = 0.03f;                            // preset 5, 0.1, 0.02
            public float worldInertia = 0.5f, moveSmoothing = 0.4f, worldMoveLimit = 5f, worldTurnLimit = 720f;  // preset inertia 1
            public float localInertia = 0.5f, localMoveLimit = 3f, localTurnLimit = 360f;            // preset inertia 1
            public float depthInertia = 0.7f, centrifugal = 0f, particleLimit = 4f;                 // preset centrifugal 0.1
            public float tether = 0.3f;
            public float distance = 1f, distanceTip = 1f;                                           // preset tip 0.5
            public float bending = 1f;
            public float restore = 0.4f, restoreTip = 0.2f, attenuation = 0.7f;                     // preset 0.2, 0.04
            public float limit = 35f, limitRoot = 0.2f;                 // degrees at the tip; at the root this share of it (preset 60)
            public bool edge = true;                                    // colliders against the mesh's edges, not only its points (preset off)
            public float friction = 0.05f, limitDistance = 0.05f;
            public bool selfCollision;
            public float thickness = 0.005f;                            // self collision surface thickness
            // Magica's clock, for every cloth in the scene (MagicaManager): steps a second (30-150; its default 90) and at
            // most this many a frame (1-5; default 3)
            public float frequency = 90f, stepsPerFrame = 3f;

            /// <summary>"key=value;..." over the defaults (any field by name; bools as 0/1).</summary>
            public MeshSettings Tuned(string tuning)
            {
                foreach (var pair in (tuning ?? "").Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=');
                    var field = kv.Length == 2 ? typeof(MeshSettings).GetField(kv[0].Trim()) : null;
                    if (field == null || !float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    {
                        Debug.LogWarning($"[ROE] Magica mesh setting not understood: '{pair}'");
                        continue;
                    }
                    if (field.FieldType == typeof(bool))
                        field.SetValue(this, v != 0f);
                    else
                        field.SetValue(this, v);
                }
                return this;
            }
        }

        /// <summary>For checks and tuning: "key=value;..." over MeshSettings (RoeClothPlayDemo -roeCloths magica:key=value).</summary>
        public static string Tuning = "";

        readonly List<MagicaCloth> cloths = new List<MagicaCloth>();
        readonly List<GameObject> made = new List<GameObject>();
        readonly RoeDoaRig doaRig;
        readonly bool presets;
        readonly List<string> missingPresets = new List<string>();
        float weight = 1f;

        public string Report { get; }

        /// <summary>Every component has finished building (Magica builds in the background) - or failed.</summary>
        public bool Ready => cloths.All(c => c == null || c.Process.IsRunning() || c.Process.Result.IsError());

        /// <summary>The components that failed to build, with Magica's reason.</summary>
        public string Failures => string.Join(", ", cloths.Where(c => c != null && c.Process.Result.IsError()).Select(c => $"{c.name}: {c.Process.Result.GetResultString()}"));

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

        /// <summary>
        /// Whether Magica can take a kind on a fighter: not a DOA6 character's breasts.  Their soft-body lattice hangs some
        /// 130 nodes straight under each breast's pivot, and a BoneCloth takes in every transform under its root bones, at
        /// most 127 children to a transform (RenderSetupData; kas011's two breast BoneCloths failed to build): they go to the
        /// bone cloth (RoeClothBackends: a kind its solver cannot take).
        /// </summary>
        public static bool Covers(RoeClothScope scope, string kind)
        {
            if (kind != "breast" && kind != "body")
                return true;
            var doa = scope.animator != null ? scope.animator.GetComponent<RoeDoaRig>() : null;
            return doa == null || doa.softs.Count == 0;
        }

        /// <summary>Magica's limit on the children of one transform in a BoneCloth.</summary>
        const int MaxChildren = 127;

        static bool TooManyChildren(Transform root) => root.GetComponentsInChildren<Transform>(true).Any(t => t.childCount > MaxChildren);

        public RoeMagicaCloth(RoeClothScope scope, bool presets = false)
        {
            this.presets = presets;
            // the pieces and colliders exactly as our Magica-style solver finds and measures them (only the kinds the
            // physics setup gives Magica: scope.kinds)
            var animator = scope.animator;
            var ours = new RoeBoneCloth(scope, legacy: false);
            var colliders = new Dictionary<RoeBoneCloth.Capsule, MagicaCapsuleCollider>();
            foreach (var cap in ours.capsules)
            {
                var go = new GameObject($"Magica {cap.name}");
                made.Add(go);
                go.transform.SetParent(cap.a, false);
                var axis = cap.b.position - cap.a.position;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                var col = go.AddComponent<MagicaCapsuleCollider>();
                col.direction = MagicaCapsuleCollider.Direction.Y;
                // from the bone's head to the next joint: a capsule that is not centred starts at its transform and runs
                // along MINUS its axis unless reversed, and its length is the whole capsule, both caps included
                // (ColliderManager: end = pos - dir * (length - r0 - r1)); until 10-04 evening it ran up out of the
                // leg, short by both radii, and the cloth hardly met the legs
                col.alignedOnCenter = false;
                col.reverseDirection = true;
                col.radiusSeparation = true;
                col.SetSize(cap.ra, cap.rb, axis.magnitude + cap.ra + cap.rb);
                col.UpdateParameters();               // (the package's runtime sample: required after a change)
                colliders[cap] = col;
            }
            var parts = new List<string>();
            var pieces = ours.Pieces();
            var root = scope.world != null ? scope.world : animator.transform;

            // the outfit's own cloth meshes: MeshCloth, in place of the chains that carry them
            var m = new MeshSettings().Tuned(Tuning);
            MagicaManager.SetSimulationFrequency(Mathf.RoundToInt(m.frequency));
            MagicaManager.SetMaxSimulationCountPerFrame(Mathf.RoundToInt(m.stepsPerFrame));
            var meshed = new HashSet<RoeBoneCloth.Piece>();
            var burst = animator.GetComponentInChildren<RoeClothesBurst>(true);
            if (m.on && burst != null && scope.Takes("skirt"))
                foreach (var bp in burst.pieces)
                {
                    var smr = bp.renderer;
                    var mesh = smr != null ? smr.sharedMesh : null;
                    if (!bp.cloth || mesh == null || !mesh.isReadable || mesh.vertexCount > 65535)
                        continue;
                    var weights = mesh.boneWeights;
                    var bones = smr.bones;
                    if (weights.Length != mesh.vertexCount)
                        continue;
                    Transform Top(BoneWeight w) => w.boneIndex0 < bones.Length ? bones[w.boneIndex0] : null;
                    // the bone cloth's pieces whose chains carry this mesh (most of some of its vertices)
                    var covered = new List<RoeBoneCloth.Piece>();
                    var chainBones = new HashSet<Transform>();
                    foreach (var q in pieces.Where(q => q.kind == "skirt" && !meshed.Contains(q)))
                    {
                        var set = new HashSet<Transform>(q.roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)));
                        if (weights.Count(w => set.Contains(Top(w))) >= 20)
                        {
                            covered.Add(q);
                            chainBones.UnionWith(set);
                        }
                    }
                    if (covered.Count == 0)
                        continue;
                    var attributes = new VertexAttribute[weights.Length];
                    int moving = 0;
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        float on = 0f;
                        if (w.boneIndex0 < bones.Length && chainBones.Contains(bones[w.boneIndex0])) on += w.weight0;
                        if (w.boneIndex1 < bones.Length && chainBones.Contains(bones[w.boneIndex1])) on += w.weight1;
                        if (w.boneIndex2 < bones.Length && chainBones.Contains(bones[w.boneIndex2])) on += w.weight2;
                        if (w.boneIndex3 < bones.Length && chainBones.Contains(bones[w.boneIndex3])) on += w.weight3;
                        bool moves = on >= m.moveWeight;
                        attributes[i] = moves ? VertexAttribute.Move : VertexAttribute.Fixed;
                        moving += moves ? 1 : 0;
                    }
                    if (moving == 0 || moving == weights.Length)
                        continue;
                    var go = new GameObject($"MC2 mesh {cloths.Count} ({bp.name})");
                    made.Add(go);
                    go.transform.SetParent(root, false);
                    var cloth = go.AddComponent<MagicaCloth>();
                    var sd = cloth.SerializeData;
                    sd.clothType = ClothProcess.ClothType.MeshCloth;
                    sd.sourceRenderers.Add(smr);
                    cloth.GetSerializeData2().vertexAttributeList.Add(attributes);
                    sd.meshWriteMode = ClothMeshWriteMode.PositionAndNormalTangent;   // the outfit's normal maps turn with the cloth
                    sd.reductionSetting.simpleDistance = m.reduction;
                    sd.reductionSetting.shapeDistance = m.shapeReduction;
                    if (!presets || !Preset(sd, "MC2_Preset_Skirt"))
                        Apply(sd, m);
                    foreach (var cap in covered.SelectMany(q => q.capsules).Distinct())
                        if (colliders.TryGetValue(cap, out var col))
                            sd.colliderCollisionConstraint.colliderList.Add(col);
                    InBindPose(smr, () => cloth.BuildAndRun());
                    cloths.Add(cloth);
                    foreach (var q in covered)
                        meshed.Add(q);
                    parts.Add($"MeshCloth {bp.name} ({mesh.vertexCount} vertices, {moving} moving, {sd.colliderCollisionConstraint.colliderList.Count} colliders" +
                              (presets ? ", MC2_Preset_Skirt" : "") + "; " +
                              $"in place of {string.Join("+", covered.SelectMany(q => q.roots).Select(r => r.name))})");
                }

            foreach (var piece in pieces.Where(p => !meshed.Contains(p)))
            {
                if (piece.roots.Any(TooManyChildren))
                {
                    parts.Add($"{piece.kind} {piece.roots[0].name}: left out (a transform under it has more than {MaxChildren} children)");
                    continue;
                }
                // on the fighter's root, not in her bone tree: a bone solver that comes next finds chains by bone names, and
                // a destroyed component lives on until the end of the frame (a name with "skirt" in it became a chain)
                var go = new GameObject($"MC2 cloth {cloths.Count} ({piece.roots[0].name})");
                made.Add(go);
                go.transform.SetParent(root, false);
                var cloth = go.AddComponent<MagicaCloth>();
                var sd = cloth.SerializeData;
                var s = piece.settings;
                sd.clothType = ClothProcess.ClothType.BoneCloth;
                sd.rootBones.AddRange(piece.roots);
                string preset = presets ? PresetFor(piece) : null;
                if (preset != null)
                {
                    // Magica's own: its preset's parameters on the set-up (a breast on its spring mode)
                    if (piece.kind == "breast" || piece.kind == "body")
                        sd.clothType = ClothProcess.ClothType.BoneSpring;
                    sd.connectionMode = piece.roots.Count > 1 && piece.mesh
                        ? piece.loop ? RenderSetupData.BoneConnectionMode.SequentialLoopMesh : RenderSetupData.BoneConnectionMode.SequentialNonLoopMesh
                        : RenderSetupData.BoneConnectionMode.Line;
                    if (Preset(sd, preset))
                    {
                        foreach (var cap in piece.capsules)
                            if (colliders.TryGetValue(cap, out var pc))
                                sd.colliderCollisionConstraint.colliderList.Add(pc);
                        cloth.BuildAndRun();
                        cloths.Add(cloth);
                        parts.Add($"{piece.kind} {string.Join("+", piece.roots.Select(r => r.name))} ({sd.clothType} {sd.connectionMode}, {preset})");
                        continue;
                    }
                    sd.clothType = ClothProcess.ClothType.BoneCloth;
                }
                // a sheet: its chains joined into one mesh in the order they are linked (round and closed for a ring:
                // a DOA6 skirt, a sleeve); single chains as lines
                sd.connectionMode = piece.roots.Count > 1 && piece.mesh
                    ? piece.loop ? RenderSetupData.BoneConnectionMode.SequentialLoopMesh : RenderSetupData.BoneConnectionMode.SequentialNonLoopMesh
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
            int meshCount = parts.Count(p => p.StartsWith("MeshCloth"));
            Report = $"Magica Cloth 2{(presets ? " on its own presets" : "")}: {meshCount} MeshCloths, {cloths.Count - meshCount} Bone cloths: " +
                     $"{string.Join(", ", parts)}; colliders {colliders.Count}" +
                     (Tuning.Length > 0 ? $"; tuning {Tuning}" : "") +
                     (missingPresets.Count > 0 ? $"; PRESETS NOT FOUND (our settings used): {string.Join(" ", missingPresets.Distinct())}" : "");
            // a DOA6 character's visible grid cloth is rebuilt from its control points: again once Magica has moved them
            doaRig = animator.GetComponent<RoeDoaRig>();
            if (doaRig != null && doaRig.surfaces.Count > 0)
                MagicaManager.OnPostSimulation += Rebuild;
        }

        /// <summary>
        /// Builds with the renderer's bones in their bind pose - the mesh as it was modelled - and puts them back.  Magica takes
        /// a MeshCloth's rest shape (the lengths and bends it keeps, the thinned mesh) from the pose the bones are in when it
        /// is built (at once, in BuildAndRun); the fighter is in her battle stance then, or in whatever pose the last round
        /// left the bones the stance does not key, and neither is the shape of the cloth.  (Hence animationPoseRatio stays 1:
        /// below it Magica blends the bones towards this pose.)
        /// </summary>
        static void InBindPose(SkinnedMeshRenderer smr, System.Action build)
        {
            var bones = smr.bones;
            var bind = smr.sharedMesh.bindposes;
            var saved = bones.Where(b => b != null).Distinct().Select(b => (b, b.localPosition, b.localRotation)).ToList();
            var toWorld = smr.transform.localToWorldMatrix;
            int Depth(Transform t)
            {
                int d = 0;
                for (; t != null; t = t.parent)
                    d++;
                return d;
            }
            foreach (int i in Enumerable.Range(0, Mathf.Min(bones.Length, bind.Length)).Where(i => bones[i] != null).OrderBy(i => Depth(bones[i])))
            {
                var m = toWorld * bind[i].inverse;
                bones[i].SetPositionAndRotation(m.GetColumn(3), m.rotation);
            }
            try
            {
                build();
            }
            finally
            {
                foreach (var (b, p, r) in saved)
                {
                    b.localPosition = p;
                    b.localRotation = r;
                }
            }
        }

        /// <summary>Magica's own preset for a piece: by its kind, hair by where it hangs and how long it is.</summary>
        static string PresetFor(RoeBoneCloth.Piece piece)
        {
            switch (piece.kind)
            {
                case "skirt":
                    return "MC2_Preset_Skirt";
                case "breast":
                case "body":
                    return "MC2_Preset_MiddleSpring";
                case "hair":
                {
                    var names = piece.roots.Select(r => r.name.ToLowerInvariant()).ToList();
                    if (names.All(n => n.Contains("front") || n.Contains("bang") || System.Text.RegularExpressions.Regex.IsMatch(n, @"_f[lrc]?\d*_")))
                        return "MC2_Preset_FrontHair";
                    float length = piece.roots.Average(r => r.GetComponentsInChildren<Transform>(true).Max(t => Vector3.Distance(t.position, r.position)));
                    return length > 0.3f ? "MC2_Preset_LongHair" : "MC2_Preset_ShortHair";
                }
                default:
                    return "MC2_Preset_Accessory";
            }
        }

        /// <summary>
        /// Magica's preset's parameters over a component's set-up (ImportJson keeps the renderers, bones, connection,
        /// colliders and pose ratio); the cloth restores towards the animation's pose (animationPoseRatio 1) and updates with
        /// the fight.  False when the preset is not to be found (our settings then).
        /// </summary>
        bool Preset(ClothSerializeData sd, string name)
        {
            string json = RoeMagicaPresets.Json(name);
#if UNITY_EDITOR
            if (json == null)
            {
                var path = System.IO.Path.Combine(Application.dataPath, "MagicaCloth2", "Res", "Preset", name + ".json");
                if (System.IO.File.Exists(path))
                    json = System.IO.File.ReadAllText(path);
            }
#endif
            if (json == null || !sd.ImportJson(json))
            {
                missingPresets.Add(name);
                return false;
            }
            sd.animationPoseRatio = 1f;
            sd.updateMode = ClothUpdateMode.Normal;
            return true;
        }

        /// <summary>A MeshCloth's parameters (MeshSettings).</summary>
        static void Apply(ClothSerializeData sd, MeshSettings m)
        {
            sd.updateMode = ClothUpdateMode.Normal;
            sd.animationPoseRatio = m.poseRatio;
            sd.gravity = m.gravity;
            sd.damping.SetValue(m.damping);
            sd.radius.SetValue(m.radius);
            var inertia = sd.inertiaConstraint;
            inertia.worldInertia = m.worldInertia;
            inertia.movementInertiaSmoothing = m.moveSmoothing;
            inertia.movementSpeedLimit.SetValue(true, m.worldMoveLimit);
            inertia.rotationSpeedLimit.SetValue(true, m.worldTurnLimit);
            inertia.localInertia = m.localInertia;
            inertia.localMovementSpeedLimit.SetValue(true, m.localMoveLimit);
            inertia.localRotationSpeedLimit.SetValue(true, m.localTurnLimit);
            inertia.depthInertia = m.depthInertia;
            inertia.centrifualAcceleration = m.centrifugal;
            inertia.particleSpeedLimit.SetValue(true, m.particleLimit);
            sd.tetherConstraint.distanceCompression = m.tether;
            sd.distanceConstraint.stiffness.SetValue(m.distance, 1f, m.distanceTip / Mathf.Max(1e-4f, m.distance), true);
            sd.triangleBendingConstraint.stiffness = m.bending;
            sd.angleRestorationConstraint.useAngleRestoration = m.restore > 0f;
            sd.angleRestorationConstraint.stiffness.SetValue(m.restore, 1f, m.restoreTip / Mathf.Max(1e-4f, m.restore), true);
            sd.angleRestorationConstraint.velocityAttenuation = m.attenuation;
            sd.angleLimitConstraint.useAngleLimit = m.limit > 0f;
            sd.angleLimitConstraint.limitAngle.SetValue(m.limit, m.limitRoot, 1f, true);
            sd.colliderCollisionConstraint.mode = m.edge ? ColliderCollisionConstraint.Mode.Edge : ColliderCollisionConstraint.Mode.Point;
            sd.colliderCollisionConstraint.friction = m.friction;
            sd.colliderCollisionConstraint.limitDistance.SetValue(m.limitDistance);
            if (m.selfCollision)
            {
                sd.selfCollisionConstraint.selfMode = SelfCollisionConstraint.SelfCollisionMode.FullMesh;
                sd.selfCollisionConstraint.surfaceThickness.SetValue(m.thickness);
            }
        }

        void Rebuild()
        {
            if (doaRig != null)
                doaRig.RebuildSurfaces();
        }

        /// <summary>Its components and colliders out of the scene again (FighterRig: before the next cloth, and on destroy).</summary>
        public void Dispose()
        {
            MagicaManager.OnPostSimulation -= Rebuild;
            foreach (var go in made)
                if (go != null)
                {
                    // gone at once, not at the end of the frame (Destroy): a MeshCloth gives its renderer the original mesh
                    // back as it is switched off, and Magica keeps one set of data per renderer - a new MeshCloth built in
                    // the same frame took over the old one's, made in the pose of the old one's build (the same setup gave
                    // 5 % or 9.5 % mean stretch by the take before it)
                    go.SetActive(false);
                    go.transform.SetParent(null, false);
                    Object.DestroyImmediate(go);
                }
            made.Clear();
            cloths.Clear();
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
