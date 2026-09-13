using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class VisualUpgradeAssetTests
    {
        const string Before="Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity";
        const string After="Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity";
        static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
        [Test]
        public void VersionedScenePreservesAllIdentityPosesAndGameplayTuning()
        {
            var a=EditorSceneManager.OpenPreviewScene(Before);var b=EditorSceneManager.OpenPreviewScene(After);
            try
            {
                var old=All<MissionController>(a).Single();var current=All<MissionController>(b).Single();
                Assert.AreEqual(120,current.targets.Length);Assert.AreSame(old.settings,current.settings);Assert.AreEqual(old.spawnPosition,current.spawnPosition);Assert.AreEqual(old.arenaCenter,current.arenaCenter);
                Assert.AreSame(old.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh,current.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh);
                var previous=old.targets.ToDictionary(t=>t.targetId);
                foreach(var t in current.targets)
                {
                    var was=previous[t.targetId];Assert.AreEqual(was.transform.position,t.transform.position);Assert.Less(Quaternion.Angle(was.transform.rotation,t.transform.rotation),.001f);Assert.AreEqual(Vector3.one,t.transform.localScale);
                    Assert.AreEqual(9,t.hitVolumes.Length);Assert.IsTrue(t.hitVolumes.All(c=>c.enabled&&!c.transform.IsChildOf(t.visualRoot.transform)));
                    for(int i=0;i<t.hitVolumes.Length;i++)Assert.Less(Vector3.Distance(was.hitVolumes[i].bounds.size*2.6f,t.hitVolumes[i].bounds.size),.02f,"Every query volume must grow with its visual");
                    Assert.AreEqual(Vector3.one*2.6f,t.visualRoot.transform.localScale);Assert.AreEqual(Vector3.one*2.6f,t.transform.Find("Sockets").localScale);
                    var presenter=t.GetComponent<ReactorDestructionPresenter>();Assert.NotNull(presenter.pool);Assert.NotNull(presenter.reactorMarker);Assert.AreEqual(new Vector3(0,-.05f,-7f),presenter.reactorMarker.localPosition);
                }
                Assert.AreEqual(JsonUtility.ToJson(All<SolarSystemBackdrop>(a).Single().Layout),JsonUtility.ToJson(All<SolarSystemBackdrop>(b).Single().Layout));
            }finally{EditorSceneManager.ClosePreviewScene(b);EditorSceneManager.ClosePreviewScene(a);}
        }
        [Test]
        public void SavedReflectionAndEffectBindingsAreBoundedAndSupported()
        {
            var s=EditorSceneManager.OpenPreviewScene(After);
            try
            {
                var pool=All<ReactorExplosionPool>(s).Single();var response=All<DropletReflectionResponse>(s).Single();Assert.AreSame(pool,response.explosionPool);Assert.AreEqual(120,response.reactorSources.Length);Assert.IsTrue(response.reactorSources.All(t=>t!=null));
                var rig=All<SolarLightingRig>(s).Single();Assert.AreEqual(1,All<ReflectionProbe>(s).Length);Assert.AreEqual(128,rig.localProbe.resolution);Assert.AreEqual(0,rig.localProbe.cullingMask&(1<<2));Assert.GreaterOrEqual(rig.settings.probeInterval,3);
                Assert.AreEqual(1,All<Light>(s).Count(l=>l.enabled&&l.gameObject.activeInHierarchy));Assert.IsEmpty(All<ShipTarget>(s).SelectMany(t=>t.GetComponentsInChildren<Light>(true)));
                var drop=response.targetRenderer.sharedMaterial;Assert.AreEqual("DropletPrototype/VisualUpgrade/PerfectChrome",drop.shader.name);Assert.AreEqual(1,drop.GetFloat("_Metallic"));Assert.GreaterOrEqual(drop.GetFloat("_Smoothness"),.98f);
                foreach(var shader in All<Renderer>(s).SelectMany(r=>r.sharedMaterials).Where(m=>m).Select(m=>m.shader).Concat(new[]{pool.fireMaterial.shader,pool.flashMaterial.shader,pool.debrisMaterial.shader}).Distinct())
                {Assert.NotNull(shader);Assert.IsTrue(shader.isSupported,shader.name);Assert.IsFalse(ShaderUtil.ShaderHasError(shader),shader.name);}
                var materials=All<ShipTarget>(s).SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();Assert.LessOrEqual(materials.Length,8);Assert.IsTrue(materials.All(AssetDatabase.Contains));
            }finally{EditorSceneManager.ClosePreviewScene(s);}
        }
        [Test]
        public void ScaledPrefabKeepsAllFiveDrivesInsideEachNativeLod()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Fleet/FusionFrigate_VisualUpgrade.prefab");var target=prefab.GetComponent<ShipTarget>();var drive=prefab.GetComponentInChildren<FusionDriveVisuals>(true);var lod=target.visualRoot.GetComponent<LODGroup>();Assert.AreEqual(3,lod.lodCount);
            foreach(var level in lod.GetLODs()){Assert.AreEqual(5,level.renderers.Intersect(drive.coreRenderers).Count());Assert.IsTrue(level.renderers.All(r=>r&&r.transform.IsChildOf(target.visualRoot.transform)));}
            foreach(var core in drive.coreRenderers)Assert.AreEqual("DropletPrototype/VisualUpgrade/ReactorCore",core.sharedMaterial.shader.name);
            Assert.IsEmpty(drive.GetComponentsInChildren<Collider>(true));Assert.AreEqual(15,drive.coreRenderers.Length);Assert.AreEqual(10,drive.detailRenderers.Length);
        }
    }
}
