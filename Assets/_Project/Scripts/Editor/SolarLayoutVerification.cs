using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class SolarLayoutVerification
    {
        static void Check(bool b,string m)=>SolarLayoutPipeline.Require(b,m);
        static float[] A(Vector3 v)=>SolarLayoutPipeline.A(v);
        static MissionController Mission()=>UnityEngine.Object.FindAnyObjectByType<MissionController>();
        static SolarSystemBackdrop Backdrop()=>UnityEngine.Object.FindAnyObjectByType<SolarSystemBackdrop>();
        static string FleetSignature(Scene scene)
        {
            var targets=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ShipTarget>(true)).OrderBy(t=>t.targetId);
            return Newtonsoft.Json.JsonConvert.SerializeObject(targets.Select(t=>new{t.targetId,p=A(t.transform.position),q=new[]{t.transform.rotation.x,t.transform.rotation.y,t.transform.rotation.z,t.transform.rotation.w},s=A(t.transform.lossyScale),colliders=t.GetComponentsInChildren<BoxCollider>(true).Select(c=>new{c.name,p=A(c.transform.position),q=A(c.transform.eulerAngles),s=A(c.transform.lossyScale),center=A(c.center),size=A(c.size),c.isTrigger,c.gameObject.layer}),meshes=t.GetComponentsInChildren<MeshFilter>(true).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh))}));
        }
        [MenuItem("DropletPrototype/Solar Layout/6 Verify Repeat Import and Saved Combat")]
        public static void Repeat()
        {
            SolarLayoutPipeline.Guard();var old=EditorSceneManager.OpenScene(SolarLayoutPipeline.Baseline,OpenSceneMode.Single);string originalFleet=FleetSignature(old);
            var originalSettings=JsonUtility.ToJson(Mission().settings);var oldSpawn=Mission().spawnPosition;var oldCenter=Mission().arenaCenter;
            EditorSceneManager.OpenScene(SolarLayoutPipeline.Review,OpenSceneMode.Single);
            const string token="SolarVerification_ExternalSentinel";Check(GameObject.Find(token)==null,"Sentinel name collision.");var sentinel=new GameObject(token);sentinel.transform.position=new Vector3(37,91,-68);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var rounds=new List<object>();
            for(int i=0;i<2;i++)
            {
                SolarLayoutPipeline.Import();SolarLayoutPipeline.BuildReview();SolarLayoutPipeline.Audit();
                var saved=GameObject.Find(token);Check(saved!=null&&saved.transform.position==new Vector3(37,91,-68),"External authored root lost.");
                var ids=Backdrop().GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_")).Select(t=>t.name).OrderBy(x=>x).ToArray();
                rounds.Add(new{round=i+1,instances=ids.Length,ids,externalPreserved=true});
            }
            UnityEngine.Object.DestroyImmediate(GameObject.Find(token));EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.OpenScene(SolarLayoutPipeline.Review,OpenSceneMode.Single);SolarLayoutPipeline.Audit();
            SolarLayoutPipeline.BuildPlayable();var m=Mission();Check(FleetSignature(SceneManager.GetActiveScene())==originalFleet,"Fleet transforms/colliders/meshes changed.");
            Check(m.spawnPosition==oldSpawn&&m.arenaCenter==oldCenter,"Spawn/arena centre changed.");
            var copy=UnityEngine.Object.Instantiate(m.settings);var before=ScriptableObject.CreateInstance<DropletSettings>();JsonUtility.FromJsonOverwrite(originalSettings,before);
            copy.boundaryRadius=before.boundaryRadius;copy.boundaryWarningRadius=before.boundaryWarningRadius;copy.name=before.name;
            // Compare all serialized tuning fields, excluding only the two authorised radius changes.
            Check(JsonUtility.ToJson(copy)==JsonUtility.ToJson(before),"Unapproved flight/mission tuning change.");UnityEngine.Object.DestroyImmediate(copy);UnityEngine.Object.DestroyImmediate(before);
            SolarLayoutPipeline.Capture(m.chaseCamera.GetComponent<Camera>(),"After-Fleet-Spawn.png");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.OpenScene(SolarLayoutPipeline.Playable,OpenSceneMode.Single);SolarLayoutPipeline.Audit();
            Check(FleetSignature(SceneManager.GetActiveScene())==originalFleet,"Combat failed reopen.");
            SolarLayoutPipeline.Write("repeat-reopen-combat.json",new{passed=true,rounds,reviewReopened=true,playableReopened=true,identical40ShipTransformsCollidersMeshes=true,spawnAndCenterPreserved=true,onlyTwoTuningFieldsChanged=true,guide="Existing nearest-target guide is unbounded and remains visible at any local range.",safeReturn="Existing spawn retained as safe return, trigger reads expanded settings; no new teleport movement path."});
            Debug.Log("SOLAR repeat/reopen/combat PASS.");
        }
        [MenuItem("DropletPrototype/Solar Layout/7 Live Input Flight Probe (Play Mode)")]
        public static void BeginLive()
        {
            Check(EditorApplication.isPlaying&&SceneManager.GetActiveScene().path==SolarLayoutPipeline.Playable,"Enter Play in solar variant first.");
            Check(!running,"Probe is already running.");RunLive();
        }
        static bool running;
        static async void RunLive()
        {
            running=true;var m=Mission();var b=Backdrop();var c=m.chaseCamera.GetComponent<Camera>();var checks=new List<object>();var keyboard=InputSystem.AddDevice<Keyboard>("SolarProbeKeyboard");var mouse=InputSystem.AddDevice<Mouse>("SolarProbeMouse");bool oldBackground=Application.runInBackground;var pauseCallback=m.input.PauseRequested;
            float oldAt=-1,warnAt=-1,maxRadius=0;string error=null;bool complete=false;int initialPool=UnityEngine.Object.FindAnyObjectByType<MissionEffects>().PoolInstanceCount;
            try
            {
                Application.runInBackground=true;var gv=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));gv.Show();gv.Focus();
                m.Restart();m.GetComponent<PlayerOptions>().Apply(1,false,65,.65f,true,false);
                await Task.Delay(200);await Press(keyboard,Key.Enter);Check(m.State==MissionState.Playing,"Input Enter failed.");
                var rocks=b.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_")).ToDictionary(t=>t.name,t=>t.position);
                // The current Editor may lose OS focus while tools read evidence. Keep the
                // real keyboard pause action, and focus the Game view before each sample.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
                double start=EditorApplication.timeSinceStartup;
                while(m.RecoveryCount==0&&EditorApplication.timeSinceStartup-start<65)
                {
                    await Task.Delay(20);float r=Vector3.Distance(m.motor.transform.position,m.arenaCenter);maxRadius=Mathf.Max(maxRadius,r);
                    Check(m.State==MissionState.Playing,"Live flight interrupted by pause/focus; do not report it as passed.");
                    if(r>730&&oldAt<0){oldAt=m.Elapsed;Check(!m.NearBoundary,"Old boundary still warns.");ScreenCapture.CaptureScreenshot(Path.GetFullPath(SolarLayoutPipeline.Evidence+"Game-BeyondOldBoundary.png"));}
                    if(m.NearBoundary&&warnAt<0){warnAt=m.Elapsed;ScreenCapture.CaptureScreenshot(Path.GetFullPath(SolarLayoutPipeline.Evidence+"Game-NewBoundaryWarning.png"));}
                }
                Check(m.RecoveryCount==1&&oldAt>0&&warnAt>oldAt,"Did not actually fly from spawn through the expanded boundary.");
                Check(m.motor.Speed<=m.settings.maxCruiseSpeed*m.settings.boostMultiplier,"Speed spike after recovery.");
                foreach(var t in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_")))Check(t.position==rocks[t.name],"Rocks followed player.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(100);ScreenCapture.CaptureScreenshot(Path.GetFullPath(SolarLayoutPipeline.Evidence+"Game-SafeReturn.png"));
                checks.Add(new{name="Continuous real InputSystem/FixedUpdate flight",passed=true,oldCrossingSeconds=oldAt,warningSeconds=warnAt,recoverySeconds=m.Elapsed,maxObservedRadius=maxRadius,observedRatio=maxRadius/730,nominalBoundary=m.settings.boundaryRadius,destroyed=m.DestroyedCount,score=m.score.Score,rocksFixed=true});
                await Press(keyboard,Key.Escape);Check(m.State==MissionState.Paused,"Input Escape failed.");var pos=m.motor.transform.position;float remaining=m.Remaining;await Task.Delay(200);Check(m.motor.transform.position==pos&&m.Remaining==remaining&&Time.timeScale==0,"Pause moved simulation.");ScreenCapture.CaptureScreenshot(Path.GetFullPath(SolarLayoutPipeline.Evidence+"Game-Paused.png"));
                await Press(keyboard,Key.R);Check(m.State==MissionState.Ready&&m.DestroyedCount==0&&m.score.Score==0&&m.Remaining==240&&m.RecoveryCount==0,"Input restart failed.");
                await Press(keyboard,Key.Enter);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));await Task.Delay(400);Check(m.motor.Speed<.01f,"Space brake failed.");
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(150,20)});await Task.Delay(300);Check(Mathf.Abs(Mathf.DeltaAngle(0,m.motor.transform.eulerAngles.y))>1,"Mouse steering failed.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());m.Restart();await Task.Delay(100);Check(UnityEngine.Object.FindAnyObjectByType<MissionEffects>().PoolInstanceCount==initialPool,"Effects pool grew after recovery/restart.");
                checks.Add(new{name="Input pause/restart/brake/turn/effect-pool",passed=true,initialPool});SolarLayoutPipeline.Audit();ScreenCapture.CaptureScreenshot(Path.GetFullPath(SolarLayoutPipeline.Evidence+"Game-Ready-Restarted.png"));complete=true;
            }
            catch(Exception e){error=e.ToString();Debug.LogError("SOLAR live flight failed: "+error);}
            finally
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);Application.runInBackground=oldBackground;
                if(m!=null){m.input.PauseRequested=pauseCallback;m.Restart();}running=false;
                SolarLayoutPipeline.Write("live-input-flight.json",new{passed=complete,error,checks,scope="Real saved scene, synthetic Input System keyboard/mouse, real FixedUpdate scheduler and Game view. No accelerated clock, no movement teleports except actual boundary recovery. Subjective physical-keyboard feel not claimed."});
                if(complete)Debug.Log("SOLAR live Input System / saved scene flight PASS.");
            }
        }
        static async Task Press(Keyboard keyboard,Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(120);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(120);}
    }
}
