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
    public sealed class DropletRebuiltPlayTests
    {
        MissionController m;MissionEffects effects;
        IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_Droplet_Rebuilt",LoadSceneMode.Single);
#endif
            yield return null;m=Object.FindAnyObjectByType<MissionController>();effects=Object.FindAnyObjectByType<MissionEffects>();Assert.AreEqual(120,m.TotalCount);m.enabled=false;m.input.enabled=false;Physics.SyncTransforms();
        }
        Transform Marker(string name)=>m.motor.visualRoot.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        [TearDown]public void Cleanup(){if(m!=null)m.Restart();Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;m=null;}
        [UnityTest]public IEnumerator HeadTailAlignWithMeasuredStraightFlightAfterTurnsAndReload()
        {
            for(int load=0;load<2;load++)
            {
                yield return Load();m.StartMission();
                foreach(var look in new[]{Vector2.zero,new Vector2(70,0),new Vector2(-100,0),new Vector2(0,65),new Vector2(0,-95)})
                {
                    // Turn samples are excluded from the straight-motion assertion.
                    for(int i=0;i<40;i++)m.Step(.02f,new FlightCommand{look=look*.03f});yield return null;
                    var start=m.motor.transform.position;for(int i=0;i<15;i++)m.Step(.02f,default);var delta=m.motor.transform.position-start;yield return null;
                    Assert.Greater(delta.magnitude,.1f);var axis=Marker("HeadMarker").position-Marker("TailMarker").position;
                    Assert.Greater(Vector3.Dot(axis.normalized,delta.normalized),.9999f,"Mesh endpoint direction opposed actual translation.");
                    Assert.Less(Vector3.Distance((Marker("HeadMarker").position+Marker("TailMarker").position)*.5f,m.motor.visualRoot.position),.0001f);
                    Assert.Greater(Vector3.Dot(m.chaseCamera.transform.up,Vector3.up),.8f);
                }
                m.Restart();yield return null;Assert.Greater(Vector3.Dot((Marker("HeadMarker").position-Marker("TailMarker").position).normalized,Vector3.forward),.9999f);
            }
        }
        [UnityTest]public IEnumerator FiveContinuousShipHitsPauseAndRestartPreserveRebuiltSurface()
        {
            yield return Load();m.StartMission();var start=m.motor.transform.position;
            for(int i=0;i<450&&m.DestroyedCount<5;i++)m.Step(.02f,new FlightCommand{throttle=1,boost=true});yield return null;
            Assert.GreaterOrEqual(m.DestroyedCount,5);Assert.Greater(m.score.Score,0);Assert.Greater(Vector3.Distance(start,m.motor.transform.position),500);
            Assert.Greater(Vector3.Dot((Marker("HeadMarker").position-Marker("TailMarker").position).normalized,(m.motor.transform.position-start).normalized),.9999f);
            m.TogglePause();var position=m.motor.transform.position;float remaining=m.Remaining;m.Step(1,new FlightCommand{boost=true});yield return new WaitForSecondsRealtime(.05f);Assert.AreEqual(position,m.motor.transform.position);Assert.AreEqual(remaining,m.Remaining);Assert.AreEqual(0,Time.timeScale);
            m.Restart();Assert.AreEqual(0,m.score.Score);Assert.AreEqual(0,m.DestroyedCount);Assert.AreEqual(540,m.Remaining);Assert.AreEqual(1,Time.timeScale);Assert.AreEqual(m.spawnPosition,m.motor.visualRoot.position);
        }
        [UnityTest]public IEnumerator SweptFullFleetVictoryAndThreeRestartsRetainMeshAndPool()
        {
            yield return Load();int pool=effects.PoolInstanceCount;var mesh=m.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh;var mat=m.motor.visualRoot.GetComponentInChildren<Renderer>().sharedMaterial;
            for(int run=0;run<3;run++)
            {
                m.StartMission();foreach(var target in m.targets)if(!target.IsDestroyed)Fly(target);Assert.AreEqual(120,m.DestroyedCount);Assert.IsTrue(m.Won);Assert.AreEqual(1,m.ResultTransitions);m.Restart();yield return null;
                Assert.AreEqual(0,m.DestroyedCount);Assert.AreEqual(0,m.score.Score);Assert.AreEqual(540,m.Remaining);Assert.AreEqual(pool,effects.PoolInstanceCount);Assert.AreEqual(0,effects.ActiveEffectCount);Assert.IsTrue(m.targets.All(t=>!t.IsDestroyed&&t.hitVolumes.All(c=>c.enabled)));
                Assert.AreSame(mesh,m.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh);Assert.AreSame(mat,m.motor.visualRoot.GetComponentInChildren<Renderer>().sharedMaterial);Assert.AreEqual(1,m.motor.visualRoot.GetComponentsInChildren<Renderer>(true).Length);Assert.Greater(Marker("HeadMarker").position.z,Marker("TailMarker").position.z);
            }
        }
        void Fly(ShipTarget target)
        {
            var main=target.hitVolumes.OrderByDescending(c=>c.bounds.size.sqrMagnitude).First();var center=main.bounds.center;var forward=target.transform.forward;var abs=new Vector3(Mathf.Abs(forward.x),Mathf.Abs(forward.y),Mathf.Abs(forward.z));float extent=target.hitVolumes.Max(c=>Mathf.Abs(Vector3.Dot(c.bounds.center-center,forward))+Vector3.Dot(c.bounds.extents,abs));float margin=m.settings.hitRadius+2;
            m.motor.ResetPose(center-forward*(extent+margin),target.transform.rotation);Physics.SyncTransforms();int steps=Mathf.CeilToInt(2*(extent+margin)/(m.settings.initialSpeed*.02f))+2;
            for(int i=0;i<steps&&m.State==MissionState.Playing;i++)m.Step(.02f,default);Assert.IsTrue(target.IsDestroyed,target.targetId);
        }
    }
}
