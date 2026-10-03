using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DropletPrototype
{
    public enum SeedUiAction { Focus, Confirm, Back, Toggle, Slider, Skip, History }

    /// <summary>Three bounded 2D voices for real UI actions and observed mission feedback.</summary>
    [DisallowMultipleComponent]
    public sealed class UiMissionAudioController : MonoBehaviour
    {
        public MissionController mission;
        public NarrativeApproachController narrative;
        public ScoreSystem score;
        public SeedAudioCatalog catalog;
        public SeedAudioMixController mix;
        public AudioMixerGroup uiGroup;
        public AudioMixerGroup missionGroup;

        public int OwnedSourceCount => 3;
        public int PlayedUiCount { get; private set; }
        public int PlayedMissionCount { get; private set; }

        AudioSource[] ui;
        AudioSource feedback;
        readonly float[] uiBaseGain = new float[2];
        float feedbackBaseGain;
        MissionController boundMission;
        NarrativeApproachController boundNarrative;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly Dictionary<string, int> cursors = new Dictionary<string, int>();
        MissionState previousState;
        bool armed, previousBoundary;
        float previousRemaining;
        int previousRecovery, previousMultiplier, uiIndex;

        void Awake()
        {
            ui = new[] { CreateSource("Ui_A", uiGroup), CreateSource("Ui_B", uiGroup) };
            feedback = CreateSource("MissionFeedback", missionGroup);
        }
        AudioSource CreateSource(string name, AudioMixerGroup group)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = group;
            return source;
        }
        void OnEnable() { Bind(); }
        void Start()
        {
            if (mission != null) { previousState = mission.State; ResetTracking(); }
            armed = true;
        }
        public void Bind()
        {
            if (boundNarrative != narrative)
            {
                if (boundNarrative != null) boundNarrative.Skipped -= OnNarrativeSkipped;
                boundNarrative = narrative;
                if (boundNarrative != null) boundNarrative.Skipped += OnNarrativeSkipped;
            }
            if (boundMission != mission)
            {
                if (boundMission != null)
                {
                    boundMission.StateChanged -= OnMissionState;
                    boundMission.Restarted -= OnRestarted;
                    boundMission.ResultsShown -= OnResults;
                }
                boundMission = mission;
                if (boundMission != null)
                {
                    boundMission.StateChanged += OnMissionState;
                    boundMission.Restarted += OnRestarted;
                    boundMission.ResultsShown += OnResults;
                }
            }
        }
        void OnNarrativeSkipped() { NotifyUi(SeedUiAction.Skip); }
        void OnMissionState(MissionState state)
        {
            if (armed && state == MissionState.Paused && previousState != MissionState.Paused)
                PlayFamily("Pause", true);
            else if (armed && previousState == MissionState.Paused &&
                (state == MissionState.Playing || state == MissionState.Narrative))
                PlayFamily("Resume", true);
            if (state == MissionState.Ready || state == MissionState.Results)
            {
                feedback.Stop();
                foreach (var source in ui) source.Stop();
                ResetTracking();
            }
            else if (state == MissionState.Playing && previousState != MissionState.Playing)
                ResetTracking();
            previousState = state;
        }
        void OnRestarted()
        {
            feedback.Stop();
            foreach (var source in ui) source.Stop();
            ResetTracking();
            if (armed) PlayFamily("Restart", true);
        }
        void OnResults()
        {
            if (mission == null) return;
            string id = mission.Won ? "ResultSting_01" :
                mission.EscapedCount > 0 ? "ResultSting_02" : "ResultSting_03";
            PlayCue(id, false);
        }

        public void NotifyUi(SeedUiAction action)
        {
            string family;
            switch (action)
            {
                case SeedUiAction.Focus: family = "Focus"; break;
                case SeedUiAction.Confirm: family = "Confirm"; break;
                case SeedUiAction.Back: family = "Back"; break;
                case SeedUiAction.Toggle: family = "Toggle"; break;
                case SeedUiAction.Slider: family = "Slider"; break;
                case SeedUiAction.Skip: family = "Skip"; break;
                case SeedUiAction.History: family = "History"; break;
                default: return;
            }
            PlayFamily(family, true);
        }

        bool PlayFamily(string family, bool isUi)
        {
            if (catalog == null) return false;
            int cursor = cursors.TryGetValue(family, out var stored) ? stored : 0;
            if (!catalog.TryNext(family, ref cursor, out var cue)) return false;
            cursors[family] = cursor;
            return Play(cue, isUi);
        }
        bool PlayCue(string id, bool isUi)
        { return catalog != null && catalog.TryGet(id, out var cue) && Play(cue, isUi); }
        bool Play(SeedAudioCue cue, bool isUi)
        {
            float now = Time.unscaledTime;
            float cooldown = Mathf.Max(cue.cooldownSeconds,
                cue.family == "Focus" ? .08f : cue.family == "Slider" ? .06f : 0f);
            if (lastPlayed.TryGetValue(cue.family, out var previous) && now - previous < cooldown) return false;
            lastPlayed[cue.family] = now;
            AudioSource source;
            if (isUi)
            {
                int slot = uiIndex++ % ui.Length;
                source = ui[slot];
                uiBaseGain[slot] = Mathf.Clamp(cue.gain, 0f, 2f);
                source.volume = uiBaseGain[slot] * (mix != null ? mix.UiGain : 1f);
                PlayedUiCount++;
            }
            else
            {
                source = feedback;
                feedbackBaseGain = Mathf.Clamp(cue.gain, 0f, 2f);
                source.volume = feedbackBaseGain * (mix != null ? mix.MissionGain : 1f);
                PlayedMissionCount++;
            }
            source.Stop();
            source.clip = cue.clip;
            source.loop = false;
            source.priority = Mathf.Clamp(cue.priority, 0, 256);
            source.Play();
            return true;
        }

        void ResetTracking()
        {
            previousBoundary = mission != null && mission.settings != null && mission.NearBoundary;
            previousRemaining = mission != null ? mission.Remaining : 0f;
            previousRecovery = mission != null ? mission.RecoveryCount : 0;
            previousMultiplier = score != null ? score.Multiplier : 1;
            lastPlayed.Clear();
            cursors.Clear();
            uiIndex = 0;
        }

        void Update()
        {
            if (mix != null)
            {
                for (int i = 0; i < ui.Length; i++) ui[i].volume = uiBaseGain[i] * mix.UiGain;
                feedback.volume = feedbackBaseGain * mix.MissionGain;
            }
            if (mission == null || mission.State != MissionState.Playing) return;
            bool boundary = mission.settings != null && mission.NearBoundary;
            int recovery = mission.RecoveryCount;
            string feedbackFamily = null;
            if (recovery > previousRecovery) feedbackFamily = "Return";
            else if (boundary && !previousBoundary) feedbackFamily = "Boundary";
            previousRecovery = recovery;
            previousBoundary = boundary;

            float remaining = mission.Remaining;
            if (remaining < previousRemaining)
            {
                bool countdown = false;
                for (int threshold = 10; threshold >= 1; threshold--)
                    if (previousRemaining > threshold && remaining <= threshold)
                    { countdown = true; }
                if (countdown && feedbackFamily == null) feedbackFamily = "Countdown";
                else if ((previousRemaining > 30f && remaining <= 30f) ||
                         (previousRemaining > 60f && remaining <= 60f))
                    if (feedbackFamily == null) feedbackFamily = "TimeWarning";
            }
            previousRemaining = remaining;

            if (score != null)
            {
                int multiplier = score.Multiplier;
                if (multiplier > previousMultiplier)
                {
                    if (score.settings != null && multiplier >= score.settings.maxMultiplier)
                    {
                        if (feedbackFamily == null) feedbackFamily = "ComboCap";
                    }
                    else if (feedbackFamily == null) feedbackFamily = "Combo";
                }
                previousMultiplier = multiplier;
            }
            if (feedbackFamily != null) PlayFamily(feedbackFamily, false);
        }

        void OnDisable()
        {
            if (boundMission != null)
            {
                boundMission.StateChanged -= OnMissionState;
                boundMission.Restarted -= OnRestarted;
                boundMission.ResultsShown -= OnResults;
            }
            boundMission = null;
            if (boundNarrative != null) boundNarrative.Skipped -= OnNarrativeSkipped;
            boundNarrative = null;
            if (ui != null) foreach (var source in ui) source.Stop();
            if (feedback != null) feedback.Stop();
            armed = false;
        }
    }
}
