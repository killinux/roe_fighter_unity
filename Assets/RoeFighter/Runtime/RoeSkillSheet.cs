using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// How the game shows one unit's battle actions (written by tools/skill_sheet.py from the
    /// game's own timelines and skill views, file Assets/ROE/&lt;id&gt;/skill_sheet_unity.json).
    /// Times are seconds from the start of the action.
    /// </summary>
    [Serializable]
    public class RoeSkillSheet
    {
        [Serializable]
        public class NodeStep
        {
            public string name;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        /// <summary>A node below the unit root: its path and the local transform of every node on the way.</summary>
        [Serializable]
        public class Node
        {
            public string path;
            public List<NodeStep> nodes = new List<NodeStep>();
        }

        [Serializable]
        public class NamedNode
        {
            public string name;
            public Node node;
        }

        [Serializable]
        public class Clip
        {
            public string clip;
            public float start, end, clipIn, speed;
            public bool muted;
            public string target;
        }

        /// <summary>A damage number: when it appears and its share of the damage.  what = hurt, directhurt, or a buff name.</summary>
        [Serializable]
        public class Hit
        {
            public float time, ratio;
            public string what;
            public bool IsDamage => what != null && what.ToLowerInvariant().Contains("hurt");
        }

        /// <summary>kind: attack (in front of the target), return (back home), anchor (a formation node), target, self.</summary>
        [Serializable]
        public class Move
        {
            public float start, end, radius;
            public string kind, name;
        }

        [Serializable]
        public class Turn
        {
            public float start, end;
            public string kind, to, name;
        }

        [Serializable]
        public class Shake
        {
            public float time, duration, amplitude, frequency;
        }

        [Serializable]
        public class Effect
        {
            public float start, end, clipIn, speed;
            public bool muted;
            public string prefab, bundle, track;
            public Node anchor;
        }

        /// <summary>An effect the game spawns at a target (kind = target, name = the target's anchor: mark, hurt ...).</summary>
        [Serializable]
        public class Spawn
        {
            public float time, radius;
            public string prefab, bundle, kind, name;
        }

        [Serializable]
        public class Sound
        {
            public float start, volume;
            public string clip;
            public bool muted;
        }

        [Serializable]
        public class VoiceClip
        {
            public string language, clip;
        }

        [Serializable]
        public class Voice
        {
            public float start;
            public string name;
            public bool muted;
            public List<VoiceClip> clips = new List<VoiceClip>();
        }

        [Serializable]
        public class SkillCamera
        {
            public float start, end, fov;
            public Node node, follow, lookAt;
        }

        [Serializable]
        public class Action
        {
            public string name, timeline;
            public float duration;
            public bool loop;
            public List<Clip> clips = new List<Clip>();
            public List<Hit> hits = new List<Hit>();
            public List<Move> moves = new List<Move>();
            public List<Turn> turns = new List<Turn>();
            public List<Shake> shakes = new List<Shake>();
            public List<Effect> effects = new List<Effect>();
            public List<Spawn> spawns = new List<Spawn>();
            public List<Sound> sounds = new List<Sound>();
            public List<Voice> voices = new List<Voice>();
            public List<SkillCamera> cameras = new List<SkillCamera>();

            /// <summary>The clip that moves the character itself (the first one that is not muted).</summary>
            public Clip Body => clips.FirstOrDefault(c => !c.muted);

            /// <summary>How long the character is busy: to the end of its own animation.</summary>
            public float BodyEnd => Body != null ? Body.end : duration;

            public IEnumerable<Hit> DamageHits => hits.Where(h => h.IsDamage);

            /// <summary>
            /// The blows of the skill in a fight: its damage numbers; a skill without any (g05's skill 2 and super heal
            /// in the game) strikes at the moments its effects land instead, one blow per moment.
            /// </summary>
            public List<Hit> Blows
            {
                get
                {
                    var damage = DamageHits.OrderBy(h => h.time).ToList();
                    return damage.Count > 0 ? damage
                        : hits.GroupBy(h => h.time).OrderBy(g => g.Key).Select(g => new Hit { time = g.Key, ratio = 1f, what = "hurt" }).ToList();
                }
            }
        }

        public string unit, id, model;
        public float receiveRadius = 1f;
        public List<NamedNode> anchors = new List<NamedNode>();
        public List<Action> actions = new List<Action>();

        public Action Find(string name)
        {
            return actions.FirstOrDefault(a => a.name == name);
        }

        public static string PathOf(string id) => $"Assets/ROE/{id}/skill_sheet_unity.json";

        public static RoeSkillSheet FromJson(string json)
        {
            return JsonUtility.FromJson<RoeSkillSheet>(json);
        }
    }
}
