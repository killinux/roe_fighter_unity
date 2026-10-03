using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// Gives the ROE characters (3ds Max Biped rigs, Generic in the game) a Unity Humanoid avatar,
    /// so that humanoid animation - a fighting move set shared by all characters - can drive them.
    ///
    /// The game's prefabs have no model importer behind them, so the avatar is built from code:
    /// map the Biped bones, straighten the limbs into a T-pose (the bind pose has the elbows bent
    /// 11-17 degrees), then AvatarBuilder.  The feet are left as they are: these characters stand
    /// on high heels, and keeping that as the "neutral" foot makes flat-footed animation land
    /// on the heels instead of flattening the foot.
    /// </summary>
    public static class RoeHumanoid
    {
        public static string AvatarPath(string id) => $"{RoeFighterBuilder.OutDir}/{id}/{id}_humanoid_avatar.asset";
        public static string FighterPath(string id) => $"{RoeFighterBuilder.OutDir}/{id}/{id}_fighter.prefab";

        // Unity human bone -> Biped bone.  UpperChest only when the rig has Spine2.
        static readonly (string human, string bone)[] Body =
        {
            ("Hips", "Bip001 Pelvis"),
            ("Spine", "Bip001 Spine"),
            ("Chest", "Bip001 Spine1"),
            ("UpperChest", "Bip001 Spine2"),
            ("Neck", "Bip001 Neck"),
            ("Head", "Bip001 Head"),
            ("LeftShoulder", "Bip001 L Clavicle"),
            ("LeftUpperArm", "Bip001 L UpperArm"),
            ("LeftLowerArm", "Bip001 L Forearm"),
            ("LeftHand", "Bip001 L Hand"),
            ("RightShoulder", "Bip001 R Clavicle"),
            ("RightUpperArm", "Bip001 R UpperArm"),
            ("RightLowerArm", "Bip001 R Forearm"),
            ("RightHand", "Bip001 R Hand"),
            ("LeftUpperLeg", "Bip001 L Thigh"),
            ("LeftLowerLeg", "Bip001 L Calf"),
            ("LeftFoot", "Bip001 L Foot"),
            ("LeftToes", "Bip001 L Toe0"),
            ("RightUpperLeg", "Bip001 R Thigh"),
            ("RightLowerLeg", "Bip001 R Calf"),
            ("RightFoot", "Bip001 R Foot"),
            ("RightToes", "Bip001 R Toe0"),
        };

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };
        static readonly string[] FingerParts = { "Proximal", "Intermediate", "Distal" };

        /// <summary>human bone name -> transform, for the bones this rig has.</summary>
        public static Dictionary<string, Transform> MapBones(GameObject character)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in character.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name))
                    byName[t.name] = t;

            var map = new Dictionary<string, Transform>();
            foreach (var (human, bone) in Body)
                if (byName.TryGetValue(bone, out var t))
                    map[human] = t;
            foreach (var side in new[] { ("Left", "L"), ("Right", "R") })
            {
                for (int finger = 0; finger < 5; finger++)
                {
                    for (int part = 0; part < 3; part++)
                    {
                        // Biped: Finger0, Finger01, Finger02 = thumb; Finger1, Finger11, Finger12 = index; ...
                        string bone = $"Bip001 {side.Item2} Finger{finger}" + (part == 0 ? "" : part.ToString());
                        if (byName.TryGetValue(bone, out var t))
                            map[$"{side.Item1} {FingerNames[finger]} {FingerParts[part]}"] = t;
                    }
                }
            }
            return map;
        }

        static void Align(Transform bone, Transform child, Vector3 worldDirection)
        {
            if (bone == null || child == null)
                return;
            var current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-10f)
                return;
            bone.rotation = Quaternion.FromToRotation(current, worldDirection) * bone.rotation;
        }

        /// <summary>
        /// Straighten arms, fingers and legs of the instance (which must be in its bind pose).
        /// Returns the largest correction in degrees, for the log.
        /// </summary>
        public static float EnforceTPose(GameObject character, Dictionary<string, Transform> map)
        {
            var forward = RoeShowcase.Forward(character);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            float worst = 0f;

            void Do(string bone, string child, Vector3 dir)
            {
                if (!map.TryGetValue(bone, out var b) || !map.TryGetValue(child, out var c))
                    return;
                var before = b.rotation;
                Align(b, c, dir);
                worst = Mathf.Max(worst, Quaternion.Angle(before, b.rotation));
            }

            foreach (var (side, dir) in new[] { ("Left", -right), ("Right", right) })
            {
                Do($"{side}UpperArm", $"{side}LowerArm", dir);
                Do($"{side}LowerArm", $"{side}Hand", dir);
                Do($"{side}Hand", $"{side} Middle Proximal", dir);
                foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" })
                {
                    Do($"{side} {finger} Proximal", $"{side} {finger} Intermediate", dir);
                    Do($"{side} {finger} Intermediate", $"{side} {finger} Distal", dir);
                }
                Do($"{side}UpperLeg", $"{side}LowerLeg", Vector3.down);
                Do($"{side}LowerLeg", $"{side}Foot", Vector3.down);
            }
            return worst;
        }

        /// <summary>
        /// Some outfits hang the thighs from the spine and the clavicles from the neck (the two
        /// 3ds Max Biped options).  A humanoid Animator keeps bone lengths fixed, so on such a rig
        /// the hip joints would swing with every bend of the spine.  Moving those bones under the
        /// pelvis / upper chest (same world pose, skinning unaffected) gives every outfit the
        /// hierarchy Unity expects.  Returns how many bones were moved.
        /// </summary>
        public static int NormalizeHierarchy(Dictionary<string, Transform> map)
        {
            int moved = 0;
            void Reparent(string child, string parent)
            {
                if (!map.TryGetValue(child, out var c) || !map.TryGetValue(parent, out var p) || c.parent == p)
                    return;
                c.SetParent(p, true);
                moved++;
            }
            Reparent("LeftUpperLeg", "Hips");
            Reparent("RightUpperLeg", "Hips");
            string chest = map.ContainsKey("UpperChest") ? "UpperChest" : "Chest";
            Reparent("LeftShoulder", chest);
            Reparent("RightShoulder", chest);
            return moved;
        }

        /// <summary>The nodes between the Animator root and the hips (Root, Bip001).</summary>
        public static List<Transform> AboveHips(GameObject character, Dictionary<string, Transform> map)
        {
            var result = new List<Transform>();
            for (var t = map["Hips"].parent; t != null && t != character.transform; t = t.parent)
                result.Add(t);
            return result;
        }

        /// <summary>
        /// Weapon-like branches: children of the nodes above the hips that carry skinned bones
        /// (a08's Point007_L / Point007_R, g04's Bip001 Prop1 with the fan below it).
        /// </summary>
        public static List<Transform> Attachments(GameObject character, Dictionary<string, Transform> map)
        {
            var skinned = new HashSet<Transform>(character.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(r => r.bones).Where(b => b != null));
            var above = AboveHips(character, map);
            var hips = map["Hips"];
            var result = new List<Transform>();
            foreach (var node in above)
                foreach (Transform child in node)
                    if (child != hips && !above.Contains(child) && child.GetComponentsInChildren<Transform>(true).Any(skinned.Contains))
                        result.Add(child);
            return result;
        }

        static readonly string[] HoldingBones = { "LeftHand", "RightHand", "Hips", "Spine", "Chest", "UpperChest", "Head" };

        /// <summary>
        /// Makes the fighter rig usable with animation that knows nothing about this outfit:
        ///   - weapons are parented to the bone that holds them in the outfit's own idle (in the
        ///     game they float in root space and every clip animates them to follow the hand);
        ///   - every other non-human bone takes its idle value as default (g04's fan is wide open
        ///     and several times its in-game size in the bind pose; the idle folds and scales it).
        /// </summary>
        static string ApplyIdleDefaults(RoeManifest.Character c, GameObject prefab, GameObject fighter, Dictionary<string, Transform> map)
        {
            var idle = c.LoadClip("idle_01");
            if (idle == null)
                return "no idle_01 clip: bind pose kept";
            var reference = Object.Instantiate(prefab);
            reference.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            RoeCapture.Pose(reference, idle, 0f);
            var refMap = MapBones(reference);
            var refByName = new Dictionary<string, Transform>();
            foreach (var t in reference.GetComponentsInChildren<Transform>(true))
                if (!refByName.ContainsKey(t.name))
                    refByName[t.name] = t;

            var human = new HashSet<Transform>(map.Values);
            var above = AboveHips(fighter, map);
            var attachments = Attachments(fighter, map);
            var notes = new List<string>();
            foreach (var att in attachments)
            {
                if (!refByName.TryGetValue(att.name, out var r))
                    continue;
                string best = null;
                float bestDistance = float.MaxValue;
                foreach (var candidate in HoldingBones)
                {
                    if (!refMap.TryGetValue(candidate, out var bone))
                        continue;
                    float d = Vector3.Distance(bone.position, r.position);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = candidate;
                    }
                }
                if (best == null)
                    continue;
                var rel = refMap[best].worldToLocalMatrix * r.localToWorldMatrix;
                att.SetParent(map[best], false);
                att.localPosition = rel.GetColumn(3);
                att.localRotation = rel.rotation;
                att.localScale = rel.lossyScale;
                notes.Add($"{att.name} -> {map[best].name} ({bestDistance:F2} m away in the idle)");
            }

            int posed = 0;
            foreach (var t in fighter.GetComponentsInChildren<Transform>(true))
            {
                if (t == fighter.transform || human.Contains(t) || above.Contains(t) || attachments.Contains(t))
                    continue;
                if (!refByName.TryGetValue(t.name, out var r) || r.parent == null || r.parent.name != t.parent.name)
                    continue;
                t.localPosition = r.localPosition;
                t.localRotation = r.localRotation;
                t.localScale = r.localScale;
                posed++;
            }
            RoeCapture.EndPosing();
            Object.DestroyImmediate(reference);
            return $"{posed} extra bones at their idle values; held: {(notes.Count > 0 ? string.Join(", ", notes) : "nothing")}";
        }

        static Avatar BuildAvatar(GameObject posed, Dictionary<string, Transform> map)
        {
            var human = new List<HumanBone>();
            foreach (var pair in map)
            {
                var hb = new HumanBone { humanName = pair.Key, boneName = pair.Value.name };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }

            var skeleton = new List<SkeletonBone>();
            foreach (var t in posed.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                });
            }

            var description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                // Where the roll of a limb goes: 0 = into the limb bone itself (elbow, knee side),
                // 1 = into the next joint (wrist, ankle).  The game's rigs keep the roll out of the
                // limb bones and have separate twist helpers below them, so 1 reproduces the original
                // animation; with Unity's default 0.5 the helpers ended up 7-13 cm off, with 0 12-22 cm.
                upperArmTwist = 1f,
                lowerArmTwist = 1f,
                upperLegTwist = 1f,
                lowerLegTwist = 1f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            return AvatarBuilder.BuildHumanAvatar(posed, description);
        }

        /// <summary>
        /// Builds the humanoid version of a character: &lt;id&gt;_fighter.prefab (normalized hierarchy,
        /// humanoid avatar on its Animator) and the avatar asset.  The Generic prefab stays as it is.
        /// </summary>
        public static GameObject BuildFighter(RoeManifest.Character c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoeFighterBuilder.PrefabPath(c.id));
            if (prefab == null)
                prefab = RoeFighterBuilder.Build(c);
            var go = Object.Instantiate(prefab);
            go.name = c.id + "_fighter";
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var goMap = MapBones(go);
            int moved = NormalizeHierarchy(goMap);
            string idleNote = ApplyIdleDefaults(c, prefab, go, goMap);

            // the avatar is defined on a T-posed copy; the prefab keeps the bind pose
            var posed = Object.Instantiate(go);
            posed.name = go.name;
            var posedMap = MapBones(posed);
            var forward = RoeShowcase.Forward(posed);
            float corrected = EnforceTPose(posed, posedMap);
            var avatar = BuildAvatar(posed, posedMap);
            avatar.name = c.id + "_humanoid";
            bool ok = avatar.isValid && avatar.isHuman;
            int nodes = posed.GetComponentsInChildren<Transform>(true).Length;
            Object.DestroyImmediate(posed);
            if (!ok)
            {
                Debug.LogError($"[ROE] {c.id}: humanoid avatar is NOT valid (valid={avatar.isValid}, human={avatar.isHuman})");
                Object.DestroyImmediate(go);
                return null;
            }

            string avatarPath = AvatarPath(c.id);
            Directory.CreateDirectory(Path.GetDirectoryName(avatarPath));
            AssetDatabase.DeleteAsset(avatarPath);
            AssetDatabase.CreateAsset(avatar, avatarPath);

            var animator = go.GetComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, FighterPath(c.id));
            Object.DestroyImmediate(go);

            Debug.Log($"[ROE] {c.id}: fighter prefab {FighterPath(c.id)} + avatar {avatarPath}; {posedMap.Count} human bones, " +
                      $"{moved} bones re-parented, bind pose faces {forward}, largest T-pose correction {corrected:F1} deg, {nodes} nodes");
            Debug.Log($"[ROE] {c.id}: {idleNote}");
            return saved;
        }

        [MenuItem("ROE Fighter/Build/Humanoid fighters")]
        public static void BuildAll()
        {
            foreach (var c in RoeManifest.Load().Chosen())     // -roeChars b10,g05: only these
                BuildFighter(c);
            AssetDatabase.SaveAssets();
        }
    }
}
