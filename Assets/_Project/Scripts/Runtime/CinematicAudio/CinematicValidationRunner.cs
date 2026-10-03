using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DropletPrototype
{
    /// <summary>Explicit rendered-player acceptance. This component never runs in an ordinary game.</summary>
    public sealed class CinematicValidationRunner : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class EventRecord
        {
            public string kind, attacker, target, damage; public long id; public int generation;
            public float time; public Vector3 position;
        }
        [Serializable] public sealed class ShotRecord { public string phase,kind;public float time;public long eventId; }
        [Serializable] public sealed class Report
        {
            public string scene, unity, gpu, cpu, graphicsApi, error;
            public string listening = "Pending human listening. Runtime output and automatic signal checks do not establish acting or mix quality.";
            public int ships, identities, width, height, maximumVoices, maximumWorldSources, nonzeroOutputSamples;
            public float quietIntensity, peakIntensity, aftermathIntensity, peakOutput;
            public bool completed, allChecksPassed;
            public Check[] checks; public EventRecord[] events; public ShotRecord[] shots;
        }
        MissionController mission; RadioController radio; BattleAudioDirector sound; ShotDirector shots;
        readonly List<Check> checks = new List<Check>();
        readonly List<EventRecord> events = new List<EventRecord>();
        readonly List<ShotRecord> shotRecords = new List<ShotRecord>();
        readonly List<string> samples = new List<string>{"wall,simulation,phase,intensity,voiceCount,worldSources,outputPeak,destroyed,pending,shot"};
        readonly float[] meter = new float[512];
        Report report; string output, phase="Startup"; bool quit, running; float nextSample; int imageSequence;
        Keyboard keyboard, oldKeyboard; Mouse mouse, oldMouse;
        ShipTarget monitoredSpeaker;
        bool observedSpeakerPlayback, observedWholeCabinStop;
        bool openingOnly, slowRoute;
        float routeSpeed=150;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Application.isEditor) return;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-cinematic-validation");
            bool opening = false;
            if(index < 0) { index=Array.IndexOf(args,"-cinematic-opening"); opening=index>=0; }
            if(index < 0) return;
            var runner=new GameObject("__OptInCinematicAudioQA").AddComponent<CinematicValidationRunner>();
            runner.openingOnly=opening;runner.slowRoute=Array.IndexOf(args,"-cinematic-slow-route")>=0;
            int speedIndex=Array.IndexOf(args,"-cinematic-route-speed");
            if(speedIndex>=0&&speedIndex+1<args.Length&&float.TryParse(args[speedIndex+1],
                System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float speed))
                runner.routeSpeed=Mathf.Clamp(speed,50,300);
            runner.Begin(index+1 < args.Length ? args[index+1] : Path.Combine(Application.persistentDataPath,"CinematicQA"), true);
        }
        public void Begin(string directory, bool exit = false)
        {
            output=Path.GetFullPath(directory); Directory.CreateDirectory(output); quit=exit;
            mission=FindAnyObjectByType<MissionController>(); radio=mission.narrative.radio;
            sound=FindAnyObjectByType<BattleAudioDirector>(); shots=FindAnyObjectByType<ShotDirector>();
            oldKeyboard=Keyboard.current;oldMouse=Mouse.current;
            if(oldKeyboard!=null)InputSystem.DisableDevice(oldKeyboard);if(oldMouse!=null)InputSystem.DisableDevice(oldMouse);
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            report=new Report{scene=mission.gameObject.scene.path,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                cpu=SystemInfo.processorType,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),ships=mission.TotalCount,
                identities=mission.targets.Select(s=>s.targetId).Distinct().Count(),width=Screen.width,height=Screen.height};
            mission.combat.EventRaised+=OnEvent;shots.ShotStarted+=OnShot;
            Application.runInBackground=true; running=true;StartCoroutine(Guard(Run()));
        }
        void OnEvent(CombatEvent e)
        {
            if(monitoredSpeaker!=null&&e.subject==monitoredSpeaker&&e.kind==CombatEventKind.CommunicationInterrupted)
                observedWholeCabinStop=radio.CurrentSpeaker!=monitoredSpeaker&&!radio.voiceSource.isPlaying&&
                    (radio.alarmSource==null||!radio.alarmSource.isPlaying)&&(radio.bedSource==null||!radio.bedSource.isPlaying);
            if(events.Count<15000)events.Add(new EventRecord{kind=e.kind.ToString(),attacker=e.attackerId,target=e.targetId,
                damage=e.damageState.ToString(),id=e.eventId,generation=e.generation,time=e.simulationTime,position=e.position});
        }
        void OnShot(CombatEvent e, CombatShotKind kind)
        {
            shotRecords.Add(new ShotRecord{phase=phase,kind=kind.ToString(),time=mission.combat.SimulatedTime,eventId=e.eventId});
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"shot-"+(imageSequence++).ToString("000")+"-"+kind+".png"));
        }
        void Update()
        {
            if(!running || sound==null || Time.realtimeSinceStartup<nextSample)return;
            nextSample=Time.realtimeSinceStartup+.1f;
            AudioListener.GetOutputData(meter,0);float peak=0;foreach(float value in meter)peak=Mathf.Max(peak,Mathf.Abs(value));
            report.maximumVoices=Mathf.Max(report.maximumVoices,radio.ActiveVoiceCount);
            report.maximumWorldSources=Mathf.Max(report.maximumWorldSources,sound.ActiveWorldSources);
            report.peakIntensity=Mathf.Max(report.peakIntensity,sound.Intensity);report.peakOutput=Mathf.Max(report.peakOutput,peak);
            if(peak>.00001f)report.nonzeroOutputSamples++;
            samples.Add(string.Join(",",Time.realtimeSinceStartup.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),
                mission.combat.SimulatedTime.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),phase,
                sound.Intensity.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),radio.ActiveVoiceCount,sound.ActiveWorldSources,
                peak.ToString("F5",System.Globalization.CultureInfo.InvariantCulture),mission.DestroyedCount,mission.PendingCount,shots.Active?shots.CurrentKind.ToString():"Chase"));
        }
        void CheckIt(string name,bool passed,string detail="")=>checks.Add(new Check{name=name,passed=passed,detail=detail});
        void Phase(string name){phase=name;File.WriteAllText(Path.Combine(output,"status.txt"),name);}
        IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
        IEnumerator Guard(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object next=null;bool moved=false;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception exception){report.error=exception.ToString();break;}
                if(!moved){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;
            }
            running=false;report.completed=string.IsNullOrEmpty(report.error);report.checks=checks.ToArray();report.events=events.ToArray();report.shots=shotRecords.ToArray();
            report.allChecksPassed=report.completed&&checks.All(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));File.WriteAllLines(Path.Combine(output,"audio-and-shots.csv"),samples);
            File.WriteAllText(Path.Combine(output,"status.txt"),"Complete="+report.allChecksPassed);
            mission.combat.EventRaised-=OnEvent;shots.ShotStarted-=OnShot;mission.Restart();
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(oldKeyboard!=null){InputSystem.EnableDevice(oldKeyboard);oldKeyboard.MakeCurrent();}if(oldMouse!=null){InputSystem.EnableDevice(oldMouse);oldMouse.MakeCurrent();}
            if(quit)Application.Quit(report.allChecksPassed?0:2);
        }
        IEnumerator Run()
        {
            yield return null;
            CheckIt("Full authored fleet",report.ships==2000&&report.identities==2000);
            CheckIt("One stable listener",FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled)==1&&
                FindAnyObjectByType<CombatListenerAnchor>()!=null);
            if(openingOnly)
            {
                Phase("Full real-time English opening"); mission.ReplayNarrative();
                float start=Time.realtimeSinceStartup;float nextImage=20;
                while(mission.State==MissionState.Narrative&&Time.realtimeSinceStartup-start<mission.narrative.duration+45)
                {
                    if(mission.narrative.Elapsed>=nextImage){yield return Capture("opening-"+nextImage.ToString("000"));nextImage+=60;}
                    yield return null;
                }
                CheckIt("All forty natural narrative cues",mission.narrative.CueCount==40,"Actual wall seconds="+(Time.realtimeSinceStartup-start));
                CheckIt("Natural Timeline completes once",mission.State==MissionState.Playing&&mission.narrative.CompletionCount==1);
                CheckIt("Actual English opening output",report.nonzeroOutputSamples>100&&report.peakOutput>0);
                yield return Capture("opening-completed");yield break;
            }
            Phase("Narrative pause and skip");mission.ReplayNarrative();yield return Wait(2);
            mission.TogglePause();float narrativeTime=mission.narrative.Elapsed;yield return Wait(.3f);
            CheckIt("Narrative pause",mission.narrative.Elapsed==narrativeTime);
            mission.narrative.Skip();mission.narrative.Skip();
            CheckIt("Single combat initialization",mission.State==MissionState.Playing&&mission.narrative.CompletionCount==1);
            string normalPhase=slowRoute?"Normal input slower flight":"Normal forward flight";
            Phase(normalPhase);mission.RestartIntoCombat();report.quietIntensity=sound.Intensity;
            var initial=mission.targets.ToDictionary(s=>s,s=>s.transform.position);
            yield return Capture("normal-start");
            // All combat in this phase is original FixedUpdate and ordinary motor input.
            // No direct damage, FireRay, retreat setters, camera forcing or time scaling.
            float normalEnd=Time.realtimeSinceStartup+(slowRoute?Mathf.Max(110,17000/routeSpeed):90);bool returnChecked=false;
            if(slowRoute)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));
                while(mission.motor.CruiseSpeed>routeSpeed+1&&mission.State==MissionState.Playing)yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                CheckIt("S key slows normal motor",mission.motor.CruiseSpeed>routeSpeed-10&&mission.motor.CruiseSpeed<=routeSpeed+1,
                    "Cruise="+mission.motor.CruiseSpeed+"; normal mission duration="+mission.settings.missionSeconds);
            }
            while(Time.realtimeSinceStartup<normalEnd)
            {
                if(shots.Active&&!returnChecked)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                    CheckIt("Independent Q returns during real shot",!shots.Active&&mission.input.GameplayEnabled&&Time.timeScale==1);
                    returnChecked=true;
                }
                yield return null;
            }
            CheckIt("Return input exercised",returnChecked);yield return Capture("normal-late");
            CheckIt("Normal penetration",events.Any(e=>e.kind=="HullPenetrated"&&e.attacker=="Droplet")&&mission.DestroyedCount>0);
            CheckIt("Intact ships actually escape formation",mission.targets.Any(s=>s.DamageState==ShipDamageState.Intact&&
                (s.BehaviorState==ShipBehaviorState.Fleeing||s.BehaviorState==ShipBehaviorState.BreakingFormation)&&Vector3.Distance(initial[s],s.transform.position)>40));
            CheckIt("Natural event close-ups",shotRecords.Count(s=>s.phase==normalPhase)>=2,
                string.Join("/",shotRecords.Where(s=>s.phase==normalPhase).Select(s=>s.kind)));
            var natural=shotRecords.Where(s=>s.phase==normalPhase).ToArray();
            CheckIt("No back-to-back close-ups",natural.Zip(natural.Skip(1),(a,b)=>b.time-a.time).All(t=>t>=11.95f));
            mission.TogglePause();float paused=mission.combat.SimulatedTime;yield return Wait(.4f);
            CheckIt("Pause stops simulation and shots",mission.combat.SimulatedTime==paused&&!shots.Active);mission.TogglePause();
            Phase("Live event stress and interrupted transmission");mission.RestartIntoCombat();mission.motor.SimulationEnabled=false;
            var victim=mission.targets.OrderBy(s=>(s.transform.position-mission.spawnPosition).sqrMagnitude).First();
            var lasers=mission.lasers;mission.lasers=null;
            // Isolate one real long cabin transmission so the destruction callback can
            // prove that ALL baked cabin layers stop, rather than merely a cleared label.
            var fullLibrary=radio.library;var proofLibrary=ScriptableObject.CreateInstance<RadioLibrary>();
            var assault=fullLibrary.combat.First(l=>l.id=="SA01");
            proofLibrary.combat=new[]{new RadioLine{id=assault.id,eventKind="HullPenetrated",channel=assault.channel,
                text=assault.text,english=assault.english,voice=assault.voice,premixed=true,pendingOnly=true,important=true,captions=assault.captions}};
            proofLibrary.premixedSceneAudio=true;proofLibrary.interruptTone=fullLibrary.interruptTone;
            radio.library=proofLibrary;radio.BeginSession(1909);monitoredSpeaker=victim;
            mission.combat.ApplyDamage(victim,new ShipHitContext(victim.transform.position,Vector3.forward,1500),DamageSource.Penetration,60001);
            float deadline=victim.ExplosionAt;int interrupted=radio.InterruptedCount;
            // Allow the pending ship's actual short emergency to start before testing pause.
            yield return Wait(.7f);observedSpeakerPlayback=radio.CurrentSpeaker==victim&&radio.voiceSource.isPlaying;
            mission.TogglePause();float before=mission.combat.SimulatedTime;yield return Wait(.3f);
            CheckIt("Paused fatal deadline unchanged",victim.ExplosionAt==deadline&&mission.combat.SimulatedTime==before);
            mission.TogglePause();mission.motor.SimulationEnabled=false;yield return Wait(5.4f);
            CheckIt("Explosion does not wait for dialogue",victim.IsDestroyed&&mission.DestroyedCount==1&&mission.PendingCount==0&&victim.ExplosionAt==deadline);
            CheckIt("Destroyed speaker fully stopped",observedSpeakerPlayback&&observedWholeCabinStop,
                "SA01 played="+observedSpeakerPlayback+" stopped at actual destruction callback="+observedWholeCabinStop+"; interrupted="+radio.InterruptedCount+" (start "+interrupted+")");
            CheckIt("Single death score unchanged",mission.score.Score==mission.settings.baseScore);
            monitoredSpeaker=null;radio.library=fullLibrary;radio.BeginSession(1701);Destroy(proofLibrary);
            // Actual 200-hull delayed-explosion stress, retaining the full 2000-ship scene.
            foreach(var ship in mission.targets.Where(s=>!s.IsResolved).OrderBy(s=>(s.transform.position-mission.motor.transform.position).sqrMagnitude).Take(200))
                mission.combat.ApplyDamage(ship,new ShipHitContext(ship.transform.position,Vector3.forward,1500),DamageSource.Penetration,60002);
            yield return Wait(6);yield return Capture("full-fleet-explosion-stress");
            CheckIt("Concentrated delayed explosions once",mission.DestroyedCount==201&&mission.PendingCount==0);
            Phase("Aftermath decay");yield return Wait(20);report.aftermathIntensity=sound.Intensity;
            CheckIt("Audio density decays",report.peakIntensity>report.quietIntensity+.05f&&report.aftermathIntensity<report.peakIntensity*.7f,
                "quiet="+report.quietIntensity+" peak="+report.peakIntensity+" aftermath="+report.aftermathIntensity);
            mission.lasers=lasers;
            CheckIt("Single principal transmission",report.maximumVoices<=1);
            CheckIt("Bounded world audio",report.maximumWorldSources<=sound.poolCapacity,"maximum sources="+report.maximumWorldSources);
            CheckIt("Actual mixer output",report.nonzeroOutputSamples>10&&report.peakOutput>0,"meter nonzero samples="+report.nonzeroOutputSamples);
            Phase("Full-fleet origin shift");mission.RestartIntoCombat();mission.motor.SimulationEnabled=false;
            // Verify the coordinate transform atomically; normal AI motion during a yielded
            // frame must not be confused with a failed rebase. Presentation still updates.
            mission.enabled=false;
            var originalSpawn=mission.spawnPosition;var originalCenter=mission.arenaCenter;var positionBefore=mission.targets.Select(s=>s.transform.position).ToArray();
            var offset=new Vector3(12000,-3500,16000);
            mission.combat.ShiftOrigin(offset);mission.spawnPosition-=offset;mission.arenaCenter-=offset;
            mission.motor.ResetPose(mission.motor.transform.position-offset,mission.motor.transform.rotation);yield return null;
            mission.combat.fleetRenderer.RefreshNow();
            bool originValid=true;
            for(int i=0;i<mission.targets.Length;i++)
            {
                var target=mission.targets[i];originValid&=Vector3.Distance(positionBefore[i]-offset,target.transform.position)<.002f;
                originValid&=mission.combat.fleetRenderer.TryGetPresentationPose(target,out var matrix,out _)&&Vector3.Distance((Vector3)matrix.GetColumn(3),target.transform.position)<.002f;
            }
            CheckIt("Origin rebases all 2000 render identities",originValid&&!shots.Active);
            mission.combat.ShiftOrigin(-offset);mission.spawnPosition=originalSpawn;mission.arenaCenter=originalCenter;
            mission.enabled=true;
            Phase("Three continuous restarts");
            for(int i=0;i<3;i++)
            {
                mission.RestartIntoCombat();yield return null;
                CheckIt("Restart "+i,mission.TotalCount==2000&&mission.DestroyedCount==0&&mission.PendingCount==0&&mission.EscapedCount==0&&
                    !shots.Active&&shots.PendingCount==0&&radio.ActiveVoiceCount==0&&Time.timeScale==1);
            }
            Phase("Results transition");mission.enabled=false;float duration=mission.settings.missionSeconds;mission.settings.missionSeconds=.02f;
            mission.RestartIntoCombat();mission.Step(.02f,default);yield return null;
            CheckIt("Results once and clean",mission.State==MissionState.Results&&mission.ResultTransitions==1&&!shots.Active&&radio.ActiveVoiceCount==0);
            mission.settings.missionSeconds=duration;mission.enabled=true;yield return Capture("results");
        }
    }
}
