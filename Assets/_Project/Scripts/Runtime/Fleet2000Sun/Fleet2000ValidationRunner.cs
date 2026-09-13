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
    public sealed class Fleet2000ValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Distribution { public int samples; public double mean, p50, p95, p99, maximum; }
        [Serializable] public sealed class Inventory
        {
            public int sceneRenderers, activeRenderers, visibleRenderers, uniqueSharedMaterials, instanceNamedMaterials;
            public int fullPrefabs, instancedShips, interactionShips, visibleInstances, lod1Instances, lod2Instances, instanceDrawCalls, chunks;
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
        bool saved, finishing, quitAfter, oldBackground, oldMissionEnabled, oldInputEnabled, oldChaseEnabled, oldHudEnabled, oldOverviewEnabled;
        FleetOverviewCamera overview;
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
                if (arg == "fleet2000-validation") { request = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i]; }
                else if (arg == "fleet2000-label" && i + 1 < args.Length) label = args[++i];
                else if (arg == "fleet2000-scene" && i + 1 < args.Length) scene = args[++i];
                else if (arg == "fleet2000-quit") quit = true;
            }
            if (!request || FindAnyObjectByType<Fleet2000ValidationRunner>() != null) return;
            var runner = new GameObject("__TemporaryFleet2000Validation").AddComponent<Fleet2000ValidationRunner>();
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
            { Debug.LogError("FLEET 2000 VALIDATION: scene is absent from this player: " + scene); if (quit) Application.Quit(2); yield break; }
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            Begin(output, label, quit);
        }

        public void Begin(string outputDirectory, string label, bool quitWhenComplete = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode or use a rendered standalone player.");
            if (IsRunning || FindObjectsByType<Fleet2000ValidationRunner>().Any(r => r != this && r.IsRunning))
                throw new InvalidOperationException("Visual upgrade validation is already running.");
            mission = FindAnyObjectByType<MissionController>();
            if (mission == null || mission.motor == null || mission.settings == null || mission.chaseCamera == null || mission.score == null)
                throw new InvalidOperationException("A configured playable fleet scene is required.");
            view = mission.chaseCamera.GetComponent<Camera>();
            if (view == null) throw new InvalidOperationException("Mission chase camera is missing its Camera component.");
            string safeLabel = string.Concat((label ?? "Run").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string parent = string.IsNullOrWhiteSpace(outputDirectory) ? Path.Combine(Application.persistentDataPath, "Fleet2000Validation") : outputDirectory;
            string directory = Path.GetFullPath(Path.Combine(parent, safeLabel + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(directory);
            report = new Report
            {
                label = label, scene = mission.gameObject.scene.path, baseline = false,
                utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), operatingSystem = SystemInfo.operatingSystem,
                outputDirectory = directory, editor = Application.isEditor, developmentBuild = Debug.isDebugBuild, batchMode = Application.isBatchMode,
                inspectionOnly = inspectionOnly,
                processorCount = SystemInfo.processorCount, graphicsMemoryMB = SystemInfo.graphicsMemorySize, systemMemoryMB = SystemInfo.systemMemorySize,
                scope = "Actual new-scene rendered frames: five phases each at least 480 frames and 6 seconds after warmup. 1080p native, no frame generation, VSync off, uncapped. Attack phase uses controlled teleports and real .02s motor sweeps. All-target regression uses 100 long diagnostic motor segments with 20 ships per column and no direct kill calls. Missing Profiler samples are unmeasured. graphicsMemoryMB is adapter capacity. allocated/reserved bytes are Unity CPU memory, not attributable VRAM. Screenshots and inventory outside timing. No subjective input, long-session or cross-hardware claim."
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
            saved = true; overview=FindAnyObjectByType<FleetOverviewCamera>();oldOverviewEnabled=overview!=null&&overview.enabled;if(overview!=null)overview.enabled=false;mission.enabled = false; mission.chaseCamera.enabled = false;
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
            AddCheck("2000 registered fleet targets", mission.TotalCount == 2000 && ordered.Length == 2000, "targets=" + mission.TotalCount);
            if (!Application.isEditor) AddCheck("Standalone 1920x1080", Screen.width == 1920 && Screen.height == 1080, Screen.width + "x" + Screen.height);
            mainRecorder = StartRecorder(ProfilerCategory.Internal, "Main Thread"); gcRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count"); passRecorder = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count");
            triangleRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            if (!inspectionOnly)
            {
                yield return Measure("NearCombat", false);
                yield return Measure("FleetMedium", false);
                yield return Measure("FleetPanorama", false);
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
            if (name == "FleetPanorama") Aim(new Vector3(-6200, 1000, 1800), new Vector3(500, 65, 2400));
            else if (name == "FleetMedium") Aim(new Vector3(-700, 390, -430), new Vector3(900, 65, 2400));
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
            SetPhase("Near middle far and occluded sun actual Game views"); mission.Restart(); mission.StartMission();
            if(hud!=null)hud.enabled=false;
            foreach(var phase in new[]{"NearCombat","FleetMedium","FleetPanorama"}){PhaseCamera(phase,ordered[0]);yield return Capture("view-"+phase,"Actual unchanged meshes, solar display and camera; no compositing.");}
            Vector3 direction=SunDirection();var first=ordered[0];
            Aim(first.transform.position-direction*80,first.transform.position);
            yield return Capture("sun-occluded-by-hull","Flare source and solar centre aligned behind an existing opaque interactive ship; SRP depth occlusion enabled.");
            yield return FlarePair("occluded");
            Aim(first.transform.position-direction*80+Vector3.up*32,first.transform.position+Vector3.up*32);
            yield return Capture("sun-unoccluded","Same solar source, camera shifted above the actual hull; SRP flare visible again.");
            yield return FlarePair("unoccluded");
            PhaseCamera("NearCombat",first);SweepTarget(first,true);yield return new WaitForSeconds(.22f);
            yield return Capture("continuous-explosion","Existing pooled reactor explosion after real swept collision.",false);
            if(hud!=null)hud.enabled=oldHudEnabled;
        }
        IEnumerator FlarePair(string label)
        {
            var sun=FindAnyObjectByType<SunDisplayRig>();bool oldFlare=sun.flareEnabled;mission.TogglePause();
            try
            {
                sun.flareEnabled=false;for(int i=0;i<12;i++)yield return null;
                yield return Capture("flare-"+label+"-OFF","Frozen geometry, source noise time, camera and exposure. Only SRP lens flare disabled.");
                sun.flareEnabled=true;for(int i=0;i<12;i++)yield return null;
                yield return Capture("flare-"+label+"-ON","Identical frozen scene. SRP flare enabled and depth occlusion settled for 12 rendered frames.");
            }
            finally {sun.flareEnabled=oldFlare;if(mission.State==MissionState.Paused)mission.TogglePause();}
        }

        IEnumerator GameplayChecks()
        {
            SetPhase("2000 actual identities, full path activation and gameplay regression");mission.Restart();mission.StartMission();
            var manager=FindAnyObjectByType<FleetRenderManager>();var first=ordered[0];
            AddCheck("2000 unique stable identities",ordered.Select(t=>t.targetId).Distinct().Count()==2000,"All mission members have independent IDs");
            SweepTarget(first,true);yield return new WaitForSeconds(.22f);
            AddCheck("Normal fixed-size motor steps destroy and score",first.IsDestroyed&&mission.score.Score>0,"Actual .02 second motor steps through hull");
            manager.RefreshNow();AddCheck("Destroyed ship removed from instance batches",!manager.IsInstanced(first)&&!first.visualRoot.activeInHierarchy&&first.hitVolumes.All(c=>!c.enabled),first.targetId);
            mission.TogglePause();Vector3 position=mission.motor.transform.position;float remaining=mission.Remaining;int score=mission.score.Score;float fxAge=explosions.ElapsedSimulationTime;
            yield return new WaitForSecondsRealtime(.3f);mission.Step(1,new FlightCommand{throttle=1,boost=true});
            AddCheck("Pause freezes motor timer score and effects",mission.State==MissionState.Paused&&position==mission.motor.transform.position&&remaining==mission.Remaining&&score==mission.score.Score&&fxAge==explosions.ElapsedSimulationTime,".3 seconds wall time plus attempted Step");mission.TogglePause();
            mission.Restart();mission.StartMission();
            var columns=ordered.GroupBy(t=>t.targetId.Substring(0,t.targetId.LastIndexOf('R'))).Select(g=>g.OrderBy(t=>t.transform.position.z).ToArray()).ToArray();
            AddCheck("Exactly 100 columns of 20 ships",columns.Length==100&&columns.All(c=>c.Length==20),"50 columns x 2 layers");
            foreach(var column in columns){SweepColumn(column);yield return null;}
            AddCheck("Every distant ship is reachable and destructible",mission.Won&&mission.State==MissionState.Results&&mission.DestroyedCount==2000,"100 diagnostic long motor segments cover all 2000 ships, including streamed colliders");
            score=mission.score.Score;mission.Step(1,default);mission.StartMission();AddCheck("Results score commits once",mission.ResultTransitions==1&&score==mission.score.Score,"Victory and post-results step");
            yield return Capture("victory-2000","All 2000 destroyed through actual motor sweeps; diagnostics use long proposed segments and column-start teleports.");
            for(int n=0;n<3;n++)
            {mission.Restart();yield return null;manager.RefreshNow();bool clean=ordered.All(t=>!t.IsDestroyed)&&mission.score.Score==0&&mission.DestroyedCount==0&&mission.Remaining==mission.settings.missionSeconds&&mission.State==MissionState.Ready&&Time.timeScale==1&&explosions.ActiveEffectCount==0&&explosions.ActiveLightCount==0&&explosions.PoolInstanceCount==reactorPool&&chrome.ActiveExplosionSources==0;
            AddCheck("Restart "+(n+1)+" restores 2000 states and clears pooled effects",clean,"Near/instanced visibility is intentionally chosen by distance; no stale destroyed state");if(n<2){mission.StartMission();SweepTarget(first,true);yield return null;}}
            mission.StartMission();foreach(var column in columns)SweepColumn(column);
            AddCheck("Same-frame 2000 hits retain victory and bounded pools",mission.Won&&mission.DestroyedCount==2000&&explosions.ActiveEffectCount<=explosions.Capacity&&explosions.ActiveLightCount<=2&&explosions.PoolInstanceCount==reactorPool,"Dropped optional effects="+explosions.DroppedEffectCount);
            mission.Restart();mission.StartMission();bool enabled=explosions.enabled;explosions.enabled=false;SweepTarget(first,true);AddCheck("Optional effects disabled preserve scoring",first.IsDestroyed&&mission.score.Score>0,"Original hit and score authority");explosions.enabled=enabled;
            mission.Restart();mission.StartMission();mission.Step(mission.Remaining, new FlightCommand{brake=true});
            AddCheck("Timeout resolves once",mission.State==MissionState.Results&&!mission.Won&&mission.ResultTransitions==1,"Diagnostic full remaining-time step with stationary braking");
            mission.Restart();PhaseCamera("FleetPanorama",ordered[0]);yield return Capture("restarted-ready","Saved overview, full 2000 fleet, clean 90 minute mission and no retained explosion lights");
        }
        void SweepColumn(ShipTarget[] column)
        {
            Vector3 start=column[0].transform.position-Vector3.forward*70;int before=mission.DestroyedCount,score=mission.score.Score;
            mission.motor.ResetPose(start,Quaternion.identity);Physics.SyncTransforms();teleportCount++;if(mission.DestroyedCount!=before||score!=mission.score.Score)teleportErrors++;
            float length=column[column.Length-1].transform.position.z-start.z+70;
            mission.Step(length/(mission.settings.maxCruiseSpeed*mission.settings.boostMultiplier),new FlightCommand{throttle=1,boost=true});
            AddCheck("Full swept column "+column[0].targetId,column.All(t=>t.IsDestroyed),"20 targets across "+length+" metres in one proposed motor segment");
        }
        void SweepTarget(ShipTarget target,bool traverseWholeHull)
        {
            if(target==null||target.IsDestroyed||mission.State!=MissionState.Playing)return;
            Vector3 start=target.transform.position-Vector3.forward*50;int before=mission.DestroyedCount,score=mission.score.Score;
            mission.motor.ResetPose(start,Quaternion.identity);Physics.SyncTransforms();teleportCount++;if(before!=mission.DestroyedCount||score!=mission.score.Score)teleportErrors++;
            for(int i=0;i<500&&mission.State==MissionState.Playing;i++){mission.Step(.02f,new FlightCommand{throttle=1,boost=true});if(target.IsDestroyed&&(!traverseWholeHull||mission.motor.transform.position.z>target.transform.position.z+50))break;}
            if(!target.IsDestroyed)throw new InvalidOperationException("Missed actual swept ship "+target.targetId);
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
            var fleet = FindAnyObjectByType<FleetRenderManager>();
            return new Inventory
            {
                sceneRenderers = renderers.Length, activeRenderers = active, visibleRenderers = visible, uniqueSharedMaterials = materials.Count,
                fullPrefabs=fleet.FullPrefabCount,instancedShips=fleet.InstancedShipCount,interactionShips=fleet.InteractionShipCount,visibleInstances=fleet.VisibleInstancedShips,lod1Instances=fleet.LastLod1Ships,lod2Instances=fleet.LastLod2Ships,instanceDrawCalls=fleet.LastDrawCalls,chunks=fleet.ChunkCount,
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
        void SetPhase(string phase) { CurrentPhase = phase; PhaseFrame = 0; WriteProgress(); Debug.Log("FLEET 2000 VALIDATION " + report.label + ": " + phase); }
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
                Safe(() => { mission.enabled = oldMissionEnabled; mission.chaseCamera.enabled = oldChaseEnabled; if (hud != null) hud.enabled = oldHudEnabled;if(overview!=null)overview.enabled=oldOverviewEnabled; });
                Safe(() => { if (mission.input != null) { mission.input.PauseRequested = pauseCallback; mission.input.StartRequested = startCallback; mission.input.RestartRequested = restartCallback; mission.input.enabled = oldInputEnabled; } });
                Safe(() => { QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldCap; });
            }
            Safe(() => Application.runInBackground = oldBackground);
            if (report != null)
            {
                report.phases = phases.ToArray(); report.checks = checks.ToArray(); report.screenshots = shots.ToArray();
                report.allChecksPassed = report.completed && string.IsNullOrEmpty(report.error) && checks.All(c => c.passed);
                LastReport = report; LastReportPath = Path.Combine(report.outputDirectory, "fleet2000-validation.json");
                try { File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true), Encoding.UTF8); File.WriteAllText(Path.Combine(report.outputDirectory, "frames.csv"), csv.ToString(), Encoding.UTF8); }
                catch (Exception ex) { Debug.LogError("Visual upgrade report could not be written: " + ex); }
                CurrentPhase = report.allChecksPassed ? "Completed" : "Finished with failures";
            }
            IsRunning = false; saved = false; routine = null; inspectionOnly = false; WriteProgress();
            Debug.Log("FLEET 2000 VALIDATION COMPLETE: " + LastReportPath + ", checks=" + (report != null && report.allChecksPassed));
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
