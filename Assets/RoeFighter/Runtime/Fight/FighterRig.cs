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

        public bool Ready => graph.IsValid();
        public string Current => current >= 0 ? clips[current].name : "";
        public float CurrentTime => current >= 0 ? (float)playables[current].GetTime() : 0f;

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
            animator.Rebind();

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
            {
                weight[guard] = target[guard] = 1f;
                Mixer(guard).SetInputWeight(slot[guard], 1f);
            }
            gameWeight = gameTarget = 0f;
            foreach (var d in directors)
            {
                if (d.director == null)
                    continue;
                d.director.playOnAwake = false;
                d.director.timeUpdateMode = DirectorUpdateMode.Manual;
            }
            current = -1;
            ShowWeapons(false);
        }

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
            layers.SetInputWeight(2, gameWeight);
            for (int i = 0; i < playables.Length; i++)
            {
                if (weight[i] <= 0f && target[i] <= 0f)
                    continue;
                weight[i] = Mathf.MoveTowards(weight[i], target[i], step);
                Mixer(i).SetInputWeight(slot[i], weight[i]);
                double t = playables[i].GetTime() + dt * rate[i];
                var c = clips[i];
                if (c.loop && c.clip.length > 0f)
                    t %= c.clip.length;
                else
                    t = Math.Min(t, c.clip.length);
                playables[i].SetTime(t);
            }
            graph.Evaluate(0f);
        }

        // ---- skills: the game's timeline in step with the model

        /// <summary>Start (or keep showing) a skill's timeline at a time; null stops it.</summary>
        public void ShowTimeline(string action, float time)
        {
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
