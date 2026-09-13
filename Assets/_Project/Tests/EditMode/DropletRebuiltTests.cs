using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DropletPrototype.Editor;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class DropletRebuiltTests
    {
        [Test]public void ActualImportedHeadIsPositiveZWithOutwardContinuousNormals()
        {var r=DropletRebuiltPipeline.InspectAsset();Assert.AreEqual(7040,r.triangles);Assert.IsTrue(r.calibration&&r.unitTransforms&&r.noColliders&&r.noAnimation);Assert.Greater(r.headZoneRadius,r.tailZoneRadius*3);}
        [Test]public void CopiedScenePreservesGameplayCameraFleetMaterialsAndCollisionGeometry()
        {
            string path=DropletRebuiltPipeline.TestScene;var bytes=File.ReadAllBytes(path);var a=EditorSceneManager.OpenPreviewScene(DropletRebuiltPipeline.Baseline);Scene b=default;
            try
            {
                b=EditorSceneManager.OpenPreviewScene(path);var old=DropletRebuiltPipeline.Components<MissionController>(a).Single();var m=DropletRebuiltPipeline.Components<MissionController>(b).Single();
                Assert.AreSame(old.settings,m.settings);Assert.AreEqual(old.spawnPosition,m.spawnPosition);Assert.AreEqual(old.arenaCenter,m.arenaCenter);Assert.AreEqual(120,m.targets.Length);Assert.AreEqual(.7f,m.settings.hitRadius,.0001);
                Assert.AreEqual(old.motor.transform.localPosition,m.motor.transform.localPosition);Assert.AreEqual(old.motor.transform.localRotation,m.motor.transform.localRotation);Assert.AreEqual(old.motor.transform.localScale,m.motor.transform.localScale);
                Assert.AreEqual(old.motor.visualRoot.localPosition,m.motor.visualRoot.localPosition);Assert.AreEqual(old.motor.visualRoot.localRotation,m.motor.visualRoot.localRotation);Assert.AreEqual(old.motor.visualRoot.localScale,m.motor.visualRoot.localScale);
                Assert.AreSame(m.motor,m.input.motor);Assert.AreSame(m.motor,m.chaseCamera.target);Assert.AreSame(m.settings,m.motor.settings);Assert.AreSame(m.settings,m.motor.hitDetector.settings);
                Assert.AreEqual(EditorJsonUtility.ToJson(old.chaseCamera.GetComponent<Camera>()),EditorJsonUtility.ToJson(m.chaseCamera.GetComponent<Camera>()));
                var of=old.motor.visualRoot.GetComponentInChildren<MeshFilter>();var f=m.motor.visualRoot.GetComponentInChildren<MeshFilter>();Assert.AreSame(of.GetComponent<Renderer>().sharedMaterial,f.GetComponent<Renderer>().sharedMaterial);Assert.AreEqual(DropletRebuiltPipeline.Model,AssetDatabase.GetAssetPath(f.sharedMesh));
                Assert.AreEqual(1,m.motor.visualRoot.GetComponentsInChildren<Renderer>(true).Length);Assert.AreEqual(0,m.motor.visualRoot.GetComponentsInChildren<Collider>(true).Length);
                Assert.AreEqual(DropletRebuiltPipeline.Components<Collider>(a).Length,DropletRebuiltPipeline.Components<Collider>(b).Length);
                foreach(var t in m.targets){var prior=old.targets.Single(x=>x.targetId==t.targetId);Assert.AreEqual(prior.transform.position,t.transform.position);Assert.AreEqual(prior.transform.rotation,t.transform.rotation);CollectionAssert.AreEqual(prior.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh),t.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));CollectionAssert.AreEqual(prior.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials),t.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials));Assert.AreEqual(prior.hitVolumes.Length,t.hitVolumes.Length);}
                var h=DropletRebuiltPipeline.Marker(f.transform,"HeadMarker");var tail=DropletRebuiltPipeline.Marker(f.transform,"TailMarker");Assert.AreEqual(new Vector3(0,0,1.2f),h.localPosition);Assert.AreEqual(new Vector3(0,0,-1.2f),tail.localPosition);
            }
            finally{if(b.IsValid())EditorSceneManager.ClosePreviewScene(b);EditorSceneManager.ClosePreviewScene(a);}
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test]public void ReimportKeepsPrefabMeshAndEndpoints()
        {
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(DropletRebuiltPipeline.Prefab);var mesh=p.GetComponentInChildren<MeshFilter>().sharedMesh;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);AssetDatabase.ImportAsset(DropletRebuiltPipeline.Model,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            p=AssetDatabase.LoadAssetAtPath<GameObject>(DropletRebuiltPipeline.Prefab);AssetDatabase.TryGetGUIDAndLocalFileIdentifier(p.GetComponentInChildren<MeshFilter>().sharedMesh,out string again,out long next);Assert.AreEqual(guid,again);Assert.AreEqual(id,next);
            Assert.Greater(DropletRebuiltPipeline.Marker(p.transform,"HeadMarker").position.z,DropletRebuiltPipeline.Marker(p.transform,"TailMarker").position.z);
        }
    }
}
