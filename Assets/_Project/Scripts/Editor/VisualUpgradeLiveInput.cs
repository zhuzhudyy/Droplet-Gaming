using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class VisualUpgradeLiveInput
    {
        [Serializable]public class Sample{public Vector3 position,head,tail,displacement;public float elapsed,dot,speed,visualCenterError;}
        [Serializable]public class Report
        {public bool passed,pause,restart,brake,left,right,up,down,cameraUpright;public string error,scope;public int score,destroyed,poolBefore,poolAfter;public float distance,elapsed,minDot=1,maxCenterError,maxInterpolationLag;public List<Sample> samples=new List<Sample>();}
        static bool running;
        static void Check(bool ok,string message)=>DropletRebuiltPipeline.Require(ok,message);
        static Transform Marker(MissionController m,string name)=>DropletRebuiltPipeline.Marker(m.motor.visualRoot,name);
        [MenuItem("DropletPrototype/Visual Upgrade/6 Live Input Flight Check")]
        public static async void Run()
        {
            Check(Application.isPlaying&&!running&&SceneManager.GetActiveScene().path==VisualUpgradePipeline.ScenePath,"Enter rebuilt playable scene in Play mode.");running=true;
            var m=Object.FindAnyObjectByType<MissionController>();var effects=Object.FindAnyObjectByType<MissionEffects>();var k=InputSystem.AddDevice<Keyboard>("RebuiltValidationKeyboard");var mouse=InputSystem.AddDevice<Mouse>("RebuiltValidationMouse");
            bool background=Application.runInBackground;Action current=()=>{k.MakeCurrent();mouse.MakeCurrent();};InputSystem.onAfterUpdate+=current;
            var r=new Report{scope="Synthetic Input System keyboard/mouse; existing real MissionController.FixedUpdate and DropletMotor.LateUpdate. No teleport, strafe, or turns during straight endpoint assertion; pause capture uses last measured displacement. Physical-keyboard subjective feel is not claimed."};
            try
            {
                Application.runInBackground=true;m.Restart();r.poolBefore=effects.PoolInstanceCount;await Task.Delay(180);await Press(k,Key.Enter);Check(m.State==MissionState.Playing,"Enter failed.");
                var start=m.motor.transform.position;var previous=start;InputSystem.QueueStateEvent(k,new KeyboardState(Key.W,Key.LeftShift));var deadline=DateTime.UtcNow.AddSeconds(20);
                while(m.DestroyedCount<5&&DateTime.UtcNow<deadline)
                {
                    await Task.Delay(65);var p=m.motor.transform.position;var delta=p-previous;previous=p;if(delta.magnitude<.01f||m.motor.Speed<1)continue;
                    var h=Marker(m,"HeadMarker").position;var t=Marker(m,"TailMarker").position;float dot=Vector3.Dot((h-t).normalized,delta.normalized);float center=Vector3.Distance((h+t)*.5f,m.motor.visualRoot.position);float lag=Vector3.Distance(m.motor.visualRoot.position,m.motor.transform.position);r.maxInterpolationLag=Mathf.Max(r.maxInterpolationLag,lag);Check(center<.0001f&&lag<=m.motor.Speed*Time.fixedDeltaTime*2+.01f,"Visual center drift beyond existing interpolation allowance.");
                    r.samples.Add(new Sample{position=p,head=h,tail=t,displacement=delta,elapsed=m.Elapsed,dot=dot,speed=m.motor.Speed,visualCenterError=center});r.minDot=Mathf.Min(r.minDot,dot);r.maxCenterError=Mathf.Max(r.maxCenterError,center);Check(dot>.9999f,"Actual head/tail opposed measured forward displacement.");
                }
                InputSystem.QueueStateEvent(k,new KeyboardState());r.distance=Vector3.Distance(start,m.motor.transform.position);r.elapsed=m.Elapsed;r.score=m.score.Score;r.destroyed=m.DestroyedCount;Check(r.destroyed>=5&&r.distance>500&&r.samples.Count>10,"Continuous five-ship flight failed.");
                ScreenCapture.CaptureScreenshot(VisualUpgradePipeline.Evidence+"Live-FiveHits.png");await Task.Delay(160);await Press(k,Key.Escape);var pos=m.motor.transform.position;float time=m.Remaining;await Task.Delay(220);r.pause=m.State==MissionState.Paused&&pos==m.motor.transform.position&&time==m.Remaining;Check(r.pause,"Pause failed.");

                await Press(k,Key.R);r.restart=m.State==MissionState.Ready&&m.DestroyedCount==0&&m.score.Score==0&&m.Remaining==540;Check(r.restart,"Restart failed.");
                await Press(k,Key.Enter);InputSystem.QueueStateEvent(k,new KeyboardState(Key.Space));await Task.Delay(700);r.brake=m.motor.Speed<.01f;Check(r.brake,"Brake failed.");
                float yaw=m.motor.transform.eulerAngles.y;await Look(mouse,new Vector2(100,0));r.right=Mathf.DeltaAngle(yaw,m.motor.transform.eulerAngles.y)>1;
                yaw=m.motor.transform.eulerAngles.y;await Look(mouse,new Vector2(-100,0));r.left=Mathf.DeltaAngle(yaw,m.motor.transform.eulerAngles.y)<-1;
                float y=m.motor.transform.forward.y;await Look(mouse,new Vector2(0,100));r.up=m.settings.invertY?m.motor.transform.forward.y<y-.01f:m.motor.transform.forward.y>y+.01f;
                y=m.motor.transform.forward.y;await Look(mouse,new Vector2(0,-100));r.down=m.settings.invertY?m.motor.transform.forward.y>y+.01f:m.motor.transform.forward.y<y-.01f;
                r.cameraUpright=Vector3.Dot(m.chaseCamera.transform.up,Vector3.up)>.8f;Check(r.left&&r.right&&r.up&&r.down&&r.cameraUpright,"Steering/camera changed.");
                InputSystem.QueueStateEvent(k,new KeyboardState());for(int i=0;i<3;i++){m.Restart();await Task.Delay(130);Check(Vector3.Dot((Marker(m,"HeadMarker").position-Marker(m,"TailMarker").position).normalized,Vector3.forward)>.9999f,"Restart flipped model.");Check(m.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.name.Contains("Droplet_Rebuilt"),"Runtime replaced model.");}
                r.poolAfter=effects.PoolInstanceCount;Check(r.poolAfter==r.poolBefore,"Effects pool grew.");r.passed=true;ScreenCapture.CaptureScreenshot(VisualUpgradePipeline.Evidence+"Live-Restarted.png");await Task.Delay(150);
            }
            catch(Exception ex){r.error=ex.ToString();}
            finally{InputSystem.onAfterUpdate-=current;InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(k);Application.runInBackground=background;m.Restart();running=false;System.IO.File.WriteAllText(VisualUpgradePipeline.Evidence+"live-flight.json",JsonUtility.ToJson(r,true));Debug.Log("VISUAL_UPGRADE_LIVE "+JsonUtility.ToJson(r));}
        }
        static async Task Press(Keyboard k,Key key){InputSystem.QueueStateEvent(k,new KeyboardState(key));await Task.Delay(120);InputSystem.QueueStateEvent(k,new KeyboardState());await Task.Delay(120);}
        static async Task Look(Mouse m,Vector2 delta){InputSystem.QueueStateEvent(m,new MouseState{delta=delta});await Task.Delay(350);}
    }
}
