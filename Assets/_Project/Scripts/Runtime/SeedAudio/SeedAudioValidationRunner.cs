using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>
    /// Opt-in rendered-player audio check. Uses the saved scene and normal mission/event APIs;
    /// it never runs in ordinary play or changes tuning assets.
    /// </summary>
    public sealed class SeedAudioValidationRunner : MonoBehaviour
    {
        [Serializable]
        public sealed class Check
        {
            public string name;
            public string status;
            public string detail;
        }

        [Serializable]
        public sealed class EventRecord
        {
            public string kind, target;
            public long id;
            public float simulationTime;
        }

        [Serializable]
        public sealed class WaveRecord
        {
            public string phase, file, limitation;
            public int sampleRate, channels, frames;
            public float peak;
            public bool saved;
        }

        [Serializable]
        public sealed class Report
        {
            public string scene, unity, gpu, cpu, graphicsApi, error;
            public string method = "Opt-in rendered player. Uses saved Seed media, MissionController.Step and real FleetCombatSimulation events. No model requests or tuning-asset changes.";
            public string listening = "Human audition and the 90-minute natural result route remain separate acceptance checks.";
            public bool completed, automatedChecksPassed, allAcceptancePassed;
            public int ships, identities, catalogCues, missingClips, maximumWorldSources, nonzeroOutputSamples;
            public float peakOutput;
            public float userMusicVolume, musicGainDuringBroadcast;
            public float userAmbienceVolume, ambienceGainDuringBroadcast;
            public Check[] checks;
            public EventRecord[] events;
            public WaveRecord[] waveCaptures;
            public string[] screenshots;
        }

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        readonly List<Check> checks = new List<Check>();
        readonly List<EventRecord> events = new List<EventRecord>();
        readonly List<WaveRecord> waveCaptures = new List<WaveRecord>();
        readonly List<string> screenshots = new List<string>();
        readonly List<string> samples = new List<string>
        {
            "wall,simulation,phase,mission,music,musicGain,ambienceGain,cruiseVolume,boostVolume,radioVoice,radioDucking,worldSources,outputPeak,destroyed,pending"
        };
        readonly float[] meter = new float[512];
        Report report;
        MissionController mission;
        StageAudioDirector stage;
        FlightAudioController flight;
        UiMissionAudioController ui;
        SeedAudioMixController mix;
        BattleAudioDirector world;
        RadioController radio;
        string directory, phase = "Startup";
        bool running, exitAfter, outputMeterAvailable = true;
        float nextSample;
        float[] captureBuffer;
        int captureActive, captureBusy, captureCount, captureChannels, captureFramesTarget;
        int captureSampleRate;
        string capturePhase;
        string blastTargetId;
        bool blastEventCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-seed-audio-validation");
            if (flag < 0) return;
            string path = flag + 1 < args.Length && !args[flag + 1].StartsWith("-", StringComparison.Ordinal)
                ? args[flag + 1] : Path.Combine(Application.persistentDataPath, "SeedAudioValidation");
            // OnAudioFilterRead receives the actual mixed buffer when attached to the
            // existing listener. Do not add a second AudioListener or alter audio data.
            var listener = FindObjectsByType<AudioListener>()
                .FirstOrDefault(source => source.isActiveAndEnabled);
            var host = listener != null ? listener.gameObject : new GameObject("__OptInSeedAudioValidation");
            var runner = host.AddComponent<SeedAudioValidationRunner>();
            runner.Begin(path, true);
        }

        public void Begin(string outputDirectory, bool quitWhenDone = false)
        {
            if (running) throw new InvalidOperationException("Seed audio validation is already running.");
            directory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(directory);
            exitAfter = quitWhenDone;
            report = new Report
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                unity = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString()
            };
            mission = FindAnyObjectByType<MissionController>();
            stage = FindAnyObjectByType<StageAudioDirector>();
            flight = FindAnyObjectByType<FlightAudioController>();
            ui = FindAnyObjectByType<UiMissionAudioController>();
            mix = FindAnyObjectByType<SeedAudioMixController>();
            world = FindAnyObjectByType<BattleAudioDirector>();
            radio = mission != null && mission.narrative != null ? mission.narrative.radio : null;
            if (mission != null && mission.combat != null) mission.combat.EventRaised += OnCombatEvent;
            Application.runInBackground = true;
            running = true;
            StartCoroutine(Guard(Run()));
        }

        void OnCombatEvent(CombatEvent message)
        {
            if (message.kind == CombatEventKind.ShipExploded &&
                message.targetId == blastTargetId &&
                capturePhase == "delayed-blast-listener-output" &&
                Volatile.Read(ref captureActive) != 0)
                blastEventCaptured = true;
            if (events.Count >= 10000) return;
            events.Add(new EventRecord
            {
                kind = message.kind.ToString(), target = message.targetId,
                id = message.eventId, simulationTime = message.simulationTime
            });
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref captureActive) == 0 || channels <= 0) return;
            Interlocked.Increment(ref captureBusy);
            try
            {
                if (Volatile.Read(ref captureActive) == 0) return;
                float[] destination = captureBuffer;
                if (destination == null) return;
                int offset = Volatile.Read(ref captureCount);
                int limit = Math.Min(destination.Length, captureFramesTarget * channels);
                int count = Math.Min(data.Length, limit - offset);
                if (count > 0) Array.Copy(data, 0, destination, offset, count);
                Volatile.Write(ref captureChannels, channels);
                Volatile.Write(ref captureCount, offset + Math.Max(0, count));
                if (offset + count >= limit) Volatile.Write(ref captureActive, 0);
            }
            finally { Interlocked.Decrement(ref captureBusy); }
        }

        void BeginWave(string name, float seconds)
        {
            if (Volatile.Read(ref captureBusy) != 0 || Volatile.Read(ref captureActive) != 0)
                throw new InvalidOperationException("A previous listener capture is still active.");
            captureSampleRate = AudioSettings.outputSampleRate;
            captureFramesTarget = Math.Max(1, Mathf.CeilToInt(seconds * Math.Max(1, captureSampleRate)));
            captureBuffer = new float[captureFramesTarget * 8]; // Up to 7.1; stereo uses only its actual channels.
            capturePhase = name;
            Volatile.Write(ref captureCount, 0);
            Volatile.Write(ref captureChannels, 0);
            Volatile.Write(ref captureActive, 1);
        }

        IEnumerator SaveWave()
        {
            Volatile.Write(ref captureActive, 0);
            while (Volatile.Read(ref captureBusy) != 0) yield return null;
            int channels = Volatile.Read(ref captureChannels);
            int count = Volatile.Read(ref captureCount);
            int frames = channels > 0 ? count / channels : 0;
            var result = new WaveRecord
            {
                phase = capturePhase, sampleRate = captureSampleRate, channels = channels,
                frames = frames, file = capturePhase + ".wav"
            };
            if (captureSampleRate <= 0 || channels <= 0 || frames == 0)
                result.limitation = "The active AudioListener supplied no OnAudioFilterRead PCM buffers on this player/audio backend.";
            else
            {
                int floatCount = frames * channels;
                using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, result.file))))
                {
                    int bytes = floatCount * sizeof(short);
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)channels);
                    writer.Write(captureSampleRate); writer.Write(captureSampleRate * channels * sizeof(short));
                    writer.Write((short)(channels * sizeof(short))); writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                    for (int i = 0; i < floatCount; i++)
                    {
                        float value = Mathf.Clamp(captureBuffer[i], -1f, 1f);
                        result.peak = Mathf.Max(result.peak, Mathf.Abs(value));
                        writer.Write((short)Mathf.RoundToInt(value * 32767f));
                    }
                }
                result.saved = true;
                if (result.peak <= .00001f)
                    result.limitation = "Listener callback was present, but this phase rendered only near-zero samples.";
            }
            waveCaptures.Add(result);
            RecordCheck("Recorded listener WAV: " + result.phase,
                result.saved && result.peak > .00001f,
                result.saved ? "frames=" + frames + "; channels=" + channels +
                    "; rate=" + captureSampleRate + "; peak=" + F(result.peak) : result.limitation);
            captureBuffer = null;
        }

        void Update()
        {
            if (!running || Time.realtimeSinceStartup < nextSample) return;
            nextSample = Time.realtimeSinceStartup + .1f;
            float peak = 0f;
            if (outputMeterAvailable)
            {
                try
                {
                    AudioListener.GetOutputData(meter, 0);
                    foreach (float value in meter) peak = Mathf.Max(peak, Mathf.Abs(value));
                    report.peakOutput = Mathf.Max(report.peakOutput, peak);
                    if (peak > .00001f) report.nonzeroOutputSamples++;
                }
                catch (Exception exception)
                {
                    outputMeterAvailable = false;
                    RecordCheck("Actual AudioListener output meter", "failed", exception.Message);
                }
            }
            if (world != null) report.maximumWorldSources = Math.Max(report.maximumWorldSources, world.ActiveWorldSources);
            samples.Add(string.Join(",", F(Time.realtimeSinceStartup),
                F(mission != null && mission.combat != null ? mission.combat.SimulatedTime : 0f),
                phase, mission != null ? mission.State.ToString() : "Missing",
                stage != null ? stage.CurrentMusicSelection ?? "None" : "Missing",
                F(mix != null ? mix.MusicGain : 0f),
                F(mix != null ? mix.AmbienceGain : 0f),
                F(flight != null && flight.CruiseSource != null ? flight.CruiseSource.volume : 0f),
                F(flight != null && flight.BoostSource != null ? flight.BoostSource.volume : 0f),
                radio != null ? radio.ActiveVoiceCount.ToString() : "0",
                mix != null && mix.IsDucking ? "1" : "0",
                world != null ? world.ActiveWorldSources.ToString() : "0", F(peak),
                mission != null ? mission.DestroyedCount.ToString() : "0",
                mission != null ? mission.PendingCount.ToString() : "0"));
        }

        static string F(float value) => value.ToString("F5", Invariant);
        void RecordCheck(string name, bool passed, string detail = "")
            => RecordCheck(name, passed ? "passed" : "failed", detail);
        void RecordCheck(string name, string status, string detail)
            => checks.Add(new Check { name = name, status = status, detail = detail });
        void Manual(string name, string detail) => RecordCheck(name, "manual", detail);
        void Phase(string value)
        {
            phase = value;
            File.WriteAllText(Path.Combine(directory, "status.txt"), value);
        }
        IEnumerator WaitReal(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string file = name + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, file));
            screenshots.Add(file);
            yield return null;
            yield return null;
        }
        IEnumerator ControlledSteps(float seconds, FlightCommand command)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && mission.State == MissionState.Playing)
            {
                mission.Step(Time.fixedDeltaTime, command);
                yield return null;
            }
        }

        IEnumerator AdvanceUntilSimulation(float targetTime)
        {
            float remaining = targetTime - mission.combat.SimulatedTime;
            float timeoutAt = Time.realtimeSinceStartup + Mathf.Max(10f, remaining * 8f);
            while (mission.combat.SimulatedTime < targetTime - .0001f)
            {
                if (mission.State != MissionState.Playing || Time.realtimeSinceStartup >= timeoutAt)
                    throw new InvalidOperationException("The full-fleet simulation did not reach the blast window: " +
                        F(mission.combat.SimulatedTime) + " / " + F(targetTime));
                mission.Step(Mathf.Min(Time.fixedDeltaTime,
                    targetTime - mission.combat.SimulatedTime), default);
                yield return null;
            }
        }

        /// <summary>Walks nested coroutines while guaranteeing iterator finally blocks run on failure.</summary>
        public static IEnumerator WalkNested(IEnumerator routine, Action<Exception> onError)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine ?? throw new ArgumentNullException(nameof(routine)));
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    bool moved = false;
                    object next = null;
                    Exception failure = null;
                    try { moved = current.MoveNext(); if (moved) next = current.Current; }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        onError?.Invoke(failure);
                        yield break;
                    }
                    if (!moved)
                    {
                        stack.Pop();
                        Exception disposalFailure = null;
                        try { (current as IDisposable)?.Dispose(); }
                        catch (Exception exception) { disposalFailure = exception; }
                        if (disposalFailure != null)
                        {
                            onError?.Invoke(disposalFailure);
                            yield break;
                        }
                        continue;
                    }
                    if (next is IEnumerator nested) stack.Push(nested);
                    else yield return next;
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception exception) { onError?.Invoke(exception); }
                }
            }
        }

        IEnumerator Guard(IEnumerator routine)
        {
            var walker = WalkNested(routine, exception => report.error = exception.ToString());
            try
            {
                while (walker.MoveNext()) yield return walker.Current;
            }
            finally { (walker as IDisposable)?.Dispose(); }
            running = false;
            if (mission != null && mission.combat != null) mission.combat.EventRaised -= OnCombatEvent;
            report.completed = string.IsNullOrEmpty(report.error);
            report.checks = checks.ToArray();
            report.events = events.ToArray();
            report.waveCaptures = waveCaptures.ToArray();
            report.screenshots = screenshots.ToArray();
            report.automatedChecksPassed = report.completed && checks.All(c => c.status != "failed");
            report.allAcceptancePassed = report.automatedChecksPassed && checks.All(c => c.status == "passed");
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllLines(Path.Combine(directory, "audio-route.csv"), samples);
            File.WriteAllText(Path.Combine(directory, "status.txt"),
                "Automated=" + report.automatedChecksPassed + "; full acceptance=" + report.allAcceptancePassed);
            if (mission != null) mission.Restart();
            if (exitAfter) Application.Quit(report.automatedChecksPassed ? 0 : 2);
        }

        IEnumerator Run()
        {
            yield return null;
            RecordCheck("Saved Seed scene", report.scene != null && report.scene.EndsWith("FleetAssault_SeedAudio.unity", StringComparison.Ordinal), report.scene);
            RecordCheck("Required audio and mission components", mission != null && mission.combat != null &&
                mission.narrative != null && stage != null && flight != null && ui != null && mix != null && world != null && radio != null);
            if (mission == null || mission.combat == null || mission.narrative == null ||
                stage == null || flight == null || ui == null || mix == null || world == null || radio == null)
                yield break;
            var catalog = stage.catalog;
            report.ships = mission.TotalCount;
            report.identities = mission.targets.Count(s => s != null && !string.IsNullOrEmpty(s.targetId)) == mission.targets.Length
                ? mission.targets.Select(s => s.targetId).Distinct().Count() : 0;
            report.catalogCues = catalog != null && catalog.cues != null ? catalog.cues.Length : 0;
            report.missingClips = catalog != null ? catalog.MissingClipCount() : 131;
            RecordCheck("Full authored 2000-ship fleet", report.ships == 2000 && report.identities == 2000);
            RecordCheck("One enabled listener", FindObjectsByType<AudioListener>().Count(l => l.isActiveAndEnabled) == 1);
            RecordCheck("All 131 imported Seed cues", catalog != null && report.catalogCues == 131 && report.missingClips == 0 &&
                catalog.cues.Select(c => c.id).Distinct(StringComparer.Ordinal).Count() == 131,
                "cues=" + report.catalogCues + "; missing clips=" + report.missingClips);
            RecordCheck("World pool fixed at 12", world.Capacity == 12 && world.poolCapacity == 12);

            Phase("Ready");
            yield return WaitReal(.5f);
            RecordCheck("Menu bed and sparse space ambience", mission.State == MissionState.Ready &&
                stage.CurrentMusicSelection == "Ready_01" && stage.CurrentAmbienceSelection == "SpaceTexture" &&
                stage.ActiveMusicSource != null && stage.ActiveMusicSource.clip != null);
            BeginWave("ready-listener-output", 1.5f);
            yield return WaitReal(1.6f);
            yield return SaveWave();
            yield return Capture("01-ready");

            Phase("Narrative");
            mission.ReplayNarrative();
            yield return WaitReal(.5f);
            if (mission.narrative.CueCount == 0) mission.narrative.ApplyCue(0);
            yield return WaitReal(.25f);
            RecordCheck("Opening stage and Seed radio", mission.State == MissionState.Narrative &&
                stage.CurrentMusicSelection == "Narrative_01" && radio.voiceSource != null &&
                radio.voiceSource.clip != null && radio.voiceSource.isPlaying,
                "stage=" + stage.CurrentMusicSelection + "; line=" + radio.CurrentLineId);
            report.userMusicVolume = mix.musicVolume;
            report.musicGainDuringBroadcast = mix.MusicGain;
            report.userAmbienceVolume = mix.ambienceVolume;
            report.ambienceGainDuringBroadcast = mix.AmbienceGain;
            RecordCheck("Radio ducks music and ambience", radio.IsBroadcasting && mix.IsDucking &&
                (mix.musicVolume <= .01f || mix.MusicGain < mix.musicVolume) &&
                (mix.ambienceVolume <= .01f || mix.AmbienceGain < mix.ambienceVolume),
                "music " + F(mix.musicVolume) + " -> " + F(mix.MusicGain) +
                "; ambience " + F(mix.ambienceVolume) + " -> " + F(mix.AmbienceGain));
            BeginWave("narrative-listener-output", 1.5f);
            yield return WaitReal(1.6f);
            yield return SaveWave();
            yield return Capture("02-narrative");
            // Select one actual authored cue from each act to verify routing without
            // pretending this abbreviated route has played the full nine-minute Timeline.
            for (int act = 0; act < 4; act++)
            {
                int cue = Array.FindIndex(radio.library.narrative, line => line != null && line.stage == act);
                if (cue >= 0) mission.narrative.ApplyCue(cue);
                yield return WaitReal(.15f);
                string expectedMusic = "Narrative_0" + (act + 1);
                string expectedCabin = act == 0 ? "CabinBed_01" : act == 3 ? "CabinBed_03" : "CabinBed_02";
                RecordCheck("Narrative act " + (act + 1) + " music and cabin", cue >= 0 &&
                    mission.narrative.CurrentStage == act && stage.CurrentMusicSelection == expectedMusic &&
                    stage.CurrentAmbienceSelection == expectedCabin,
                    "cue=" + cue + "; music=" + stage.CurrentMusicSelection +
                    "; cabin=" + stage.CurrentAmbienceSelection);
            }

            Phase("Pause");
            var pausedSource = stage.ActiveMusicSource;
            AudioClip pausedClip = pausedSource != null ? pausedSource.clip : null;
            mission.TogglePause();
            yield return WaitReal(.1f);
            int pausedAt = pausedSource != null ? pausedSource.timeSamples : -1;
            float narrativeAt = mission.narrative.Elapsed;
            yield return WaitReal(.35f);
            RecordCheck("Pause preserves background playback position", mission.State == MissionState.Paused &&
                stage.IsPaused && pausedSource != null && pausedSource.clip == pausedClip &&
                Mathf.Abs(pausedSource.timeSamples - pausedAt) <= 4096 &&
                Mathf.Abs(mission.narrative.Elapsed - narrativeAt) < .0001f,
                "paused samples=" + pausedAt + " -> " + (pausedSource != null ? pausedSource.timeSamples : -1));
            mission.TogglePause();
            yield return WaitReal(.3f);
            RecordCheck("Resume continues same background source", mission.State == MissionState.Narrative &&
                !stage.IsPaused && stage.ActiveMusicSource == pausedSource &&
                pausedSource != null && pausedSource.clip == pausedClip && pausedSource.timeSamples >= pausedAt);
            mission.narrative.Skip();
            mission.narrative.Skip();
            yield return null;
            RecordCheck("Skip enters battle once", mission.State == MissionState.Playing &&
                mission.narrative.CompletionCount == 1 && stage.CurrentMusicSelection == "BattleLow");

            Phase("Direct combat flight");
            mission.enabled = false; // Explicit fixed steps below avoid a second autonomous MissionController.FixedUpdate.
            mission.RestartIntoCombat();
            BeginWave("cruise-listener-output", 1.5f);
            yield return ControlledSteps(1.7f, default);
            yield return SaveWave();
            float cruiseGain = flight.CruiseSource != null ? flight.CruiseSource.volume : 0f;
            float quietBoostGain = flight.BoostSource != null ? flight.BoostSource.volume : 0f;
            BeginWave("sprint-listener-output", 2.5f);
            yield return ControlledSteps(4.5f, new FlightCommand { boost = true });
            yield return SaveWave();
            float boostedGain = flight.BoostSource != null ? flight.BoostSource.volume : 0f;
            RecordCheck("Cruise and sprint audible weights differ", cruiseGain > quietBoostGain + .05f &&
                boostedGain > quietBoostGain + .05f && flight.CurrentSpeedFraction >
                FlightAudioController.NormalizeSpeed(mission.settings.initialSpeed,
                    mission.settings.maxCruiseSpeed * mission.settings.boostMultiplier) + .05f,
                "cruise=" + F(cruiseGain) + "; boost " + F(quietBoostGain) + " -> " + F(boostedGain));
            RecordCheck("Direct combat beds", stage.CurrentMusicSelection == "BattleLow" &&
                stage.CurrentAmbienceSelection == "SpaceTexture" &&
                flight.CruiseSource != null && flight.CruiseSource.clip != null &&
                flight.BoostSource != null && flight.BoostSource.clip != null);
            yield return Capture("03-battle-flight");

            Phase("Penetration and delayed explosion");
            mission.RestartIntoCombat();
            var target = mission.targets.Where(s => s != null && !s.IsResolved)
                .OrderBy(s => (s.transform.position - mission.spawnPosition).sqrMagnitude).First();
            var sourceLibrary = radio.library;
            RadioLibrary proofLibrary = null;
            bool radioProofAvailable = false;
            try
            {
                var line = sourceLibrary != null && sourceLibrary.combat != null
                    ? sourceLibrary.combat.FirstOrDefault(l => l.id == "SA01" && l.voice != null) : null;
                if (line != null)
                {
                    proofLibrary = ScriptableObject.CreateInstance<RadioLibrary>();
                    proofLibrary.combat = new[] { new RadioLine
                    {
                        id = line.id, eventKind = "HullPenetrated", channel = line.channel,
                        role = line.role, text = line.text, english = line.english,
                        voice = line.voice, premixed = true, pendingOnly = true,
                        important = true, captions = line.captions
                    } };
                    proofLibrary.premixedSceneAudio = true;
                    proofLibrary.interruptTone = sourceLibrary.interruptTone;
                    radio.library = proofLibrary;
                    radio.BeginSession(1909);
                    radioProofAvailable = true;
                }
                int firstEvent = events.Count;
                // Keep the first recording before the minimum two-second blast delay.
                BeginWave("penetration-listener-output", 1f);
                bool damaged = mission.combat.ApplyDamage(target,
                    new ShipHitContext(target.transform.position, Vector3.forward, 1500),
                    DamageSource.Penetration, 69001);
                float deadline = target.ExplosionAt;
                yield return WaitReal(.75f);
                bool actualTransmission = radioProofAvailable && radio.CurrentSpeaker == target &&
                    radio.voiceSource != null && radio.voiceSource.isPlaying;
                int interruptedBefore = radio.InterruptedCount;
                yield return WaitReal(.4f);
                yield return SaveWave();
                // This controlled fast-forward may take longer than the recording on a
                // slow 2000-ship player. Hold the ongoing radio line so the real blast
                // event can still prove interruption of a live transmission.
                radio.SetPaused(true);
                try { yield return AdvanceUntilSimulation(deadline - .35f); }
                finally { radio.SetPaused(false); }
                int worldPlayedBeforeBlast = world.PlayedWorldEvents;
                blastTargetId = target.targetId;
                blastEventCaptured = false;
                BeginWave("delayed-blast-listener-output", 4.5f);
                yield return AdvanceUntilSimulation(deadline + .05f);
                yield return WaitReal(.6f);
                yield return SaveWave();
                RecordCheck("Delayed blast event falls inside listener capture", blastEventCaptured &&
                    waveCaptures.Last().saved && waveCaptures.Last().peak > .00001f,
                    "target=" + blastTargetId + "; simulated deadline=" + F(deadline) +
                    "; captured event=" + blastEventCaptured);
                RecordCheck("Live blast routes a Seed world clip", world.explosionClips != null &&
                    world.explosionClips.Any(clip => clip != null) &&
                    world.PlayedWorldEvents > worldPlayedBeforeBlast,
                    "world plays " + worldPlayedBeforeBlast + " -> " + world.PlayedWorldEvents);
                blastTargetId = null;
                var chain = events.Skip(firstEvent).Where(e => e.target == target.targetId).Select(e => e.kind).ToList();
                int p = chain.IndexOf("HullPenetrated"), r = chain.IndexOf("ReactorUnstable");
                int i = chain.IndexOf("CommunicationInterrupted"), x = chain.IndexOf("ShipExploded");
                RecordCheck("Penetration, reactor, interruption, delayed blast", damaged && target.IsDestroyed &&
                    p >= 0 && r > p && i > r && x > i && mission.PendingCount == 0 &&
                    Mathf.Abs(deadline - target.ExplosionAt) < .0001f,
                    "events=" + string.Join("/", chain) + "; deadline=" + F(deadline));
                if (radioProofAvailable)
                    RecordCheck("Fatal speaker stops all old cabin layers", actualTransmission &&
                        radio.InterruptedCount > interruptedBefore && radio.CurrentSpeaker != target &&
                        !radio.voiceSource.isPlaying && (radio.alarmSource == null || !radio.alarmSource.isPlaying) &&
                        (radio.bedSource == null || !radio.bedSource.isPlaying),
                        "transmission=" + actualTransmission + "; interruptions=" + radio.InterruptedCount);
                else Manual("Fatal speaker audio interruption", "No accepted SA01 recording was available for a controlled live interruption; inspect the actual battle route.");
            }
            finally
            {
                radio.library = sourceLibrary;
                radio.BeginSession(1701);
                if (proofLibrary != null) Destroy(proofLibrary);
            }
            yield return Capture("04-after-delayed-blast");
            RecordCheck("World sources stay within 12", report.maximumWorldSources <= 12 &&
                world.MaxWorldSourcesObserved <= 12,
                "observed=" + Math.Max(report.maximumWorldSources, world.MaxWorldSourcesObserved));
            RecordCheck("Actual nonzero listener output", outputMeterAvailable && report.nonzeroOutputSamples > 10 &&
                report.peakOutput > .00001f,
                "nonzero sample windows=" + report.nonzeroOutputSamples + "; peak=" + F(report.peakOutput));

            Phase("Three restarts");
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                mission.RestartIntoCombat();
                yield return null;
                RecordCheck("Clean restart " + attempt, mission.TotalCount == 2000 &&
                    mission.DestroyedCount == 0 && mission.PendingCount == 0 &&
                    mission.EscapedCount == 0 && world.ActiveWorldSources == 0 &&
                    radio.ActiveVoiceCount == 0 && stage.CurrentMusicSelection == "BattleLow" &&
                    Time.timeScale == 1f);
            }
            yield return Capture("05-restarted-three-times");
            Phase("Accelerated timer result");
            mission.RestartIntoCombat();
            var originalLasers = mission.lasers;
            mission.lasers = null;
            mission.motor.SimulationEnabled = false;
            try { mission.Step(mission.Remaining, default); }
            finally { mission.lasers = originalLasers; }
            yield return null;
            RecordCheck("Real results transition clears old audio", mission.State == MissionState.Results &&
                mission.ResultTransitions == 1 &&
                stage.CurrentMusicSelection == (mission.Won ? "ResultBed_01" : "ResultBed_02") &&
                world.ActiveWorldSources == 0 && radio.ActiveVoiceCount == 0 &&
                (flight.CruiseSource == null || !flight.CruiseSource.isPlaying) &&
                (flight.BoostSource == null || !flight.BoostSource.isPlaying),
                "outcome=" + (mission.Won ? "victory" : "incomplete") + "; music=" + stage.CurrentMusicSelection);
            yield return Capture("06-accelerated-results");
            Manual("Natural 90-minute result and victory", "The technical result check uses one accelerated MissionController.Step without changing missionSeconds, targets or settings. Run the full timed and victory routes in the shipping player.");
            Manual("Human mix and loop audition", "Listen through every stage, flight layer, communication and long loop seam on speakers or headphones; signal checks cannot judge artistic quality.");
            Manual("All four narrative stages and real-input attack", "The player route proves opening and skip. Play the full four-stage Timeline and collide using controls to assess timing and event audio.");
        }
    }
}
