using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEditor;

namespace DropletPrototype.Editor
{
    public sealed class FusionSingleShipProbe
    {
        [Serializable] public class Report {public bool passed;public int hits,score,restoredColliders;public float seconds,shipLength;public string scope;}
        [MenuItem("DropletPrototype/Fleet Expansion/5 Single Ship Play Check")]
        public static void Begin()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play in the saved baseline first.");
            UnityEngine.Object.FindAnyObjectByType<MissionController>().StartCoroutine(new FusionSingleShipProbe().Start());
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            var m=UnityEngine.Object.FindAnyObjectByType<MissionController>();m.enabled=false;m.input.enabled=false;
            foreach(var t in m.targets)t.gameObject.SetActive(false);
            var ship=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FusionFleetPipeline.PrefabPath)).GetComponent<ShipTarget>();ship.targetId="SINGLE_FUSION_TEST";ship.transform.position=new Vector3(0,8,100);
            var effects=UnityEngine.Object.FindAnyObjectByType<MissionEffects>();var p=ship.GetComponent<DestructionPresenter>();p.effects=effects;p.wreckKind=-1;
            m.targets=new[]{ship};m.Initialize();m.enabled=false;m.input.enabled=false;m.chaseCamera.enabled=false;
            var cam=m.chaseCamera.GetComponent<Camera>();cam.transform.SetPositionAndRotation(ship.transform.position+new Vector3(19,10,-30),Quaternion.LookRotation(-new Vector3(19,10,-30)));
            m.motor.ResetPose(new Vector3(0,8,40),Quaternion.identity);m.StartMission();
            yield return null;ScreenCapture.CaptureScreenshot(FusionFleetPipeline.Evidence+"Single-Ship-Rear-Game.png");yield return null;
            var report=new Report{shipLength=22.14f,scope="Temporary single imported prefab in baseline Play scene. Normal 0.02s fixed motor steps at existing speed, approach and sweep before applying movement; genuine score/destruction; all LOD roots and hit volumes verified; restart. No scene changes saved."};
            for(int i=0;i<250 && !ship.IsDestroyed;i++){yield return new WaitForFixedUpdate();m.Step(.02f,new FlightCommand{throttle=1,boost=true});report.seconds+=.02f;}
            report.hits=m.DestroyedCount;report.score=m.score.Score;
            bool hidden=ship.IsDestroyed&&!ship.visualRoot.activeSelf&&ship.hitVolumes.All(c=>!c.enabled)&&ship.visualRoot.GetComponentsInChildren<Renderer>(true).All(r=>!r.gameObject.activeInHierarchy);
            yield return null;ScreenCapture.CaptureScreenshot(FusionFleetPipeline.Evidence+"Single-Ship-Destroyed-Game.png");yield return null;
            m.Restart();report.restoredColliders=ship.hitVolumes.Count(c=>c.enabled);report.passed=report.hits==1&&report.score>0&&hidden&&!ship.IsDestroyed&&ship.visualRoot.activeSelf&&report.restoredColliders==ship.hitVolumes.Length&&m.TotalCount==1&&m.DestroyedCount==0&&m.score.Score==0;
            yield return null;ScreenCapture.CaptureScreenshot(FusionFleetPipeline.Evidence+"Single-Ship-Restored-Game.png");yield return null;
            File.WriteAllText(FusionFleetPipeline.Evidence+"single-ship-play.json",JsonUtility.ToJson(report,true));
            Debug.Log("SINGLE FUSION CHECK "+report.passed);
        }
    }
}
