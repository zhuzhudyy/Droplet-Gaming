using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DropletPrototype
{
    /// <summary>Explicit standalone QA. Never present in ordinary game startup.</summary>
    public sealed class EnhancementValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Phase { public string name, workload; public int frames, focusedFrames, width, height, ships, pending, escaped, fleeing; public double meanMs,p95Ms,maxMs,cpuMeanMs=-1,gpuMeanMs=-1; public int frameTimingSamples,invalidGpuTimingSamples; public bool volume; }
        [Serializable] public sealed class Report
        {
            public string unity,cpu,gpu,graphicsApi,scene,error,voiceStatus;
            public bool completed,allChecksPassed,editor;public int identities,width,height;
            public float renderScale,narrativeSeconds,physicalSunDegrees,displaySunDegrees;
            public string frameGeneration="None",vram="See separate PID-scoped WDDM CSV; graphicsMemorySize is capacity, not measured usage.";
            public Check[] checks;public Phase[] phases;
        }
        MissionController mission;RadioController radio;Camera view;SolarScatteringVolume volume;FleetEscapeDiagnostics diagnostics;
        readonly List<Check> checks=new List<Check>();readonly List<Phase> phases=new List<Phase>();
        Report report;string output;bool quit;Coroutine routine;
        Keyboard proofKeyboard, previousKeyboard;Mouse proofMouse, previousMouse;ShipTarget proofShip;Vector3 proofStart;
        readonly FrameTiming[] frameTiming=new FrameTiming[1];
        public string CurrentPhase {get;private set;}="Idle";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Application.isEditor)return;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-enhancement-validation");if(index<0)return;
            new GameObject("__OptInEnhancementQA").AddComponent<EnhancementValidationRunner>().Begin(index+1<args.Length?args[index+1]:Application.persistentDataPath,true);
        }
        public void Begin(string directory,bool exit=false)
        {
            output=Path.GetFullPath(directory);Directory.CreateDirectory(output);quit=exit;
            // Input isolation is confined to this explicit QA process. Physical
            // mouse events must not redirect a scripted normal-flight observation.
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            if(previousKeyboard!=null)InputSystem.DisableDevice(previousKeyboard);
            if(previousMouse!=null)InputSystem.DisableDevice(previousMouse);
            proofKeyboard=InputSystem.AddDevice<Keyboard>();proofKeyboard.MakeCurrent();
            proofMouse=InputSystem.AddDevice<Mouse>();proofMouse.MakeCurrent();
            mission=FindAnyObjectByType<MissionController>();radio=FindAnyObjectByType<RadioController>();view=mission.chaseCamera.GetComponent<Camera>();volume=view.GetComponent<SolarScatteringVolume>();
            diagnostics=mission.combat.gameObject.AddComponent<FleetEscapeDiagnostics>();diagnostics.simulation=mission.combat;diagnostics.captureEnabled=true;diagnostics.Bind();
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report=new Report{unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),scene=mission.gameObject.scene.path,
                editor=Application.isEditor,identities=mission.targets.Select(s=>s.targetId).Distinct().Count(),width=Screen.width,height=Screen.height,renderScale=pipeline!=null?pipeline.renderScale:1,
                narrativeSeconds=mission.narrative.duration,physicalSunDegrees=volume.sun.PhysicalAngularDiameter,displaySunDegrees=volume.sun.EffectiveAngularDiameter,
                voiceStatus="English Seed-TTS request blocked by resource permission; retained playable legacy audio. Expanded English duration/listening acceptance NOT PASSED."};
            routine=StartCoroutine(Guard(Run()));
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object next=null;bool moved=false;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception exception){report.error=exception.ToString();break;}
                if(!moved){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;
            }
            report.completed=string.IsNullOrEmpty(report.error);report.checks=checks.ToArray();report.phases=phases.ToArray();report.allChecksPassed=report.completed&&checks.All(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"status.txt"),"Complete: "+report.allChecksPassed);mission.Restart();
            if(proofKeyboard!=null)InputSystem.RemoveDevice(proofKeyboard);if(proofMouse!=null)InputSystem.RemoveDevice(proofMouse);
            if(previousKeyboard!=null){InputSystem.EnableDevice(previousKeyboard);previousKeyboard.MakeCurrent();}
            if(previousMouse!=null){InputSystem.EnableDevice(previousMouse);previousMouse.MakeCurrent();}
            if(quit)Application.Quit(report.allChecksPassed?0:2);
        }
        void CheckIt(string name,bool pass,string detail=""){checks.Add(new Check{name=name,passed=pass,detail=detail});}
        void PhaseName(string name){CurrentPhase=name;File.WriteAllText(Path.Combine(output,"status.txt"),name);}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
        IEnumerator Wait(double seconds){double until=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<until)yield return null;}
        IEnumerator Run()
        {
            yield return null;CheckIt("Saved identities",mission.TotalCount==report.identities&&mission.TotalCount==2000,"No ships removed; all remain interactive.");
            PhaseName("Skip and pause at each narrative quarter");
            for(int i=0;i<4;i++)
            {
                mission.ReplayNarrative();mission.narrative.Step(mission.narrative.duration*i/4f);
                mission.TogglePause();float t=mission.narrative.Elapsed;var p=mission.motor.transform.position;yield return Wait(.2);
                CheckIt("Narrative pause "+i,mission.narrative.Elapsed==t&&mission.motor.transform.position==p);
                mission.narrative.Skip();mission.narrative.Skip();CheckIt("One-shot skip "+i,mission.State==MissionState.Playing&&mission.narrative.CompletionCount==1);
            }
            PhaseName("Real-time retained narrative (new English permission blocked)");mission.ReplayNarrative();
            double started=Time.realtimeSinceStartupAsDouble;int safety=0;
            while(mission.State==MissionState.Narrative&&Time.realtimeSinceStartupAsDouble-started<mission.narrative.duration+25){if(safety++==10)yield return Capture("opening");yield return null;}
            CheckIt("Retained narrative natural completion",mission.State==MissionState.Playing&&mission.narrative.CompletionCount==1,"Actual wall="+(Time.realtimeSinceStartupAsDouble-started)+"s; does not satisfy requested new 4–6 minute English delivery.");
            PhaseName("Normal live FixedUpdate forward attack and escape");mission.RestartIntoCombat();
            var initial=mission.targets.ToDictionary(s=>s,s=>s.transform.position);yield return Capture("normal-flight-0");
            double waitForHit=Time.realtimeSinceStartupAsDouble+8;
            while(mission.PendingCount+mission.DestroyedCount==0&&Time.realtimeSinceStartupAsDouble<waitForHit)yield return null;
            // Use the actual brake input after an ordinary forward penetration.
            // This keeps nearby escaping hulls in the ordinary chase view; it never directs their behavior.
            InputSystem.QueueStateEvent(proofKeyboard,new KeyboardState(Key.Space));yield return Wait(.3);
            proofShip=mission.targets.Where(s=>s.DamageState==ShipDamageState.Intact&&Vector3.Dot(s.transform.position-mission.motor.transform.position,Vector3.forward)>50)
                .OrderBy(s=>(s.transform.position-mission.motor.transform.position).sqrMagnitude).First();proofStart=initial[proofShip];
            Directory.CreateDirectory(Path.Combine(output,"EscapeFrames"));
            for(int frame=0;frame<65;frame++){yield return Capture("EscapeFrames/frame-"+frame.ToString("000"));yield return Wait(.2);}
            yield return Capture("normal-flight-16");proofShip=null;InputSystem.QueueStateEvent(proofKeyboard,new KeyboardState());
            // Capture duration is wall time; diagnosis checkpoints use simulation
            // time since the actual threat, which can occur later than the first screenshot.
            double diagnosticDeadline=Time.realtimeSinceStartupAsDouble+20;
            while(!diagnostics.Samples.Any(s=>s.checkpointSeconds==10)&&Time.realtimeSinceStartupAsDouble<diagnosticDeadline)yield return null;
            File.WriteAllText(Path.Combine(output,"normal-escape.json"),diagnostics.ToJson());
            int fleeing=mission.targets.Count(s=>s.DamageState==ShipDamageState.Intact&&(s.BehaviorState==ShipBehaviorState.Fleeing||s.BehaviorState==ShipBehaviorState.BreakingFormation)&&Vector3.Distance(initial[s],s.transform.position)>50);
            CheckIt("Normal fixed-step attack triggered intact movement",mission.DestroyedCount+mission.PendingCount>0&&fleeing>0,"destroyed="+mission.DestroyedCount+" pending="+mission.PendingCount+" intact moved="+fleeing+"; no ForceFlee/RequestRetreat or direct damage setup in this phase.");
            CheckIt("Authority and render agree",diagnostics.Samples.Any(s=>s.checkpointSeconds==10)&&diagnostics.Samples.Where(s=>s.checkpointSeconds>0).All(s=>s.renderedErrorMeters<.2f&&s.transformErrorMeters<.2f),"samples="+diagnostics.Samples.Count+" tenSecondSamples="+diagnostics.Samples.Count(s=>s.checkpointSeconds==10));
            mission.TogglePause();var pausedPositions=mission.targets.Select(s=>s.transform.position).ToArray();float simulation=mission.combat.SimulatedTime;yield return Wait(.5);
            CheckIt("Pause freezes delayed explosions and fleet",mission.combat.SimulatedTime==simulation&&mission.targets.Select((s,i)=>s.transform.position==pausedPositions[i]).All(x=>x));mission.TogglePause();
            // Small-duration actual frames are needed for optical validation, separate from analytic ray tests.
            PhaseName("Third-person concentrated reflection");mission.RestartIntoCombat();mission.motor.SimulationEnabled=false;
            var source=mission.targets.First(s=>!s.IsResolved);var aim=mission.lasers.dropletSurface.AimPoint;
            for(int i=0;i<8;i++)mission.lasers.FireRay(source,aim+new Vector3(-12+i*3,5,20),new Vector3(12-i*3,-5,-20),10000+i);
            yield return Capture("reflection-third-person");yield return Measure("Concentrated reflections",()=>
            {var a=mission.lasers.dropletSurface.AimPoint;mission.lasers.FireRay(source,a+new Vector3(12,5,20),new Vector3(-12,-5,-20),200000+Time.frameCount);},"Stationary droplet for repeatable optical stress; normal fleet simulation enabled, real ray queries each frame.");
            // Reproducible view direction, FOV unchanged. Simulation still advances.
            mission.RestartIntoCombat();mission.motor.SimulationEnabled=false;mission.chaseCamera.enabled=false;
            var sun=volume.sun;sun.ApplyForCamera(view);view.transform.rotation=Quaternion.LookRotation(sun.SunDirection,Vector3.up);
            volume.scatteringEnabled=false;yield return Wait(.3);yield return Capture("sun-volume-off");yield return Measure("Corona off",null,"Fixed camera to sun; 2000 target simulation, same exposure/FOV.");
            volume.scatteringEnabled=true;yield return Wait(.3);yield return Capture("sun-volume-on");yield return Measure("Corona on",null,"Half-resolution16 sample depth-clipped local corona; same camera/exposure/FOV.");
            CheckIt("Calibrated solar size",sun.PhysicalAngularDiameter>.20f&&sun.PhysicalAngularDiameter<.23f&&sun.EffectiveAngularDiameter<.5f);
            view.transform.rotation=Quaternion.LookRotation(-sun.SunDirection,Vector3.up);yield return Capture("sun-offscreen");CheckIt("Corona exits with sun",!volume.IsVisible(view));
            // Reuse the real authored hull as a temporary visual-only depth occluder.
            view.transform.rotation=Quaternion.LookRotation(sun.SunDirection,Vector3.up);
            var blocker=Instantiate(source.visualRoot,view.transform.position+sun.SunDirection*200,Quaternion.LookRotation(view.transform.right,Vector3.up));blocker.name="__TemporaryHullOcclusionQA";blocker.SetActive(true);
            foreach(var renderer in blocker.GetComponentsInChildren<Renderer>(true)){renderer.forceRenderingOff=false;renderer.enabled=true;}
            foreach(var lod in blocker.GetComponentsInChildren<LODGroup>(true)){lod.enabled=true;lod.ForceLOD(0);}
            yield return Wait(.3);yield return Capture("sun-occluded");Destroy(blocker);yield return null;
            mission.chaseCamera.enabled=true;mission.RestartIntoCombat();mission.motor.SimulationEnabled=false;
            PhaseName("Concentrated delayed explosions and normal fleet loss response");
            foreach(var s in mission.targets.OrderBy(s=>(s.transform.position-mission.motor.transform.position).sqrMagnitude).Take(200))mission.combat.ApplyDamage(s,new ShipHitContext(s.transform.position,Vector3.forward,1500),DamageSource.Penetration,300000);
            CheckIt("Delay retains pending hulls",mission.PendingCount==200&&mission.DestroyedCount==0);
            yield return Measure("Delayed explosion and retreat",null,"Controlled 200 hull damage stress; 2000 identities retained. Natural threat logic triggers remaining intact ships.");yield return Capture("retreat-stress");
            CheckIt("Pending resolved once",mission.PendingCount==0&&mission.DestroyedCount==200);
            yield return Measure("Fleet retreat",null,"1800 intact ships after fleet-loss threshold; no RequestRetreat or disabled far decisions.");
            for(int r=0;r<3;r++){mission.RestartIntoCombat();yield return null;CheckIt("Continuous restart "+r,mission.DestroyedCount==0&&mission.PendingCount==0&&mission.EscapedCount==0&&Time.timeScale==1&&mission.TotalCount==2000&&mission.lasers.beamPool.ActiveCount==0);}
            // Resolve without any unreachable final target. Uses normal mission timeout path.
            mission.enabled=false;float oldSeconds=mission.settings.missionSeconds;mission.settings.missionSeconds=.05f;mission.RestartIntoCombat();mission.Step(.05f,default);
            CheckIt("Timeout results once",mission.State==MissionState.Results&&mission.ResultTransitions==1);mission.Step(1,default);CheckIt("No duplicate results",mission.ResultTransitions==1);mission.settings.missionSeconds=oldSeconds;mission.enabled=true;
            yield return Capture("results");
        }
        IEnumerator Measure(string name,Action everyFrame,string workload)
        {
            PhaseName(name);yield return Wait(.3);var times=new List<double>(2048);double begin=Time.realtimeSinceStartupAsDouble,last=begin;int focused=0,timingCount=0,invalidTiming=0;double cpu=0,gpu=0;
            while(times.Count<480||Time.realtimeSinceStartupAsDouble-begin<6)
            {everyFrame?.Invoke();FrameTimingManager.CaptureFrameTimings();yield return null;double now=Time.realtimeSinceStartupAsDouble;times.Add((now-last)*1000);last=now;if(Application.isFocused)focused++;
                if(FrameTimingManager.GetLatestTimings(1,frameTiming)>0&&frameTiming[0].gpuFrameTime>0)
                {
                    // D3D12 can return an invalid timestamp difference (~billions of ms).
                    // Preserve a rejection count rather than presenting it as measured GPU cost.
                    if(double.IsNaN(frameTiming[0].gpuFrameTime)||double.IsInfinity(frameTiming[0].gpuFrameTime)||frameTiming[0].gpuFrameTime>=1000){invalidTiming++;continue;}
                    cpu+=frameTiming[0].cpuFrameTime;gpu+=frameTiming[0].gpuFrameTime;timingCount++;
                }}
            times.Sort();phases.Add(new Phase{name=name,workload=workload,frames=times.Count,focusedFrames=focused,width=Screen.width,height=Screen.height,ships=mission.TotalCount,pending=mission.PendingCount,escaped=mission.EscapedCount,
                fleeing=mission.targets.Count(s=>s.BehaviorState==ShipBehaviorState.Fleeing),volume=volume.scatteringEnabled,meanMs=times.Average(),p95Ms=times[Mathf.FloorToInt((times.Count-1)*.95f)],maxMs=times[times.Count-1],
                frameTimingSamples=timingCount,invalidGpuTimingSamples=invalidTiming,cpuMeanMs=timingCount>0?cpu/timingCount:-1,gpuMeanMs=timingCount>0?gpu/timingCount:-1});
        }
        void OnGUI()
        {
            if(proofShip==null||view==null)return;
            var actual=view.WorldToScreenPoint(proofShip.transform.position);var original=view.WorldToScreenPoint(proofStart);
            var style=new GUIStyle(GUI.skin.box){fontSize=17,alignment=TextAnchor.MiddleLeft};
            GUI.Box(new Rect(Screen.width-650,190,620,90),"NORMAL ATTACK / BRAKE OBSERVATION\n"+proofShip.targetId+"  "+proofShip.BehaviorState+"\nMoved "+(Vector3.Distance(proofStart,proofShip.transform.position)*.1f).ToString("F2")+" km  |  "+(proofShip.Velocity.magnitude*.1f).ToString("F2")+" km/s",style);
            if(original.z>0)GUI.Label(new Rect(original.x,Screen.height-original.y,150,24),"+ formation start");
            if(actual.z>0)GUI.Label(new Rect(actual.x,Screen.height-actual.y,180,24),"+ tracked intact hull");
        }
    }
}
