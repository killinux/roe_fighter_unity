using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace RoeFighter.Fight
{
    /// <summary>
    /// The fight: two fighters on one of the game's stages, best of three rounds, 60 seconds each.
    /// Runs at a fixed 60 steps per second.  Each step: inputs (keyboard, pad or CPU), the fighters'
    /// state machines, their poses, then the hit checks on the posed bodies (the striking bone
    /// against the opponent's body capsule), the special moves' hits, the effects, the round
    /// rules, the camera.  Step() can also be driven from the editor to film a CPU match.
    /// </summary>
    public class FightGame : MonoBehaviour
    {
        [Serializable]
        public class NamedPrefab
        {
            public string name;
            public GameObject prefab;
        }

        public FighterRig[] rigs = new FighterRig[2];
        public bool[] cpu = { false, true };
        public List<Move> moves = new List<Move>();
        public List<Special> specials = new List<Special>();
        public List<NamedPrefab> effects = new List<NamedPrefab>();
        public string hitEffect;                     // a small hit spark for strikes
        public Vector3 centre;
        public Vector3 axis = Vector3.right;         // round start: fighters stand along this line
        public float arenaRadius = 7f;
        public float startGap = 3.2f;
        public Camera cam;
        public FightHud hud;
        public int roundsToWin = 2;
        public float roundSeconds = 60f;
        public string voiceLanguage = "2";           // the game's voice language: 2 Japanese, 1 English
        public int seed = 1;
        public AudioClip music;                      // the stage's battle music, looped
        public float musicVolume = 0.3f;
        AudioSource musicSource;
        public List<MotionPack> motionPacks = new List<MotionPack>();   // basic moves that can be swapped (F3)
        public int motionPack;
        float noticeTime;

        /// <summary>A line under the timer: the motion pack and the cloth in use, for a few seconds after a switch or a new match.</summary>
        public string Notice => noticeTime <= 0f ? "" :
            string.Join("    ", new[] { Pack != null ? $"MOTIONS: {Pack.title}" : null, $"CLOTH: {RoeClothBackends.Find(FighterRig.ClothBackend).title}",
                                       RoeClothBackends.Find(FighterRig.ClothBackend).skinnedSkirt ? $"SKIRT: {(RoeSkirtRig.Drape > 0f ? "hangs" : "fitted")}" : null,
                                       rigs.Any(r => r != null && r.burst != null) ? $"CLOTHES BURST: {(RoeClothesBurst.Enabled ? "on" : "off")}" : null }.Where(s => s != null));

        public MotionPack Pack => motionPacks.Count > 0 ? motionPacks[Mathf.Clamp(motionPack, 0, motionPacks.Count - 1)] : null;

        public const float Dt = 1f / 60f;
        public readonly Fighter[] f = new Fighter[2];
        public int frame;
        public float timer;
        public int round;
        public readonly int[] wins = new int[2];
        public string message = "";
        public enum Phase { Intro, Fight, RoundOver, MatchOver }
        public Phase phase;
        float phaseTime, hitStop, slowMotion;
        float accumulator;
        readonly FightAI[] ai = new FightAI[2];

        // what the editor needs to make a video: sounds with their frame numbers
        [Serializable]
        public struct SoundEvent
        {
            public int frame;
            public string fighter, name;
            public float volume;
        }

        public readonly List<SoundEvent> soundLog = new List<SoundEvent>();

        class LiveEffect
        {
            public GameObject instance;
            public PlayableDirector director;
            public ParticleSystem[] roots;
            public float time, life, simulated = -1f;
        }

        readonly List<LiveEffect> live = new List<LiveEffect>();

        class Scheduled
        {
            public float at;
            public Action run;
        }

        readonly List<Scheduled> schedule = new List<Scheduled>();
        float clock;
        Vector3 camSide;
        Vector3 camPos, camTarget;
        float shakeTime, shakeAmplitude, shakeDuration;

        void Start()
        {
            // the player: ROEFighter.exe -roeBurst 0 starts with the clothes burst off (F6 switches it)
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-roeBurst");
            if (at >= 0 && at + 1 < args.Length)
                RoeClothesBurst.Enabled = args[at + 1] != "0";
            Setup();
        }

        public void Setup()
        {
            Application.targetFrameRate = 60;
            // the basic moves of the chosen motion pack, on both fighters
            var pack = Pack;
            if (pack != null)
            {
                foreach (var rig in rigs)
                    rig.UseMotions(pack);
                moves = pack.CopyStrikes();
                foreach (var m in moves)
                    if (string.IsNullOrEmpty(m.hitSound))
                        m.hitSound = "hit";
                noticeTime = 4f;
            }
            for (int i = 0; i < 2; i++)
            {
                rigs[i].Init();
                f[i] = new Fighter { rig = rigs[i], game = this, index = i };
                ai[i] = new FightAI(seed * 7919 + i * 104729);
            }
            f[0].foe = f[1];
            f[1].foe = f[0];
            // a new match: the outfits are whole again
            for (int i = 0; i < 2; i++)
            {
                var burst = rigs[i].burst;
                if (burst == null)
                    continue;
                burst.Restore(seed * 7919 + i * 104729 + 31);
                var who = f[i];
                burst.onPieceOff = (at, size, cloth) => PieceOff(who, at, size, cloth);
            }
            wins[0] = wins[1] = 0;
            round = 0;
            frame = 0;
            clock = 0f;
            soundLog.Clear();
            if (music != null && Application.isPlaying)
            {
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                    musicSource.loop = true;
                    musicSource.playOnAwake = false;
                    musicSource.spatialBlend = 0f;
                }
                musicSource.clip = music;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }
            if (hud != null)
                hud.Build(this);
            StartRound();
            SnapCamera();
        }

        void StartRound()
        {
            round++;
            timer = roundSeconds;
            var a = centre - axis * (startGap * 0.5f);
            var b = centre + axis * (startGap * 0.5f);
            f[0].Reset(a, Yaw(b - a));
            f[1].Reset(b, Yaw(a - b));
            foreach (var x in live)
                if (x.instance != null)
                    DestroyAny(x.instance);
            live.Clear();
            schedule.Clear();
            // what came off stays off for the whole match; the pieces still lying about go
            foreach (var rig in rigs)
                if (rig.burst != null)
                    rig.burst.ClearDebris();
            phase = Phase.Intro;
            phaseTime = 0f;
            message = $"ROUND {round}";
            hitStop = 0f;
            slowMotion = 0f;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        void Update()
        {
            if (!Application.isPlaying || f[0] == null)
                return;
            if (Input.GetKeyDown(KeyCode.F1))
                cpu[0] = !cpu[0];
            if (Input.GetKeyDown(KeyCode.F2))
                cpu[1] = !cpu[1];
            if (Input.GetKeyDown(KeyCode.F3) && motionPacks.Count > 1)
            {
                // the next motion pack; the match starts over with it
                motionPack = (motionPack + 1) % motionPacks.Count;
                Setup();
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                // the next cloth backend (Magica-style bone cloth, the 10-02 one, none); the match starts over
                FighterRig.ClothBackend = RoeClothBackends.Next(FighterRig.ClothBackend);
                Setup();
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                // the skirt's animation pose: hanging on the body (RoeSkirtRig.Drape 1) or the fitted pose
                // (0, as of 52f11de); takes effect at once
                RoeSkirtRig.Drape = RoeSkirtRig.Drape > 0f ? 0f : 1f;
                noticeTime = 4f;
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                // clothes burst on / off; off puts every outfit back on at once
                RoeClothesBurst.Enabled = !RoeClothesBurst.Enabled;
                if (!RoeClothesBurst.Enabled)
                    for (int i = 0; i < 2; i++)
                        if (rigs[i].burst != null)
                            rigs[i].burst.Restore(seed * 7919 + i * 104729 + 31 + frame);
                noticeTime = 4f;
            }
            if (phase == Phase.MatchOver && phaseTime > 2f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton7)))
                Setup();
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            var pending = new FighterInput[2];
            for (int i = 0; i < 2; i++)
                pending[i] = cpu[i] ? default : PlayerInput.Read(i);
            bool first = true;
            while (accumulator >= Dt)
            {
                // presses count once, in the first step of this frame
                Step(first ? pending : new[] { Held(pending[0]), Held(pending[1]) });
                first = false;
                accumulator -= Dt;
            }
            UpdateCamera(Time.deltaTime);
            if (hud != null)
                hud.Refresh(this);
        }

        static FighterInput Held(FighterInput i) => new FighterInput { x = i.x, y = i.y };

        /// <summary>One simulation step (1/60 s).  Human inputs in screen terms; CPU fighters decide here.</summary>
        public void Step(FighterInput[] inputs)
        {
            frame++;
            phaseTime += Dt;
            noticeTime -= Dt;
            float dt = Dt * (slowMotion > 0f ? 0.35f : 1f);
            if (slowMotion > 0f)
                slowMotion -= Dt;
            clock += dt;
            RunSchedule();

            if (hitStop > 0f)
            {
                hitStop -= Dt;
                foreach (var x in f)
                    x.rig.Tick(0f);
                StepEffects(0f);
                return;
            }

            switch (phase)
            {
                case Phase.Intro:
                    if (phaseTime > 1.2f)
                        message = "FIGHT!";
                    if (phaseTime > 2.0f)
                    {
                        message = "";
                        phase = Phase.Fight;
                        phaseTime = 0f;
                        foreach (var x in f)
                            x.Go();
                    }
                    break;
                case Phase.Fight:
                    timer = Mathf.Max(0f, timer - dt);
                    break;
            }

            var camRight = cam != null ? Flat(cam.transform.right) : Vector3.right;
            var camForward = cam != null ? Flat(cam.transform.forward) : Vector3.forward;
            for (int i = 0; i < 2; i++)
            {
                var me = f[i];
                var input = phase == Phase.Fight
                    ? (cpu[i] ? ai[i].Decide(me, dt, camRight) : inputs != null && inputs.Length > i ? inputs[i] : default)
                    : default;
                var toFoe = Flat(me.foe.pos - me.pos);
                int screenSign = Vector3.Dot(toFoe, camRight) >= 0f ? 1 : -1;
                int fwd = input.x * screenSign;
                // a side step goes into the screen (up) or out of it (down), across the line between the two
                var across = Vector3.Cross(Vector3.up, toFoe).normalized;
                if (Vector3.Dot(across, camForward) < 0f)
                    across = -across;
                var sideDir = input.y != 0 ? across * input.y : Vector3.zero;
                me.Step(dt, fwd, input.y, sideDir, input);
            }
            KeepApart();
            foreach (var x in f)
            {
                x.Place();
                x.rig.Tick(dt);
                x.AfterPose(dt);
            }
            if (phase == Phase.Fight)
                CheckStrikes();
            StepEffects(dt);
            Rules();
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }

        void KeepApart()
        {
            var d = f[1].pos - f[0].pos;
            d.y = 0f;
            float dist = d.magnitude;
            float min = Fighter.BodyRadius * 2f;
            bool special = f[0].state == FightState.Special || f[1].state == FightState.Special;
            if (dist < min && dist > 1e-4f && !special)
            {
                var push = d / dist * ((min - dist) * 0.5f);
                f[0].pos -= push;
                f[1].pos += push;
            }
            foreach (var x in f)
            {
                var r = x.pos - centre;
                r.y = 0f;
                float limit = arenaRadius - 0.4f;
                if (r.magnitude > limit)
                    x.pos = centre + r.normalized * limit + Vector3.up * (x.pos.y - centre.y);
            }
        }

        void CheckStrikes()
        {
            for (int i = 0; i < 2; i++)
            {
                var atk = f[i];
                var vic = f[1 - i];
                var p = atk.ActiveHit();
                if (p == null || !vic.Hittable || vic.state == FightState.Special)
                    continue;
                var m = atk.move;
                if (vic.DistanceToBody(p.Value) > m.radius + Fighter.BodyRadius)
                    continue;
                atk.hitDone = true;
                atk.connected = true;
                var dir = Flat(vic.pos - atk.pos);
                if (vic.CanBlock)
                {
                    vic.BlockHit(m.blockstun, dir * (m.push * 0.5f / 0.12f));
                    atk.meter = Mathf.Min(100f, atk.meter + m.meterGain * 0.5f);
                    PlaySound(vic, "block", 0.8f);
                    Spark(p.Value, 0.6f);
                    hitStop = 0.05f;
                }
                else
                {
                    vic.TakeHit(m.damage, m.hitstun, dir * (m.push / 0.12f), m.knockdown);
                    atk.meter = Mathf.Min(100f, atk.meter + m.meterGain);
                    PlaySound(atk, m.hitSound, 1f);
                    Spark(p.Value, 1f);
                    hitStop = m.damage >= 80 ? 0.1f : 0.07f;
                    Shake(m.damage >= 80 ? 0.6f : 0.3f, 0.2f);
                }
            }
        }

        // ---- specials

        public void OnSpecialStart(Fighter atk, RoeSkillSheet.Action action)
        {
            float start = clock;
            foreach (var s in action.sounds.Where(s => !s.muted))
                At(start + s.start, () => PlaySound(atk, s.clip, s.volume));
            foreach (var v in action.voices.Where(v => !v.muted))
            {
                var clip = v.clips.FirstOrDefault(c => c.language == voiceLanguage)?.clip;
                if (clip != null)
                    At(start + v.start, () => PlaySound(atk, clip, 1f));
            }
            foreach (var s in action.shakes)
                At(start + s.time, () => Shake(s.amplitude, s.duration));
            foreach (var s in action.spawns)
            {
                var prefab = effects.FirstOrDefault(e => e.name == s.prefab)?.prefab;
                if (prefab == null)
                    continue;
                At(start + s.time, () =>
                {
                    if (atk.state != FightState.Special)
                        return;
                    var at = s.kind == "target" ? atk.foe : atk;
                    var anchor = at.rig.Anchor(s.kind == "target" ? s.name : "center");
                    Spawn(prefab, anchor.position, atk.rig.transform.rotation);
                });
            }
            if (action.name == "skill3")
                slowMotion = 0.25f;
        }

        /// <summary>A skill's blows reach this far beyond the opponent's own radius (the game's receiveRadius).</summary>
        public const float SpecialReachExtra = 1.0f + 0.9f;

        /// <summary>
        /// A damage moment of a skill.  The first one decides: the opponent blocks it (holding back),
        /// has stepped out of reach (a miss), or is hit.  A hit or a block then holds the opponent
        /// until the skill is over, the way the game stages it; the super's last hit knocks down.
        /// </summary>
        public void SpecialHit(Fighter atk, Special s, int damage, bool last)
        {
            var vic = atk.foe;
            if (atk.specialOutcome == 0)
            {
                float reach = (vic.rig.sheet != null ? vic.rig.sheet.receiveRadius : 1f) + SpecialReachExtra;
                atk.specialOutcome = !vic.Hittable || Vector3.Distance(atk.pos, vic.pos) > reach ? 3 : vic.CanBlock ? 2 : 1;
            }
            if (atk.specialOutcome == 3 || !vic.Hittable)
                return;
            var dir = Flat(vic.pos - atk.pos);
            float hold = atk.SpecialRemaining + 0.15f;
            if (atk.specialOutcome == 2)
            {
                vic.hp = Mathf.Max(1, vic.hp - Mathf.RoundToInt(damage * 0.1f));
                vic.BlockHit(last ? 0.3f : hold, dir * 0.6f);
                hitStop = 0.04f;
                return;
            }
            bool knockdown = last && s.meterCost > 0f;
            vic.TakeHit(damage, last ? 0.6f : hold, dir * (last ? 2.5f : 0.4f), knockdown);
            atk.meter = Mathf.Min(100f, atk.meter + (s.meterCost > 0f ? 0f : 4f));
            hitStop = last ? 0.12f : 0.04f;
            // the super's finishing blow takes the next stage of her outfit off
            if (knockdown)
                BurstClothes(vic, dir);
        }

        // ---- clothes burst (爆衣): a super that lands and a KO each take the next stage of the victim's outfit off

        /// <summary>Take the next stage of a fighter's outfit off; false if burst is off or nothing is left.</summary>
        public bool BurstClothes(Fighter who, Vector3 dir)
        {
            var burst = who.rig.burst;
            return burst != null && burst.Drop(dir, who.pos.y);
        }

        int burstSoundFrame = -1;

        /// <summary>A piece came off: armour gives a spark; one sound per fighter and step.</summary>
        void PieceOff(Fighter who, Vector3 at, float size, bool cloth)
        {
            if (!cloth)
                Spark(at, Mathf.Clamp(size * 2.5f, 0.5f, 1.2f));
            if (burstSoundFrame != frame * 2 + who.index)
            {
                burstSoundFrame = frame * 2 + who.index;
                PlaySound(who, "hit", 0.8f);
            }
        }

        void At(float time, Action run)
        {
            schedule.Add(new Scheduled { at = time, run = run });
        }

        void RunSchedule()
        {
            for (int i = 0; i < schedule.Count; i++)
            {
                if (schedule[i].at > clock)
                    continue;
                var s = schedule[i];
                schedule.RemoveAt(i--);
                s.run();
            }
        }

        // ---- effects (the game's prefabs, stepped like the game's timelines step them)

        void Spark(Vector3 at, float scale)
        {
            var prefab = effects.FirstOrDefault(e => e.name == hitEffect)?.prefab;
            if (prefab == null)
                return;
            var e = Spawn(prefab, at, cam != null ? Quaternion.LookRotation(cam.transform.position - at) : Quaternion.identity);
            if (e != null)
                e.instance.transform.localScale *= scale;
        }

        LiveEffect Spawn(GameObject prefab, Vector3 at, Quaternion rotation)
        {
            var go = Instantiate(prefab, at, rotation);
            go.hideFlags = HideFlags.DontSave;
            var e = new LiveEffect { instance = go };
            e.director = go.GetComponentInChildren<PlayableDirector>(true);
            if (e.director != null && e.director.playableAsset != null)
            {
                e.director.playOnAwake = false;
                e.director.timeUpdateMode = DirectorUpdateMode.Manual;
                e.director.RebuildGraph();
                e.life = (float)e.director.playableAsset.duration;
            }
            else
            {
                e.director = null;
                e.roots = go.GetComponentsInChildren<ParticleSystem>(true)
                    .Where(p => p.transform.parent == null || p.transform.parent.GetComponentInParent<ParticleSystem>() == null).ToArray();
                foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    p.useAutoRandomSeed = false;
                }
                float longest = 0.5f;
                foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = p.main;
                    longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                }
                e.life = Mathf.Min(longest, 4f);
            }
            live.Add(e);
            return e;
        }

        void StepEffects(float dt)
        {
            foreach (var rig in rigs)
                if (rig.burst != null)
                    rig.burst.Step(dt, centre, arenaRadius);
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                e.time += dt;
                if (e.instance == null || e.time > e.life)
                {
                    if (e.instance != null)
                        DestroyAny(e.instance);
                    live.RemoveAt(i--);
                    continue;
                }
                if (e.director != null)
                {
                    e.director.time = e.time;
                    e.director.Evaluate();
                }
                else if (e.roots != null)
                {
                    foreach (var p in e.roots)
                    {
                        if (e.simulated < 0f)
                            p.Simulate(0f, true, true, false);
                        p.Simulate(e.time - Mathf.Max(0f, e.simulated), true, false, true);
                    }
                    e.simulated = e.time;
                }
            }
        }

        static void DestroyAny(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        void PlaySound(Fighter who, string name, float volume)
        {
            if (string.IsNullOrEmpty(name))
                return;
            who.rig.Sound(name, volume);
            soundLog.Add(new SoundEvent { frame = frame, fighter = who.rig.id, name = name, volume = volume });
        }

        // ---- rounds

        void Rules()
        {
            switch (phase)
            {
                case Phase.Fight:
                {
                    bool ko0 = f[0].hp <= 0, ko1 = f[1].hp <= 0;
                    if (ko0 || ko1 || timer <= 0f)
                    {
                        int winner = ko0 && !ko1 ? 1 : ko1 && !ko0 ? 0 : f[0].hp > f[1].hp ? 0 : f[1].hp > f[0].hp ? 1 : -1;
                        message = ko0 || ko1 ? "K.O." : "TIME UP";
                        if (ko0 || ko1)
                            slowMotion = 0.8f;
                        // a KO takes the next stage of the loser's outfit off
                        if (ko0)
                            BurstClothes(f[0], f[0].lastHitDir);
                        if (ko1)
                            BurstClothes(f[1], f[1].lastHitDir);
                        if (winner >= 0)
                            wins[winner]++;
                        phase = Phase.RoundOver;
                        phaseTime = 0f;
                        roundWinner = winner;
                    }
                    break;
                }
                case Phase.RoundOver:
                    if (phaseTime > 1.6f && phaseTime - Dt <= 1.6f)
                    {
                        for (int i = 0; i < 2; i++)
                            f[i].Finish(i == roundWinner);
                        message = roundWinner >= 0 ? $"{f[roundWinner].rig.displayName} WINS" : "DRAW";
                    }
                    if (phaseTime > 4.2f)
                    {
                        if (wins[0] >= roundsToWin || wins[1] >= roundsToWin)
                        {
                            phase = Phase.MatchOver;
                            phaseTime = 0f;
                            int w = wins[0] >= roundsToWin ? 0 : 1;
                            message = $"{f[w].rig.displayName} WINS THE MATCH";
                        }
                        else
                        {
                            StartRound();
                        }
                    }
                    break;
            }
        }

        int roundWinner = -1;

        // ---- camera: from the side of the line between the two, far enough to keep both in the picture

        public void SnapCamera()
        {
            camSide = Vector3.zero;
            UpdateCamera(10f);
        }

        public void UpdateCamera(float dt)
        {
            if (cam == null || f[0] == null)
                return;
            var a = f[0].pos;
            var b = f[1].pos;
            var line = Flat(b - a);
            var side = Vector3.Cross(line, Vector3.up).normalized;
            if (camSide.sqrMagnitude < 0.5f)
                camSide = Flat(cam.transform.position - (a + b) * 0.5f);
            if (Vector3.Dot(side, camSide) < 0f)
                side = -side;
            camSide = Vector3.Slerp(camSide, side, 1f - Mathf.Exp(-dt * 3f)).normalized;
            float sep = Vector3.Distance(a, b);
            float distance = Mathf.Clamp(sep * 0.85f + 2.9f, 3.6f, 9.5f);
            var target = (a + b) * 0.5f + Vector3.up * 1.0f;
            var eye = target + camSide * distance + Vector3.up * 0.45f;
            float k = 1f - Mathf.Exp(-dt * 5f);
            camPos = dt >= 1f ? eye : Vector3.Lerp(camPos, eye, k);
            camTarget = dt >= 1f ? target : Vector3.Lerp(camTarget, target, k);
            var shake = Vector3.zero;
            if (shakeTime < shakeDuration)
            {
                shakeTime += dt;
                float fade = 1f - shakeTime / Mathf.Max(0.01f, shakeDuration);
                float amp = shakeAmplitude * 0.045f * fade * fade;
                float w = 2f * Mathf.PI * 14f * shakeTime;
                shake = cam.transform.right * (amp * Mathf.Sin(w)) + Vector3.up * (amp * Mathf.Sin(w * 1.37f + 1.3f));
            }
            cam.transform.position = camPos + shake;
            cam.transform.LookAt(camTarget + shake, Vector3.up);
        }

        public void Shake(float amplitude, float duration)
        {
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTime = 0f;
        }
    }
}
