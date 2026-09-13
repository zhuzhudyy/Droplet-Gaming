using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DropletPrototype
{
    /// <summary>Opt-in rendered evidence, never enabled in ordinary play. Normal
    /// FixedUpdate/input flight is reported separately from controlled stress setup.</summary>
    public sealed class NarrativeCombatValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Distribution
        { public int samples; public double mean = -1, p50 = -1, p95 = -1, p99 = -1, maximum = -1; }
        [Serializable] public sealed class Phase
        {
            public string name, workload; public int frames, focusedFrames, width, height, targets, destroyed, pending, escaped;
            public int peakBeams, peakReactorEffects, shots, reflections, damageFromReflection;
            public int retreatRequested, breakingFormation, fleeing, peakBreakingFormation, peakFleeing, movedShips;
            public float retreatSimulationSeconds, maximumShipSpeedMetersPerSecond;
            public float minimumDisplacementMeters, meanDisplacementMeters, maximumDisplacementMeters, maximumTurnDegrees;
            public bool playerMotionDisabled, unchangedRetreatIdentities, noRetreatDamage;
            public int fleetInstancedDrawCalls = -1, peakFleetInstancedDrawCalls = -1, fleetVisibleInstancedShips = -1;
            public int minimumFleetVisibleInstancedShips = -1, fleetChunkCount = -1, fleetFullPrefabShips = -1;
            public float fleetChunkSizeUnits;
            public double rendererRefreshCpuMsPerFrame, rendererSubmissionCpuMsPerFrame, fleetBeginCpuMsPerFrame, fleetEndCpuMsPerFrame;
            public double seconds, meanFps, controlledDamageSubmissionMs; public Distribution frameMs, cpuFrameMs, mainThreadMs, gpuMs, drawCalls, gcBytes, unityGraphicsBytes;
            public Vector3 cameraPosition, cameraForward; public float renderScale; public bool dynamicResolution;
        }
        [Serializable] public sealed class FlightEvidence
        {
            public string inputMethod, setup; public float simulationSeconds, measuredDistanceUnits, measuredMetersPerSecond;
            public float minimumHeadForwardDot = 1, cruiseMaximumRelativeHudError, cruiseMetersPerSecond;
            public float sprintPeakMetersPerSecond, brakeMinimumMetersPerSecond = float.MaxValue, mouseYawDegrees, mousePitchDegrees;
            public int fixedPoseSamples, newlyDamaged, exploded, recoveries, shots, reflections;
            public int releasedUpdateFrames, observedSprintFrames, observedBrakeFrames, observedMouseFrames, focusedFrames;
            public string inputBackgroundBehavior, editorInputBehavior;
            public Vector3 start, end; public bool missionFixedUpdateRemainedEnabled;
        }
        [Serializable] public sealed class Report
        {
            public string scene, utc, unity, cpu, gpu, graphicsApi, operatingSystem, outputDirectory, scope, error;
            public string validationMode;
            public bool completed, allChecksPassed, editor, batchMode, developmentBuild;
            public bool inputOnly, retreatOnly;
            public int totalTargets, distinctIds, width, height, cpuThreads, systemMemoryMB, graphicsMemoryCapacityMB;
            public long actualProcessVramBytes = -1;
            public string vramStatus = "Unmeasured: adapter capacity is not allocation. Gfx Used Memory, if sampled, is Unity tracked graphics allocation, not total process VRAM.";
            public string frameGeneration = "No frame generation implementation is used by this project; renderScale and dynamic resolution are reported per phase.";
            public Check[] checks; public Phase[] phases; public string[] screenshots; public FlightEvidence flight;
        }
        public bool IsRunning { get; private set; }
        public string CurrentPhase { get; private set; } = "Idle";
        public int PhaseFrame { get; private set; }
        public string LastReportPath { get; private set; }
        public Report LastReport { get; private set; }
        [Min(90)] public float maximumWallSeconds = 420;
        readonly List<Check> checks = new List<Check>();
        readonly List<Phase> phases = new List<Phase>();
        readonly List<string> screenshots = new List<string>();
        readonly StringBuilder csv = new StringBuilder(262144);
        readonly StringBuilder flightCsv = new StringBuilder(32768);
        readonly FrameTiming[] frameTiming = new FrameTiming[1];
        MissionController mission; Camera view; ReactorExplosionPool reactor; FleetOverviewCamera overview;
        RadioController radio; ShipTarget[] ships; Report report; Coroutine routine; IEnumerator guarded;
        Keyboard keyboard, previousKeyboard; Mouse mouse, previousMouse;
        InputSettings.BackgroundBehavior oldInputBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
#endif
        ProfilerRecorder main, gpu, gc, draws, graphicsMemory;
        bool finishing, quitAfter, saved, oldMission, oldInput, oldChase, oldOverview, oldBackground;
        bool observeRelease;
        bool inputOnly, retreatOnly;
        int releasedUpdateFrames;
        int oldVsync, oldCap; float oldFov; Vector3 oldCameraPosition; Quaternion oldCameraRotation;
        double beganAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return;
            string output = null; bool requested = false, quit = false, onlyRetreat = false;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-narrative-validation")
                { requested = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i]; }
                else if (args[i] == "-narrative-retreat-validation")
                { requested = true; onlyRetreat = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i]; }
                else if (args[i] == "-narrative-quit") quit = true;
            }
            if (!requested || FindAnyObjectByType<NarrativeCombatValidationRunner>() != null) return;
            var runner = new GameObject("__OptInNarrativeCombatValidation").AddComponent<NarrativeCombatValidationRunner>();
            if (onlyRetreat) runner.BeginRetreatOnly(output, quit); else runner.Begin(output, quit);
        }
        public void Begin(string outputDirectory, bool quitWhenComplete = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Rendered validation requires Play mode or a standalone player.");
            if (IsRunning) throw new InvalidOperationException("Narrative validation already running.");
            mission = FindAnyObjectByType<MissionController>();
            if (mission == null || mission.combat == null || mission.lasers == null || mission.narrative == null || mission.input == null)
                throw new InvalidOperationException("Open the fully connected NarrativeCombat Small or 2000 scene.");
            view = mission.chaseCamera.GetComponent<Camera>();
            string parent = string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Application.persistentDataPath, "NarrativeCombatValidation") : outputDirectory;
            string directory = Path.GetFullPath(Path.Combine(parent, SceneManager.GetActiveScene().name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(directory);
            report = new Report
            {
                scene = mission.gameObject.scene.path, utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                operatingSystem = SystemInfo.operatingSystem, outputDirectory = directory, editor = Application.isEditor,
                batchMode = Application.isBatchMode, developmentBuild = Debug.isDebugBuild, cpuThreads = SystemInfo.processorCount,
                validationMode = retreatOnly ? "retreat-only" : inputOnly ? "input-only" : "full-four-phase",
                inputOnly = this.inputOnly, retreatOnly = this.retreatOnly,
                systemMemoryMB = SystemInfo.systemMemorySize, graphicsMemoryCapacityMB = SystemInfo.graphicsMemorySize,
                scope = "Same saved scene and all stable IDs throughout. Natural 60-second Timeline, pause/skip/replay; ten simulation seconds of virtual Keyboard/Mouse through the existing Update and FixedUpdate; four rendered phases each >=480 frames and >=6 wall seconds. Concentrated fatal damage uses explicitly controlled ApplyDamage setup. AllFleetRetreatPanorama requests retreat on every saved identity, then measures actual autonomous acceleration/steering with the panorama rendered and player motion disabled. Neither controlled setup is human input. Screenshots outside measured samples. No subjective physical-keyboard, thermal soak, cross-hardware, or total-process VRAM claim. Unknown counters use -1 or empty CSV cells."
            };
            checks.Clear(); phases.Clear(); screenshots.Clear(); csv.Clear(); flightCsv.Clear();
            csv.AppendLine("phase,frame,frameMs,cpuFrameMs,mainThreadMs,gpuMs,drawCalls,gcBytes,unityGraphicsBytes,focused,damagedPending,exploded,escaped,beams,reactorEffects,breakingFormation,fleeing,fleetInstancedDrawCalls,fleetVisibleInstancedShips,fleetChunkCount,maximumShipSpeedMetersPerSecond");
            flightCsv.AppendLine("simulationTime,x,y,z,stepSeconds,stepDistance,hudUnitsPerSecond,measuredUnitsPerSecond,headForwardDot,damagedPending,exploded,shots,reflections,sprintKeyObserved,brakeKeyObserved,mouseDeltaXObserved,mouseDeltaYObserved,gameplayEnabled,focused,yaw,pitch");
            quitAfter = quitWhenComplete; IsRunning = true; finishing = false; beganAt = Time.realtimeSinceStartupAsDouble;
            guarded = Guard(Run()); routine = StartCoroutine(guarded);
        }
        public void BeginInputOnly(string outputDirectory, bool quitWhenComplete = false)
        {
            if (IsRunning) throw new InvalidOperationException("Narrative validation already running.");
            retreatOnly = false; inputOnly = true; Begin(outputDirectory, quitWhenComplete);
            report.scope = "Focused input-only regression rerun: existing Update/FixedUpdate, recorded device states, >=0.35-second neutral release gate, measured sprint/brake/yaw/pitch response. This run does not repeat or claim Timeline, stress or performance checks.";
        }
        public void BeginRetreatOnly(string outputDirectory, bool quitWhenComplete = false)
        {
            if (IsRunning) throw new InvalidOperationException("Narrative validation already running.");
            inputOnly = false; retreatOnly = true; Begin(outputDirectory, quitWhenComplete);
            report.scope = "Retreat-only rendering/performance experiment: preserves every saved ship identity, requests retreat on the whole fleet, then measures ordinary fleet FixedUpdate acceleration/steering, space partitioning, dynamic LOD and a 70-degree panorama for >=480 rendered frames and >=6 wall seconds. Player motion is disabled and no damage is submitted. This run does NOT execute or claim narrative, player-input, laser-reflection, delayed-explosion, or three-restart validation. Recorded renderer draw calls count only FleetRenderManager instanced submissions, not all Unity rendering. It is not a full-duration flight to the evacuation boundary.";
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            try
            {
                while (stack.Count > 0)
                {
                    bool moved = false; object value = null; Exception failure = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null) { RecordError(failure); break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (value is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return value;
                }
            }
            finally { while (stack.Count > 0) { try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception) { } } Finish(); }
        }
        void Update()
        {
            if (IsRunning && Time.realtimeSinceStartupAsDouble - beganAt > maximumWallSeconds)
                Cancel("Validation exceeded " + maximumWallSeconds + " real seconds; no successful completion is claimed.");
        }
        void LateUpdate()
        {
            // LateUpdate follows DropletInput.Update. Multiple neutral frames prove
            // the normal release gate had an opportunity to clear after ResetInput.
            if (!observeRelease || keyboard == null || !keyboard.added || mission == null || !mission.input.enabled) return;
            if (Keyboard.current == keyboard && !keyboard.wKey.isPressed && !keyboard.sKey.isPressed && !keyboard.aKey.isPressed &&
                !keyboard.dKey.isPressed && !keyboard.leftShiftKey.isPressed && !keyboard.spaceKey.isPressed) releasedUpdateFrames++;
            else releasedUpdateFrames = 0;
        }
        IEnumerator Run()
        {
            yield return null;
            oldMission = mission.enabled; oldInput = mission.input.enabled; oldChase = mission.chaseCamera.enabled;
            oldFov = view.fieldOfView; oldCameraPosition = view.transform.position; oldCameraRotation = view.transform.rotation;
            oldVsync = QualitySettings.vSyncCount; oldCap = Application.targetFrameRate; oldBackground = Application.runInBackground;
            oldInputBackground = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            overview = FindAnyObjectByType<FleetOverviewCamera>(); oldOverview = overview != null && overview.enabled;
            saved = true; if (overview != null) overview.enabled = false;
            // Synthetic input must reach the existing gameplay scripts even when
            // the automation host owns focus. This temporary diagnostic routing is
            // restored on completion/cancellation and does not change normal play.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true;
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            keyboard = InputSystem.AddDevice<Keyboard>("NarrativeValidationKeyboard"); mouse = InputSystem.AddDevice<Mouse>("NarrativeValidationMouse");
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            InputSystem.onAfterUpdate += SelectDevices;
            mission.enabled = true; mission.input.enabled = true;
            reactor = FindAnyObjectByType<ReactorExplosionPool>(); reactor?.InitializePool(); mission.lasers.beamPool?.InitializePool();
            radio = FindAnyObjectByType<RadioController>();
            yield return null;
            ships = mission.targets.Where(t => t != null).OrderBy(t => (t.transform.position - mission.spawnPosition).sqrMagnitude).ToArray();
            report.totalTargets = mission.TotalCount; report.distinctIds = ships.Select(s => s.targetId).Distinct(StringComparer.Ordinal).Count();
            report.width = Screen.width; report.height = Screen.height;
            AddCheck("Rendered graphics device", SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, report.graphicsApi);
            int expected = SceneManager.GetActiveScene().name.EndsWith("_Small", StringComparison.Ordinal) ? 12 : 2000;
            AddCheck("Saved fleet count and stable IDs", ships.Length == expected && mission.TotalCount == expected && report.distinctIds == expected, "expected=" + expected + ", actual=" + ships.Length + ", unique=" + report.distinctIds);
            if (!Application.isEditor) AddCheck("1080p standalone", Screen.width == 1920 && Screen.height == 1080, Screen.width + "x" + Screen.height);
            main = Recorder(ProfilerCategory.Internal, "Main Thread"); gpu = Recorder(ProfilerCategory.Render, "GPU Frame Time");
            gc = Recorder(ProfilerCategory.Memory, "GC Allocated In Frame"); draws = Recorder(ProfilerCategory.Render, "Draw Calls Count");
            graphicsMemory = Recorder(ProfilerCategory.Memory, "Gfx Used Memory");
            if (inputOnly) { yield return RealInputFlight(); report.completed = true; yield break; }
            if (retreatOnly)
            {
                yield return Measure("AllFleetRetreatPanorama");
                AddCheck("Retreat-only fleet count preserved at completion", mission.TotalCount == report.totalTargets && mission.targets.Length == report.totalTargets,
                    "final=" + mission.TotalCount + ", mode=retreat-only; other feature validation not run");
                report.completed = true; yield break;
            }
            yield return NarrativeChecks();
            yield return RealInputFlight();
            yield return Measure("FleetPanorama");
            yield return Measure("NearLaserCombat");
            yield return Measure("ConcentratedDelayedExplosions");
            yield return Measure("AllFleetRetreatPanorama");
            yield return CaptureTransientCombat();
            yield return RestartChecks();
            AddCheck("Fleet count preserved at completion", mission.TotalCount == report.totalTargets && mission.targets.Length == report.totalTargets, "final=" + mission.TotalCount);
            report.completed = true;
        }
        IEnumerator NarrativeChecks()
        {
            SetPhase("Natural narrative fragment, pause and keyboard Tab skip");
            mission.ReplayNarrative(); Neutral();
            float timer = mission.Remaining; int generation = mission.combat.Generation;
            yield return WaitUntil(() => mission.narrative.Elapsed >= 2.2f, 15, "narrative fragment did not advance");
            Capture("01-narrative-fragment");
            AddCheck("Narrative does not start combat", mission.Elapsed == 0 && mission.Remaining == timer && mission.DestroyedCount == 0 && mission.PendingCount == 0 && mission.EscapedCount == 0 && mission.lasers.ShotsFired == 0, "combatSeconds=" + mission.Elapsed + ", generation=" + generation);
            yield return Press(Key.Escape);
            AddCheck("Keyboard Escape pauses narrative", mission.State == MissionState.Paused, mission.State.ToString());
            float elapsed = mission.narrative.Elapsed; Vector3 position = mission.motor.transform.position;
            yield return RealDelay(.35f);
            AddCheck("Narrative pause freezes movement and time", mission.narrative.Elapsed == elapsed && mission.motor.transform.position == position, "narrativeSeconds=" + elapsed);
            yield return Press(Key.Escape);
            yield return WaitUntil(() => mission.State == MissionState.Narrative, 5, "narrative resume failed");
            yield return Press(Key.Tab);
            yield return WaitUntil(() => mission.State == MissionState.Playing, 5, "Tab did not hand over to combat");
            AddCheck("Skip enters combat once and restores controls", mission.narrative.CompletionCount == 1 && mission.input.GameplayEnabled && mission.chaseCamera.enabled && Cursor.lockState == CursorLockMode.Locked && mission.PendingCount == 0, "completions=" + mission.narrative.CompletionCount + ", state=" + mission.State);
            Capture("02-skip-combat-handover");

            SetPhase("Natural full 60-second Timeline replay"); mission.ReplayNarrative(); Neutral();
            double started = Time.realtimeSinceStartupAsDouble; bool middle = false, warning = false;
            while (mission.narrative.IsActive)
            {
                if (Time.realtimeSinceStartupAsDouble - started > 110) throw new TimeoutException("Natural 60-second Timeline did not complete.");
                if (!middle && mission.narrative.Elapsed >= 24) { middle = true; Capture("03-narrative-middle"); }
                if (!warning && mission.narrative.Elapsed >= 53) { warning = true; Capture("04-narrative-last-warning"); }
                PhaseFrame++; if (PhaseFrame % 180 == 0) WriteProgress();
                yield return null;
            }
            AddCheck("Natural Timeline completes once with all cues", mission.State == MissionState.Playing && mission.narrative.CompletionCount == 1 && mission.narrative.CueCount >= 12 && mission.narrative.Elapsed >= 59.99f,
                "wallSeconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("F3", CultureInfo.InvariantCulture) + ", timelineSeconds=" + mission.narrative.Elapsed + ", cues=" + mission.narrative.CueCount);
            AddCheck("Normal ending restores player and camera", mission.input.GameplayEnabled && mission.chaseCamera.enabled && !mission.narrative.IsActive && mission.PendingCount == 0, "state=" + mission.State);
        }
        IEnumerator RealInputFlight()
        {
            SetPhase("Existing input Update and mission FixedUpdate: ten simulation seconds");
            mission.RestartIntoCombat(); mission.enabled = true; mission.motor.SimulationEnabled = true; mission.chaseCamera.enabled = true;
            releasedUpdateFrames = 0; observeRelease = true; Neutral();
            yield return RealDelay(.35f);
            yield return WaitUntil(() => releasedUpdateFrames >= 4, 4, "Synthetic keyboard did not expose four released Update frames after ResetInput.");
            observeRelease = false;
            var flight = new FlightEvidence
            {
                inputMethod = "InputSystem.QueueStateEvent on temporary Keyboard/Mouse, consumed by existing DropletInput.Update and MissionController.FixedUpdate. No calls to Motor.Simulate or Mission.Step in this phase.",
                setup = "Restart at saved spawn, then >=0.35 real seconds and four neutral LateUpdate observations to clear the existing release gate. W cruise; Shift boost at 4-5 s; Space brake at 8-9 s; one 100/-30 pixel mouse event after 9 s. Fleet remains autonomous. Sprint, brake and turn each require measured motor response.",
                start = mission.motor.transform.position, missionFixedUpdateRemainedEnabled = true, releasedUpdateFrames = releasedUpdateFrames,
                inputBackgroundBehavior = InputSystem.settings.backgroundBehavior.ToString(), editorInputBehavior = "Not an Editor player"
            };
#if UNITY_EDITOR
            flight.editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode.ToString();
#endif
            report.flight = flight;
            float startTime = mission.combat.SimulatedTime, previousTime = startTime;
            Vector3 previousPosition = flight.start; float previousHud = mission.motor.MeasuredSpeed;
            bool mouseQueued = false; float yawBeforeMouse = 0, pitchBeforeMouse = 0;
            double wall = Time.realtimeSinceStartupAsDouble;
            while (mission.combat.SimulatedTime - startTime < 10)
            {
                if (Time.realtimeSinceStartupAsDouble - wall > 35) throw new TimeoutException("Actual-input flight stopped advancing; focus/pause or frame throughput needs investigation.");
                float time = mission.combat.SimulatedTime - startTime;
                var keyState = time >= 8 && time < 9 ? new KeyboardState(Key.Space) : time >= 4 && time < 5 ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.W);
                InputSystem.QueueStateEvent(keyboard, keyState);
                Vector2 mouseDelta = Vector2.zero;
                if (time >= 9 && !mouseQueued)
                { mouseQueued = true; yawBeforeMouse = mission.motor.transform.eulerAngles.y; pitchBeforeMouse = mission.motor.transform.eulerAngles.x; mouseDelta = new Vector2(100, -30); }
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = mouseDelta });
                yield return null;
                if (Application.isFocused) flight.focusedFrames++;
                if (keyboard.leftShiftKey.isPressed) flight.observedSprintFrames++;
                if (keyboard.spaceKey.isPressed) flight.observedBrakeFrames++;
                Vector2 observedMouse = mouse.delta.ReadValue();
                if (observedMouse.sqrMagnitude > .0001f) flight.observedMouseFrames++;
                float now = mission.combat.SimulatedTime, dt = now - previousTime;
                if (dt <= .000001f) continue;
                Vector3 point = mission.motor.transform.position, delta = point - previousPosition;
                float distance = delta.magnitude, measured = distance / dt, hud = mission.motor.MeasuredSpeed;
                float headDot = delta.sqrMagnitude > .000001f ? Vector3.Dot(mission.lasers.dropletSurface.SurfaceMatrix.MultiplyVector(Vector3.forward).normalized, delta.normalized) : 1;
                flight.measuredDistanceUnits += distance; flight.minimumHeadForwardDot = Mathf.Min(flight.minimumHeadForwardDot, headDot); flight.fixedPoseSamples++;
                if (now - startTime < 3.5f && measured > .01f && Mathf.Abs(hud - previousHud) < .001f)
                { flight.cruiseMaximumRelativeHudError = Mathf.Max(flight.cruiseMaximumRelativeHudError, Mathf.Abs(measured - hud) / measured); flight.cruiseMetersPerSecond = mission.settings.ToMeters(measured); }
                flight.missionFixedUpdateRemainedEnabled &= mission.enabled;
                float relative = now - startTime;
                if (relative >= 4 && relative <= 5.25f) flight.sprintPeakMetersPerSecond = Mathf.Max(flight.sprintPeakMetersPerSecond, mission.settings.ToMeters(hud));
                if (relative >= 8.25f && relative <= 9) flight.brakeMinimumMetersPerSecond = Mathf.Min(flight.brakeMinimumMetersPerSecond, mission.settings.ToMeters(hud));
                flightCsv.AppendFormat(CultureInfo.InvariantCulture, "{0:F5},{1:F5},{2:F5},{3:F5},{4:F5},{5:F5},{6:F5},{7:F5},{8:F7},{9},{10},{11},{12},{13},{14},{15:F4},{16:F4},{17},{18},{19:F4},{20:F4}\n",
                    now, point.x, point.y, point.z, dt, distance, hud, measured, headDot, mission.PendingCount, mission.DestroyedCount, mission.lasers.ShotsFired, mission.lasers.ReflectionCount,
                    keyboard.leftShiftKey.isPressed ? 1 : 0, keyboard.spaceKey.isPressed ? 1 : 0, observedMouse.x, observedMouse.y, mission.input.GameplayEnabled ? 1 : 0, Application.isFocused ? 1 : 0,
                    mission.motor.transform.eulerAngles.y, mission.motor.transform.eulerAngles.x);
                previousPosition = point; previousTime = now; previousHud = hud; PhaseFrame++;
            }
            Neutral();
            flight.end = mission.motor.transform.position; flight.simulationSeconds = mission.combat.SimulatedTime - startTime;
            flight.measuredMetersPerSecond = mission.settings.ToMeters(flight.measuredDistanceUnits / flight.simulationSeconds);
            flight.newlyDamaged = mission.PendingCount + mission.DestroyedCount; flight.exploded = mission.DestroyedCount;
            flight.recoveries = mission.RecoveryCount; flight.shots = mission.lasers.ShotsFired; flight.reflections = mission.lasers.ReflectionCount;
            flight.mouseYawDegrees = mouseQueued ? Mathf.Abs(Mathf.DeltaAngle(yawBeforeMouse, mission.motor.transform.eulerAngles.y)) : 0;
            flight.mousePitchDegrees = mouseQueued ? Mathf.Abs(Mathf.DeltaAngle(pitchBeforeMouse, mission.motor.transform.eulerAngles.x)) : 0;
            AddCheck("Ten seconds actual FixedUpdate input flight", flight.simulationSeconds >= 10 && flight.fixedPoseSamples > 50 && flight.missionFixedUpdateRemainedEnabled && flight.measuredDistanceUnits > 1000, "seconds=" + flight.simulationSeconds + ", distanceUU=" + flight.measuredDistanceUnits + ", damage=" + flight.newlyDamaged);
            AddCheck("Cruise HUD agrees with displacement and physical scale", flight.cruiseMaximumRelativeHudError < .015f && Mathf.Abs(flight.cruiseMetersPerSecond - 30000) < 450, "cruise m/s=" + flight.cruiseMetersPerSecond + ", maxRelativeError=" + flight.cruiseMaximumRelativeHudError);
            AddCheck("Preserved round head stays forward", flight.minimumHeadForwardDot > .99f, "minimumDot=" + flight.minimumHeadForwardDot);
            AddCheck("Actual flight damages saved moving fleet", flight.newlyDamaged > 0 && flight.recoveries == 0, "damaged=" + flight.newlyDamaged + ", recoveries=" + flight.recoveries);
            AddCheck("Injected Shift produces actual acceleration", flight.observedSprintFrames > 0 && flight.sprintPeakMetersPerSecond > 60000,
                "observedKeyFrames=" + flight.observedSprintFrames + ", peakMetersPerSecond=" + flight.sprintPeakMetersPerSecond);
            AddCheck("Injected Space produces actual braking", flight.observedBrakeFrames > 0 && flight.brakeMinimumMetersPerSecond < 500,
                "observedKeyFrames=" + flight.observedBrakeFrames + ", minimumMetersPerSecond=" + flight.brakeMinimumMetersPerSecond);
            AddCheck("Injected mouse changes actual yaw and pitch", flight.mouseYawDegrees > 3 && flight.mousePitchDegrees > 1,
                "yawDegrees=" + flight.mouseYawDegrees + ", pitchDegrees=" + flight.mousePitchDegrees + ", observedMouseFrames=" + flight.observedMouseFrames);
            Capture("05-actual-input-flight");
        }
        IEnumerator Measure(string name)
        {
            bool retreatPanorama = name == "AllFleetRetreatPanorama";
            float phasePreviousFov = view.fieldOfView;
            var fleetRenderer = mission.combat.fleetRenderer;
            Vector3[] retreatPositions = null;
            Quaternion[] retreatRotations = null;
            float retreatSimulationStart = 0;
            SetPhase(name + " setup"); Neutral(); mission.RestartIntoCombat(); mission.motor.SimulationEnabled = false;
            mission.chaseCamera.enabled = false; mission.enabled = name == "NearLaserCombat";
            var bounds = new Bounds(ships[0].transform.position, Vector3.zero); foreach (var ship in ships) bounds.Encapsulate(ship.transform.position);
            if (name == "FleetPanorama" || retreatPanorama)
            {
                if (retreatPanorama) view.fieldOfView = 70;
                Vector3 eye = bounds.center + new Vector3(-Mathf.Max(1400, bounds.extents.x * .45f), Mathf.Max(1200, bounds.extents.magnitude * .5f), -Mathf.Max(2200, bounds.extents.z * 1.65f));
                Aim(eye, bounds.center);
            }
            else
            {
                var muzzle = ships[0].transform.Find("LaserMuzzle"); Vector3 origin = muzzle != null ? muzzle.position : ships[0].transform.position;
                Teleport(origin + Vector3.forward * 120 + Vector3.up * 2, Quaternion.LookRotation(Vector3.back));
                mission.motor.SimulationEnabled = false;
                Aim(mission.motor.transform.position + new Vector3(4, 3, 8), ships[0].transform.position);
            }
            for (int i = 0; i < 30; i++) yield return null;
            var phase = new Phase
            {
                name = name, targets = mission.TotalCount, width = Screen.width, height = Screen.height,
                workload = name == "FleetPanorama" ? "Fixed panorama of the entire unchanged saved fleet; simulation intentionally held to isolate rendering."
                    : name == "NearLaserCombat" ? "Normal mission FixedUpdate advances autonomous fleet and bounded lasers; diagnostic player pose is held near the nearest ship. No direct attack calls."
                    : retreatPanorama ? "Controlled retreat request for EVERY saved ship, preserving all IDs and ordinary fleet FixedUpdate, acceleration, turning, avoidance, spatial indexing and dynamic LOD. Camera remains at a 70-degree panorama. Player motor is disabled; retreating ships cannot fire, and no damage is submitted. Includes per-frame managed state/speed instrumentation for every ship; position and identity audits and screenshot are outside timing. Not a full-duration escape-boundary soak."
                    : "Controlled stress: one ApplyDamage per ALL saved ships in one rendered frame; standard 2-5 simulation-second deadlines, pools, radio and actual mission loop complete all explosions. This is not human input.",
                cameraPosition = view.transform.position, cameraForward = view.transform.forward, dynamicResolution = view.allowDynamicResolution,
                renderScale = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.renderScale ?? 1
            };
            if (name == "ConcentratedDelayedExplosions")
            {
                // Controlled placement is explicit and never represented as a user attack.
                mission.enabled = false;
                int submitted = 0; double submissionStart = Time.realtimeSinceStartupAsDouble;
                foreach (var ship in ships) if (mission.combat.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0), DamageSource.Penetration, 800000L + submitted)) submitted++;
                phase.controlledDamageSubmissionMs = (Time.realtimeSinceStartupAsDouble - submissionStart) * 1000;
                AddCheck("Controlled all-fleet fatal setup retains hulls", submitted == report.totalTargets && mission.PendingCount == report.totalTargets && mission.DestroyedCount == 0, "submitted=" + submitted + ", pending=" + mission.PendingCount);
                float deadline = ships[0].ExplosionAt;
                bool repeated = mission.combat.ApplyDamage(ships[0], new ShipHitContext(ships[0].transform.position, Vector3.forward, 0), DamageSource.Penetration, 800000L);
                AddCheck("Repeated fatal damage never reschedules", !repeated && ships[0].ExplosionAt == deadline, "deadline=" + deadline);
                mission.TogglePause(); float frozen = mission.combat.SimulatedTime; yield return RealDelay(.3f);
                AddCheck("Paused delayed explosions remain pending", mission.combat.SimulatedTime == frozen && mission.PendingCount == report.totalTargets && mission.DestroyedCount == 0, "time=" + frozen);
                mission.TogglePause(); mission.motor.SimulationEnabled = false; mission.enabled = true;
                Capture("07-all-fleet-fatal-pending");
            }
            else if (retreatPanorama)
            {
                retreatPositions = new Vector3[ships.Length]; retreatRotations = new Quaternion[ships.Length];
                for (int i = 0; i < ships.Length; i++)
                {
                    retreatPositions[i] = ships[i].transform.position; retreatRotations[i] = ships[i].transform.rotation;
                    mission.combat.RequestRetreat(ships[i], 0); phase.retreatRequested++;
                }
                retreatSimulationStart = mission.combat.SimulatedTime;
                // All requests are delivered synchronously before the next FixedUpdate.
                // That update changes every intact ship to BreakingFormation before laser
                // scheduling, so normal CanAttack state rules suppress weapon damage.
                // The disabled player motor cannot submit penetration sweeps.
                mission.motor.SimulationEnabled = false; mission.enabled = true;
                phase.playerMotionDisabled = true;
                AddCheck("All-fleet retreat requests preserve every saved identity", phase.retreatRequested == report.totalTargets && mission.IntactCount == report.totalTargets,
                    "requested=" + phase.retreatRequested + ", intact=" + mission.IntactCount + ", controlledSetup=true");
            }
            SetPhase(name + " measuring >=480 frames and >=6 seconds");
            const int capacity = 16384;
            var samples = new double[capacity, 7]; var workload = new int[capacity, 11];
            var retreatSpeedSamples = retreatPanorama ? new float[capacity] : null;
            yield return null;
            int count = 0; double started = Time.realtimeSinceStartupAsDouble;
            if (fleetRenderer != null) fleetRenderer.MeasureCpuCost = true;
            mission.combat.MeasureCpuCost = true;
            double refreshCost = fleetRenderer != null ? fleetRenderer.RefreshCpuMilliseconds : 0;
            double renderCost = fleetRenderer != null ? fleetRenderer.RenderCpuMilliseconds : 0;
            double beginCost = mission.combat.BeginStepCpuMilliseconds, endCost = mission.combat.EndStepCpuMilliseconds;
            while (count < 480 || Time.realtimeSinceStartupAsDouble - started < 6)
            {
                if (Time.realtimeSinceStartupAsDouble - started > 120) throw new TimeoutException(name + " did not reach 480 rendered frames within 120 seconds.");
                if (count >= capacity) throw new InvalidOperationException("Validation frame capacity exceeded before six seconds.");
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                uint timings = FrameTimingManager.GetLatestTimings(1, frameTiming);
                double gpuTime = timings > 0 && frameTiming[0].gpuFrameTime > 0 ? frameTiming[0].gpuFrameTime : Sample(gpu, 1000000);
                samples[count, 0] = Time.unscaledDeltaTime * 1000.0;
                samples[count, 1] = timings > 0 && frameTiming[0].cpuFrameTime > 0 ? frameTiming[0].cpuFrameTime : -1;
                samples[count, 2] = Sample(main, 1000000); samples[count, 3] = gpuTime; samples[count, 4] = Sample(draws);
                samples[count, 5] = Sample(gc); samples[count, 6] = Sample(graphicsMemory);
                PhaseFrame = count + 1; if (Application.isFocused) phase.focusedFrames++;
                int activeBeams = mission.lasers.beamPool != null ? mission.lasers.beamPool.ActiveCount : 0;
                int effects = reactor != null ? reactor.ActiveEffectCount : 0;
                phase.peakBeams = Mathf.Max(phase.peakBeams, activeBeams); phase.peakReactorEffects = Mathf.Max(phase.peakReactorEffects, effects);
                workload[count, 0] = Application.isFocused ? 1 : 0; workload[count, 1] = mission.PendingCount;
                workload[count, 2] = mission.DestroyedCount; workload[count, 3] = mission.EscapedCount;
                workload[count, 4] = activeBeams; workload[count, 5] = effects;
                // Explicit renderer-owned submission counts are not total Unity draw
                // calls. Record them even when the global Profiler counter is unavailable.
                workload[count, 8] = fleetRenderer != null ? fleetRenderer.LastDrawCalls : -1;
                workload[count, 9] = fleetRenderer != null ? fleetRenderer.VisibleInstancedShips : -1;
                workload[count, 10] = fleetRenderer != null ? fleetRenderer.ChunkCount : -1;
                phase.fleetInstancedDrawCalls = workload[count, 8];
                phase.peakFleetInstancedDrawCalls = Mathf.Max(phase.peakFleetInstancedDrawCalls, workload[count, 8]);
                phase.fleetVisibleInstancedShips = workload[count, 9]; phase.fleetChunkCount = workload[count, 10];
                if (workload[count, 9] >= 0)
                    phase.minimumFleetVisibleInstancedShips = phase.minimumFleetVisibleInstancedShips < 0 ? workload[count, 9] : Mathf.Min(phase.minimumFleetVisibleInstancedShips, workload[count, 9]);
                if (retreatPanorama)
                {
                    int breaking = 0, fleeing = 0; float maximumSpeedSquared = 0;
                    // Managed data only inside the timing window; avoid native Transform
                    // reads or LINQ allocations for the per-frame fleet audit.
                    foreach (var ship in ships)
                    {
                        if (ship.BehaviorState == ShipBehaviorState.BreakingFormation) breaking++;
                        else if (ship.BehaviorState == ShipBehaviorState.Fleeing) fleeing++;
                        maximumSpeedSquared = Mathf.Max(maximumSpeedSquared, ship.Velocity.sqrMagnitude);
                    }
                    phase.breakingFormation = workload[count, 6] = breaking;
                    phase.fleeing = workload[count, 7] = fleeing;
                    phase.peakBreakingFormation = Mathf.Max(phase.peakBreakingFormation, breaking);
                    phase.peakFleeing = Mathf.Max(phase.peakFleeing, fleeing);
                    float physicalSpeed = mission.combat.scale.UnitsToMeters(Mathf.Sqrt(maximumSpeedSquared));
                    retreatSpeedSamples[count] = physicalSpeed;
                    phase.maximumShipSpeedMetersPerSecond = Mathf.Max(phase.maximumShipSpeedMetersPerSecond, physicalSpeed);
                    phase.playerMotionDisabled &= !mission.motor.SimulationEnabled;
                }
                count++;
            }
            phase.seconds = Time.realtimeSinceStartupAsDouble - started; phase.frames = count; phase.meanFps = phase.frames / phase.seconds;
            phase.rendererRefreshCpuMsPerFrame = fleetRenderer != null ? (fleetRenderer.RefreshCpuMilliseconds - refreshCost) / count : -1;
            phase.rendererSubmissionCpuMsPerFrame = fleetRenderer != null ? (fleetRenderer.RenderCpuMilliseconds - renderCost) / count : -1;
            phase.fleetBeginCpuMsPerFrame = (mission.combat.BeginStepCpuMilliseconds - beginCost) / count;
            phase.fleetEndCpuMsPerFrame = (mission.combat.EndStepCpuMilliseconds - endCost) / count;
            if (fleetRenderer != null) fleetRenderer.MeasureCpuCost = false;
            mission.combat.MeasureCpuCost = false;
            phase.frameMs = Summary(samples, count, 0); phase.cpuFrameMs = Summary(samples, count, 1); phase.mainThreadMs = Summary(samples, count, 2); phase.gpuMs = Summary(samples, count, 3);
            phase.drawCalls = Summary(samples, count, 4); phase.gcBytes = Summary(samples, count, 5); phase.unityGraphicsBytes = Summary(samples, count, 6);
            // Formatting and file I/O are deliberately outside the measured window.
            for (int frame = 0; frame < count; frame++)
            {
                csv.Append(name).Append(',').Append(frame + 1);
                for (int column = 0; column < 7; column++) { csv.Append(','); double value = samples[frame, column]; if (value >= 0) csv.Append(value.ToString("F5", CultureInfo.InvariantCulture)); }
                for (int column = 0; column < 11; column++) csv.Append(',').Append(workload[frame, column]);
                csv.Append(','); if (retreatPanorama) csv.Append(retreatSpeedSamples[frame].ToString("F5", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }
            phase.destroyed = mission.DestroyedCount; phase.pending = mission.PendingCount; phase.escaped = mission.EscapedCount;
            phase.fleetFullPrefabShips = fleetRenderer != null ? fleetRenderer.FullPrefabCount : -1;
            phase.fleetChunkSizeUnits = fleetRenderer != null ? fleetRenderer.chunkSize : -1;
            phase.shots = mission.lasers.ShotsFired; phase.reflections = mission.lasers.ReflectionCount; phase.damageFromReflection = mission.lasers.ReflectedDamageCount;
            phases.Add(phase);
            AddCheck(name + " full measurement window", phase.frames >= 480 && phase.seconds >= 6 && phase.targets == report.totalTargets, "frames=" + phase.frames + ", seconds=" + phase.seconds.ToString("F3", CultureInfo.InvariantCulture));
            AddCheck(name + " native resolution scale", Mathf.Abs(phase.renderScale - 1) < .0001f && !phase.dynamicResolution, "renderScale=" + phase.renderScale + ", dynamic=" + phase.dynamicResolution);
            if (name == "NearLaserCombat")
            {
                AddCheck("Autonomous lasers actually fire and reflect", phase.shots > 0 && phase.reflections > 0, "shots=" + phase.shots + ", reflected=" + phase.reflections + ", reflectedDamage=" + phase.damageFromReflection);
                Capture("06-near-laser-combat");
            }
            else if (name == "ConcentratedDelayedExplosions")
            {
                yield return WaitUntil(() => mission.PendingCount == 0, 8, "all-fleet explosion deadlines did not complete");
                AddCheck("All fleet delayed explosions submit each kill once", mission.DestroyedCount == report.totalTargets && mission.PendingCount == 0 && mission.EscapedCount == 0 && mission.State == MissionState.Results && mission.ResultTransitions == 1,
                    "destroyed=" + mission.DestroyedCount + ", pending=" + mission.PendingCount + ", results=" + mission.ResultTransitions);
                AddCheck("Controlled stress uses bounded presentation", (reactor == null || phase.peakReactorEffects <= reactor.Capacity) && (mission.lasers.beamPool == null || phase.peakBeams <= mission.lasers.beamPool.Capacity), "reactorPeak=" + phase.peakReactorEffects + ", beamPeak=" + phase.peakBeams);
                Capture("08-all-fleet-explosion-results");
            }
            else if (retreatPanorama)
            {
                phase.retreatSimulationSeconds = mission.combat.SimulatedTime - retreatSimulationStart;
                phase.minimumDisplacementMeters = float.PositiveInfinity;
                float displacementSum = 0;
                for (int i = 0; i < ships.Length; i++)
                {
                    float displacement = mission.combat.scale.UnitsToMeters(Vector3.Distance(retreatPositions[i], ships[i].transform.position));
                    if (displacement > .01f) phase.movedShips++;
                    phase.minimumDisplacementMeters = Mathf.Min(phase.minimumDisplacementMeters, displacement);
                    phase.maximumDisplacementMeters = Mathf.Max(phase.maximumDisplacementMeters, displacement); displacementSum += displacement;
                    phase.maximumTurnDegrees = Mathf.Max(phase.maximumTurnDegrees, Quaternion.Angle(retreatRotations[i], ships[i].transform.rotation));
                }
                phase.meanDisplacementMeters = displacementSum / Mathf.Max(1, ships.Length);
                phase.unchangedRetreatIdentities = mission.TotalCount == report.totalTargets && mission.targets.Length == report.totalTargets &&
                    ships.Select(s => s.targetId).Distinct(StringComparer.Ordinal).Count() == report.distinctIds;
                phase.noRetreatDamage = mission.PendingCount == 0 && mission.DestroyedCount == 0 && mission.lasers.ShotsFired == 0 &&
                    mission.EscapedCount == 0 && mission.IntactCount == report.totalTargets;
                AddCheck("All-fleet retreat advances real autonomous movement and steering", phase.movedShips == report.totalTargets &&
                    phase.fleeing == report.totalTargets && phase.maximumTurnDegrees > 1 && phase.retreatSimulationSeconds >= 5.9f,
                    "breaking=" + phase.breakingFormation + ", fleeing=" + phase.fleeing + ", moved=" + phase.movedShips +
                    ", simulationSeconds=" + phase.retreatSimulationSeconds.ToString("F3", CultureInfo.InvariantCulture) +
                    ", displacementMeters(min/mean/max)=" + phase.minimumDisplacementMeters.ToString("F2", CultureInfo.InvariantCulture) + "/" +
                    phase.meanDisplacementMeters.ToString("F2", CultureInfo.InvariantCulture) + "/" + phase.maximumDisplacementMeters.ToString("F2", CultureInfo.InvariantCulture) +
                    ", maxTurnDegrees=" + phase.maximumTurnDegrees.ToString("F2", CultureInfo.InvariantCulture));
                AddCheck("All-fleet retreat uses configured speed caps without player damage", phase.maximumShipSpeedMetersPerSecond > 0 &&
                    phase.maximumShipSpeedMetersPerSecond <= mission.combat.scale.fleeMaxMetersPerSecond + 1 && phase.playerMotionDisabled && phase.noRetreatDamage && phase.unchangedRetreatIdentities,
                    "peakShipMetersPerSecond=" + phase.maximumShipSpeedMetersPerSecond.ToString("F2", CultureInfo.InvariantCulture) +
                    ", cap=" + mission.combat.scale.fleeMaxMetersPerSecond + ", playerDisabled=" + phase.playerMotionDisabled +
                    ", noDamage=" + phase.noRetreatDamage + ", identitiesPreserved=" + phase.unchangedRetreatIdentities);
                Capture("12-all-fleet-retreat-panorama");
                view.fieldOfView = phasePreviousFov;
            }
            else Capture("00-complete-fleet-panorama");
        }
        IEnumerator CaptureTransientCombat()
        {
            SetPhase("Transient beam and delayed explosion camera evidence (outside timing)");
            mission.RestartIntoCombat(); mission.enabled = true; mission.chaseCamera.enabled = false;
            var targetShip = ships[0]; var muzzle = targetShip.transform.Find("LaserMuzzle");
            Vector3 point = muzzle != null ? muzzle.position : targetShip.transform.position;
            Teleport(point + Vector3.forward * 120 + Vector3.up * 2, Quaternion.LookRotation(Vector3.back));
            mission.motor.SimulationEnabled = false;
            Aim(mission.motor.transform.position + new Vector3(2, 1.5f, 6), targetShip.transform.position);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (mission.lasers.beamPool.ActiveCount == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            bool visible = mission.lasers.beamPool.ActiveCount > 0;
            AddCheck("Live reflected beam presentation can be captured", visible, "activeBeams=" + mission.lasers.beamPool.ActiveCount);
            Capture("10-live-beam-contact");
            if (targetShip.DamageState == ShipDamageState.Intact)
                mission.combat.ApplyDamage(targetShip, new ShipHitContext(targetShip.transform.position, Vector3.forward, 0), DamageSource.Penetration, 870000);
            Aim(targetShip.transform.position + new Vector3(40, 18, -65), targetShip.transform.position);
            yield return WaitUntil(() => targetShip.IsDestroyed, 10, "transient inspection explosion did not complete");
            yield return RealDelay(.18);
            Capture("11-live-delayed-reactor-explosion");
        }
        IEnumerator RestartChecks()
        {
            SetPhase("Three consecutive restarts and stale-deadline observation");
            for (int i = 0; i < 3; i++)
            {
                mission.RestartIntoCombat(); int generation = mission.combat.Generation;
                mission.combat.ApplyDamage(ships[0], new ShipHitContext(ships[0].transform.position, Vector3.forward, 0), DamageSource.Penetration, 990000 + i);
                mission.RestartIntoCombat(); mission.motor.SimulationEnabled = false;
                AddCheck("Restart " + (i + 1) + " clears previous generation", mission.combat.Generation > generation && mission.PendingCount == 0 && mission.DestroyedCount == 0 && mission.EscapedCount == 0 && mission.IntactCount == report.totalTargets && mission.ResultTransitions == 0,
                    "generation=" + mission.combat.Generation + ", targets=" + mission.IntactCount);
                yield return null;
            }
            Teleport(mission.arenaCenter + Vector3.up * 6000, Quaternion.identity); mission.motor.SimulationEnabled = false;
            float start = mission.combat.SimulatedTime;
            yield return WaitUntil(() => mission.combat.SimulatedTime >= start + 5.5f, 20, "post-restart observation did not advance");
            AddCheck("No old explosion after three restarts", mission.PendingCount == 0 && mission.DestroyedCount == 0 && mission.EscapedCount == 0 && mission.IntactCount == report.totalTargets, "observedSimulationSeconds=" + (mission.combat.SimulatedTime - start));
            Capture("09-third-restart-clean");
        }
        void Teleport(Vector3 position, Quaternion rotation)
        {
            int damage = mission.DestroyedCount + mission.PendingCount;
            mission.motor.ResetPose(position, rotation); mission.combat.ResetSweepHistory();
            AddCheck("Diagnostic teleport is not an attack", damage == mission.DestroyedCount + mission.PendingCount && mission.motor.MeasuredSpeed == 0, "position=" + position);
        }
        void Aim(Vector3 eye, Vector3 target) => view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
        IEnumerator Press(Key key)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null; Neutral(); yield return null; }
        void Neutral()
        { if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState()); if (mouse != null) InputSystem.QueueStateEvent(mouse, new MouseState()); }
        void SelectDevices() { if (keyboard != null && keyboard.added) keyboard.MakeCurrent(); if (mouse != null && mouse.added) mouse.MakeCurrent(); }
        IEnumerator RealDelay(double seconds)
        { double until = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < until) yield return null; }
        IEnumerator WaitUntil(Func<bool> condition, double seconds, string message)
        { double until = Time.realtimeSinceStartupAsDouble + seconds; while (!condition()) { if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException(message); yield return null; } }
        void Capture(string name)
        {
            // A real URP camera request, not a mock screenshot. Overlay-only UI can
            // be absent from this camera artifact; full UI behavior is logged separately.
            int width = Screen.width, height = Screen.height;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                string path = Path.Combine(report.outputDirectory, name + ".png"); File.WriteAllBytes(path, texture.EncodeToPNG()); screenshots.Add(path);
            }
            finally { RenderTexture.active = previous; Destroy(texture); RenderTexture.ReleaseTemporary(target); }
        }
        static ProfilerRecorder Recorder(ProfilerCategory category, string name)
        { try { return ProfilerRecorder.StartNew(category, name, 1); } catch (Exception) { return default; } }
        static double Sample(ProfilerRecorder recorder, double divisor = 1) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue / divisor : -1;
        static Distribution Summary(double[,] values, int count, int column)
        {
            var filtered = new List<double>(count);
            for (int i = 0; i < count; i++) { double value = values[i, column]; if (value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value)) filtered.Add(value); }
            double[] data = filtered.ToArray(); Array.Sort(data);
            if (data.Length == 0) return new Distribution();
            return new Distribution { samples = data.Length, mean = data.Average(), p50 = data[Mathf.CeilToInt(data.Length * .5f) - 1],
                p95 = data[Mathf.CeilToInt(data.Length * .95f) - 1], p99 = data[Mathf.CeilToInt(data.Length * .99f) - 1], maximum = data[data.Length - 1] };
        }
        void AddCheck(string name, bool passed, string detail) => checks.Add(new Check { name = name, passed = passed, detail = detail });
        void SetPhase(string phase) { CurrentPhase = phase; PhaseFrame = 0; WriteProgress(); Debug.Log("NARRATIVE VALIDATION: " + phase); }
        void WriteProgress()
        {
            if (report == null) return;
            try { File.WriteAllText(Path.Combine(report.outputDirectory, "progress.json"), JsonUtility.ToJson(new Progress { phase = CurrentPhase, frames = PhaseFrame, running = IsRunning, completedPhases = phases.Count, focused = Application.isFocused, wallSeconds = Time.realtimeSinceStartupAsDouble - beganAt }, true), Encoding.UTF8); }
            catch (Exception exception) { Debug.LogWarning("Could not write validation progress: " + exception.Message); }
        }
        [Serializable] sealed class Progress { public string phase; public int frames, completedPhases; public bool running, focused; public double wallSeconds; }
        void RecordError(Exception exception) { if (report != null) report.error = (report.error ?? "") + exception + "\n"; }
        void Safe(Action action) { try { action(); } catch (Exception exception) { RecordError(exception); } }
        void Finish()
        {
            if (!IsRunning || finishing) return; finishing = true;
            if (mission != null && mission.combat != null)
            {
                mission.combat.MeasureCpuCost = false;
                if (mission.combat.fleetRenderer != null) mission.combat.fleetRenderer.MeasureCpuCost = false;
            }
            Safe(() => main.Dispose()); Safe(() => gpu.Dispose()); Safe(() => gc.Dispose()); Safe(() => draws.Dispose()); Safe(() => graphicsMemory.Dispose());
            Neutral();
            InputSystem.onAfterUpdate -= SelectDevices;
            if (keyboard != null) Safe(() => InputSystem.RemoveDevice(keyboard)); if (mouse != null) Safe(() => InputSystem.RemoveDevice(mouse));
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            if (saved)
            {
                Safe(() => InputSystem.settings.backgroundBehavior = oldInputBackground);
#if UNITY_EDITOR
                Safe(() => InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput);
#endif
                Safe(() => mission.Restart());
                Safe(() => { mission.enabled = oldMission; mission.input.enabled = oldInput; mission.chaseCamera.enabled = oldChase; mission.motor.SimulationEnabled = false; if (overview != null) overview.enabled = oldOverview; });
                Safe(() => { view.fieldOfView = oldFov; view.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); });
                QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldCap; Application.runInBackground = oldBackground;
            }
            if (report != null)
            {
                report.checks = checks.ToArray(); report.phases = phases.ToArray(); report.screenshots = screenshots.ToArray();
                report.allChecksPassed = report.completed && string.IsNullOrEmpty(report.error) && checks.All(c => c.passed);
                LastReport = report; LastReportPath = Path.Combine(report.outputDirectory, "narrative-combat-validation.json");
                try
                {
                    File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(report.outputDirectory, "frames.csv"), csv.ToString(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(report.outputDirectory, "actual-input-flight.csv"), flightCsv.ToString(), Encoding.UTF8);
                }
                catch (Exception exception) { Debug.LogError("Narrative evidence write failed: " + exception); }
            }
            IsRunning = false; inputOnly = retreatOnly = false; CurrentPhase = report != null && report.allChecksPassed ? "Completed" : "Finished with failures"; WriteProgress();
            Debug.Log("NARRATIVE VALIDATION COMPLETE: " + LastReportPath + ", allChecksPassed=" + report?.allChecksPassed);
            if (quitAfter && !Application.isEditor) Application.Quit(report != null && report.allChecksPassed ? 0 : 1);
        }
        public void Cancel(string reason)
        {
            if (!IsRunning || finishing) return; RecordError(new OperationCanceledException(reason));
            if (routine != null) StopCoroutine(routine); (guarded as IDisposable)?.Dispose(); Finish();
        }
        void OnDisable() => Cancel("Validation disabled before completion.");
    }
}
