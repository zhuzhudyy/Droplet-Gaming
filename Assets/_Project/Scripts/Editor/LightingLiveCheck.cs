using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DropletPrototype.Editor
{
    public static class LightingLiveCheck
    {
        [Serializable] public class Report{public bool passed;public string error,scope;public int destroyed,score,pool;public float elapsed,distance,maxSpeed;public Vector3 endPosition,endForward;public string[] hitIds;}
        static bool running;
        [MenuItem("DropletPrototype/Lighting Upgrade/3 Live Input Flight Check")]
        public static async void Run()
        {
            if(running||!Application.isPlaying)throw new InvalidOperationException("Enter Play and run one live check at a time.");running=true;
            var m=UnityEngine.Object.FindAnyObjectByType<MissionController>();var e=UnityEngine.Object.FindAnyObjectByType<MissionEffects>();
            var keyboard=InputSystem.AddDevice<Keyboard>("FusionCheckKeyboard");var mouse=InputSystem.AddDevice<Mouse>("FusionCheckMouse");var bg=Application.runInBackground;
            // Keep physical editor mouse motion from replacing the diagnostic current devices.
            Action selectDevices=()=>{keyboard.MakeCurrent();mouse.MakeCurrent();};InputSystem.onAfterUpdate+=selectDevices;
            var report=new Report{scope="Saved expanded scene; synthetic Input System Enter/W/Shift/Esc/R/Space/mouse; real FixedUpdate and normal timeScale, no pose teleports along the flight, no direct remaining-count writes. Not subjective physical keyboard acceptance."};
            try
            {
                Application.runInBackground=true;m.Restart();m.enabled=true;m.input.enabled=true;m.chaseCamera.enabled=true;report.pool=e.PoolInstanceCount;
                await Press(keyboard,Key.Enter);Check(m.State==MissionState.Playing,"Enter failed.");
                var start=m.motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
                var deadline=DateTime.UtcNow.AddSeconds(12);
                while(m.DestroyedCount<5&&DateTime.UtcNow<deadline){await Task.Delay(100);report.maxSpeed=Mathf.Max(report.maxSpeed,m.motor.Speed);}
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());report.destroyed=m.DestroyedCount;report.score=m.score.Score;report.elapsed=m.Elapsed;report.distance=Vector3.Distance(start,m.motor.transform.position);report.endPosition=m.motor.transform.position;report.endForward=m.motor.transform.forward;report.hitIds=m.targets.Where(t=>t.IsDestroyed).Select(t=>t.targetId).ToArray();
                Check(report.destroyed>=5&&report.score>0&&report.distance>500,"Continuous first five-ship attack lane failed.");
                ScreenCapture.CaptureScreenshot(LightingUpgradeInspection.Evidence+"Lighting-Live-Five-Hits.png");await Task.Delay(150);
                await Press(keyboard,Key.Escape);Check(m.State==MissionState.Paused,"Escape failed.");var pos=m.motor.transform.position;float time=m.Remaining;await Task.Delay(250);Check(m.motor.transform.position==pos&&m.Remaining==time,"Pause advanced.");
                ScreenCapture.CaptureScreenshot(LightingUpgradeInspection.Evidence+"Lighting-Live-Paused.png");await Task.Delay(150);
                await Press(keyboard,Key.R);Check(m.State==MissionState.Ready&&m.TotalCount==120&&m.DestroyedCount==0&&m.score.Score==0&&m.Remaining==540,"R reset failed.");
                await Press(keyboard,Key.Enter);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));await Task.Delay(500);Check(m.motor.Speed<.01f,"Space brake failed.");
                float yaw=m.motor.transform.eulerAngles.y;InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(100,15)});await Task.Delay(250);Check(Mathf.Abs(Mathf.DeltaAngle(yaw,m.motor.transform.eulerAngles.y))>1,"Mouse turn failed.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());m.Restart();await Task.Delay(100);Check(e.PoolInstanceCount==report.pool,"Effect pool grew.");ScreenCapture.CaptureScreenshot(LightingUpgradeInspection.Evidence+"Lighting-Live-Restarted.png");report.passed=true;
            }
            catch(Exception ex){report.error=ex.ToString();}
            finally
            {
                InputSystem.onAfterUpdate-=selectDevices;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);Application.runInBackground=bg;m.Restart();running=false;
                File.WriteAllText(LightingUpgradeInspection.Evidence+"live-input.json",JsonUtility.ToJson(report,true));Debug.Log("FUSION live input "+report.passed+" "+report.error);
            }
        }
        static void Check(bool b,string message){if(!b)throw new InvalidOperationException(message);}
        static async Task Press(Keyboard keyboard,Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(120);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(120);}
    }
}
