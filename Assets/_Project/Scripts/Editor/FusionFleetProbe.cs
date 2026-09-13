using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace DropletPrototype.Editor
{
    // Explicit, temporary Play-mode diagnostic. Never saved in a scene or player.
    public sealed class FusionFleetProbe
    {
        [Serializable] public class Phase
        {
            public string name; public int frames,width,height,targets,renderers,drawCalls,triangles,effectsPeak,pool;
            public float meanMs,p50Ms,p95Ms,p99Ms,mainMeanMs,gcMeanBytes;
            public bool mainAvailable,gcAvailable; public long allocated,reserved,mono;
            public Vector3 cameraPosition,cameraForward; public int[] selectedLods; public long selectedFrustumTriangles;
        }
        [Serializable] public class Report
        {
            public string scene,utc,unity,cpu,gpu,api,scope,error;public bool completed; public Phase[] phases;
        }
        readonly List<Phase> phases = new List<Phase>();
        static FusionFleetProbe active;
        MissionController mission;
        MissionEffects effects;
        DropletInput capturedInput;
        ChaseCamera capturedCamera;
        Camera view;
        Report report;
        string prefix;
        ProfilerRecorder main, gc;
        Coroutine runningCoroutine;
        IEnumerator guardedRoutine;
        bool capturedState, missionWasEnabled, inputWasEnabled, cameraWasEnabled;
        bool previousRunInBackground, finished;

        [MenuItem("DropletPrototype/Fleet Expansion/8 Measure Current Play Scene")]
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before measurement.");
            if (active != null) throw new InvalidOperationException("A fleet measurement is already running.");
            var host = UnityEngine.Object.FindAnyObjectByType<MissionController>();
            if (host == null) throw new InvalidOperationException("The current Play scene has no mission controller.");
            var probe = new FusionFleetProbe
            {
                mission = host,
                prefix = host.gameObject.scene.name.Contains("Expanded") ? "After" : "Before",
                report = new Report
                {
                    scene = host.gameObject.scene.path,
                    utc = DateTime.UtcNow.ToString("o"),
                    unity = Application.unityVersion,
                    cpu = SystemInfo.processorType,
                    gpu = SystemInfo.graphicsDeviceName,
                    api = SystemInfo.graphicsDeviceType.ToString(),
                    scope = "Editor Play mode only. Same actual GameView dimensions and camera offsets in both scenes. Time.unscaledDeltaTime includes Editor overhead/pacing; Main Thread and GC markers include editor work, not isolated player costs. Native selected LOD/frustum triangle estimate is not GPU submission. Dense phase uses explicit ResetPose attack starts followed by real fixed motor steps, not a continuous navigation route."
                }
            };
            active = probe;
            EditorApplication.playModeStateChanged += probe.OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += probe.OnBeforeAssemblyReload;
            probe.guardedRoutine = probe.Guarded(probe.Start());
            try { probe.runningCoroutine = host.StartCoroutine(probe.guardedRoutine); }
            catch (Exception exception)
            {
                probe.RecordFailure(exception);
                probe.Finish();
            }
        }

        // Unity normally owns yielded nested iterators. Driving that stack here lets
        // one guard observe failures in Measure as well as failures in Start.
        // The yield is inside try/finally only, never inside a try/catch block.
        IEnumerator Guarded(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            try
            {
                while (stack.Count > 0 && !finished)
                {
                    IEnumerator iterator = stack.Peek();
                    bool moved = false;
                    object current = null;
                    Exception failure = null;
                    try
                    {
                        moved = iterator.MoveNext();
                        if (moved) current = iterator.Current;
                    }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null) { RecordFailure(failure); break; }
                    if (!moved)
                    {
                        stack.Pop();
                        DisposeIterator(iterator);
                        if (!string.IsNullOrEmpty(report.error)) break;
                        continue;
                    }
                    if (current is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return current;
                }
                if (!finished && stack.Count == 0 && string.IsNullOrEmpty(report.error))
                    report.completed = true;
            }
            finally
            {
                while (stack.Count > 0) DisposeIterator(stack.Pop());
                if (!finished && !report.completed && string.IsNullOrEmpty(report.error))
                    RecordFailure(new OperationCanceledException("Fleet measurement stopped before all phases completed."));
                Finish();
            }
        }

        void DisposeIterator(IEnumerator iterator)
        {
            try { (iterator as IDisposable)?.Dispose(); }
            catch (Exception exception) { RecordFailure(exception); }
        }

        void RecordFailure(Exception exception)
        {
            report.completed = false;
            report.error = string.IsNullOrEmpty(report.error) ? exception.ToString() : report.error + "\n" + exception;
        }

        void CleanupAction(Action action)
        {
            try { action(); }
            catch (Exception exception) { RecordFailure(exception); }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            CleanupAction(() => main.Dispose());
            CleanupAction(() => gc.Dispose());
            if (capturedState)
            {
                // Restart clears committed test destruction and temporary feedback.
                // Each restoration is independent so a faulty listener cannot skip
                // the remaining component or application-state restoration.
                CleanupAction(() => { if (mission != null) mission.Restart(); });
                CleanupAction(() => { if (capturedCamera != null) capturedCamera.enabled = cameraWasEnabled; });
                CleanupAction(() => { if (capturedInput != null) capturedInput.enabled = inputWasEnabled; });
                CleanupAction(() => { if (mission != null) mission.enabled = missionWasEnabled; });
                CleanupAction(() => Application.runInBackground = previousRunInBackground);
            }
            report.phases = phases.ToArray();
            string directory = "docs/verification/FleetExpansion/";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(directory + prefix + "-performance.json", JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
                Debug.LogError("Unable to save fleet measurement report: " + report.error);
            }
            finally { if (ReferenceEquals(active, this)) active = null; }
            if (report.completed) Debug.Log("FLEET MEASUREMENT COMPLETE " + prefix);
            else Debug.LogError("FLEET MEASUREMENT FAILED " + prefix + ": " + report.error);
        }

        void Cancel(string reason)
        {
            if (finished) return;
            RecordFailure(new OperationCanceledException(reason));
            CleanupAction(() => { if (mission != null && runningCoroutine != null) mission.StopCoroutine(runningCoroutine); });
            // Explicit disposal also covers editor shutdown/reload paths where a
            // normal MonoBehaviour OnDestroy callback cannot exist on this class.
            DisposeIterator(guardedRoutine);
            Finish();
        }

        void OnBeforeAssemblyReload() => Cancel("Editor assembly reload interrupted the fleet measurement.");
        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Cancel("Leaving Play mode interrupted the fleet measurement.");
        }

        IEnumerator Start()
        {
            effects = UnityEngine.Object.FindAnyObjectByType<MissionEffects>();
            capturedInput = mission.input;
            capturedCamera = mission.chaseCamera;
            if (effects == null || capturedInput == null || capturedCamera == null)
                throw new InvalidOperationException("Measurement requires the mission's input, camera and shared effects.");
            view = capturedCamera.GetComponent<Camera>();
            if (view == null) throw new InvalidOperationException("The chase camera has no Camera component.");
            missionWasEnabled = mission.enabled;
            inputWasEnabled = capturedInput.enabled;
            cameraWasEnabled = capturedCamera.enabled;
            previousRunInBackground = Application.runInBackground;
            capturedState = true;
            Application.runInBackground = true;
            mission.enabled = false;
            capturedInput.enabled = false;
            capturedCamera.enabled = false;
            effects.InitializePool();
            mission.Restart();
            try { main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); } catch (Exception) { }
            try { gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); } catch (Exception) { }
            yield return Measure("Near", false);
            yield return Measure("Panorama", false);
            yield return Measure("Penetrations", true);
        }
        IEnumerator Measure(string label,bool attack)
        {
            mission.Restart();mission.StartMission();
            var first=mission.targets.OrderBy(t=>Vector3.Distance(t.transform.position,mission.spawnPosition)).First();
            if(label=="Panorama")view.transform.SetPositionAndRotation(new Vector3(950,720,-1150),Quaternion.LookRotation(new Vector3(0,40,600)-new Vector3(950,720,-1150)));
            else view.transform.SetPositionAndRotation(first.transform.position+new Vector3(19,10,-30),Quaternion.LookRotation(first.transform.position-(first.transform.position+new Vector3(19,10,-30))));
            mission.motor.ResetPose(new Vector3(0,8,-200),Quaternion.identity);
            for(int i=0;i<45;i++)yield return null;
            var frames=new float[300];var mains=new float[300];var gcs=new float[300];int peak=0;
            var ordered=mission.targets.OrderBy(t=>t.targetId).ToArray();int next=0;
            for(int i=0;i<frames.Length;i++)
            {
                if(attack && i%12==0 && next<ordered.Length)
                {
                    var t=ordered[next++];mission.motor.ResetPose(t.transform.position-t.transform.forward*18,t.transform.rotation);
                    for(int n=0;n<100 && !t.IsDestroyed;n++)mission.Step(.02f,new FlightCommand{throttle=1,boost=true});
                    view.transform.SetPositionAndRotation(t.transform.position+new Vector3(19,10,-30),Quaternion.LookRotation(-new Vector3(19,10,-30)));
                }
                yield return null;
                frames[i]=Time.unscaledDeltaTime*1000;mains[i]=main.Valid?main.LastValue/1000000f:0;gcs[i]=gc.Valid?gc.LastValue:0;peak=Math.Max(peak,effects.ActiveEffectCount);
            }
            var phase=new Phase{name=label,frames=frames.Length,width=Screen.width,height=Screen.height,targets=mission.TotalCount,renderers=mission.targets.Sum(t=>t.GetComponentsInChildren<Renderer>(true).Length),meanMs=frames.Average(),mainMeanMs=mains.Average(),gcMeanBytes=gcs.Average(),mainAvailable=main.Valid,gcAvailable=gc.Valid,allocated=Profiler.GetTotalAllocatedMemoryLong(),reserved=Profiler.GetTotalReservedMemoryLong(),mono=Profiler.GetMonoUsedSizeLong(),drawCalls=UnityStats.drawCalls,triangles=UnityStats.triangles,effectsPeak=peak,pool=effects.PoolInstanceCount,cameraPosition=view.transform.position,cameraForward=view.transform.forward};
            Array.Sort(frames);phase.p50Ms=frames[149];phase.p95Ms=frames[284];phase.p99Ms=frames[296];
            NativeLods(view,mission.targets,out phase.selectedLods,out phase.selectedFrustumTriangles);
            phases.Add(phase);
            yield return null;
            ScreenCapture.CaptureScreenshot("docs/verification/FleetExpansion/"+prefix+"-"+label+"-Game.png");
            yield return null;
        }
        public static void NativeLods(Camera camera,ShipTarget[] targets,out int[] counts,out long triangles)
        {
            counts=new int[4];triangles=0;var planes=GeometryUtility.CalculateFrustumPlanes(camera);
            var method=typeof(LODUtility).GetMethod("CalculateVisualizationData",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            foreach(var t in targets)
            {
                if(t.IsDestroyed)continue;
                var g=t.GetComponentInChildren<LODGroup>();Renderer[] rs;
                if(g==null){counts[0]++;rs=t.visualRoot.GetComponentsInChildren<Renderer>();}
                else
                {
                    var native=method.Invoke(null,new object[]{camera,g,-1});
                    int index=(int)native.GetType().GetField("activeLODLevel",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(native);
                    if(index<0||index>=g.lodCount){counts[3]++;continue;}counts[index]++;rs=g.GetLODs()[index].renderers;
                }
                foreach(var r in rs)if(r!=null&&GeometryUtility.TestPlanesAABB(planes,r.bounds)){var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh!=null)for(int j=0;j<mesh.subMeshCount;j++)triangles+=mesh.GetIndexCount(j)/3;}
            }
        }
    }
}
