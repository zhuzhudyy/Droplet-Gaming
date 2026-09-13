using System.IO;
using System.Linq;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class PerfectDropletImportTests
    {
        [Test]
        public void ImportedMeshHasExpectedScaleForwardAndContinuousOutwardNormals()
        {
            var result=PerfectDropletImport.InspectAsset();
            Assert.AreEqual(5632,result.triangles);Assert.AreEqual(1,result.meshes);
            Assert.IsTrue(result.normalsImported && result.bakeAxisConversion && result.noAnimation && result.noColliders);
            Assert.AreEqual(0,result.inwardTriangles);Assert.AreEqual(0,result.badNormals);Assert.AreEqual(0,result.zeroArea);
            Assert.Less(result.seamNormalMaxDegrees,.1f);
            var importer=(ModelImporter)AssetImporter.GetAtPath(PerfectDropletImport.Model);
            Assert.IsFalse(importer.isReadable);Assert.AreEqual(ModelImporterMeshCompression.Off,importer.meshCompression);
        }

        [Test]
        public void SavedTestCopyChangesOnlyDropletMeshAndPreservesGameplayAndFleet()
        {
            var before=File.ReadAllBytes(PerfectDropletImport.BaselineScene);
            var after=File.ReadAllBytes(PerfectDropletImport.TestScene);
            var a=EditorSceneManager.OpenPreviewScene(PerfectDropletImport.BaselineScene);Scene b=default;
            try
            {
                b=EditorSceneManager.OpenPreviewScene(PerfectDropletImport.TestScene);
                var old=PerfectDropletImport.Components<MissionController>(a).Single();
                var current=PerfectDropletImport.Components<MissionController>(b).Single();
                Assert.AreSame(old.settings,current.settings);Assert.AreEqual(old.spawnPosition,current.spawnPosition);
                Assert.AreEqual(old.arenaCenter,current.arenaCenter);Assert.AreEqual(120,current.targets.Length);
                Assert.AreEqual(old.settings.hitRadius,current.motor.hitDetector.settings.hitRadius);
                Assert.AreEqual(old.motor.transform.localScale,current.motor.transform.localScale);
                Assert.AreEqual(old.motor.visualRoot.localScale,current.motor.visualRoot.localScale);
                Assert.AreEqual(old.chaseCamera.GetComponent<Camera>().fieldOfView,current.chaseCamera.GetComponent<Camera>().fieldOfView);
                var oldVisual=old.motor.visualRoot.GetComponentsInChildren<MeshFilter>(true).Single();
                var visual=current.motor.visualRoot.GetComponentsInChildren<MeshFilter>(true).Single();
                Assert.AreEqual(oldVisual.name,visual.name);Assert.AreNotSame(oldVisual.sharedMesh,visual.sharedMesh);
                Assert.AreEqual(PerfectDropletImport.Model,AssetDatabase.GetAssetPath(visual.sharedMesh));
                Assert.AreSame(oldVisual.GetComponent<Renderer>().sharedMaterial,visual.GetComponent<Renderer>().sharedMaterial);
                Assert.AreEqual(PerfectDropletImport.Components<Transform>(a).Length,PerfectDropletImport.Components<Transform>(b).Length);
                Assert.AreEqual(PerfectDropletImport.Components<Collider>(a).Length,PerfectDropletImport.Components<Collider>(b).Length);
                foreach(var t in current.targets)
                {
                    var previous=old.targets.Single(x=>x.targetId==t.targetId);
                    Assert.AreEqual(previous.transform.position,t.transform.position);
                    Assert.AreEqual(previous.transform.rotation,t.transform.rotation);
                    Assert.AreEqual(previous.hitVolumes.Length,t.hitVolumes.Length);
                    CollectionAssert.AreEqual(previous.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh),t.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));
                }
                Assert.AreEqual(1,current.motor.visualRoot.GetComponentsInChildren<Renderer>(true).Length);
                Assert.AreEqual(0,current.motor.visualRoot.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsNotNull(current.input);Assert.IsNotNull(current.motor.hitDetector);
                Assert.IsTrue(current.targets.All(t=>t!=null));
            }
            finally {if(b.IsValid())EditorSceneManager.ClosePreviewScene(b);EditorSceneManager.ClosePreviewScene(a);}
            CollectionAssert.AreEqual(before,File.ReadAllBytes(PerfectDropletImport.BaselineScene));
            CollectionAssert.AreEqual(after,File.ReadAllBytes(PerfectDropletImport.TestScene));
        }

        [Test]
        public void ReimportKeepsGuidMeshLocalIdAndVisualPrefabReferences()
        {
            string path=PerfectDropletImport.Model;string guid=AssetDatabase.AssetPathToGUID(path);
            var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string meshGuid,out long fileId));
            byte[] scene=File.ReadAllBytes(PerfectDropletImport.TestScene);
            // Only this task's imported mesh is reimported. No authoring or scene save.
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(path));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PerfectDropletImport.VisualPrefab);
            var again=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(again,out string newGuid,out long newId));
            Assert.AreEqual(meshGuid,newGuid);Assert.AreEqual(fileId,newId);
            Assert.AreEqual(PerfectDropletImport.MaterialPath,AssetDatabase.GetAssetPath(prefab.GetComponentInChildren<Renderer>().sharedMaterial));
            CollectionAssert.AreEqual(scene,File.ReadAllBytes(PerfectDropletImport.TestScene));
        }
    }
}
