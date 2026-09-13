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
using UnityEngine.SceneManagement;

namespace DropletPrototype
{
    /// <summary>Opt-in rendered comparison and actual motor-path regression checks; never part of normal play.</summary>
    public sealed class VisualUpgradeValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Distribution { public int samples; public double mean, p50, p95, p99, maximum; }
        [Serializable] public sealed class Inventory
        {
            public int sceneRenderers, activeRenderers, visibleRenderers, uniqueSharedMaterials, instanceNamedMaterials;
            public int sceneLights, enabledRealtimeLights, particles, particleSystems, probes, enabledRealtimeProbes;
            public int legacyPoolTransforms, reactorPoolTransforms, reactorCapacity, reflectionSources;
            public long allocatedBytes, reservedBytes, monoBytes;
        }
        [Serializable] public sealed class Phase
        {
            public string name, workload; public int frames, width, height, focusedFrames, destroyed, activeEffectsPeak, reactorEffectsPeak;
            public int realtimeLightsPeak, particlesPeak, probeCaptures, reactorDropped, legacyDropped;
            public float reflectionStrengthPeak; public double seconds, meanFps;
            public Distribution renderedFrameMs, mainThreadMs, gpuFrameMs, drawCalls, setPassCalls, triangles, gcBytes;
            public Inventory before, after; public Vector3 cameraPosition, cameraForward;
        }
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Shot
        { public string name, path, purpose; public bool written; public float fov; public int width, height; public Vector3 cameraPosition, cameraForward; }
        [Serializable] public sealed class Report
        {
            public string label, scene, utc, unity, cpu, gpu, graphicsApi, operatingSystem, outputDirectory, scope, error;
            public bool baseline, editor, developmentBuild, batchMode, inspectionOnly, completed, allChecksPassed;
            public int width, height, vSyncCount, targetFrameRate, previousVSyncCount, previousTargetFrameRate, totalTargets;
            public int processorCount, graphicsMemoryMB, systemMemoryMB;
            public Phase[] phases; public Check[] checks; public Shot[] screenshots;
        }
        [Serializable] public sealed class Progress
        { public string phase, label, outputDirectory, utc; public int frame, phaseFrame, completedPhases; public bool running, focused; }

        public bool IsRunning { get; private set; }
        [Tooltip("Explicit diagnostic opt-in: skip the four timing phases but retain rendered inspections and gameplay checks.")]
        public bool inspectionOnly;
        public string LastReportPath { get; private set; }
        public string CurrentPhase { get; private set; } = "Idle";
        public int PhaseFrame { get; private set; }
        public Report LastReport { get; private set; }
        readonly List<Phase> phases = new List<Phase>();
        readonly List<Check> checks = new List<Check>();
        readonly List<Shot> shots = new List<Shot>();
        readonly StringBuilder csv = new StringBuilder(160000);
        readonly FrameTiming[] timing = new FrameTiming[1];
        MissionController mission; MissionEffects legacy; ReactorExplosionPool explosions; DropletReflectionResponse chrome;
        SolarSystemBackdrop backdrop; SolarLightingRig lighting; Camera view; HudPresenter hud;
        Light[] sceneLights; ParticleSystem[] sceneParticles; ShipTarget[] ordered;
        ProfilerRecorder mainRecorder, drawRecorder, passRecorder, triangleRecorder, gcRecorder;
        Report report; Coroutine routine; IEnumerator guarded;
        bool saved, finishing, quitAfter, oldBackground, oldMissionEnabled, oldInputEnabled, oldChaseEnabled, oldHudEnabled;
        int oldVsync, oldCap, legacyPool, reactorPool, teleportCount, teleportErrors;
        float oldFov; Vector3 oldCameraPosition; Quaternion oldCameraRotation;
        Action pauseCallback, startCallback, restartCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs(); string output = null, label = "Player", scene = null; bool request = false, quit = false;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].TrimStart('-');
                if (arg == "visual-upgrade-validation") { request = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i]; }
                else if (arg == "visual-upgrade-label" && i + 1 < args.Length) label = args[++i];
                else if (arg == "visual-upgrade-scene" && i + 1 < args.Length) scene = args[++i];
                else if (arg == "visual-upgrade-quit") quit = true;
            }
            if (!request || FindAnyObjectByType<VisualUpgradeValidationRunner>() != null) return;
            var runner = new GameObject("__TemporaryVisualUpgradeValidation").AddComponent<VisualUpgradeValidationRunner>();
            if (string.IsNullOrEmpty(scene) || SceneManager.GetActiveScene().name == scene) runner.Begin(output, label, quit);
            else
            {
                DontDestroyOnLoad(runner.gameObject);
                runner.StartCoroutine(runner.LoadRequestedScene(scene, output, label, quit));
            }
        }

        IEnumerator LoadRequestedScene(string scene, string output, string label, bool quit)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogError("VISUAL UPGRADE VALIDATION: scene is absent from this player: " + scene); if (quit) Application.Quit(2); yield break; }
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            Begin(output, label, quit);
        }

        public void Begin(string outputDirectory, string label, bool quitWhenComplete = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode or use a rendered standalone player.");
            if (IsRunning || FindObjectsByType<VisualUpgradeValidationRunner>().Any(r => r != this && r.IsRunning))
                throw new InvalidOperationException("Visual upgrade validation is already running.");
            mission = FindAnyObjectByType<MissionController>();
            if (mission == null || mission.motor == null || mission.settings == null || mission.chaseCamera == null || mission.score == null)
                throw new InvalidOperationException("A configured playable fleet scene is required.");
            view = mission.chaseCamera.GetComponent<Camera>();
            if (view == null) throw new InvalidOperationException("Mission chase camera is missing its Camera component.");
            string safeLabel = string.Concat((label ?? "Run").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string parent = string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Application.persistentDataPath, "VisualUpgradeValidation") : outputDirectory;
            string directory = Path.GetFullPath(Path.Combine(parent, safeLabel + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(directory);
            report = new Report
            {
                label = label, scene = mission.gameObject.scene.path, baseline = !mission.gameObject.scene.name.Contains("VisualUpgrade"),
                utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), operatingSystem = SystemInfo.operatingSystem,
                outputDirectory = directory, editor = Application.isEditor, developmentBuild = Debug.isDebugBuild, batchMode = Application.isBatchMode,
                inspectionOnly = inspectionOnly,
                processorCount = SystemInfo.processorCount, graphicsMemoryMB = SystemInfo.graphicsMemorySize, systemMemoryMB = SystemInfo.systemMemorySize,
                scope = "Actual rendered frames in this process; each of 4 phases has at least 480 measured frames AND 6 real seconds after 60 warmup frames (16384-frame safety limit), VSync disabled and frame cap unlimited in BOTH comparisons. Standalone requests 1920x1080 windowed; actual dimensions and focus are reported per phase. NearCombat/FleetMedium/FacingSun are fixed identical world-space viewpoints in both scenes. ConcentratedExplosions teleports between existing targets then traverses each target using actual MissionController.Step(0.02) / DropletMotor sweeps every 0.16 real seconds, moves the inspection camera with the selected target, and does not directly destroy or score. These controlled attack starts are not natural gameplay pacing. Separate 120-target burst also uses real motor sweeps. Profiler recorder values exist only when marker samples are available; GPU timing is unavailable when FrameTimingManager supplies no nonzero timing. Frame time is rendered process interval, not pure GPU cost; inventory uses all scene renderers and isVisible may include Editor cameras. Screenshots and inventory/file formatting are outside timing intervals. Frozen reflection A/B changes only the component explosionReflections flag at an identical paused camera/time. No physical input, subjective feel, long-session stability or cross-hardware claim."
            };
            phases.Clear(); checks.Clear(); shots.Clear(); csv.Clear();
            csv.AppendLine("phase,frame,frameMs,mainThreadMs,gpuMs,drawCalls,setPassCalls,triangles,gcBytes,focused,targetsDestroyed,legacyEffects,reactorEffects,realtimeLights,particles,reflectionStrength");
            oldBackground = Application.runInBackground; Application.runInBackground = true;
            quitAfter = quitWhenComplete; IsRunning = true; finishing = false; saved = false; teleportCount = teleportErrors = 0;
            guarded = Guard(Run()); routine = StartCoroutine(guarded);
        }

        IEnumerator Guard(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            try
            {
                while (stack.Count > 0)
                {
                    bool moved = false; object value = null; Exception error = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                    if (error != null) { RecordError(error); break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (value is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return value;
                }
            }
            finally { while (stack.Count > 0) Safe(() => (stack.Pop() as IDisposable)?.Dispose()); Finish(); }
        }

        IEnumerator Run()
        {
            yield return null;
            legacy = FindAnyObjectByType<MissionEffects>(); explosions = FindAnyObjectByType<ReactorExplosionPool>(); chrome = FindAnyObjectByType<DropletReflectionResponse>();
            backdrop = FindAnyObjectByType<SolarSystemBackdrop>(); lighting = FindAnyObjectByType<SolarLightingRig>(); hud = FindAnyObjectByType<HudPresenter>();
            oldMissionEnabled = mission.enabled; oldInputEnabled = mission.input != null && mission.input.enabled; oldChaseEnabled = mission.chaseCamera.enabled;
            oldHudEnabled = hud != null && hud.enabled; oldFov = view.fieldOfView; oldCameraPosition = view.transform.position; oldCameraRotation = view.transform.rotation;
            oldVsync = QualitySettings.vSyncCount; oldCap = Application.targetFrameRate;
            if (mission.input != null) { pauseCallback = mission.input.PauseRequested; startCallback = mission.input.StartRequested; restartCallback = mission.input.RestartRequested; }
            saved = true; mission.enabled = false; mission.chaseCamera.enabled = false;
            if (mission.input != null) { mission.input.enabled = false; mission.input.PauseRequested = null; mission.input.StartRequested = null; mission.input.RestartRequested = null; }
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            legacy?.InitializePool(); explosions?.InitializePool(); mission.Restart();
            SetPhase("Initial rendered warmup"); yield return new WaitForSecondsRealtime(1);
            sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            sceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
            ordered = mission.targets.Where(t => t != null).OrderBy(t => (t.transform.position - mission.spawnPosition).sqrMagnitude).ThenBy(t => t.targetId, StringComparer.Ordinal).ToArray();
            legacyPool = legacy != null ? legacy.PoolInstanceCount : 0; reactorPool = explosions != null ? explosions.PoolInstanceCount : 0;
            report.width = Screen.width; report.height = Screen.height; report.totalTargets = mission.TotalCount;
            report.vSyncCount = QualitySettings.vSyncCount; report.targetFrameRate = Application.targetFrameRate; report.previousVSyncCount = oldVsync; report.previousTargetFrameRate = oldCap;
            AddCheck("Rendered graphics device", SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, report.graphicsApi);
            AddCheck("120 registered fleet targets", mission.TotalCount == 120 && ordered.Length == 120, "targets=" + mission.TotalCount);
            if (!Application.isEditor) AddCheck("Standalone 1920x1080", Screen.width == 1920 && Screen.height == 1080, Screen.width + "x" + Screen.height);
            mainRecorder = StartRecorder(ProfilerCategory.Internal, "Main Thread"); gcRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count"); passRecorder = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count");
            triangleRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            if (!inspectionOnly)
            {
                yield return Measure("NearCombat", false);
                yield return Measure("FleetMedium", false);
                yield return Measure("ConcentratedExplosions", true);
                yield return Measure("FacingSun", false);
            }
            yield return InspectVisuals();
            yield return GameplayChecks();
            AddCheck("Teleport setup never awards a hit", teleportCount > 0 && teleportErrors == 0, "setups=" + teleportCount + ", unexpected hits=" + teleportErrors);
            report.completed = true;
        }

        static ProfilerRecorder StartRecorder(ProfilerCategory category, string name)
        { try { return ProfilerRecorder.StartNew(category, name, 1); } catch (Exception) { return default; } }
        static double Sample(ProfilerRecorder recorder, double divisor = 1) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue / divisor : double.NaN;
        void Aim(Vector3 eye, Vector3 target)
        { view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up)); backdrop?.ApplyMapping(view); }
        Vector3 SunDirection()
        {
            if (backdrop != null && SolarLayoutMath.TryProjectBody(backdrop.Layout, backdrop.Layout?.FindBody("sun"), mission.spawnPosition, out SolarBodyProjection sun)) return sun.direction;
            return RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : new Vector3(1, .15f, 1).normalized;
        }
        void PhaseCamera(string name, ShipTarget target)
        {
            view.fieldOfView = oldFov;
            if (name == "FleetMedium") Aim(new Vector3(950, 720, -1150), new Vector3(0, 40, 600));
            else if (name == "FacingSun") { Vector3 eye = mission.spawnPosition + new Vector3(0, 2.2f, -8); Aim(eye, eye + SunDirection() * 1000); }
            else Aim(target.transform.position + new Vector3(42, 19, -72), target.transform.position);
        }
        IEnumerator Measure(string name, bool attack)
        {
            const int minimumFrames = 480, capacity = 16384;
            const double minimumSeconds = 6;
            SetPhase(name + " warmup"); mission.Restart(); mission.StartMission();
            mission.motor.ResetPose(mission.spawnPosition, Quaternion.identity); PhaseCamera(name, ordered[0]);
            for (int i = 0; i < 60; i++) { PhaseFrame = i + 1; yield return null; }
            var phase = new Phase { name = name, width = Screen.width, height = Screen.height,
                workload = attack ? "Attack selected existing target every 0.16 real seconds; full real motor sweep through hull; camera tracks selected target" : "Fixed camera; fleet intact; motor held stationary; normal shaders, lights, probes and presentation update", before = ReadInventory() };
            int capturesBefore = lighting != null ? lighting.CaptureCount : 0, next = 0;
            var samples = new double[capacity, 7]; var focus = new bool[capacity]; var destroys = new int[capacity];
            var oldEffects = new int[capacity]; var newEffects = new int[capacity]; var lights = new int[capacity]; var particles = new int[capacity]; var strengths = new float[capacity];
            SetPhase(name + " measuring at least 480 frames and 6 seconds"); yield return null;
            double begin = Time.realtimeSinceStartupAsDouble, nextAttack = begin; int count = 0;
            for (int i = 0; i < capacity && (i < minimumFrames || Time.realtimeSinceStartupAsDouble - begin < minimumSeconds); i++)
            {
                if (attack && Time.realtimeSinceStartupAsDouble >= nextAttack)
                {
                    nextAttack = Time.realtimeSinceStartupAsDouble + .16;
                    while (next < ordered.Length && ordered[next].IsDestroyed) next++;
                    if (next < ordered.Length) { ShipTarget target = ordered[next++]; SweepTarget(target, true); PhaseCamera(name, target); }
                }
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                samples[i, 0] = Time.unscaledDeltaTime * 1000.0; samples[i, 1] = Sample(mainRecorder, 1000000);
                uint haveGpu = FrameTimingManager.GetLatestTimings(1, timing);
                samples[i, 2] = haveGpu > 0 && timing[0].gpuFrameTime > 0 ? timing[0].gpuFrameTime : double.NaN;
                samples[i, 3] = Sample(drawRecorder); samples[i, 4] = Sample(passRecorder); samples[i, 5] = Sample(triangleRecorder); samples[i, 6] = Sample(gcRecorder);
                focus[i] = Application.isFocused; if (focus[i]) phase.focusedFrames++;
                destroys[i] = mission.DestroyedCount; oldEffects[i] = legacy != null ? legacy.ActiveEffectCount : 0; newEffects[i] = explosions != null ? explosions.ActiveEffectCount : 0;
                lights[i] = CountLights(); particles[i] = CountParticles(); strengths[i] = chrome != null ? chrome.PeakExplosionStrength : 0;
                phase.activeEffectsPeak = Mathf.Max(phase.activeEffectsPeak, oldEffects[i]); phase.reactorEffectsPeak = Mathf.Max(phase.reactorEffectsPeak, newEffects[i]);
                phase.realtimeLightsPeak = Mathf.Max(phase.realtimeLightsPeak, lights[i]); phase.particlesPeak = Mathf.Max(phase.particlesPeak, particles[i]);
                phase.reflectionStrengthPeak = Mathf.Max(phase.reflectionStrengthPeak, strengths[i]); PhaseFrame = i + 1;
                count = i + 1;
            }
            phase.frames = count; phase.seconds = Time.realtimeSinceStartupAsDouble - begin; phase.after = ReadInventory();
            phase.destroyed = mission.DestroyedCount; phase.probeCaptures = lighting != null ? lighting.CaptureCount - capturesBefore : 0;
            phase.reactorDropped = explosions != null ? explosions.DroppedEffectCount : 0; phase.legacyDropped = legacy != null ? legacy.DroppedEffectCount : 0;
            phase.cameraPosition = view.transform.position; phase.cameraForward = view.transform.forward;
            phase.renderedFrameMs = Summarize(Column(samples, 0, count)); phase.mainThreadMs = Summarize(Column(samples, 1, count)); phase.gpuFrameMs = Summarize(Column(samples, 2, count));
            phase.drawCalls = Summarize(Column(samples, 3, count)); phase.setPassCalls = Summarize(Column(samples, 4, count)); phase.triangles = Summarize(Column(samples, 5, count)); phase.gcBytes = Summarize(Column(samples, 6, count));
            phase.meanFps = phase.renderedFrameMs.mean > 0 ? 1000 / phase.renderedFrameMs.mean : 0;
            for (int i = 0; i < count; i++)
            {
                csv.Append(name).Append(',').Append(i);
                for (int k = 0; k < 7; k++) { csv.Append(','); if (!double.IsNaN(samples[i, k])) csv.Append(samples[i, k].ToString("R", CultureInfo.InvariantCulture)); }
                csv.Append(',').Append(focus[i] ? 1 : 0).Append(',').Append(destroys[i]).Append(',').Append(oldEffects[i]).Append(',').Append(newEffects[i])
                    .Append(',').Append(lights[i]).Append(',').Append(particles[i]).Append(',').Append(strengths[i].ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            }
            phases.Add(phase);
            yield return Capture("performance-" + name, "Actual phase camera after measurement; screenshot and formatting excluded from the timed interval.");
        }

        IEnumerator InspectVisuals()
        {
            SetPhase("Material, scale, reactor and explosion inspection"); mission.Restart(); mission.StartMission();
            ShipTarget target = ordered[0]; if (hud != null) hud.enabled = false;
            Bounds bounds = TargetBounds(target);
            Vector3 sizeEyeOffset = new Vector3(24, 9, -42);
            Vector3 screenRight = Vector3.Cross(Vector3.up, -sizeEyeOffset).normalized;
            float projectedHalfWidth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(screenRight.x), Mathf.Abs(screenRight.y), Mathf.Abs(screenRight.z)));
            // Place beside the hull at the same camera depth, never on its occluded far side.
            mission.motor.ResetPose(bounds.center + screenRight * (projectedHalfWidth + 1.8f), Quaternion.identity);
            Aim(bounds.center + sizeEyeOffset, bounds.center);
            yield return Capture("01-ship-droplet-size", "Real-size ship and droplet side by side at the same camera depth. Camera has fixed world-space offset; droplet sits outside the projected hull bounds to prevent occlusion.");
            Vector3 droplet = mission.motor.visualRoot.position;
            Aim(droplet + new Vector3(-2.6f, 1.05f, -3.8f), droplet);
            if (lighting != null)
            {
                lighting.RequestProbeRefresh(); double deadline = Time.realtimeSinceStartupAsDouble + 7;
                while (!lighting.ProbeHasFreshCapture && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            }
            yield return Capture("02-chrome-before-explosion", "Close material inspection; no replacement material or exposure changes.");
            PhaseCamera("NearCombat", target);
            yield return Capture("03-reactor-normal", "Intact main reactor and four auxiliary engine structures; solar direction unchanged.");
            SweepTarget(target, false);
            yield return Capture("04-penetration-instability", "First actual motor hit; capture occurs on subsequent rendered frames, exact latency depends on frame time.", false);
            yield return new WaitForSeconds(.19f);
            yield return Capture("05-main-reactor-blast", "Actual pooled blast after the short instability stage.", false);
            droplet = mission.motor.visualRoot.position;
            Aim(droplet + new Vector3(-2.6f, 1.05f, -3.8f), droplet);
            yield return Capture("06-chrome-orange-reflection", "Same actual explosion; close droplet surface response, world position and material unchanged.", false);
            // Separate typical chase view on another fresh impact; the response comes from the actual source behind the droplet.
            mission.Restart(); mission.StartMission(); SweepTarget(target, true); mission.chaseCamera.ResetCamera();
            yield return new WaitForSeconds(.24f);
            yield return Capture("07-chase-explosion-reflection", "Normal third-person camera reset behind the droplet after a full real motor path through the hull.", false);
            float response = chrome != null ? chrome.PeakExplosionStrength : 0;
            if (chrome != null) AddCheck("Actual explosion produces a droplet reflection source", chrome.ActiveExplosionSources > 0 && response > .001f,
                "sources=" + chrome.ActiveExplosionSources + ", strength=" + response);
            if (chrome != null)
            {
                // Identical frozen geometry, camera, exposure, environment and source age.
                // Only the optional explosion term changes; the baseline gets its own unchanged material screenshot above.
                mission.TogglePause(); bool previousResponse = chrome.explosionReflections;
                try
                {
                    chrome.explosionReflections = false; yield return null;
                    yield return Capture("07c-frozen-CHASE-explosion-response-OFF", "Exact normal third-person camera at paused impact age; only explosionReflections=false. HUD hidden.");
                    chrome.explosionReflections = true; yield return null;
                    yield return Capture("07d-frozen-CHASE-explosion-response-ON", "Identical normal third-person camera/source/exposure at paused impact age; only explosionReflections=true. HUD hidden.");
                    droplet = mission.motor.visualRoot.position; Aim(droplet + new Vector3(-2.6f, 1.05f, -3.8f), droplet);
                    chrome.explosionReflections = false; yield return null;
                    yield return Capture("07a-frozen-chrome-explosion-response-OFF", "Paused camera/source/exposure; only explosionReflections=false. HUD hidden.");
                    chrome.explosionReflections = true; yield return null;
                    yield return Capture("07b-frozen-chrome-explosion-response-ON", "Identical paused camera/source/exposure; only explosionReflections=true. HUD hidden.");
                    AddCheck("Frozen A/B re-enables the same explosion source", chrome.ActiveExplosionSources > 0 && chrome.PeakExplosionStrength > .001f,
                        "sources=" + chrome.ActiveExplosionSources + ", strength=" + chrome.PeakExplosionStrength);
                }
                finally { chrome.explosionReflections = previousResponse; if (mission.State == MissionState.Paused) mission.TogglePause(); }
            }
            yield return new WaitForSeconds(1.1f); PhaseCamera("NearCombat", target);
            yield return Capture("08-reactor-afterglow", "Finite fading explosion residue; no collision targets created by the presentation.");
            yield return new WaitForSeconds(2);
            if (chrome != null) AddCheck("Explosion reflection naturally decays", chrome.ActiveExplosionSources == 0 && chrome.PeakExplosionStrength <= .001f,
                "sources=" + chrome.ActiveExplosionSources + ", strength=" + chrome.PeakExplosionStrength);
            if (backdrop != null && backdrop.sunProxy != null)
            {
                bool previousBackdrop = backdrop.enabled; float previousNear = view.nearClipPlane;
                Vector3 previousEye = view.transform.position; Quaternion previousRotation = view.transform.rotation;
                try
                {
                    // Keep the actual saved proxy mesh/material, temporarily freeze its
                    // angular mapping and move only the diagnostic camera close to it.
                    backdrop.ApplyMapping(view); backdrop.enabled = false;
                    Transform sun = backdrop.sunProxy; Vector3 center = sun.position;
                    float radius = Mathf.Max(.01f, Mathf.Abs(sun.lossyScale.x));
                    Vector3 towardCamera = (previousEye - center).normalized;
                    view.nearClipPlane = Mathf.Max(.001f, radius * .01f);
                    view.transform.SetPositionAndRotation(center + towardCamera * radius * 3.3f, Quaternion.LookRotation(-towardCamera));
                    yield return Capture("08a-solar-surface-DIAGNOSTIC-NOT-GAME-SCALE", "Diagnostic close view of the actual solar mesh/material with celestial proxy mapping temporarily frozen; the gameplay angular size is NOT changed and this is NOT a game-scale shot.");
                }
                finally
                {
                    view.nearClipPlane = previousNear; view.transform.SetPositionAndRotation(previousEye, previousRotation);
                    backdrop.enabled = previousBackdrop; backdrop.ApplyMapping(view);
                }
            }
            if (hud != null) hud.enabled = oldHudEnabled;
        }

        IEnumerator GameplayChecks()
        {
            SetPhase("Real motor gameplay regression"); mission.Restart(); mission.StartMission();
            ShipTarget first = ordered[0]; SweepTarget(first, true);
            AddCheck("Real motor path crosses enlarged hull and scores", first.IsDestroyed && mission.DestroyedCount > 0 && mission.score.Score > 0,
                first.targetId + ", score=" + mission.score.Score + ", hit=" + first.LastHit.point + ", droplet=" + mission.motor.transform.position);
            AddCheck("Destroyed collision volumes immediately disabled", first.hitVolumes.All(c => c != null && !c.enabled), "All compound hit volumes checked after sweep.");
            yield return new WaitForSeconds(.22f);
            AddCheck("Intact ship and engine subtree hidden after instability", first.visualRoot != null && !first.visualRoot.activeInHierarchy,
                "VisualRoot may remain visible for the intentional 0.16-second instability, then must hide.");
            mission.TogglePause(); Vector3 position = mission.motor.transform.position; float remaining = mission.Remaining;
            int score = mission.score.Score, destroyed = mission.DestroyedCount; float age = legacy != null ? legacy.ElapsedSimulationTime : 0, reactorAge = explosions != null ? explosions.ElapsedSimulationTime : 0;
            yield return new WaitForSecondsRealtime(.3f); mission.Step(1, new FlightCommand { throttle = 1, boost = true });
            AddCheck("Pause freezes movement timer score and both effect clocks", mission.State == MissionState.Paused && position == mission.motor.transform.position &&
                remaining == mission.Remaining && score == mission.score.Score && destroyed == mission.DestroyedCount &&
                (legacy == null || age == legacy.ElapsedSimulationTime) && (explosions == null || reactorAge == explosions.ElapsedSimulationTime), "0.3 real seconds and an attempted one-second mission step while paused.");
            mission.TogglePause();
            foreach (ShipTarget target in ordered) { if (mission.State != MissionState.Playing) break; if (!target.IsDestroyed) SweepTarget(target, true); yield return null; }
            AddCheck("All 120 actual motor sweeps reach victory", mission.TotalCount == 120 && mission.DestroyedCount == mission.TotalCount && mission.Won && mission.State == MissionState.Results,
                mission.DestroyedCount + "/" + mission.TotalCount + ", state=" + mission.State + ", elapsed=" + mission.Elapsed + ", score=" + mission.score.Score);
            score = mission.score.Score; mission.Step(1, new FlightCommand { throttle = 1, boost = true }); mission.StartMission();
            AddCheck("Results and score resolve once", mission.ResultTransitions == 1 && mission.score.Score == score, "result transitions=" + mission.ResultTransitions);
            yield return Capture("09-victory", "All targets destroyed by real motor swept paths; no direct kill or scoring calls.");
            for (int round = 0; round < 3; round++)
            {
                mission.Restart(); yield return null;
                bool restored = ordered.All(t => !t.IsDestroyed && t.visualRoot != null && t.visualRoot.activeInHierarchy && t.hitVolumes.All(c => c != null && c.enabled));
                bool clean = (legacy == null || legacy.ActiveEffectCount == 0 && legacy.ActiveAudioCount == 0 && legacy.PoolInstanceCount == legacyPool) &&
                    (explosions == null || explosions.ActiveEffectCount == 0 && explosions.ActiveLightCount == 0 && explosions.PoolInstanceCount == reactorPool) &&
                    (chrome == null || chrome.ActiveExplosionSources == 0 && chrome.PeakExplosionStrength <= .001f);
                AddCheck("Restart " + (round + 1) + " fully restores gameplay and presentation", restored && clean && mission.State == MissionState.Ready &&
                    mission.DestroyedCount == 0 && mission.score.Score == 0 && mission.score.Combo == 0 && mission.Elapsed == 0 &&
                    Mathf.Approximately(mission.Remaining, mission.settings.missionSeconds) && Time.timeScale == 1 && mission.motor.transform.position == mission.spawnPosition,
                    "ships restored=" + restored + ", pools/lights/reflection clean=" + clean);
                if (round < 2) { mission.StartMission(); SweepTarget(first, true); yield return null; }
            }
            // One frame of 120 attacks: keep the presentation budget hard while all logical hits still resolve.
            mission.StartMission(); PhaseCamera("NearCombat", first);
            foreach (ShipTarget target in ordered) if (!target.IsDestroyed && mission.State == MissionState.Playing) SweepTarget(target, false);
            AddCheck("120-target same-frame hit burst preserves victory", mission.Won && mission.DestroyedCount == mission.TotalCount, "destroyed=" + mission.DestroyedCount);
            if (explosions != null) AddCheck("Explosion pool stays bounded under 120-hit burst", explosions.ActiveEffectCount <= explosions.Capacity && explosions.PoolInstanceCount == reactorPool && explosions.ActiveLightCount <= 2,
                "active=" + explosions.ActiveEffectCount + ", capacity=" + explosions.Capacity + ", dropped=" + explosions.DroppedEffectCount + ", pool transforms=" + explosions.PoolInstanceCount);
            yield return new WaitForSeconds(.24f); yield return Capture("10-bounded-120-hit-burst", "120 target events in one frame using motor sweeps; pooled effects remain bounded.", false);
            mission.Restart(); mission.StartMission();
            bool legacyEnabled = legacy != null && legacy.enabled, reactorEnabled = explosions != null && explosions.enabled;
            try
            {
                if (legacy != null) legacy.enabled = false; if (explosions != null) explosions.enabled = false;
                SweepTarget(first, true);
                AddCheck("Disabled VFX leaves destruction and scoring correct", first.IsDestroyed && mission.DestroyedCount > 0 && mission.score.Score > 0,
                    "Both optional effect pool components disabled during actual motor sweep.");
            }
            finally { if (legacy != null) legacy.enabled = legacyEnabled; if (explosions != null) explosions.enabled = reactorEnabled; mission.Restart(); }
            yield return Capture("11-restarted-ready", "Final clean restart; mission ready, full intact fleet, no retained explosion light.");
        }

        Bounds TargetBounds(ShipTarget target)
        {
            Bounds bounds = default; bool found = false;
            foreach (Collider collider in target.hitVolumes)
                if (collider != null && collider.enabled) { if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds); }
            if (!found) throw new InvalidOperationException("Target has no enabled hit geometry: " + target.targetId);
            return bounds;
        }
        void SweepTarget(ShipTarget target, bool traverseWholeHull)
        {
            if (target == null || target.IsDestroyed || mission.State != MissionState.Playing) return;
            Bounds bounds = TargetBounds(target); Vector3 direction = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude < .9f) direction = Vector3.forward;
            float extent = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(direction.x), 0, Mathf.Abs(direction.z)));
            float clearance = extent + mission.settings.hitRadius + 4;
            Vector3 start = bounds.center - direction * clearance;
            int before = mission.DestroyedCount, score = mission.score.Score;
            mission.motor.ResetPose(start, Quaternion.LookRotation(direction)); Physics.SyncTransforms(); teleportCount++;
            if (mission.DestroyedCount != before || mission.score.Score != score) teleportErrors++;
            for (int step = 0; step < 500 && mission.State == MissionState.Playing; step++)
            {
                mission.Step(.02f, new FlightCommand { throttle = 1, boost = true });
                if (target.IsDestroyed && (!traverseWholeHull || Vector3.Dot(mission.motor.transform.position - bounds.center, direction) >= clearance)) break;
            }
            if (!target.IsDestroyed) throw new InvalidOperationException("Actual motor sweep missed target " + target.targetId);
            if (traverseWholeHull && mission.State == MissionState.Playing && Vector3.Dot(mission.motor.transform.position - bounds.center, direction) < clearance)
                throw new InvalidOperationException("Motor did not cross the entire hull of " + target.targetId);
        }

        int CountLights() { int count = 0; foreach (Light light in sceneLights) if (light != null && light.isActiveAndEnabled && light.bakingOutput.lightmapBakeType != LightmapBakeType.Baked && light.intensity > 0) count++; return count; }
        int CountParticles() { int count = 0; foreach (ParticleSystem particles in sceneParticles) if (particles != null && particles.gameObject.activeInHierarchy) count += particles.particleCount; return count; }
        Inventory ReadInventory()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            var materials = new HashSet<Material>(); int active = 0, visible = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);
                if (renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy) active++;
                if (renderer.isVisible) visible++;
            }
            var probes = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include);
            return new Inventory
            {
                sceneRenderers = renderers.Length, activeRenderers = active, visibleRenderers = visible, uniqueSharedMaterials = materials.Count,
                instanceNamedMaterials = materials.Count(m => m.name.Contains("(Instance)")), sceneLights = sceneLights.Length, enabledRealtimeLights = CountLights(),
                particles = CountParticles(), particleSystems = sceneParticles.Length, probes = probes.Length,
                enabledRealtimeProbes = probes.Count(p => p.isActiveAndEnabled && p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime),
                legacyPoolTransforms = legacy != null ? legacy.PoolInstanceCount : 0, reactorPoolTransforms = explosions != null ? explosions.PoolInstanceCount : 0,
                reactorCapacity = explosions != null ? explosions.Capacity : 0, reflectionSources = chrome != null ? chrome.ActiveExplosionSources : 0,
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = Profiler.GetTotalReservedMemoryLong(), monoBytes = Profiler.GetMonoUsedSizeLong()
            };
        }

        IEnumerator Capture(string name, string purpose, bool settle = true)
        {
            SetPhase("Screenshot " + name); if (settle) { yield return null; yield return null; }
            string path = Path.Combine(report.outputDirectory, name + ".png");
            var shot = new Shot { name = name, path = path, purpose = purpose, cameraPosition = view.transform.position, cameraForward = view.transform.forward, fov = view.fieldOfView, width = Screen.width, height = Screen.height };
            ScreenCapture.CaptureScreenshot(path); yield return null;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            shot.written = File.Exists(path) && new FileInfo(path).Length > 0; shots.Add(shot); AddCheck("Screenshot " + name, shot.written, path);
        }
        static double[] Column(double[,] samples, int column, int count)
        { var data = new List<double>(count); for (int i = 0; i < count; i++) if (!double.IsNaN(samples[i, column])) data.Add(samples[i, column]); return data.ToArray(); }
        static Distribution Summarize(double[] values)
        {
            var summary = new Distribution { samples = values.Length }; if (values.Length == 0) return summary;
            summary.mean = values.Average(); Array.Sort(values); summary.p50 = values[Mathf.CeilToInt(values.Length * .5f) - 1];
            summary.p95 = values[Mathf.CeilToInt(values.Length * .95f) - 1]; summary.p99 = values[Mathf.CeilToInt(values.Length * .99f) - 1]; summary.maximum = values[values.Length - 1]; return summary;
        }
        void AddCheck(string name, bool passed, string detail) => checks.Add(new Check { name = name, passed = passed, detail = detail });
        public Progress GetProgress() => new Progress { phase = CurrentPhase, label = report != null ? report.label : "", outputDirectory = report != null ? report.outputDirectory : "", utc = DateTime.UtcNow.ToString("o"), frame = Time.frameCount, phaseFrame = PhaseFrame, completedPhases = phases.Count, running = IsRunning, focused = Application.isFocused };
        public void WriteProgress()
        { if (report != null) Safe(() => File.WriteAllText(Path.Combine(report.outputDirectory, "progress.json"), JsonUtility.ToJson(GetProgress(), true), Encoding.UTF8)); }
        void SetPhase(string phase) { CurrentPhase = phase; PhaseFrame = 0; WriteProgress(); Debug.Log("VISUAL UPGRADE VALIDATION " + report.label + ": " + phase); }
        void RecordError(Exception exception) { if (report != null) { report.completed = false; report.error = (report.error ?? "") + exception + "\n"; } }
        void Safe(Action action) { try { action(); } catch (Exception ex) { RecordError(ex); } }
        void Finish()
        {
            if (!IsRunning || finishing) return; finishing = true;
            Safe(() => mainRecorder.Dispose()); Safe(() => gcRecorder.Dispose()); Safe(() => drawRecorder.Dispose()); Safe(() => passRecorder.Dispose()); Safe(() => triangleRecorder.Dispose());
            if (saved)
            {
                Safe(() => mission.Restart());
                Safe(() => { view.fieldOfView = oldFov; view.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); });
                Safe(() => { mission.enabled = oldMissionEnabled; mission.chaseCamera.enabled = oldChaseEnabled; if (hud != null) hud.enabled = oldHudEnabled; });
                Safe(() => { if (mission.input != null) { mission.input.PauseRequested = pauseCallback; mission.input.StartRequested = startCallback; mission.input.RestartRequested = restartCallback; mission.input.enabled = oldInputEnabled; } });
                Safe(() => { QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldCap; });
            }
            Safe(() => Application.runInBackground = oldBackground);
            if (report != null)
            {
                report.phases = phases.ToArray(); report.checks = checks.ToArray(); report.screenshots = shots.ToArray();
                report.allChecksPassed = report.completed && string.IsNullOrEmpty(report.error) && checks.All(c => c.passed);
                LastReport = report; LastReportPath = Path.Combine(report.outputDirectory, "visual-upgrade-validation.json");
                try { File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true), Encoding.UTF8); File.WriteAllText(Path.Combine(report.outputDirectory, "frames.csv"), csv.ToString(), Encoding.UTF8); }
                catch (Exception ex) { Debug.LogError("Visual upgrade report could not be written: " + ex); }
                CurrentPhase = report.allChecksPassed ? "Completed" : "Finished with failures";
            }
            IsRunning = false; saved = false; routine = null; inspectionOnly = false; WriteProgress();
            Debug.Log("VISUAL UPGRADE VALIDATION COMPLETE: " + LastReportPath + ", checks=" + (report != null && report.allChecksPassed));
            if (report != null && !string.IsNullOrEmpty(report.error)) Debug.LogError(report.error);
            if (quitAfter && !Application.isEditor) Application.Quit(report != null && report.allChecksPassed ? 0 : 1);
        }
        public void Cancel(string reason)
        {
            if (!IsRunning || finishing) return; RecordError(new OperationCanceledException(reason));
            Safe(() => { if (routine != null) StopCoroutine(routine); }); Safe(() => (guarded as IDisposable)?.Dispose()); Finish();
        }
        void OnDisable() => Cancel("Visual upgrade validation disabled before completion.");
    }
}
