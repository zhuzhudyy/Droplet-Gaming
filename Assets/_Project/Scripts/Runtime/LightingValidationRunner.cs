using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace DropletPrototype
{
    /// <summary>Explicit rendered diagnostics. Inert unless Begin or the command-line opt-in is used.</summary>
    public sealed class LightingValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Distribution
        { public int samples; public double mean, p50, p95, p99, maximum; }
        [Serializable] public sealed class Phase
        {
            public string name; public int frames, width, height, focusedFrames, targets, destroyedAtEnd, effectsPeak, pool, probeCaptures;
            public Distribution renderedFrameMs, mainThreadMs, gcAllocatedBytes;
            public bool mainThreadAvailable, gcAvailable;
            public long allocatedBytes, reservedBytes, monoBytes;
            public double measuredSeconds;
            public Vector3 cameraPosition, cameraForward;
        }
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Shot
        { public string name, path, purpose; public Vector3 cameraPosition, cameraForward; public float fov; public bool written; }
        [Serializable] public sealed class Report
        {
            public string label, scene, utc, unity, cpu, gpu, graphicsApi, operatingSystem, quality, effectQuality;
            public string outputDirectory, scope, error;
            public bool editor, developmentBuild, batchMode, completed, allChecksPassed;
            public int width, height, processorCount, systemMemoryMB, graphicsMemoryMB, vSyncCount, targetFrameRate;
            public Phase[] phases; public Check[] checks; public Shot[] screenshots;
        }
        [Serializable] public sealed class Progress
        {
            public string label, phase, utc, outputDirectory;
            public int phaseFrame, completedPhases, screenshots, unityFrame;
            public bool running, focused, active, enabled, runInBackground;
            public float timeScale;
        }

        public bool IsRunning { get; private set; }
        public string LastReportPath { get; private set; }
        public Report LastReport { get; private set; }
        public string CurrentPhase { get; private set; } = "Idle";
        public int PhaseFrame { get; private set; }
        public string OutputDirectory => report != null ? report.outputDirectory : null;
        readonly List<Phase> phases = new List<Phase>();
        readonly List<Check> checks = new List<Check>();
        readonly List<Shot> shots = new List<Shot>();
        readonly StringBuilder frameCsv = new StringBuilder(65536);
        MissionController mission;
        MissionEffects effects;
        Camera view;
        HudPresenter hud;
        SolarSystemBackdrop backdrop;
        ProfilerRecorder mainRecorder, gcRecorder;
        Report report;
        Coroutine routine;
        IEnumerator guarded;
        bool saved, finishing, quitAfter, missionEnabled, inputEnabled, chaseEnabled, hudEnabled, background, backdropEnabled;
        int initialPool, teleportChecks, teleportFailures;
        float oldFov, oldNear, oldFar;
        Vector3 oldCameraPosition;
        Quaternion oldCameraRotation;
        Action oldPauseCallback, oldStartCallback, oldRestartCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs(); string output = null, label = "Player"; bool quit = false, requested = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lighting-validation")
                {
                    requested = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i];
                }
                else if (args[i] == "-lighting-validation-label" && i + 1 < args.Length) label = args[++i];
                else if (args[i] == "-lighting-validation-quit") quit = true;
            }
            if (requested && FindAnyObjectByType<LightingValidationRunner>() == null)
                new GameObject("__TemporaryLightingValidation").AddComponent<LightingValidationRunner>().Begin(output, label, quit);
        }

        public void Begin(string outputDirectory, string label, bool quitWhenComplete = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Lighting validation requires Play mode or a rendered player.");
            if (IsRunning || Resources.FindObjectsOfTypeAll<LightingValidationRunner>().Any(x => x != this && x.IsRunning))
                throw new InvalidOperationException("A lighting validation is already running.");
            mission = FindAnyObjectByType<MissionController>();
            if (mission == null || mission.settings == null || mission.motor == null || mission.chaseCamera == null || mission.score == null)
                throw new InvalidOperationException("Open a configured playable fleet scene before validation.");
            view = mission.chaseCamera.GetComponent<Camera>();
            if (view == null) throw new InvalidOperationException("Mission chase camera has no Camera.");
            string parent = string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Application.persistentDataPath, "LightingValidation") : outputDirectory;
            string safeLabel = string.Concat((label ?? "Run").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string directory = Path.GetFullPath(Path.Combine(parent, safeLabel + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(directory);
            report = new Report
            {
                label = label, scene = mission.gameObject.scene.path, utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                operatingSystem = SystemInfo.operatingSystem, editor = Application.isEditor, developmentBuild = Debug.isDebugBuild,
                batchMode = Application.isBatchMode, processorCount = SystemInfo.processorCount, systemMemoryMB = SystemInfo.systemMemorySize,
                graphicsMemoryMB = SystemInfo.graphicsMemorySize, outputDirectory = directory,
                scope = "Actual rendered per-frame measurements in this process only. Each phase has 45 warmup frames. Near/Panorama/Penetrations have 300 measured frames; optional ReflectionRefresh/ReflectionsOff have 2400 each at the same near camera. Refresh invalidates the one probe every 120 frames, while its production 3-second interval remains unchanged; probeCaptures records actual requests during measurement. Off disables only the nearby probe, retaining the allocated capture texture. Near and Panorama are fixed stationary gameplay views; Penetrations resets attack starts then calls MissionController.Step at 0.02 s, exercising real DropletMotor sweeps. No direct target destruction or scoring writes. Frame timing includes Editor overhead when editor=true; not GPU time. Main Thread/GC markers are reported only when available. Memory is total process Unity tracked memory, not asset-attributable VRAM. Screenshots use the actual rendered scene camera; Earth diagnostic temporarily freezes celestial mapping and moves the camera close, and is not game-scale evidence. This does not validate physical keyboard input, subjective handling, listening, long-session stability or cross-hardware performance."
            };
            quitAfter = quitWhenComplete; phases.Clear(); checks.Clear(); shots.Clear(); frameCsv.Clear();
            frameCsv.AppendLine("phase,frame,renderedFrameMs,mainThreadMs,gcAllocatedBytes,focused,destroyed,effects");
            initialPool = teleportChecks = teleportFailures = 0;
            IsRunning = true; finishing = false;
            // A tool-driven Editor can be unfocused. Enable background frames before
            // the very first yield, otherwise the coroutine cannot reach Run's setup.
            background = Application.runInBackground; Application.runInBackground = true;
            SetPhase("Waiting for first rendered frame");
            guarded = Guard(Run());
            try { routine = StartCoroutine(guarded); }
            catch (Exception exception) { RecordError(exception); Finish(); throw; }
        }

        IEnumerator Guard(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            try
            {
                while (stack.Count > 0)
                {
                    bool moved = false; object value = null; Exception error = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; }
                    catch (Exception exception) { error = exception; }
                    if (error != null) { RecordError(error); break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (value is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return value;
                }
            }
            finally
            {
                while (stack.Count > 0) Safe(() => (stack.Pop() as IDisposable)?.Dispose());
                Finish();
            }
        }

        IEnumerator Run()
        {
            // Bootstrap can run before other Start methods; allow them to finish first.
            yield return null;
            SetPhase("Capturing initial state");
            effects = FindAnyObjectByType<MissionEffects>(); hud = FindAnyObjectByType<HudPresenter>(); backdrop = FindAnyObjectByType<SolarSystemBackdrop>();
            missionEnabled = mission.enabled; inputEnabled = mission.input != null && mission.input.enabled; chaseEnabled = mission.chaseCamera.enabled;
            hudEnabled = hud != null && hud.enabled;
            backdropEnabled = backdrop != null && backdrop.enabled;
            oldCameraPosition = view.transform.position; oldCameraRotation = view.transform.rotation;
            oldFov = view.fieldOfView; oldNear = view.nearClipPlane; oldFar = view.farClipPlane;
            if (mission.input != null)
            {
                oldPauseCallback = mission.input.PauseRequested; oldStartCallback = mission.input.StartRequested; oldRestartCallback = mission.input.RestartRequested;
            }
            saved = true; Application.runInBackground = true;
            mission.enabled = false; mission.chaseCamera.enabled = false;
            if (mission.input != null)
            {
                mission.input.enabled = false;
                mission.input.PauseRequested = null; mission.input.StartRequested = null; mission.input.RestartRequested = null;
            }
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            effects?.InitializePool(); initialPool = effects != null ? effects.PoolInstanceCount : 0;
            mission.Restart(); SetPhase("Initial render warmup"); yield return new WaitForSecondsRealtime(1);
            report.width = Screen.width; report.height = Screen.height;
            int quality = QualitySettings.GetQualityLevel(); report.quality = QualitySettings.names[quality];
            report.effectQuality = effects != null ? effects.Quality.ToString() : "None";
            report.vSyncCount = QualitySettings.vSyncCount; report.targetFrameRate = Application.targetFrameRate;
            CheckResult("Rendered graphics device", SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                report.graphicsApi + ", " + Screen.width + "x" + Screen.height);
            CheckResult("120 registered stationary targets", mission.TotalCount == 120 && mission.targets.Length == 120, "total=" + mission.TotalCount);
            try { mainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); } catch (Exception) { }
            try { gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); } catch (Exception) { }
            yield return Measure("Near", false);
            yield return Measure("Panorama", false);
            yield return Measure("Penetrations", true);
            var lighting = FindAnyObjectByType<SolarLightingRig>();
            if (lighting != null)
            {
                bool reflections = lighting.enhancedReflections;
                try
                {
                    lighting.enhancedReflections = true;
                    yield return Measure("ReflectionRefresh", false, 2400, true);
                    CheckResult("Probe refresh cost sampled", phases.Last().probeCaptures > 0, "Actual capture requests=" + phases.Last().probeCaptures);
                    lighting.enhancedReflections = false;
                    yield return Measure("ReflectionsOff", false, 2400);
                }
                finally { lighting.enhancedReflections = reflections; lighting.RequestProbeRefresh(); }
            }
            yield return VisualViews();
            yield return GameplayChecks();
            CheckResult("ResetPose itself never damaged ships or awarded points", teleportChecks > 0 && teleportFailures == 0,
                "repositions=" + teleportChecks + ", unexpected changes=" + teleportFailures);
            report.completed = true;
        }

        ShipTarget FirstTarget() => mission.targets.Where(t => t != null).OrderBy(t => (t.transform.position - mission.spawnPosition).sqrMagnitude).First();
        void Aim(Vector3 position, Vector3 focus)
        {
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
            backdrop?.ApplyMapping(view);
        }
        void SetPhaseCamera(string label, ShipTarget target)
        {
            if (label == "Panorama") Aim(new Vector3(950, 720, -1150), new Vector3(0, 40, 600));
            else Aim(target.transform.position + new Vector3(19, 10, -30), target.transform.position);
        }
        IEnumerator Measure(string name, bool attack, int count = 300, bool refreshProbe = false)
        {
            SetPhase(name + " warmup");
            mission.Restart(); mission.StartMission();
            mission.motor.ResetPose(new Vector3(0, 8, -200), Quaternion.identity);
            SetPhaseCamera(name, FirstTarget());
            if (hud != null) hud.enabled = hudEnabled;
            for (int i = 0; i < 45; i++) { PhaseFrame = i + 1; yield return null; }
            var lighting = FindAnyObjectByType<SolarLightingRig>();
            int capturesBefore = lighting != null ? lighting.CaptureCount : 0;
            var frames = new double[count]; var mains = new List<double>(count); var gcs = new List<double>(count);
            var sampledMains = new double[count]; var sampledGcs = new double[count];
            var mainValid = new bool[count]; var gcValid = new bool[count]; var focused = new bool[count];
            var destroyedCounts = new int[count]; var effectCounts = new int[count];
            var phase = new Phase { name = name, frames = count, width = Screen.width, height = Screen.height, targets = mission.TotalCount };
            ShipTarget[] ordered = mission.targets.OrderBy(t => t.targetId, StringComparer.Ordinal).ToArray(); int next = 0;
            SetPhase(name + " measuring " + count + " frames");
            yield return null; // Exclude sample-array creation and phase setup from the first measured interval.
            double begin = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < count; i++)
            {
                if (refreshProbe && i % 120 == 0) lighting.RequestProbeRefresh();
                if (attack && i % 12 == 0)
                {
                    while (next < ordered.Length && ordered[next].IsDestroyed) next++;
                    if (next < ordered.Length) { ShipTarget target = ordered[next++]; SweepTarget(target); SetPhaseCamera(name, target); }
                }
                yield return null;
                double frame = Time.unscaledDeltaTime * 1000.0; frames[i] = frame;
                bool haveMain = mainRecorder.Valid && mainRecorder.Count > 0, haveGc = gcRecorder.Valid && gcRecorder.Count > 0;
                double main = haveMain ? mainRecorder.LastValue / 1000000.0 : 0, gc = haveGc ? gcRecorder.LastValue : 0;
                if (haveMain) mains.Add(main); if (haveGc) gcs.Add(gc);
                if (Application.isFocused) phase.focusedFrames++;
                int active = effects != null ? effects.ActiveEffectCount : 0; phase.effectsPeak = Mathf.Max(phase.effectsPeak, active);
                sampledMains[i] = main; sampledGcs[i] = gc; mainValid[i] = haveMain; gcValid[i] = haveGc;
                focused[i] = Application.isFocused; destroyedCounts[i] = mission.DestroyedCount; effectCounts[i] = active;
                PhaseFrame = i + 1;
            }
            phase.measuredSeconds = Time.realtimeSinceStartupAsDouble - begin;
            phase.probeCaptures = lighting != null ? lighting.CaptureCount - capturesBefore : 0;
            phase.allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(); phase.reservedBytes = Profiler.GetTotalReservedMemoryLong(); phase.monoBytes = Profiler.GetMonoUsedSizeLong();
            phase.destroyedAtEnd = mission.DestroyedCount; phase.pool = effects != null ? effects.PoolInstanceCount : 0;
            phase.cameraPosition = view.transform.position; phase.cameraForward = view.transform.forward;
            // Formatting and file I/O stay outside the measured interval.
            for (int i = 0; i < count; i++)
                frameCsv.Append(name).Append(',').Append(i).Append(',').Append(frames[i].ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(mainValid[i] ? sampledMains[i].ToString("R", CultureInfo.InvariantCulture) : "").Append(',')
                    .Append(gcValid[i] ? sampledGcs[i].ToString("R", CultureInfo.InvariantCulture) : "").Append(',')
                    .Append(focused[i] ? 1 : 0).Append(',').Append(destroyedCounts[i]).Append(',').Append(effectCounts[i]).AppendLine();
            phase.renderedFrameMs = Summarize(frames); phase.mainThreadMs = Summarize(mains.ToArray()); phase.gcAllocatedBytes = Summarize(gcs.ToArray());
            phase.mainThreadAvailable = mains.Count > 0; phase.gcAvailable = gcs.Count > 0; phases.Add(phase);
            yield return Capture("performance-" + name, "Fixed comparison viewpoint after " + count + " measured rendered frames; screenshot work excluded from timing.");
        }

        IEnumerator VisualViews()
        {
            mission.Restart(); mission.StartMission(); if (hud != null) hud.enabled = false;
            view.fieldOfView = oldFov; mission.motor.ResetPose(mission.spawnPosition, Quaternion.identity);
            mission.chaseCamera.ResetCamera();
            yield return Capture("01-spawn-game-scale", "Normal spawn camera, fixed gameplay scale and FOV.");
            Vector3 sun = SunDirection();
            Vector3[] eyes = { mission.spawnPosition + new Vector3(0, 2.2f, -8), new Vector3(950, 720, -1150), new Vector3(-3000, 600, -1200) };
            string[] names = { "02-spawn-sun-facing", "03-outskirts-sun-facing", "04-open-space-sun-facing" };
            for (int i = 0; i < eyes.Length; i++)
            {
                Aim(eyes[i], eyes[i] + sun * 1000);
                yield return Capture(names[i], "Same gameplay FOV and unmodified celestial mapping; camera aimed at physical sun direction.");
            }
            ShipTarget first = FirstTarget(); Transform ship = first.transform;
            Aim(ship.TransformPoint(new Vector3(0, 2.5f, -26)), ship.TransformPoint(new Vector3(0, 0, -6)));
            yield return Capture("05-engine-rear", "Main and four auxiliary nozzles from the rear, normal scene effects.");
            Aim(ship.TransformPoint(new Vector3(16, 9, -23)), ship.TransformPoint(new Vector3(0, 0, -3)));
            yield return Capture("06-engine-three-quarter", "Rear three-quarter view checks fireball depth and metal nozzle separation.");
            Aim(ship.TransformPoint(new Vector3(25, 15, -220)), ship.position);
            yield return Capture("07-engine-far", "Unforced native LOD at 220 m; engine effects must follow the rendered ship.");
            Aim(ship.TransformPoint(new Vector3(0, 100, -1800)), ship.position);
            yield return Capture("08-fleet-distant-lod", "Unforced native LOD and effect visibility at about 1800 m.");
            mission.motor.ResetPose(ship.position + ship.transform.right * 8, Quaternion.Euler(0, 35, 0));
            Vector3 drop = mission.motor.transform.position;
            Aim(drop + new Vector3(-3.8f, 1.2f, -2.8f), drop + Vector3.up * .1f);
            var lighting=FindAnyObjectByType<SolarLightingRig>();
            if(lighting!=null)
            {
                lighting.RequestProbeRefresh();double limit=Time.realtimeSinceStartupAsDouble+8;
                while(!lighting.ProbeHasFreshCapture&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
                CheckResult("Nearby reflection completed before mirror inspection",lighting.ProbeHasFreshCapture,"captures="+lighting.CaptureCount);
            }
            yield return Capture("09-droplet-reflection", "Existing player visual at gameplay scale; close inspection camera, no material replacement.");
            if (backdrop != null)
            {
                Renderer rock = backdrop.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterial!=null&&r.sharedMaterial.name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(r => (r.bounds.center - mission.spawnPosition).sqrMagnitude).FirstOrDefault();
                if (rock != null)
                {
                    float radius = Mathf.Max(4, rock.bounds.extents.magnitude);
                    Aim(rock.bounds.center + (sun + new Vector3(.35f, .35f, -.05f)).normalized * radius * 2.7f, rock.bounds.center);
                    yield return Capture("10-rock-material", "Local rock material and sun-directed contrast, scene light unchanged.");
                }
                if (backdrop.earthProxy != null)
                {
                    backdrop.ApplyMapping(view); backdrop.enabled = false;
                    Transform earth = backdrop.earthProxy; Vector3 center = earth.position;
                    float radius = Mathf.Max(.05f, earth.lossyScale.x);
                    // Fixed mapped proxy: diagnostic camera only, never resize the actual Earth.
                    Vector3 eye = center + (sun + Vector3.right * .45f).normalized * radius * 3.4f;
                    view.nearClipPlane = Mathf.Max(.001f, radius * .01f);
                    view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(center - eye));
                    yield return Capture("11-earth-material-DIAGNOSTIC-NOT-GAME-SCALE", "Diagnostic close camera with celestial mapping temporarily frozen. Not an angular-size or gameplay-distance screenshot.");
                    view.nearClipPlane = oldNear; backdrop.enabled = backdropEnabled;
                }
            }
            if (hud != null) hud.enabled = hudEnabled; view.fieldOfView = oldFov;
        }

        Vector3 SunDirection()
        {
            if (backdrop != null && SolarLayoutMath.TryProjectBody(backdrop.Layout, backdrop.Layout?.FindBody("sun"), mission.spawnPosition, out SolarBodyProjection p)) return p.direction;
            if (RenderSettings.sun != null) return -RenderSettings.sun.transform.forward;
            return new Vector3(1, .15f, 1).normalized;
        }

        IEnumerator GameplayChecks()
        {
            SetPhase("Gameplay sweep, pause, victory and restart checks");
            mission.Restart(); mission.StartMission(); SetPhaseCamera("Near", FirstTarget());
            ShipTarget first = FirstTarget(); SweepTarget(first);
            CheckResult("Real motor sweep destroys and scores", first.IsDestroyed && mission.DestroyedCount > 0 && mission.score.Score > 0,
                "id=" + first.targetId + ", hits=" + mission.DestroyedCount + ", score=" + mission.score.Score);
            CheckResult("Destroyed ship and attached engine renderers are inactive", first.visualRoot != null && !first.visualRoot.activeInHierarchy &&
                first.visualRoot.GetComponentsInChildren<Renderer>(true).All(r => !r.gameObject.activeInHierarchy), "checked entire ShipTarget.visualRoot subtree");
            yield return Capture("12-real-impact-engines-off", "Real motor swept hit. Intact ship including attached engine effects is inactive.");
            mission.TogglePause();
            Vector3 position = mission.motor.transform.position; float remaining = mission.Remaining, effectsAge = effects != null ? effects.ElapsedSimulationTime : 0;
            int score = mission.score.Score, destroyed = mission.DestroyedCount;
            yield return new WaitForSecondsRealtime(.3f); mission.Step(1, new FlightCommand { throttle = 1, boost = true });
            CheckResult("Pause freezes timer, movement, score, hits and pooled effects", mission.State == MissionState.Paused && position == mission.motor.transform.position &&
                remaining == mission.Remaining && score == mission.score.Score && destroyed == mission.DestroyedCount &&
                (effects == null || effectsAge == effects.ElapsedSimulationTime), "0.3 real seconds plus an attempted Mission.Step while paused");
            mission.TogglePause();
            foreach (ShipTarget target in mission.targets.OrderBy(t => t.targetId, StringComparer.Ordinal))
            {
                if (mission.State != MissionState.Playing) break;
                if (!target.IsDestroyed) SweepTarget(target);
                yield return null;
            }
            CheckResult("All 120 ships swept to victory", mission.TotalCount == 120 && mission.DestroyedCount == mission.TotalCount && mission.Won && mission.State == MissionState.Results,
                "hits=" + mission.DestroyedCount + "/" + mission.TotalCount + ", score=" + mission.score.Score + ", elapsed=" + mission.Elapsed + ", state=" + mission.State);
            int transitions = mission.ResultTransitions, resultScore = mission.score.Score;
            mission.Step(1, new FlightCommand { throttle = 1, boost = true }); mission.StartMission();
            CheckResult("Results and score resolve once", transitions == 1 && mission.ResultTransitions == 1 && mission.score.Score == resultScore, "transitions=" + mission.ResultTransitions);
            yield return Capture("13-victory", "All targets destroyed by actual simulated motor paths.");
            for (int round = 0; round < 3; round++)
            {
                mission.Restart();
                bool restored = mission.targets.All(t => !t.IsDestroyed && t.visualRoot != null && t.visualRoot.activeInHierarchy && t.hitVolumes.All(c => c != null && c.enabled));
                CheckResult("Restart " + (round + 1) + " restores all ships and engine hierarchy", restored && mission.State == MissionState.Ready &&
                    mission.DestroyedCount == 0 && mission.score.Score == 0 && mission.score.Combo == 0 && mission.Elapsed == 0 &&
                    Mathf.Approximately(mission.Remaining, mission.settings.missionSeconds) && Time.timeScale == 1 &&
                    mission.motor.transform.position == mission.spawnPosition && Mathf.Approximately(mission.motor.Speed, mission.settings.initialSpeed) &&
                    (effects == null || effects.ActiveEffectCount == 0 && effects.ActiveAudioCount == 0 && effects.PoolInstanceCount == initialPool),
                    "restored=" + restored + ", pool=" + (effects != null ? effects.PoolInstanceCount : 0));
                if (round < 2) { mission.StartMission(); SweepTarget(FirstTarget()); yield return null; }
            }
            yield return Capture("14-restarted-ready", "Ready state after three clean restart cycles.");
        }

        void SweepTarget(ShipTarget target)
        {
            if (target == null || target.IsDestroyed || mission.State != MissionState.Playing) return;
            Bounds bounds = default; bool found = false;
            foreach (Collider volume in target.hitVolumes)
                if (volume != null && volume.enabled) { if (!found) { bounds = volume.bounds; found = true; } else bounds.Encapsulate(volume.bounds); }
            if (!found) throw new InvalidOperationException("Target has no enabled hit geometry: " + target.targetId);
            // Horizontal local-forward path honours the motor's yaw/pitch contract.
            Vector3 direction = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .9f) direction = Vector3.forward;
            float extent = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(direction.x), 0, Mathf.Abs(direction.z)));
            Vector3 start = bounds.center - direction * (extent + mission.settings.hitRadius + 4);
            int before = mission.DestroyedCount, score = mission.score.Score;
            mission.motor.ResetPose(start, Quaternion.LookRotation(direction)); Physics.SyncTransforms(); teleportChecks++;
            if (mission.DestroyedCount != before || mission.score.Score != score) teleportFailures++;
            for (int step = 0; step < 250 && !target.IsDestroyed && mission.State == MissionState.Playing; step++)
                mission.Step(.02f, new FlightCommand { throttle = 1, boost = true });
            if (!target.IsDestroyed) throw new InvalidOperationException("Real fixed-step sweep missed target " + target.targetId);
        }

        IEnumerator Capture(string name, string purpose)
        {
            SetPhase("Screenshot " + name);
            // Two rendered frames settle transform mapping/LOD before the capture request.
            yield return null; yield return null;
            string path = Path.Combine(report.outputDirectory, name + ".png");
            var shot = new Shot { name = name, path = path, purpose = purpose, cameraPosition = view.transform.position, cameraForward = view.transform.forward, fov = view.fieldOfView };
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.15f);
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            shot.written = File.Exists(path) && new FileInfo(path).Length > 0; shots.Add(shot);
            CheckResult("Screenshot written " + name, shot.written, path);
        }
        static Distribution Summarize(double[] data)
        {
            var result = new Distribution { samples = data.Length }; if (data.Length == 0) return result;
            result.mean = data.Average(); Array.Sort(data);
            result.p50 = data[Mathf.CeilToInt(data.Length * .5f) - 1]; result.p95 = data[Mathf.CeilToInt(data.Length * .95f) - 1];
            result.p99 = data[Mathf.CeilToInt(data.Length * .99f) - 1]; result.maximum = data[data.Length - 1]; return result;
        }
        void CheckResult(string name, bool passed, string detail) => checks.Add(new Check { name = name, passed = passed, detail = detail });
        void SetPhase(string phase)
        {
            CurrentPhase = phase; PhaseFrame = 0;
            WriteProgress();
            Debug.Log("LIGHTING VALIDATION PROGRESS " + (report != null ? report.label : "") + ": " + phase);
        }
        public Progress GetProgress() => new Progress
        {
            label = report != null ? report.label : "", phase = CurrentPhase, phaseFrame = PhaseFrame, utc = DateTime.UtcNow.ToString("o"),
            outputDirectory = OutputDirectory, completedPhases = phases.Count, screenshots = shots.Count, unityFrame = Time.frameCount,
            running = IsRunning, focused = Application.isFocused, active = gameObject.activeInHierarchy, enabled = enabled,
            runInBackground = Application.runInBackground, timeScale = Time.timeScale
        };
        public void WriteProgress()
        {
            if (report == null) return;
            try { File.WriteAllText(Path.Combine(report.outputDirectory, "progress.json"), JsonUtility.ToJson(GetProgress(), true), Encoding.UTF8); }
            catch (Exception exception) { Debug.LogWarning("Lighting validation progress write failed: " + exception.Message); }
        }
        void RecordError(Exception error) { if (report != null) { report.completed = false; report.error = string.IsNullOrEmpty(report.error) ? error.ToString() : report.error + "\n" + error; } }
        void Safe(Action action) { try { action(); } catch (Exception exception) { RecordError(exception); } }
        void Finish()
        {
            if (!IsRunning || finishing) return; finishing = true;
            Safe(() => mainRecorder.Dispose()); Safe(() => gcRecorder.Dispose());
            if (saved)
            {
                Safe(() => { if (backdrop != null) backdrop.enabled = backdropEnabled; });
                Safe(() => { if (mission != null) mission.Restart(); });
                Safe(() => { if (view != null) { view.fieldOfView = oldFov; view.nearClipPlane = oldNear; view.farClipPlane = oldFar; view.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); } });
                Safe(() => { if (hud != null) hud.enabled = hudEnabled; });
                Safe(() => { if (mission != null && mission.chaseCamera != null) mission.chaseCamera.enabled = chaseEnabled; });
                Safe(() =>
                {
                    if (mission != null && mission.input != null)
                    {
                        mission.input.PauseRequested = oldPauseCallback; mission.input.StartRequested = oldStartCallback; mission.input.RestartRequested = oldRestartCallback;
                        mission.input.enabled = inputEnabled;
                    }
                });
                Safe(() => { if (mission != null) mission.enabled = missionEnabled; });
            }
            Safe(() => Application.runInBackground = background);
            if (report == null)
            {
                IsRunning = false; saved = false; routine = null;
                CurrentPhase = "Cancelled stale runner without report";
                return;
            }
            report.phases = phases.ToArray(); report.checks = checks.ToArray(); report.screenshots = shots.ToArray();
            report.allChecksPassed = report.completed && string.IsNullOrEmpty(report.error) && checks.All(c => c.passed);
            LastReport = report; LastReportPath = Path.Combine(report.outputDirectory, "lighting-validation.json");
            try
            {
                File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true), Encoding.UTF8);
                File.WriteAllText(Path.Combine(report.outputDirectory, "frames.csv"), frameCsv.ToString(), Encoding.UTF8);
            }
            catch (Exception exception) { Debug.LogError("Lighting validation report write failed: " + exception); }
            IsRunning = false; saved = false; routine = null;
            CurrentPhase = report.allChecksPassed ? "Completed" : "Finished with failures"; WriteProgress();
            Debug.Log("LIGHTING VALIDATION COMPLETE " + report.label + ", completed=" + report.completed + ", checks=" + report.allChecksPassed + ", report=" + LastReportPath);
            if (!string.IsNullOrEmpty(report.error)) Debug.LogError(report.error);
            if (quitAfter && !Application.isEditor) Application.Quit(report.allChecksPassed ? 0 : 1);
        }
        public void Cancel(string reason)
        {
            if (!IsRunning || finishing) return;
            RecordError(new OperationCanceledException(reason));
            Safe(() => { if (routine != null) StopCoroutine(routine); });
            Safe(() => (guarded as IDisposable)?.Dispose());
            Finish();
        }
        void OnDisable() => Cancel("Lighting validation component disabled before completion.");
    }
}
