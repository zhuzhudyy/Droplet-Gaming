using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class PerfectDropletPlayTests
    {
        MissionController mission;MissionEffects effects;
        const string Path="Assets/_Project/Scenes/FleetAssault_PerfectDroplet_Test.unity";
        IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Path,new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_PerfectDroplet_Test",LoadSceneMode.Single);
#endif
            yield return null;mission=Object.FindAnyObjectByType<MissionController>();effects=Object.FindAnyObjectByType<MissionEffects>();
            Assert.AreEqual(120,mission.TotalCount);Assert.AreEqual(540,mission.Remaining);
            Assert.IsTrue(mission.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.name.Contains("PerfectDroplet"));
            Assert.AreEqual(1,mission.motor.visualRoot.GetComponentsInChildren<Renderer>(true).Length);
            mission.enabled=false;mission.motor.enabled=false;mission.input.enabled=false;Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup(){if(mission!=null)mission.Restart();mission=null;effects=null;Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}

        [UnityTest] public IEnumerator NewMeshSurvivesSweptVictoryAndThreeRestarts()
        {
            yield return Load();var mesh=mission.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
            var renderer=mission.motor.visualRoot.GetComponentInChildren<MeshRenderer>();var mat=renderer.sharedMaterial;
            int pool=effects.PoolInstanceCount;
            for(int run=0;run<3;run++)
            {
                mission.Restart();mission.StartMission();
                foreach(var target in mission.targets)if(!target.IsDestroyed)FlyThrough(target);
                Assert.AreEqual(120,mission.DestroyedCount);Assert.IsTrue(mission.Won);Assert.AreEqual(MissionState.Results,mission.State);Assert.AreEqual(1,mission.ResultTransitions);
                Assert.Greater(mission.score.Score,0);mission.Restart();yield return null;
                Assert.AreEqual(MissionState.Ready,mission.State);Assert.AreEqual(0,mission.DestroyedCount);Assert.AreEqual(0,mission.score.Score);Assert.AreEqual(540,mission.Remaining);
                Assert.AreEqual(pool,effects.PoolInstanceCount);Assert.AreEqual(0,effects.ActiveEffectCount);
                Assert.AreSame(mesh,renderer.GetComponent<MeshFilter>().sharedMesh);Assert.AreSame(mat,renderer.sharedMaterial);Assert.IsTrue(renderer.enabled&&renderer.gameObject.activeInHierarchy);
                Assert.IsTrue(mission.targets.All(t=>!t.IsDestroyed&&t.hitVolumes.All(c=>c.enabled)));
                Assert.AreEqual(mission.spawnPosition,mission.motor.transform.position);
            }
        }

        [UnityTest] public IEnumerator ContinuousFiveShipFlightPauseAndRestartKeepVisualAligned()
        {
            yield return Load();mission.StartMission();var start=mission.motor.transform.position;
            for(int i=0;i<450&&mission.DestroyedCount<5;i++)mission.Step(.02f,new FlightCommand{throttle=1,boost=true});
            Assert.GreaterOrEqual(mission.DestroyedCount,5);Assert.Greater(Vector3.Distance(start,mission.motor.transform.position),500);
            mission.TogglePause();var position=mission.motor.transform.position;float time=mission.Remaining;
            mission.Step(1,new FlightCommand{boost=true});yield return new WaitForSecondsRealtime(.05f);
            Assert.AreEqual(position,mission.motor.transform.position);Assert.AreEqual(time,mission.Remaining);Assert.AreEqual(0,Time.timeScale);
            Assert.Less(Vector3.Distance(mission.motor.visualRoot.position,position),.001f);
            mission.Restart();Assert.AreEqual(1,Time.timeScale);Assert.AreEqual(mission.spawnPosition,mission.motor.visualRoot.position);
            Assert.Less(Quaternion.Angle(mission.motor.visualRoot.rotation,Quaternion.identity),.001f);
        }
        void FlyThrough(ShipTarget target)
        {
            var main=target.hitVolumes.OrderByDescending(c=>c.bounds.size.sqrMagnitude).First();var center=main.bounds.center;var forward=target.transform.forward;
            var abs=new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z));
            float extent=target.hitVolumes.Max(c=>Mathf.Abs(Vector3.Dot(c.bounds.center-center,forward))+Vector3.Dot(c.bounds.extents,abs));
            float margin=mission.settings.hitRadius+2;mission.motor.ResetPose(center-forward*(extent+margin),target.transform.rotation);Physics.SyncTransforms();
            int steps=Mathf.CeilToInt(2*(extent+margin)/(mission.settings.initialSpeed*.02f))+2;
            for(int i=0;i<steps&&mission.State==MissionState.Playing;i++)mission.Step(.02f,default);
            Assert.IsTrue(target.IsDestroyed,target.targetId);Assert.AreEqual(0,mission.RecoveryCount);
        }
    }
}
