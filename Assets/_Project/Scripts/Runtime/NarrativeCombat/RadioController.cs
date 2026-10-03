using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DropletPrototype
{
    /// <summary>One bounded radio queue for the whole fleet. Audio and captions never own gameplay.</summary>
    public sealed class RadioController : MonoBehaviour
    {
        public RadioLibrary library;
        public FleetCombatSimulation fleet;
        public MissionController mission;
        public Transform listener;
        public Camera viewCamera;
        public AudioSource voiceSource, signalSource, alarmSource, bedSource;
        public AudioMixerSnapshot normalMix, broadcastMix;
        public SeedAudioMixController seedMix;
        public bool showVoiceProvenance;
        public RadioPresenter presenter;
        public string CurrentText { get; private set; } = "等待截获信号";
        public string CurrentChannel { get; private set; } = "无线电监听";
        public string SignalStatus { get; private set; } = "待机";
        public ShipTarget CurrentSpeaker { get; private set; }
        public int QueuedCount => queue.Count;
        public int InterruptedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int SpokenCount { get; private set; }
        public int HistoryRevision { get; private set; }
        public bool IsBroadcasting => clock < speakingUntil;
        public bool IsDucking => ducked;
        public string CurrentLineId { get; private set; }
        public int ActiveVoiceCount => voiceSource != null && voiceSource.isPlaying ? 1 : 0;
        public bool CurrentScenePremixed { get; private set; }
        public float CombatIntensity { get; set; }
        public int ExpiredCount { get; private set; }
        public float CurrentVoiceTime => voiceSource != null && voiceSource.clip != null ? voiceSource.time : 0;
        public IReadOnlyList<string> History => history;
        public float Volume { get => volume; set { volume = Mathf.Clamp01(value); ApplyVolume(); PlayerPrefs.SetFloat("NarrativeCombat.RadioVolume", volume); } }
        readonly List<Transmission> queue = new List<Transmission>(8);
        readonly List<string> history = new List<string>(48);
        readonly Queue<string> recent = new Queue<string>();
        readonly Dictionary<string, float> speakerCooldowns = new Dictionary<string, float>();
        readonly HashSet<long> handledEvents = new HashSet<long>();
        readonly Queue<long> eventOrder = new Queue<long>();
        System.Random random = new System.Random(1701);
        FleetCombatSimulation boundFleet;
        MissionController boundMission;
        float clock, availableAt, speakingUntil, volume = .7f;
        int generation, budgetFrame = -1, consumedEvents;
        bool paused, narrativeMode, ducked, currentImportant;
        float lastPriorityInterrupt = -100, nextAlarmAt;
        RadioCaption[] currentCaptions;
        string currentSpeakerId;
        int currentFleetGeneration;
        float transmissionStarted;
        struct Transmission { public RadioLine line; public ShipTarget speaker; public string speakerId; public float expires, priority; public int generation, fleetGeneration; }

        void Awake()
        {
            volume = PlayerPrefs.GetFloat("NarrativeCombat.RadioVolume", .7f);
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            if (signalSource == null) signalSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = signalSource.playOnAwake = false;
            voiceSource.spatialBlend = signalSource.spatialBlend = 0;
            ApplyVolume();
        }
        void OnEnable() { Bind(); }
        void Start() { Bind(); }
        public void Bind()
        {
            if (boundFleet != fleet)
            {
                if (boundFleet != null) { boundFleet.EventRaised -= Receive; boundFleet.SimulationReset -= OnRestart; }
                boundFleet = fleet;
                if (boundFleet != null) { boundFleet.EventRaised += Receive; boundFleet.SimulationReset += OnRestart; }
            }
            if (boundMission != mission)
            {
                if (boundMission != null) { boundMission.Restarted -= OnRestart; boundMission.StateChanged -= OnStateChanged; }
                boundMission = mission;
                if (boundMission != null) { boundMission.Restarted += OnRestart; boundMission.StateChanged += OnStateChanged; }
            }
        }
        void OnRestart() { BeginSession(1701); }
        void OnStateChanged(MissionState state)
        {
            SetPaused(state == MissionState.Paused);
            if (state == MissionState.Ready || state == MissionState.Results) { Clear(); narrativeMode = false; }
        }
        void OnDisable()
        {
            if (boundFleet != null) { boundFleet.EventRaised -= Receive; boundFleet.SimulationReset -= OnRestart; }
            if (boundMission != null) { boundMission.Restarted -= OnRestart; boundMission.StateChanged -= OnStateChanged; }
            boundFleet = null; boundMission = null; Clear();
        }
        public void BeginSession(int seed)
        {
            Clear(); random = new System.Random(seed); history.Clear(); HistoryRevision++; recent.Clear(); speakerCooldowns.Clear();
            handledEvents.Clear(); eventOrder.Clear(); CombatIntensity = 0; ExpiredCount = 0;
            clock = 0; lastPriorityInterrupt = -100; nextAlarmAt = 0; budgetFrame = -1; consumedEvents = 0; InterruptedCount = DroppedCount = SpokenCount = 0; paused = false; narrativeMode = false; Bind();
        }
        public void Clear()
        {
            generation++; queue.Clear(); voiceSource?.Stop(); signalSource?.Stop(); alarmSource?.Stop(); bedSource?.Stop(); CurrentSpeaker = null; CurrentLineId = null; currentImportant = false;
            currentCaptions = null; currentSpeakerId = null; CurrentScenePremixed = false;
            CurrentText = "等待截获信号"; CurrentChannel = "无线电监听"; SignalStatus = "待机";
            speakingUntil = availableAt = 0;
            SetDucking(false);
        }
        public void SetNarrativeMode(bool value) { Clear(); narrativeMode = value; StartBed(); }
        void StartBed()
        {
            if (library != null && library.premixedSceneAudio) return;
            if (bedSource == null || library == null || library.equipmentBed == null || bedSource.isPlaying) return;
            bedSource.clip = library.equipmentBed; bedSource.loop = true; bedSource.Play();
        }
        public void SetPaused(bool value)
        {
            paused = value;
            if (value) { voiceSource?.Pause(); signalSource?.Pause(); alarmSource?.Pause(); bedSource?.Pause(); }
            else { voiceSource?.UnPause(); signalSource?.UnPause(); alarmSource?.UnPause(); bedSource?.UnPause(); }
        }
        void Update() { if (seedMix != null) ApplyVolume(); Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if (paused || dt <= 0) return;
            clock += dt;
            SetDucking(clock < speakingUntil);
            if (CurrentSpeaker != null && (!CanSpeak(CurrentSpeaker) || CurrentSpeaker.targetId != currentSpeakerId || (fleet != null && fleet.Generation != currentFleetGeneration))) InterruptSpeaker(CurrentSpeaker);
            if (currentCaptions != null && clock < speakingUntil)
            {
                float elapsed = voiceSource != null && voiceSource.isPlaying ? voiceSource.time : clock - transmissionStarted;
                foreach (var caption in currentCaptions)
                    if (elapsed >= caption.time && elapsed < caption.time + caption.duration)
                    { CurrentText = caption.text; break; }
            }
            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].expires <= clock || queue[i].generation != generation || !MatchesState(queue[i].line, queue[i].speaker) || queue[i].speaker.targetId != queue[i].speakerId || (fleet != null && queue[i].fleetGeneration != fleet.Generation)) { queue.RemoveAt(i); DroppedCount++; ExpiredCount++; }
            if (clock < speakingUntil || narrativeMode) return;
            if (CurrentSpeaker != null || SignalStatus == "正在接收") { CurrentSpeaker = null; SignalStatus = "监听中"; }
            if (clock < availableAt || queue.Count == 0) return;
            int best = 0; for (int i = 1; i < queue.Count; i++) if (queue[i].priority > queue[best].priority) best = i;
            var next = queue[best]; queue.RemoveAt(best);
            currentImportant = IsImportant(next.line); CurrentLineId = next.line.id;
            AudioClip clip = next.line.voice;
            if (CombatIntensity < .25f && next.line.lowIntensityVoice != null) clip = next.line.lowIntensityVoice;
            else if (CombatIntensity > .65f && next.line.highIntensityVoice != null) clip = next.line.highIntensityVoice;
            bool shortRescue = next.speaker.DamageState == ShipDamageState.FatalPending && fleet != null && next.speaker.ExplosionAt - fleet.SimulatedTime < 3.3f && next.line.shortVoice != null;
            if (shortRescue) clip = next.line.shortVoice;
            CurrentScenePremixed = next.line.premixed;
            currentCaptions = shortRescue ? null : next.line.captions;
            StartTransmission(next.line.channel, shortRescue ? "反应堆破损！快离开！" : next.line.text, next.speaker, clip, clip != null ? clip.length : Mathf.Max(2.7f, next.line.text.Length * .1f));
            recent.Enqueue(next.line.id); while (recent.Count > library.recentLineLimit) recent.Dequeue();
        }
        public static bool CanSpeak(ShipTarget ship) => ship != null && ship.DamageState != ShipDamageState.Exploded && !ship.IsEscaped;
        public static bool MatchesState(RadioLine line, ShipTarget speaker)
        {
            if (!CanSpeak(speaker)) return false;
            if (line.evacuationOnly && speaker.BehaviorState != ShipBehaviorState.BreakingFormation && speaker.BehaviorState != ShipBehaviorState.Fleeing) return false;
            bool pendingEvent = line.pendingOnly || line.eventKind == "HullPenetrated" || line.eventKind == "ReactorUnstable" || line.eventKind == "RescueRequested";
            return speaker.DamageState == (pendingEvent ? ShipDamageState.FatalPending : ShipDamageState.Intact);
        }
        public void Receive(CombatEvent message)
        {
            if (fleet != null && message.generation != fleet.Generation) { DroppedCount++; return; }
            if (message.eventId != 0)
            {
                if (!handledEvents.Add(message.eventId)) return;
                eventOrder.Enqueue(message.eventId); if (eventOrder.Count > 256) handledEvents.Remove(eventOrder.Dequeue());
            }
            // Interruption is never subject to the presentation budget.
            if (message.kind == CombatEventKind.ShipExploded || message.kind == CombatEventKind.CommunicationInterrupted) InterruptSpeaker(message.subject);
            if (library == null || narrativeMode || paused) return;
            if (message.kind == CombatEventKind.WeaponFired || message.kind == CombatEventKind.DropletContact) return;
            if (fleet != null && fleet.SimulatedTime - message.simulationTime > library.eventExpiry) { DroppedCount++; ExpiredCount++; return; }
            if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; consumedEvents = 0; }
            if (++consumedEvents > library.eventBudgetPerFrame) { DroppedCount++; return; }
            ShipTarget speaker = message.speaker;
            Vector3 eventPosition = message.PositionAtOrigin(fleet != null ? fleet.AccumulatedOriginOffset : Vector3.zero);
            bool observer = message.kind == CombatEventKind.ShipExploded || message.kind == CombatEventKind.CommunicationInterrupted || message.kind == CombatEventKind.ShipEscaped;
            if (observer && (speaker == message.subject || !CanSpeak(speaker))) speaker = FindObserver(message.subject, eventPosition);
            if (!CanSpeak(speaker)) return;
            string key = speaker.targetId + ":" + message.kind;
            if (speakerCooldowns.TryGetValue(key, out float previous) && clock - previous < library.speakerEventCooldown) return;
            RadioLine chosen = Choose(message.kind.ToString(), speaker);
            if (chosen == null) return;
            bool urgent = IsImportant(chosen);
            float distance = listener != null ? Vector3.Distance(listener.position, eventPosition) : 0;
            float priority = (observer ? 2 : chosen.pendingOnly ? 5 : 3) + (urgent ? 5 : 0);
            float proximityScale = fleet != null && fleet.scale != null ? fleet.scale.MetersToUnits(100000) : 1000;
            priority += 3 / (1 + distance / Mathf.Max(.001f, proximityScale));
            if (viewCamera != null) { var p = viewCamera.WorldToViewportPoint(eventPosition); if (p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1) priority += 2; }
            var entry = new Transmission { line = chosen, speaker = speaker, speakerId = speaker.targetId, expires = clock + library.eventExpiry, priority = priority, generation = generation, fleetGeneration = fleet != null ? fleet.Generation : 0 };
            if (queue.Count >= library.queueLimit)
            {
                int lowest = 0; for (int i = 1; i < queue.Count; i++) if (queue[i].priority < queue[lowest].priority) lowest = i;
                if (priority <= queue[lowest].priority) { DroppedCount++; return; }
                queue.RemoveAt(lowest); DroppedCount++;
            }
            speakerCooldowns[key] = clock; queue.Add(entry);
            // A warning can preempt routine traffic, but equivalent warning floods cannot
            // repeatedly restart the only voice. The displaced line is never requeued.
            if (urgent && !currentImportant && clock < speakingUntil && clock - lastPriorityInterrupt >= 1.5f)
            {
                voiceSource?.Stop(); alarmSource?.Stop(); bedSource?.Stop(); currentCaptions = null; CurrentSpeaker = null; speakingUntil = availableAt = clock;
                lastPriorityInterrupt = clock; InterruptedCount++;
            }
            if (urgent && !library.premixedSceneAudio && clock >= nextAlarmAt && alarmSource != null && library.alarm != null)
            { alarmSource.PlayOneShot(library.alarm); nextAlarmAt = clock + 4; }
        }
        static bool IsImportant(RadioLine line) => line.important || line.eventKind == "LaserReflected" || line.eventKind == "ReactorUnstable" || line.eventKind == "RetreatOrdered";
        ShipTarget FindObserver(ShipTarget subject, Vector3 position)
        {
            if (mission == null || mission.targets == null) return null;
            ShipTarget best = null; float distance = float.PositiveInfinity;
            // Events are globally budgeted; transforms are the fleet's authoritative roots, including distant LODs.
            foreach (var ship in mission.targets)
            {
                if (ship == subject || !CanSpeak(ship) || ship.DamageState != ShipDamageState.Intact) continue;
                float d = (ship.transform.position - position).sqrMagnitude;
                if (d < distance) { distance = d; best = ship; }
            }
            return best;
        }
        RadioLine Choose(string kind, ShipTarget speaker)
        {
            float total = 0;
            foreach (var line in library.combat) if (line.eventKind == kind && MatchesState(line, speaker) && CombatIntensity >= line.minimumIntensity && CombatIntensity <= line.maximumIntensity && !recent.Contains(line.id) && !IsQueued(line.id)) total += Mathf.Max(.01f, line.weight);
            if (total <= 0) return null;
            float pick = (float)random.NextDouble() * total;
            foreach (var line in library.combat)
                if (line.eventKind == kind && MatchesState(line, speaker) && CombatIntensity >= line.minimumIntensity && CombatIntensity <= line.maximumIntensity && !recent.Contains(line.id) && !IsQueued(line.id)) { pick -= Mathf.Max(.01f, line.weight); if (pick <= 0) return line; }
            return null;
        }
        bool IsQueued(string id) { foreach (var item in queue) if (item.line.id == id) return true; return false; }
        public void InterruptSpeaker(ShipTarget ship)
        {
            if (ship == null) return;
            for (int i = queue.Count - 1; i >= 0; i--) if (queue[i].speaker == ship) queue.RemoveAt(i);
            if (CurrentSpeaker != ship) return;
            voiceSource?.Stop(); signalSource?.Stop(); alarmSource?.Stop(); bedSource?.Stop(); currentCaptions = null; CurrentSpeaker = null; InterruptedCount++; SignalStatus = "信号中断 / 短促杂音";
            currentImportant = false; CurrentLineId = null;
            CurrentText += " 〔通信中断〕"; speakingUntil = clock + .3f; availableAt = clock + .8f;
            if (signalSource != null && library != null && library.interruptTone != null) signalSource.PlayOneShot(library.interruptTone);
        }
        public void PlayNarrative(NarrativeLine line)
        {
            if (line == null || !narrativeMode) return;
            currentImportant = false; CurrentLineId = line.id;
            CurrentScenePremixed = line.premixed; currentCaptions = line.captions;
            StartTransmission(line.channel, line.text, null, line.voice, line.voice != null ? line.voice.length : line.duration);
        }
        void StartTransmission(string channel, string text, ShipTarget speaker, AudioClip voice, float duration)
        {
            voiceSource?.Stop(); CurrentSpeaker = speaker;
            currentSpeakerId = speaker != null ? speaker.targetId : null; currentFleetGeneration = fleet != null ? fleet.Generation : 0; transmissionStarted = clock;
            if (CurrentScenePremixed) { alarmSource?.Stop(); bedSource?.Stop(); signalSource?.Stop(); }
            CurrentChannel = speaker != null ? speaker.targetId + " / " + channel : channel;
            CurrentText = text; SignalStatus = "正在接收"; speakingUntil = clock + duration;
            SetDucking(true); StartBed();
            availableAt = speakingUntil + (library != null ? library.globalCooldown * (library.premixedSceneAudio ? Mathf.Lerp(3.1f,.8f,CombatIntensity) : 1) : 1);
            SpokenCount++; HistoryRevision++; history.Add(CurrentChannel + "：" + text);
            while (history.Count > (library != null ? library.historyLimit : 40)) history.RemoveAt(0);
            if (!CurrentScenePremixed && signalSource != null && library != null && library.connectTone != null) signalSource.PlayOneShot(library.connectTone, .6f);
            if (voiceSource != null && voice != null) { voiceSource.clip = voice; voiceSource.Play(); }
        }
        void SetDucking(bool value)
        {
            if (ducked == value) return;
            ducked = value;
            if (value) broadcastMix?.TransitionTo(.08f); else normalMix?.TransitionTo(.22f);
        }
        void ApplyVolume()
        {
            float effective = volume * (seedMix != null ? seedMix.CommunicationsGain : 1f);
            if (voiceSource != null) voiceSource.volume = effective;
            if (signalSource != null) signalSource.volume = effective * .3f;
            if (alarmSource != null) alarmSource.volume = effective * .38f;
            if (bedSource != null) bedSource.volume = effective * .18f;
        }
    }
}
