using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace DropletPrototype
{
    /// <summary>Opt-in rendered benchmark and scripted game-flow check, never an input/feel test.</summary>
    public sealed class FleetBenchmark : MonoBehaviour
    {
        public MissionController mission;
        public MissionEffects effects;
        public bool IsRunning { get; private set; }
        public string LastReportPath { get; private set; }
        public string LastSummary { get; private set; }

        [Serializable] public sealed class Distribution
        { public int samples; public double mean, p50, p95, p99, maximum; }
        [Serializable] public sealed class Phase
        {
            public string name;
            public Distribution renderedFrameMs, mainThreadMs, gcAllocatedBytes;
            public bool mainThreadRecorderAvailable, gcRecorderAvailable;
            public double measuredSeconds;
            public int focusedFrames, effectsPeak, audioPeak, poolObjectCount, destroyedAtEnd;
            public long allocatedMemoryBytes, reservedMemoryBytes, monoUsedBytes;
        }
        [Serializable] public sealed class Check
        { public string name, detail; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string startedUtc, unityVersion, platform, scene, cpu, gpu, graphicsApi, quality, effectQuality;
            public string operatingSystem, outputDirectory, error, scope;
            public int processorCount, systemMemoryMB, graphicsMemoryMB, width, height, vSyncCount, targetFrameRate;
            public bool editor, developmentBuild, batchMode, completed, allFlowChecksPassed;
            public Phase[] phases;
            public Check[] checks;
            public string[] screenshots;
        }

        readonly List<Phase> phases = new List<Phase>(4);
        readonly List<Check> checks = new List<Check>(16);
        readonly List<string> screenshots = new List<string>(6);
        readonly double[] frameSamples = new double[16384];
        readonly double[] mainSamples = new double[16384];
        readonly double[] gcSamples = new double[16384];
        Report report;
        ProfilerRecorder mainThreadRecorder, gcRecorder;
        bool oldMissionEnabled, oldInputEnabled, oldRunInBackground, quitAfter;
        Action oldPauseCallback;
        Coroutine routine;
        float simulationAccumulator;
        int initialPoolCount;
        int safeRepositions, teleportDamageFailures;
        Camera inspectionView;
        ChaseCamera inspectionChase;
        Vector3 savedInspectionPosition;
        Quaternion savedInspectionRotation;
        bool savedChaseEnabled;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool requested = false;
            string output = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-droplet-benchmark") requested = true;
                if (args[i] == "-droplet-benchmark-quit") quitAfter = true;
                if (args[i] == "-droplet-benchmark-output" && i + 1 < args.Length) output = args[++i];
            }
            if (requested) Begin(output);
        }

        public void Begin(string outputDirectory)
        {
            if (!Application.isPlaying || IsRunning) return;
            if (mission == null) mission = FindAnyObjectByType<MissionController>();
            if (effects == null) effects = FindAnyObjectByType<MissionEffects>();
            if (mission == null || mission.motor == null || mission.settings == null || mission.score == null)
            { Debug.LogError("FleetBenchmark requires a configured, saved gameplay scene.", this); return; }
            string parent = string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Application.persistentDataPath, "G09Benchmark") : outputDirectory;
            string directory = Path.GetFullPath(Path.Combine(parent, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(directory);
            report = new Report {
                startedUtc = DateTime.UtcNow.ToString("o"), outputDirectory = directory,
                unityVersion = Application.unityVersion, platform = Application.platform.ToString(),
                scene = gameObject.scene.path, cpu = SystemInfo.processorType, processorCount = SystemInfo.processorCount,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemoryMB = SystemInfo.graphicsMemorySize, systemMemoryMB = SystemInfo.systemMemorySize,
                operatingSystem = SystemInfo.operatingSystem, editor = Application.isEditor,
                developmentBuild = Debug.isDebugBuild, batchMode = Application.isBatchMode,
                scope = "Rendered scripted route through the existing MissionController.Step and DropletMotor.ResetPose. Measures this process only; not subjective handling, keyboard input, or listening acceptance. Screenshot capture and pause waits are excluded from measured flight phases. The explicitly named impact-inspection screenshots use a temporarily fixed side/rear camera looking at SPAWN_Small_003 and its real event-created wreck, not the normal chase-camera angle; chase behavior is restored afterwards." };
            phases.Clear(); checks.Clear(); screenshots.Clear();
            safeRepositions = 0; teleportDamageFailures = 0;
            oldMissionEnabled = mission.enabled; oldInputEnabled = mission.input != null && mission.input.enabled;
            oldPauseCallback = mission.input != null ? mission.input.PauseRequested : null;
            oldRunInBackground = Application.runInBackground;
            mission.enabled = false;
            if (mission.input != null) { mission.input.enabled = false; mission.input.PauseRequested = null; }
            Application.runInBackground = true;
            IsRunning = true;
            routine = StartCoroutine(GuardedRun(Run()));
        }

        IEnumerator GuardedRun(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            while (stack.Count > 0)
            {
                object current = null; bool advanced = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) current = stack.Peek().Current; }
                catch (Exception exception) { report.error = exception.ToString(); break; }
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            Finish();
        }

        IEnumerator Run()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            effects?.InitializePool(); initialPoolCount = effects != null ? effects.PoolInstanceCount : 0;
            mission.Restart();
            AddCheck("Formal fleet has 30-60 interactive targets", mission.TotalCount >= 30 && mission.TotalCount <= 60,
                "Mission targets=" + mission.TotalCount);
            AddCheck("Presentation pool is configured", effects != null && effects.settings != null && initialPoolCount > 0 && effects.Capacity > 0,
                "Prewarmed objects=" + initialPoolCount);
            yield return new WaitForSecondsRealtime(3);
            report.width = Screen.width; report.height = Screen.height;
            int qualityIndex = QualitySettings.GetQualityLevel();
            report.quality = qualityIndex >= 0 && qualityIndex < QualitySettings.names.Length ? QualitySettings.names[qualityIndex] : qualityIndex.ToString();
            report.effectQuality = effects != null ? effects.Quality.ToString() : "No MissionEffects";
            report.vSyncCount = QualitySettings.vSyncCount; report.targetFrameRate = Application.targetFrameRate;
            AddCheck("Rendered 1080p", Screen.width == 1920 && Screen.height == 1080 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                Screen.width + "x" + Screen.height + ", graphics=" + report.graphicsApi);
            yield return Capture("01-ready");
            StartRecorders();

            mission.StartMission(); simulationAccumulator = 0;
            yield return Measure("Normal flight", 8, false);
            yield return Capture("02-flight");

            mission.Restart(); mission.StartMission();
            int before = mission.DestroyedCount;
            SweepFirstChain();
            AddCheck("A single actual movement sweeps multiple targets", mission.DestroyedCount - before >= 3,
                "SPAWN_Small_001 through SPAWN_Small_003; destroyed in one segment: " + (mission.DestroyedCount - before));
            yield return CaptureImpactInspection();
            yield return Capture("03-chain");
            yield return VerifyPause();
            yield return Measure("Dense penetrations", 8, true);
            // Complete any remaining genuine targets if this hardware rendered too
            // few scheduling frames during the fixed measurement interval.
            int guard = mission.TotalCount + 1;
            while (mission.State == MissionState.Playing && guard-- > 0)
            {
                ShipTarget target = NextTarget(); if (target == null) break;
                SweepTarget(target); yield return new WaitForSecondsRealtime(.12f);
            }
            AddCheck("Entire fleet destroyed and won", mission.State == MissionState.Results && mission.Won && mission.DestroyedCount == mission.TotalCount && mission.score.Score > 0,
                "destroyed=" + mission.DestroyedCount + "/" + mission.TotalCount + ", state=" + mission.State + ", score=" + mission.score.Score);
            int resultCount = mission.ResultTransitions, scoreAtResult = mission.score.Score;
            mission.Step(10, new FlightCommand { throttle = 1, boost = true }); mission.StartMission();
            AddCheck("Results resolved once", resultCount == 1 && mission.ResultTransitions == 1 && mission.score.Score == scoreAtResult,
                "transitions=" + mission.ResultTransitions);
            yield return Capture("04-results");

            yield return MeasureRestarts();
            AddCheck("Reposition never damages the crossed path", teleportDamageFailures == 0 && safeRepositions > 0,
                "Direct ResetPose checks=" + safeRepositions + ", damage failures=" + teleportDamageFailures);
            yield return Capture("05-restarted-ready");
            report.completed = true;
        }

        void StartRecorders()
        {
            try { mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); }
            catch (Exception) { /* Unsupported markers are reported as unavailable. */ }
            try { gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); }
            catch (Exception) { }
        }
        IEnumerator Measure(string name, float seconds, bool dense)
        {
            var phase = new Phase { name = name };
            int count = 0, mainCount = 0, gcCount = 0;
            double elapsed = 0; float nextHit = 0;
            // Discard the transition frame (including screenshot/restart work).
            yield return null;
            while (elapsed < seconds && count < frameSamples.Length)
            {
                if (mission.State != MissionState.Playing) break;
                if (dense)
                {
                    if (elapsed >= nextHit)
                    {
                        ShipTarget target = NextTarget(); if (target != null) SweepTarget(target);
                        nextHit += .24f;
                    }
                }
                else AdvanceFlight();
                yield return null;
                double dt = Time.unscaledDeltaTime; elapsed += dt;
                frameSamples[count++] = dt * 1000;
                if (mainThreadRecorder.Valid && mainThreadRecorder.Count > 0) mainSamples[mainCount++] = mainThreadRecorder.LastValue / 1000000.0;
                if (gcRecorder.Valid && gcRecorder.Count > 0) gcSamples[gcCount++] = gcRecorder.LastValue;
                if (Application.isFocused) phase.focusedFrames++;
                SampleEffects(phase);
            }
            CompletePhase(phase, count, mainCount, gcCount, elapsed);
        }
        void AdvanceFlight()
        {
            simulationAccumulator += Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
            while (simulationAccumulator >= Time.fixedDeltaTime && mission.State == MissionState.Playing)
            {
                mission.Step(Time.fixedDeltaTime, new FlightCommand { throttle = .25f });
                simulationAccumulator -= Time.fixedDeltaTime;
            }
        }
        IEnumerator VerifyPause()
        {
            mission.TogglePause();
            float remaining = mission.Remaining, effectAge = effects != null ? effects.ElapsedSimulationTime : 0;
            Vector3 position = mission.motor.transform.position;
            int score = mission.score.Score, destroyed = mission.DestroyedCount;
            yield return new WaitForSecondsRealtime(.3f);
            mission.Step(1, new FlightCommand { throttle = 1, boost = true });
            AddCheck("Pause freezes timer, movement, score, hits and effects", mission.State == MissionState.Paused &&
                remaining == mission.Remaining && position == mission.motor.transform.position && score == mission.score.Score &&
                destroyed == mission.DestroyedCount && (effects == null || effectAge == effects.ElapsedSimulationTime),
                "Waited 0.3 real seconds and attempted Mission.Step while paused.");
            mission.TogglePause();
        }
        IEnumerator MeasureRestarts()
        {
            var phase = new Phase { name = "Three restart frames" };
            int count = 0, mainCount = 0, gcCount = 0; double elapsed = 0;
            for (int round = 0; round < 3; round++)
            {
                mission.Restart(); mission.StartMission();
                ShipTarget target = NextTarget(); if (target != null) SweepTarget(target);
                yield return null;
                mission.Restart();
                int live = 0;
                foreach (ShipTarget ship in mission.targets) if (ship != null && !ship.IsDestroyed) live++;
                bool pass = mission.State == MissionState.Ready && mission.DestroyedCount == 0 && live == mission.TotalCount &&
                    mission.score.Score == 0 && mission.score.Combo == 0 && mission.Elapsed == 0 &&
                    Mathf.Approximately(mission.Remaining, mission.settings.missionSeconds) &&
                    mission.motor.transform.position == mission.spawnPosition && Mathf.Approximately(mission.motor.Speed, mission.settings.initialSpeed) &&
                    Time.timeScale == 1 && (effects == null || effects.ActiveEffectCount == 0 && effects.ActiveAudioCount == 0 && effects.PoolInstanceCount == initialPoolCount);
                // Record the actual rendered interval that includes Restart, not
                // the waiting frames between rounds or the cleanup screenshot.
                yield return null;
                double dt = Time.unscaledDeltaTime; elapsed += dt; frameSamples[count++] = dt * 1000;
                if (mainThreadRecorder.Valid && mainThreadRecorder.Count > 0) mainSamples[mainCount++] = mainThreadRecorder.LastValue / 1000000.0;
                if (gcRecorder.Valid && gcRecorder.Count > 0) gcSamples[gcCount++] = gcRecorder.LastValue;
                if (Application.isFocused) phase.focusedFrames++;
                SampleEffects(phase);
                int actualTargets = FindObjectsByType<ShipTarget>(FindObjectsInactive.Include).Length;
                AddCheck("Restart " + (round + 1), pass && actualTargets == mission.TotalCount,
                    "live=" + live + ", total=" + mission.TotalCount + ", actual scene targets=" + actualTargets + ", pool=" + (effects != null ? effects.PoolInstanceCount : 0));
                yield return new WaitForSecondsRealtime(.15f);
            }
            CompletePhase(phase, count, mainCount, gcCount, elapsed);
        }

        void SweepFirstChain()
        {
            // This is the authored straight practice lane, not arbitrary nearest
            // neighbours whose near-vertical direction the flight pitch limit changes.
            ShipTarget first = TargetById("SPAWN_Small_001");
            ShipTarget middle = TargetById("SPAWN_Small_002");
            ShipTarget last = TargetById("SPAWN_Small_003");
            if (first == null || middle == null || last == null)
            {
                AddCheck("Authored three-ship practice lane exists", false, "Requires stable IDs SPAWN_Small_001, 002 and 003.");
                return;
            }
            Bounds firstBounds = BoundsOf(first), lastBounds = BoundsOf(last);
            Vector3 direction = (lastBounds.center - firstBounds.center).normalized;
            float middleDistance = Vector3.Cross(BoundsOf(middle).center - firstBounds.center, direction).magnitude;
            bool straight = Mathf.Abs(direction.y) < .001f && middleDistance < .01f;
            AddCheck("Authored three-ship practice lane is horizontal and straight", straight,
                "Middle distance from line=" + middleDistance + ", direction=" + direction);
            if (!straight) return;
            float firstExtent = Vector3.Dot(firstBounds.extents, new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
            float lastExtent = Vector3.Dot(lastBounds.extents, new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
            float padding = firstExtent + mission.settings.hitRadius + 3;
            Vector3 start = firstBounds.center - direction * padding;
            float travel = Vector3.Distance(firstBounds.center, lastBounds.center) + padding + lastExtent + mission.settings.hitRadius + 3;
            SweepFrom(start, direction, travel);
        }
        ShipTarget TargetById(string id)
        {
            foreach (ShipTarget target in mission.targets) if (target != null && target.targetId == id) return target;
            return null;
        }
        IEnumerator CaptureImpactInspection()
        {
            ShipTarget target = TargetById("SPAWN_Small_003");
            inspectionChase = mission.chaseCamera;
            inspectionView = inspectionChase != null ? inspectionChase.GetComponent<Camera>() : Camera.main;
            if (target == null || inspectionView == null)
            {
                AddCheck("Impact inspection camera and target available", false, "Cannot inspect SPAWN_Small_003 without the actual scene camera.");
                inspectionView = null; inspectionChase = null; yield break;
            }
            savedInspectionPosition = inspectionView.transform.position;
            savedInspectionRotation = inspectionView.transform.rotation;
            savedChaseEnabled = inspectionChase != null && inspectionChase.enabled;
            if (inspectionChase != null) inspectionChase.enabled = false;
            Vector3 focus = target.transform.position + target.transform.forward * 2;
            Vector3 eye = target.transform.position + target.transform.right * 28 + Vector3.up * 14 - target.transform.forward * 23;
            inspectionView.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye, Vector3.up));
            AddCheck("Impact inspection uses real destroyed target and pooled effects",
                target.IsDestroyed && effects != null && effects.ActiveEffectCount >= 3,
                "Inspection angle only; target=" + target.targetId + ", camera=" + eye + ", effects=" + (effects != null ? effects.ActiveEffectCount : 0));
            // Capture immediately after the actual sweep so its short warm flash
            // is present, then capture the same real wreck after fragments drift.
            yield return Capture("03-impact-inspection-flash");
            yield return new WaitForSecondsRealtime(.55f);
            yield return Capture("03-impact-inspection-wreck");
            RestoreInspectionCamera();
        }
        void RestoreInspectionCamera()
        {
            if (inspectionView == null) return;
            inspectionView.transform.SetPositionAndRotation(savedInspectionPosition, savedInspectionRotation);
            if (inspectionChase != null)
            {
                inspectionChase.enabled = savedChaseEnabled;
                if (savedChaseEnabled) inspectionChase.ResetCamera();
            }
            inspectionView = null; inspectionChase = null;
        }
        void SweepTarget(ShipTarget target)
        {
            Bounds bounds = BoundsOf(target); Vector3 direction = target.transform.forward;
            float padding = bounds.extents.magnitude + mission.settings.hitRadius + 3;
            SweepFrom(bounds.center - direction * padding, direction, padding * 2);
        }
        void SweepFrom(Vector3 start, Vector3 direction, float travel)
        {
            int before = mission.DestroyedCount, score = mission.score.Score;
            mission.motor.ResetPose(start, Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > .99f ? Vector3.right : Vector3.up));
            Physics.SyncTransforms(); mission.chaseCamera?.ResetCamera();
            safeRepositions++;
            if (mission.DestroyedCount != before || mission.score.Score != score) teleportDamageFailures++;
            // Solve distance = speed-at-end-of-step * step for the existing motor's
            // assisted acceleration law. This avoids overshooting far past the wreck.
            float low = 0, high = travel / Mathf.Max(1, mission.settings.initialSpeed) + 1;
            for (int i = 0; i < 24; i++)
            {
                float dt = (low + high) * .5f;
                float cruise = Mathf.Clamp(mission.settings.initialSpeed + mission.settings.speedAdjustment * dt, 0, mission.settings.maxCruiseSpeed);
                float speed = Mathf.MoveTowards(mission.settings.initialSpeed, cruise * mission.settings.boostMultiplier, mission.settings.acceleration * dt);
                if (speed * dt < travel) low = dt; else high = dt;
            }
            float step = high;
            // An explicit long simulation step exercises the same full path query
            // API; it is intentionally separate from the normal fixed-step phase.
            mission.Step(step, new FlightCommand { throttle = 1, boost = true });
        }
        ShipTarget NextTarget()
        {
            foreach (ShipTarget target in mission.targets) if (target != null && !target.IsDestroyed) return target;
            return null;
        }
        static Bounds BoundsOf(ShipTarget target)
        {
            Bounds bounds = new Bounds(target.transform.position, Vector3.zero); bool found = false;
            if (target.hitVolumes != null) foreach (Collider collider in target.hitVolumes)
                if (collider != null && collider.enabled) { if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds); }
            return bounds;
        }
        void SampleEffects(Phase phase)
        {
            if (effects == null) return;
            phase.effectsPeak = Mathf.Max(phase.effectsPeak, effects.ActiveEffectCount);
            phase.audioPeak = Mathf.Max(phase.audioPeak, effects.ActiveAudioCount);
        }
        void CompletePhase(Phase phase, int count, int mainCount, int gcCount, double seconds)
        {
            phase.renderedFrameMs = Summarize(frameSamples, count); phase.mainThreadMs = Summarize(mainSamples, mainCount);
            phase.gcAllocatedBytes = Summarize(gcSamples, gcCount); phase.mainThreadRecorderAvailable = mainCount > 0;
            phase.gcRecorderAvailable = gcCount > 0; phase.measuredSeconds = seconds;
            phase.poolObjectCount = effects != null ? effects.PoolInstanceCount : 0; phase.destroyedAtEnd = mission.DestroyedCount;
            phase.allocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(); phase.reservedMemoryBytes = Profiler.GetTotalReservedMemoryLong();
            phase.monoUsedBytes = Profiler.GetMonoUsedSizeLong(); phases.Add(phase);
        }
        static Distribution Summarize(double[] samples, int count)
        {
            var value = new Distribution { samples = count }; if (count == 0) return value;
            double sum = 0; for (int i = 0; i < count; i++) sum += samples[i];
            Array.Sort(samples, 0, count); value.mean = sum / count;
            value.p50 = samples[Mathf.Clamp(Mathf.CeilToInt(count * .5f) - 1, 0, count - 1)];
            value.p95 = samples[Mathf.Clamp(Mathf.CeilToInt(count * .95f) - 1, 0, count - 1)];
            value.p99 = samples[Mathf.Clamp(Mathf.CeilToInt(count * .99f) - 1, 0, count - 1)];
            value.maximum = samples[count - 1]; return value;
        }
        IEnumerator Capture(string name)
        {
            string path = Path.Combine(report.outputDirectory, name + ".png");
            ScreenCapture.CaptureScreenshot(path); screenshots.Add(path);
            // Capture includes the rendered HUD. Waiting is excluded from all
            // measured phases; screenshots are evidence requests, not visual QA.
            yield return new WaitForSecondsRealtime(.35f);
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            AddCheck("Screenshot written: " + name, File.Exists(path), path);
        }
        void AddCheck(string name, bool pass, string detail)
        { checks.Add(new Check { name = name, passed = pass, detail = detail }); }

        void Finish()
        {
            if (!IsRunning) return;
            RestoreInspectionCamera();
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
            if (gcRecorder.Valid) gcRecorder.Dispose();
            if (mission != null)
            {
                mission.Restart(); mission.enabled = oldMissionEnabled;
                if (mission.input != null) { mission.input.PauseRequested = oldPauseCallback; mission.input.enabled = oldInputEnabled; }
            }
            Application.runInBackground = oldRunInBackground;
            report.phases = phases.ToArray(); report.checks = checks.ToArray(); report.screenshots = screenshots.ToArray();
            report.allFlowChecksPassed = report.completed && string.IsNullOrEmpty(report.error);
            foreach (Check check in checks) if (!check.passed) report.allFlowChecksPassed = false;
            LastReportPath = Path.Combine(report.outputDirectory, "benchmark.json");
            var summary = new StringBuilder();
            summary.AppendLine("DropletPrototype rendered benchmark / scripted flow");
            summary.AppendLine("Completed: " + report.completed + "; all recorded checks: " + report.allFlowChecksPassed);
            summary.AppendLine("Actual: " + report.width + "x" + report.height + ", " + report.quality + ", effects " + report.effectQuality + ", " + report.gpu);
            foreach (Phase phase in phases) summary.AppendLine(phase.name + ": " + phase.renderedFrameMs.samples + " frames; mean " + phase.renderedFrameMs.mean.ToString("F2", CultureInfo.InvariantCulture) + " ms, p95 " + phase.renderedFrameMs.p95.ToString("F2", CultureInfo.InvariantCulture) + " ms; effects/audio peak " + phase.effectsPeak + "/" + phase.audioPeak);
            foreach (Check check in checks) summary.AppendLine((check.passed ? "PASS " : "FAIL ") + check.name + ": " + check.detail);
            summary.AppendLine(report.scope);
            if (!string.IsNullOrEmpty(report.error)) summary.AppendLine(report.error);
            LastSummary = summary.ToString();
            try { File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true), Encoding.UTF8); File.WriteAllText(Path.Combine(report.outputDirectory, "summary.txt"), LastSummary, Encoding.UTF8); }
            catch (Exception exception) { Debug.LogError("Benchmark report write failed: " + exception.Message, this); }
            IsRunning = false; routine = null; Debug.Log("FleetBenchmark complete: " + LastReportPath + ", checks=" + report.allFlowChecksPassed, this);
            if (quitAfter && !Application.isEditor) Application.Quit(report.allFlowChecksPassed ? 0 : 1);
        }
        void OnDisable()
        {
            if (!IsRunning) return;
            if (routine != null) StopCoroutine(routine);
            report.error = "Benchmark interrupted because its component was disabled."; Finish();
        }
    }
}
