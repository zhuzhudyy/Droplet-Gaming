// This inspector uses native APIs present in installed Unity 6000.5.10f1.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    [InitializeOnLoad]
    public static class SpaceEnvironmentFrameProbe
    {
        const string ReviewScene = "Assets/_Project/Scenes/SpaceEnvironment_Review.unity";
        const string Evidence = "docs/verification/SpaceEnvironment/";
        const string InternalPrefix = "UnityEditorInternal.FrameDebuggerInternal.";
        const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        const BindingFlags AnyStatic = PublicStatic | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static Type utilityType, eventDataType, debuggerType;
        static EditorWindow gameView, ownedDebuggerWindow;
        static bool ownsCapture, oldPaused, collecting, pendingCollect;
        static double started, eventStarted;
        static int eventIndex, ticks, capturedHash;
        static Array rawEvents;
        static CaptureReport report;
        static string status = "Idle";
        static readonly List<EventRecord> records = new List<EventRecord>();

        static SpaceEnvironmentFrameProbe()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisableCapture;
            EditorApplication.quitting += DisableCapture;
        }

        [Serializable] public class CameraRecord
        {
            public string name, path;
            public bool enabled, orthographic;
            public float fov, near, far, aspect, orthographicSize;
            public int pixelWidth, pixelHeight, cullingMask;
            public Vector3 position, forward, up;
        }
        [Serializable] public class EventRecord
        {
            public int index, returnedEventIndex, indexCount, vertexCount, instanceCount, drawCallCount, meshSubset;
            public bool validData, isGeometryDrawEvent, attributedToEnvironment, allBatchMeshPathsAreEnvironment;
            public string type, name, eventObjectName, eventObjectPath, componentEntityId, componentPath;
            public string meshName, meshPath, meshEntityId, passName, passLightMode, shaderName, unavailableReason;
            public string[] meshEntityIds, meshPaths;
        }
        [Serializable] public class CaptureReport
        {
            public string label, utc, unity, context, interpretation, status;
            public bool completed, allEventDataValid, sourceSceneWasDirty, wasPlaying, wasPaused;
            public int eventCount, validEvents, invalidEvents, initialEventsHash, finalEventsHash;
            public int gameViewStatsTrianglesAtBegin, gameViewStatsDrawCallsAtBegin;
            public long attributedRawIndexCount, attributedRawVertexCount;
            public CameraRecord[] cameras;
            public EventRecord[] events;
            public string[] warnings;
        }
        [Serializable] public class NativeGroupRecord
        {
            public string camera, group, path;
            public int activeLODLevel, lodCount, nativeSelectedTriangleCount, nativeSelectedVertexCount;
            public float activeRelativeScreenSize, activePixelSize, activeDistance, worldSpaceSize, activeLODFade;
            public bool nativeLodCulled, selectedRendererIntersectsFrustum;
            public long selectedMeshTriangles;
            public float[] transitionHeights;
            public string[] selectedMeshPaths;
        }
        [Serializable] public class NativeViewRecord
        {
            public CameraRecord camera;
            public int lod0, lod1, lod2, nativeLodCulled, frustumExcluded;
            public long nativeSelectedFrustumMeshTriangleEstimate;
            public NativeGroupRecord[] groups;
        }
        [Serializable] public class NativeDistanceRecord
        {
            public string asset, expectation;
            public bool matchesExpectedLevel, beyondAuthoredReferenceFarClip;
            public float requestedRelativeHeight, diagnosticDistance;
            public CameraRecord camera;
            public NativeGroupRecord result;
        }
        [Serializable] public class NativeReport
        {
            public string utc, unity, context, interpretation;
            public bool allRepresentativeLevelsVerified, temporaryPreviewSceneClosed;
            public float lodBias;
            public int maximumLODLevel;
            public NativeViewRecord[] views;
            public NativeDistanceRecord[] representativeDistances;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        static void InitializeReflection()
        {
            if (utilityType != null) return;
            utilityType = typeof(UnityEditor.Editor).Assembly.GetType(InternalPrefix + "FrameDebuggerUtility", true);
            eventDataType = typeof(UnityEditor.Editor).Assembly.GetType(InternalPrefix + "FrameDebuggerEventData", true);
            debuggerType = typeof(Camera).Assembly.GetType("UnityEngine.FrameDebugger", true);
        }
        static object Invoke(string name, params object[] args)
        {
            InitializeReflection();
            var method = utilityType.GetMethods(AnyStatic).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
            return method.Invoke(null, args);
        }
        static T UtilityProperty<T>(string name)
        {
            InitializeReflection();
            return (T)utilityType.GetProperty(name, AnyStatic).GetValue(null);
        }
        static T Field<T>(object value, string name, T fallback = default(T))
        {
            if (value == null) return fallback;
            var field = value.GetType().GetField(name, AnyInstance);
            return field == null ? fallback : (T)field.GetValue(value);
        }
        static object RawField(object value, string name)
        {
            return value?.GetType().GetField(name, AnyInstance)?.GetValue(value);
        }
        static bool DebuggerEnabled()
        {
            InitializeReflection();
            return (bool)debuggerType.GetProperty("enabled", AnyStatic).GetValue(null);
        }
        static string ObjectPath(UnityEngine.Object value)
        {
            if (value == null) return "";
            string asset = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(asset)) return asset;
            Transform t = value is GameObject go ? go.transform : (value as Component)?.transform;
            if (t == null) return value.name;
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
        static UnityEngine.Object ResolveEntity(object entity)
        {
            if (entity == null) return null;
            var method = typeof(EditorUtility).GetMethods(AnyStatic).FirstOrDefault(m =>
                m.Name == "EntityIdToObject" && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsInstanceOfType(entity));
            return method?.Invoke(null, new[] { entity }) as UnityEngine.Object;
        }
        static CameraRecord CameraInfo(Camera camera)
        {
            return new CameraRecord { name = camera.name, path = ObjectPath(camera), enabled = camera.enabled,
                orthographic = camera.orthographic, fov = camera.fieldOfView, near = camera.nearClipPlane,
                far = camera.farClipPlane, aspect = camera.aspect, orthographicSize = camera.orthographicSize,
                pixelWidth = camera.pixelWidth, pixelHeight = camera.pixelHeight, cullingMask = camera.cullingMask,
                position = camera.transform.position, forward = camera.transform.forward, up = camera.transform.up };
        }
        static void ReviewGuard()
        {
            Require(SceneManager.GetActiveScene().path == ReviewScene, "Open the isolated SpaceEnvironment_Review scene.");
            Require(GameObject.Find("SpaceEnvironment") != null, "The saved environment prefab is missing.");
        }
        static void RepaintCapture()
        {
            typeof(EditorApplication).GetMethod("SetSceneRepaintDirty",AnyStatic)?.Invoke(null,null);
            if (gameView != null) gameView.Repaint();
            if (ownedDebuggerWindow != null) ownedDebuggerWindow.Repaint();
            // A background Editor can defer normal repaint requests indefinitely.
            // Send real IMGUI repaint events so GameView follows its actual native
            // camera-render path; this does not fabricate or estimate frame data.
            if(ownsCapture && gameView!=null)gameView.SendEvent(new Event{type=EventType.Repaint});
            if(ownsCapture && ownedDebuggerWindow!=null)ownedDebuggerWindow.SendEvent(new Event{type=EventType.Repaint});
            EditorApplication.QueuePlayerLoopUpdate();
        }
        [MenuItem("DropletPrototype/Space Environment/Frame Capture Reference")]
        public static void CaptureReferenceMenu()=>BeginViewCapture("ReferenceCamera");
        [MenuItem("DropletPrototype/Space Environment/Frame Capture Side")]
        public static void CaptureSideMenu()=>BeginViewCapture("SideCamera");
        [MenuItem("DropletPrototype/Space Environment/Frame Capture Top")]
        public static void CaptureTopMenu()=>BeginViewCapture("TopCamera");
        [MenuItem("DropletPrototype/Space Environment/Frame Capture Gameplay Turn")]
        public static void CaptureGameplayMenu()=>BeginViewCapture("GameplayTurnCamera");
        [MenuItem("DropletPrototype/Space Environment/Frame Capture Belt Close")]
        public static void CaptureBeltMenu()=>BeginViewCapture("BeltCloseCamera");
        static void BeginViewCapture(string name)
        {
            ReviewGuard();Require(!ownsCapture && !DebuggerEnabled(),"Finish existing capture first.");
            foreach(var camera in GameObject.Find("PreviewOnly_SpaceEnvironment_v1").GetComponentsInChildren<Camera>())camera.enabled=camera.name==name;
            BeginCapture(name);
        }

        // Root chooses the enabled camera before calling. This does not alter its pose,
        // material, LOD configuration or target texture. It refuses another owner's debugger.
        public static void BeginCapture(string label)
        {
            ReviewGuard(); InitializeReflection();
            Require(!ownsCapture && !collecting, "This probe already owns a capture; collect or disable it first.");
            Require(!DebuggerEnabled(), "Frame Debugger is already enabled; this probe will not replace another capture.");
            Require(UtilityProperty<bool>("locallySupported"), "Local Frame Debugger is unsupported on this graphics device.");
            Require(!string.IsNullOrWhiteSpace(label) && label.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'), "Use a simple capture label containing letters/digits/_/-.");
            Camera[] cameras = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
            Require(cameras.Length == 1, "Enable exactly one review camera to keep frame attribution explicit.");
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView", true);
            gameView = EditorWindow.GetWindow(type);
            gameView.Show(); gameView.Focus(); gameView.Repaint();
            var windowType=typeof(UnityEditor.Editor).Assembly.GetTypes().First(t=>t.Name=="FrameDebuggerWindow");
            ownedDebuggerWindow=(EditorWindow)ScriptableObject.CreateInstance(windowType);ownedDebuggerWindow.ShowUtility();
            oldPaused = EditorApplication.isPaused;
            report = new CaptureReport { label = label, utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                context = EditorApplication.isPlaying ? "Editor Play Mode Game View" : "Editor Edit Mode Game View",
                sourceSceneWasDirty = SceneManager.GetActiveScene().isDirty,
                wasPlaying = EditorApplication.isPlaying, wasPaused = oldPaused,
                gameViewStatsTrianglesAtBegin = UnityStats.triangles,
                gameViewStatsDrawCallsAtBegin = UnityStats.drawCalls,
                cameras = cameras.Select(CameraInfo).ToArray(),
                interpretation = "Actual Frame Debugger events from the visible Game View. Raw index/vertex/instance/draw counts are retained. Raw index totals are NOT automatically multiplied by instance count or reported as submitted triangles: batch semantics require a separate cross-check. Camera settings and event object/mesh paths establish scope; all passes are retained.",
                warnings = new[] { "UnityStats at Begin is an adjacent Editor snapshot, not an attributed per-camera submitted-triangle measurement.",
                    "Internal frame data can arrive after repaint. Events with missing/stale data remain explicitly invalid.",
                    "The environment has no shadows; any repeated mesh pass is retained, not silently deduplicated.",
                    "This installed version has no EnterCapturingScope; captures use the Game View player-loop scope." } };
            records.Clear(); rawEvents = null;
            try
            {
                if (EditorApplication.isPlaying) EditorApplication.isPaused = true;
                ownsCapture = true;
                ownedDebuggerWindow.GetType().GetMethod("EnableFrameDebugger",AnyInstance).Invoke(ownedDebuggerWindow,null);
                Invoke("SetEnabled",true,UnityEditorInternal.ProfilerDriver.connectedProfiler);
                started = EditorApplication.timeSinceStartup;
                status = "Capture enabled; allow Game View repaints, then call CollectCapture().";
                RepaintCapture();
            }
            catch
            {
                DisableCapture();
                throw;
            }
            Debug.Log(status);
        }

        // Calls return immediately. Collection advances one selected event at a time
        // through Editor updates, because GetFrameEventData may otherwise be stale.
        [MenuItem("DropletPrototype/Space Environment/Collect Frame Events")]
        public static void CollectCapture()
        {
            Require(ownsCapture && DebuggerEnabled(), "BeginCapture must own an enabled capture first.");
            Require(!collecting, "Event collection is already running.");
            rawEvents = (Array)Invoke("GetFrameEvents");
            if (rawEvents.Length == 0 || !DebuggerWindowReady())
            {
                if(!pendingCollect){pendingCollect=true;EditorApplication.update+=AwaitFrameEvents;}
                RepaintCapture();
                status="Waiting for Game View frame events before collection.";
                return;
            }
            capturedHash = UtilityProperty<int>("eventsHash");
            report.initialEventsHash = capturedHash; report.eventCount = rawEvents.Length;
            collecting = true; eventIndex = 0; records.Clear();
            SelectEvent();
            EditorApplication.update += CollectUpdate;
            status = "Collecting 0 / " + rawEvents.Length + " actual frame events.";
        }
        static void AwaitFrameEvents()
        {
            if(!ownsCapture||!DebuggerEnabled()){EditorApplication.update-=AwaitFrameEvents;pendingCollect=false;return;}
            if(EditorApplication.timeSinceStartup-started>60){DisableCapture();Debug.LogError("No Game View events became available within 60 seconds; no measurement recorded.");return;}
            RepaintCapture();
            if(((Array)Invoke("GetFrameEvents")).Length==0||!DebuggerWindowReady())return;
            EditorApplication.update-=AwaitFrameEvents;pendingCollect=false;
            CollectCapture();
        }
        static bool DebuggerWindowReady()=>ownedDebuggerWindow!=null &&
            Field(ownedDebuggerWindow,"m_EnablingWaitCounter",0)>=4 && RawField(ownedDebuggerWindow,"m_TreeView")!=null;
        static void SelectEvent()
        {
            // Keep the debugger tree and its native replay limit synchronized.
            // Setting only the native property lets the window repaint restore
            // its previous selection, producing stale GetFrameEventData values.
            ownedDebuggerWindow.GetType().GetMethod("ChangeFrameEventLimit", AnyInstance,
                null, new[] { typeof(int) }, null).Invoke(ownedDebuggerWindow, new object[] { eventIndex + 1 });
            // External limit changes happen before OnGUI's old/new comparison.
            // Explicitly request the same four replay frames the official GUI uses.
            ownedDebuggerWindow.GetType().GetMethod("RepaintOnLimitChange", AnyInstance).Invoke(ownedDebuggerWindow,null);
            ticks = 0; eventStarted = EditorApplication.timeSinceStartup;
            RepaintCapture();
        }
        static EventRecord ReadEvent(int index, object data, bool valid, string reason)
        {
            object evt = rawEvents.GetValue(index);
            var eventObject = Invoke("GetFrameEventObject", index) as UnityEngine.Object;
            var mesh = Field<Mesh>(data, "m_Mesh");
            var ids = RawField(data, "m_MeshEntityIds") as Array;
            var idValues = ids == null ? new object[0] : ids.Cast<object>().ToArray();
            var paths = idValues.Select(ResolveEntity).Select(ObjectPath).ToArray();
            object componentId = RawField(data, "m_ComponentEntityId");
            var record = new EventRecord { index = index, type = RawField(evt, "m_Type")?.ToString() ?? "Unknown",
                name = Invoke("GetFrameEventInfoName", index) as string ?? "",
                eventObjectName = eventObject != null ? eventObject.name : "", eventObjectPath = ObjectPath(eventObject),
                validData = valid, unavailableReason = reason, returnedEventIndex = Field(data, "m_FrameEventIndex", -1),
                indexCount = Field(data, "m_IndexCount", -1), vertexCount = Field(data, "m_VertexCount", -1),
                instanceCount = Field(data, "m_InstanceCount", -1), drawCallCount = Field(data, "m_DrawCallCount", -1),
                meshSubset = Field(data, "m_MeshSubset", -1), meshName = mesh != null ? mesh.name : "", meshPath = ObjectPath(mesh),
                meshEntityId = RawField(data, "m_MeshEntityId")?.ToString() ?? "",
                componentEntityId = componentId?.ToString() ?? "", componentPath = ObjectPath(ResolveEntity(componentId)),
                meshEntityIds = idValues.Select(id => id.ToString()).ToArray(), meshPaths = paths,
                passName = Field(data, "m_PassName", ""), passLightMode = Field(data, "m_PassLightMode", ""),
                shaderName = Field(data, "m_RealShaderName", "") };
            const string modelPrefix = "Assets/_Project/Art/Environment/SpaceEnvironment/Models/";
            record.isGeometryDrawEvent = new[] { "Mesh", "InstancedMesh", "SRPBatch", "StaticBatch", "DynamicBatch", "HybridBatch",
                "DynamicGeometry", "GLDraw", "DrawProcedural", "DrawProceduralIndirect", "DrawProceduralIndexed", "DrawProceduralIndexedIndirect" }.Contains(record.type);
            record.allBatchMeshPathsAreEnvironment = paths.Length > 0 && paths.All(p => p.StartsWith(modelPrefix, StringComparison.Ordinal));
            // Clear/resolve/hierarchy events can retain previous native draw details.
            // Preserve their raw fields, but never include them in geometry totals.
            record.attributedToEnvironment = valid && record.isGeometryDrawEvent && (record.meshPath.StartsWith(modelPrefix, StringComparison.Ordinal) ||
                record.allBatchMeshPathsAreEnvironment || record.eventObjectPath.StartsWith("SpaceEnvironment/", StringComparison.Ordinal) ||
                record.componentPath.StartsWith("SpaceEnvironment/", StringComparison.Ordinal));
            return record;
        }
        static void CollectUpdate()
        {
            if (!collecting) return;
            try
            {
                Require(ownsCapture && DebuggerEnabled(), "Frame Debugger was disabled during collection.");
                Require(UtilityProperty<int>("eventsHash") == capturedHash, "Captured event list changed while collecting; discard this incomplete capture and retry.");
                Require(EditorApplication.timeSinceStartup - started < 240, "Frame event collection exceeded its four-minute safety bound.");
                if(UtilityProperty<int>("limit")!=eventIndex+1){SelectEvent();return;}
                RepaintCapture();
                if (++ticks < 3) return;
                object data = Activator.CreateInstance(eventDataType, true);
                bool valid = (bool)Invoke("GetFrameEventData", eventIndex, data);
                valid = valid && Field(data, "m_FrameEventIndex", -1) == eventIndex;
                if (!valid && EditorApplication.timeSinceStartup - eventStarted < 5.0) return;
                records.Add(ReadEvent(eventIndex, data, valid,
                    valid ? "" : "GetFrameEventData unavailable or returned a stale event after repaint retries."));
                eventIndex++;
                status = "Collecting " + eventIndex + " / " + rawEvents.Length + " actual frame events.";
                if (eventIndex < rawEvents.Length) SelectEvent();
                else FinishCapture(true, "Collection completed.");
            }
            catch (Exception exc)
            {
                FinishCapture(false, "Collection failed: " + exc.GetBaseException().Message);
            }
        }
        static void FinishCapture(bool completed, string message)
        {
            try
            {
                report.completed = completed; report.status = message; report.events = records.ToArray();
                report.validEvents = records.Count(e => e.validData); report.invalidEvents = report.eventCount - report.validEvents;
                report.allEventDataValid = completed && report.invalidEvents == 0;
                report.finalEventsHash = UtilityProperty<int>("eventsHash");
                report.attributedRawIndexCount = records.Where(e => e.attributedToEnvironment && e.indexCount >= 0).Sum(e => (long)e.indexCount);
                report.attributedRawVertexCount = records.Where(e => e.attributedToEnvironment && e.vertexCount >= 0).Sum(e => (long)e.vertexCount);
                Directory.CreateDirectory(Evidence);
                File.WriteAllText(Evidence + "frame-" + report.label + ".json", JsonUtility.ToJson(report, true));
                status = message + " Valid events: " + report.validEvents + " / " + report.eventCount + ". Saved frame-" + report.label + ".json";
                Debug.Log(status);
            }
            finally { DisableCapture(); }
        }
        public static string CaptureStatus() => status;
        [MenuItem("DropletPrototype/Space Environment/Frame Probe State")]
        public static void DumpCaptureState()
        {
            var parent=gameView==null?null:RawField(gameView,"m_Parent");
            var active=parent?.GetType().GetProperty("actualView",AnyInstance)?.GetValue(parent);
            File.WriteAllText(Evidence+"frame-probe-state.txt",$"status={status}\nowns={ownsCapture}; pending={pendingCollect}; collecting={collecting}; index={eventIndex}; ticks={ticks}\nenabled={DebuggerEnabled()}; events={((Array)Invoke("GetFrameEvents")).Length}; limit={UtilityProperty<int>("limit")}\nwindowCounter={Field(ownedDebuggerWindow,"m_EnablingWaitCounter",-1)}; windowTree={RawField(ownedDebuggerWindow,"m_TreeView")}\ngameView={gameView}; actualView={active}; same={ReferenceEquals(gameView,active)}\n");
        }
        [MenuItem("DropletPrototype/Space Environment/Disable Frame Capture")]
        public static void DisableCapture()
        {
            EditorApplication.update -= AwaitFrameEvents; pendingCollect=false;
            EditorApplication.update -= CollectUpdate; collecting = false;
            if (!ownsCapture) return;
            try { Invoke("SetEnabled", false, UnityEditorInternal.ProfilerDriver.connectedProfiler); }
            finally
            {
                ownsCapture = false;
                if(ownedDebuggerWindow!=null){ownedDebuggerWindow.Close();ownedDebuggerWindow=null;}
                if (EditorApplication.isPlaying) EditorApplication.isPaused = oldPaused;
                RepaintCapture();
            }
        }

        static MethodInfo NativeMethod(string name)
        {
            return typeof(LODUtility).GetMethod(name, AnyStatic) ?? throw new MissingMethodException("Installed Unity lacks LODUtility." + name);
        }
        static NativeGroupRecord NativeGroup(Camera camera, LODGroup group)
        {
            var method = NativeMethod("CalculateVisualizationData");
            object native = method.Invoke(null, new object[] { camera, group, -1 });
            int level = Field(native, "activeLODLevel", -1);
            var lods = group.GetLODs(); bool culled = level < 0 || level >= lods.Length;
            object selected = culled ? null : method.Invoke(null, new object[] { camera, group, level });
            var renderers = culled ? new Renderer[0] : lods[level].renderers.Where(r => r != null).ToArray();
            var meshes = renderers.Select(r => r.GetComponent<MeshFilter>()?.sharedMesh).Where(m => m != null).ToArray();
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return new NativeGroupRecord { camera = camera.name, group = group.name, path = ObjectPath(group),
                activeLODLevel = level, lodCount = lods.Length, nativeLodCulled = culled,
                activeRelativeScreenSize = Field(native, "activeRelativeScreenSize", 0f),
                activePixelSize = Field(native, "activePixelSize", 0f), activeDistance = Field(native, "activeDistance", 0f),
                worldSpaceSize = Field(native, "worldSpaceSize", 0f), activeLODFade = Field(native, "activeLODFade", 0f),
                nativeSelectedTriangleCount = Field(selected, "triangleCount", 0), nativeSelectedVertexCount = Field(selected, "vertexCount", 0),
                selectedRendererIntersectsFrustum = renderers.Any(r => GeometryUtility.TestPlanesAABB(planes, r.bounds)),
                selectedMeshTriangles = meshes.Sum(Triangles), selectedMeshPaths = meshes.Select(AssetDatabase.GetAssetPath).ToArray(),
                transitionHeights = lods.Select(l => l.screenRelativeTransitionHeight).ToArray() };
        }
        static long Triangles(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            return count;
        }
        [MenuItem("DropletPrototype/Space Environment/6 Native LOD Audit")]
        public static void NativeLodAudit()
        {
            ReviewGuard(); Require(!ownsCapture && !DebuggerEnabled(), "Disable Frame Debugger before native LOD diagnostics.");
            var root = GameObject.Find("SpaceEnvironment");
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            var preview = GameObject.Find("PreviewOnly_SpaceEnvironment_v1");
            Require(preview != null, "Missing review camera root.");
            string[] names = { "ReferenceCamera", "SideCamera", "TopCamera", "GameplayTurnCamera", "BeltCloseCamera" };
            var cameras = names.Select(n => preview.GetComponentsInChildren<Camera>(true).Single(c => c.name == n)).ToArray();
            var result = new NativeReport { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                context = EditorApplication.isPlaying ? "Editor Play Mode native LOD inspection" : "Editor Edit Mode native LOD inspection",
                interpretation = "activeLODLevel comes from installed native LODUtility.CalculateVisualizationData, the same API used by Unity's LODGroup inspector. It is an actual native LOD selection query, not a captured draw. Frustum-selected triangle sums are resource estimates, not submitted GPU triangles. Diagnostic camera distances may exceed the authored camera far clip; that is recorded explicitly. No ForceLOD, renderer, material, group, authored camera or scene asset changes are made.",
                lodBias = QualitySettings.lodBias, maximumLODLevel = QualitySettings.maximumLODLevel };
            result.views = cameras.Select(camera => {
                var recordsForView = groups.Select(g => NativeGroup(camera, g)).ToArray();
                return new NativeViewRecord { camera = CameraInfo(camera), groups = recordsForView,
                    lod0 = recordsForView.Count(r => r.activeLODLevel == 0 && !r.nativeLodCulled),
                    lod1 = recordsForView.Count(r => r.activeLODLevel == 1 && !r.nativeLodCulled),
                    lod2 = recordsForView.Count(r => r.activeLODLevel == 2 && !r.nativeLodCulled),
                    nativeLodCulled = recordsForView.Count(r => r.nativeLodCulled),
                    frustumExcluded = recordsForView.Count(r => !r.nativeLodCulled && !r.selectedRendererIntersectsFrustum),
                    nativeSelectedFrustumMeshTriangleEstimate = recordsForView.Where(r => r.selectedRendererIntersectsFrustum).Sum(r => r.selectedMeshTriangles) };
            }).ToArray();
            var distanceRecords = new List<NativeDistanceRecord>();
            Scene tempScene = EditorSceneManager.NewPreviewScene();
            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TEMP_NativeLodDiagnosticCamera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraObject, tempScene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.fieldOfView = cameras[0].fieldOfView; camera.aspect = 16f / 9f;
                camera.pixelRect = new Rect(0, 0, 1600, 900); camera.nearClipPlane = .01f;
                foreach (string asset in new[] { "Rock05", "Earth" })
                {
                    // Choose the smallest placed representative to keep diagnostic
                    // positions modest; it still references the actual saved group.
                    var group = groups.Where(g => g.name.StartsWith("ENV_" + asset + "_", StringComparison.Ordinal))
                        .OrderBy(g => g.size * g.transform.lossyScale.x).First();
                    var lods = group.GetLODs(); Require(lods.Length == 3, asset + " needs exactly three levels.");
                    float[] height = { Mathf.Min(.8f, lods[0].screenRelativeTransitionHeight * 2),
                        Mathf.Sqrt(lods[0].screenRelativeTransitionHeight * lods[1].screenRelativeTransitionHeight),
                        Mathf.Sqrt(lods[1].screenRelativeTransitionHeight * lods[2].screenRelativeTransitionHeight),
                        lods[2].screenRelativeTransitionHeight * .4f };
                    for (int i = 0; i < 4; i++)
                    {
                        float distance = QualitySettings.lodBias * (float)NativeMethod("CalculateDistance").Invoke(null, new object[] { camera, height[i], group });
                        Require(float.IsFinite(distance) && distance > 0, "Native LOD diagnostic distance is invalid.");
                        Vector3 reference = group.transform.TransformPoint(group.localReferencePoint);
                        camera.transform.SetPositionAndRotation(reference - Vector3.forward * distance, Quaternion.identity);
                        camera.farClipPlane = Mathf.Max(cameras[0].farClipPlane, distance * 2 + group.size * group.transform.lossyScale.x);
                        var native = NativeGroup(camera, group);
                        distanceRecords.Add(new NativeDistanceRecord { asset = asset, expectation = i == 3 ? "culled" : "LOD" + i,
                            requestedRelativeHeight = height[i], diagnosticDistance = distance,
                            beyondAuthoredReferenceFarClip = distance > cameras[0].farClipPlane,
                            matchesExpectedLevel = i == 3 ? native.nativeLodCulled : native.activeLODLevel == i,
                            camera = CameraInfo(camera), result = native });
                    }
                }
            }
            finally
            {
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                EditorSceneManager.ClosePreviewScene(tempScene);
                result.temporaryPreviewSceneClosed = true;
            }
            result.representativeDistances = distanceRecords.ToArray();
            result.allRepresentativeLevelsVerified = distanceRecords.Count == 8 && distanceRecords.All(r => r.matchesExpectedLevel);
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "native-lod-audit.json", JsonUtility.ToJson(result, true));
            Debug.Log("Native LOD audit saved: 5 review cameras, " + distanceRecords.Count + " diagnostic samples; all representative levels verified=" + result.allRepresentativeLevelsVerified);
            Require(result.allRepresentativeLevelsVerified, "Native LOD level sweep did not match all expected levels; inspect native-lod-audit.json.");
        }
    }
}
