using UnityEngine;
using UnityEngine.Audio;

namespace DropletPrototype
{
    /// <summary>Two local beds follow mission state and recent combat activity, without driving gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class StageAudioDirector : MonoBehaviour
    {
        public MissionController mission;
        public NarrativeApproachController narrative;
        public BattleAudioDirector battle;
        public SeedAudioCatalog catalog;
        public SeedAudioMixController mix;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup ambienceGroup;
        [Min(.05f)] public float fadeSeconds = 2.5f;
        [Min(0f)] public float minimumBattleTierSeconds = 8f;

        public string CurrentMusicSelection => musicLayer != null ? musicLayer.Selection : null;
        public string CurrentAmbienceSelection => ambienceLayer != null ? ambienceLayer.Selection : null;
        public AudioSource ActiveMusicSource => musicLayer != null ? musicLayer.ActiveSource : null;
        public bool IsPaused => paused;
        public int OwnedSourceCount => 5;

        sealed class Layer
        {
            readonly AudioSource[] sources;
            SeedAudioCatalog catalog;
            string selection;
            bool exact;
            int active = -1, cursor;
            float fadeDuration, fadeElapsed, outgoingVolume, targetVolume, rotationRemaining;
            float busGain = 1f;
            readonly float[] rawVolume = new float[2];
            bool fading;

            public Layer(AudioSource first, AudioSource second)
            { sources = new[] { first, second }; }
            public string Selection => selection;
            public AudioSource ActiveSource => active >= 0 ? sources[active] : null;

            public void Select(SeedAudioCatalog sourceCatalog, string value, bool exactCue,
                float seconds, bool hard)
            {
                if (!hard && selection == value && exact == exactCue) return;
                if (hard) Stop();
                catalog = sourceCatalog;
                selection = value;
                exact = exactCue;
                cursor = 0;
                StartNext(seconds);
            }

            void StartNext(float seconds)
            {
                if (catalog == null || string.IsNullOrEmpty(selection)) return;
                SeedAudioCue cue;
                bool found = exact ? catalog.TryGet(selection, out cue) : catalog.TryNext(selection, ref cursor, out cue);
                if (!found) { Stop(); return; }
                int next = active < 0 ? 0 : 1 - active;
                var incoming = sources[next];
                incoming.Stop();
                incoming.clip = cue.clip;
                incoming.loop = false;
                incoming.priority = Mathf.Clamp(cue.priority, 0, 256);
                targetVolume = Mathf.Clamp(cue.gain, 0f, 2f);
                fadeDuration = active < 0 ? 0f : Mathf.Min(Mathf.Max(.05f, seconds), cue.clip.length * .5f);
                fadeElapsed = 0f;
                fading = fadeDuration > 0f;
                outgoingVolume = active < 0 ? 0f : rawVolume[active];
                rawVolume[next] = fading ? 0f : targetVolume;
                ApplyOutput();
                incoming.Play();
                if (!fading && active >= 0) sources[active].Stop();
                active = next;
                rotationRemaining = Mathf.Max(.1f, cue.clip.length - Mathf.Max(.05f, seconds));
            }

            public void Tick(float dt, float seconds)
            {
                if (active < 0 || dt <= 0f) return;
                if (fading)
                {
                    fadeElapsed += dt;
                    float fraction = Mathf.Clamp01(fadeElapsed / fadeDuration);
                    rawVolume[active] = targetVolume * fraction;
                    var outgoing = sources[1 - active];
                    rawVolume[1 - active] = outgoingVolume * (1f - fraction);
                    ApplyOutput();
                    if (fraction >= 1f) { outgoing.Stop(); fading = false; }
                }
                rotationRemaining -= dt;
                if (!fading && rotationRemaining <= 0f) StartNext(seconds);
            }

            public void Pause()
            { foreach (var source in sources) source.Pause(); }
            public void UnPause()
            { foreach (var source in sources) source.UnPause(); }
            public void SetBusGain(float gain)
            { busGain = Mathf.Clamp01(gain); ApplyOutput(); }
            void ApplyOutput()
            { for (int i = 0; i < sources.Length; i++) sources[i].volume = rawVolume[i] * busGain; }
            public void Stop()
            {
                foreach (var source in sources) { source.Stop(); source.clip = null; }
                rawVolume[0] = rawVolume[1] = 0f;
                active = -1;
                selection = null;
                fading = false;
                rotationRemaining = 0f;
            }
        }

        Layer musicLayer, ambienceLayer;
        AudioSource overlay;
        MissionController boundMission;
        bool paused, heardCombat;
        float overlayBaseGain;
        int lastEventCount, battleTier, transitionCursor;
        float battleTierAt, lastCombatAt;

        void Awake()
        {
            musicLayer = new Layer(CreateSource("Music_A", musicGroup), CreateSource("Music_B", musicGroup));
            ambienceLayer = new Layer(CreateSource("Ambience_A", ambienceGroup), CreateSource("Ambience_B", ambienceGroup));
            overlay = CreateSource("TransitionOrPauseBed", musicGroup);
            overlay.ignoreListenerPause = true;
        }

        AudioSource CreateSource(string name, AudioMixerGroup group)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        void OnEnable() { Bind(); }
        void Start() { if (mission != null) ApplyMissionState(mission.State, true); }
        public void Bind()
        {
            if (boundMission == mission) return;
            if (boundMission != null) boundMission.StateChanged -= MissionStateChanged;
            boundMission = mission;
            if (boundMission != null) boundMission.StateChanged += MissionStateChanged;
        }

        void MissionStateChanged(MissionState state)
        {
            if (state == MissionState.Paused)
            {
                if (paused) return;
                paused = true;
                musicLayer.Pause(); ambienceLayer.Pause();
                PlayOverlay("PauseBed_01", true);
                return;
            }
            if (paused)
            {
                overlay.Stop();
                musicLayer.UnPause(); ambienceLayer.UnPause();
                paused = false;
                // Both source playback positions and the fade clock continue where they stopped.
                ApplyMissionState(state, false);
                return;
            }
            bool hard = state == MissionState.Ready || state == MissionState.Results;
            ApplyMissionState(state, hard);
        }

        void ApplyMissionState(MissionState state, bool hard)
        {
            if (catalog == null) return;
            if (hard)
            {
                overlay.Stop();
                if (state == MissionState.Ready)
                { heardCombat = false; lastEventCount = 0; battleTier = 0; transitionCursor = 0; lastCombatAt = 0f; battleTierAt = Time.time; }
            }
            string music;
            bool exact;
            switch (state)
            {
                case MissionState.Ready: music = "Ready_01"; exact = true; break;
                case MissionState.Narrative:
                    music = "Narrative_0" + (Mathf.Clamp(narrative != null ? narrative.CurrentStage : 0, 0, 3) + 1);
                    exact = true; break;
                case MissionState.Playing:
                    music = SelectBattleMusic(); exact = false; break;
                case MissionState.Results:
                    music = mission != null && mission.Won ? "ResultBed_01" : "ResultBed_02";
                    exact = true; break;
                default: return;
            }
            string previous = musicLayer.Selection;
            string ambience = "SpaceTexture";
            bool exactAmbience = false;
            if (state == MissionState.Narrative)
            {
                int act = Mathf.Clamp(narrative != null ? narrative.CurrentStage : 0, 0, 3);
                ambience = act == 0 ? "CabinBed_01" : act == 3 ? "CabinBed_03" : "CabinBed_02";
                exactAmbience = true;
            }
            musicLayer.SetBusGain(mix != null ? mix.MusicGain : 1f);
            ambienceLayer.SetBusGain(mix != null ? mix.AmbienceGain : 1f);
            musicLayer.Select(catalog, music, exact, fadeSeconds, hard);
            ambienceLayer.Select(catalog, ambience, exactAmbience, fadeSeconds, hard);
            if (!hard && previous != null && previous != music) PlayTransition();
        }

        string SelectBattleMusic()
        {
            int eventCount = battle != null ? battle.ReceivedEvents : 0;
            if (eventCount > lastEventCount)
            { heardCombat = true; lastCombatAt = Time.time; }
            lastEventCount = eventCount;
            float intensity = battle != null ? battle.Intensity : 0f;
            if (heardCombat && intensity < .08f && Time.time - lastCombatAt >= 8f) return "Aftermath";
            if (Time.time - battleTierAt >= minimumBattleTierSeconds)
            {
                int next = battleTier;
                if (battleTier == 0 && intensity >= .32f) next = 1;
                else if (battleTier == 1 && intensity >= .72f) next = 2;
                else if (battleTier == 1 && intensity < .18f) next = 0;
                else if (battleTier == 2 && intensity < .58f) next = 1;
                if (next != battleTier) { battleTier = next; battleTierAt = Time.time; }
            }
            return battleTier == 2 ? "BattleHigh" : battleTier == 1 ? "BattleMedium" : "BattleLow";
        }

        void PlayTransition()
        {
            if (paused || catalog == null || !catalog.TryNext("Transition", ref transitionCursor, out var cue)) return;
            overlay.Stop(); overlay.clip = cue.clip; overlay.loop = false;
            overlayBaseGain = Mathf.Clamp(cue.gain, 0f, 2f);
            overlay.volume = overlayBaseGain * (mix != null ? mix.MusicGain : 1f);
            overlay.priority = Mathf.Clamp(cue.priority, 0, 256);
            overlay.Play();
        }
        void PlayOverlay(string id, bool loop)
        {
            overlay.Stop();
            if (catalog == null || !catalog.TryGet(id, out var cue)) return;
            overlay.clip = cue.clip; overlay.loop = loop;
            overlayBaseGain = Mathf.Clamp(cue.gain, 0f, 2f);
            overlay.volume = overlayBaseGain * (mix != null ? mix.musicVolume : 1f);
            overlay.priority = Mathf.Clamp(cue.priority, 0, 256);
            overlay.Play();
        }

        void Update()
        {
            if (mission == null || paused) return;
            if (mission.State == MissionState.Narrative || mission.State == MissionState.Playing)
                ApplyMissionState(mission.State, false);
            float dt = Time.deltaTime;
            musicLayer.Tick(dt, fadeSeconds);
            ambienceLayer.Tick(dt, fadeSeconds);
            musicLayer.SetBusGain(mix != null ? mix.MusicGain : 1f);
            ambienceLayer.SetBusGain(mix != null ? mix.AmbienceGain : 1f);
            if (overlay.isPlaying) overlay.volume = overlayBaseGain * (mix != null ? mix.MusicGain : 1f);
        }

        void OnDisable()
        {
            if (boundMission != null) boundMission.StateChanged -= MissionStateChanged;
            boundMission = null;
            musicLayer?.Stop(); ambienceLayer?.Stop();
            if (overlay != null) overlay.Stop();
            paused = false;
        }
    }
}
