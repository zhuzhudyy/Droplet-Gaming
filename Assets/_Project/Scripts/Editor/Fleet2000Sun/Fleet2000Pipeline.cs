using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class Fleet2000Pipeline
    {
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault_2000_Sun.unity";
        public const string Baseline = "Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity";
        public const string Art = "Assets/_Project/Art/Fleet2000Sun/";
        public const string Evidence = "docs/verification/Fleet2000Sun/";
        public const string LayoutPath = Art + "FleetLayout_2000_Sun.json";
        public const string PrefabPath = "Assets/_Project/Prefabs/Fleet/FusionFrigate_VisualUpgrade.prefab";
        [Serializable] public class Marker { public string name,id,modelId,group; public float[] position,forward,up,scale; }
        [Serializable] public class Layout { public int schemaVersion,targetCount,columns,rows,layers; public float shipLength; public Marker[] markers; }
        [Serializable] public class Audit
        {
            public string scene,unity,layout; public int targets,uniqueIds,colliders,uniqueMeshes,uniqueMaterials,created,updated;
            public bool unitRoots,posesMatch,allReferences,oldFleetAbsent; public Vector3 min,max,shipSize;
            public float missionSeconds,boundary,warning,minimumBoundsGap; public string[] ids;
        }
        public static void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
        static void Guard()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Stop Play and compilation before authoring.");
            Require(PrefabStageUtility.GetCurrentPrefabStage()==null,"Close Prefab stage before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene protected: "+SceneManager.GetSceneAt(i).path);
        }
        static Vector3 V(float[] v) { Require(v!=null&&v.Length==3,"Invalid layout vector");var r=new Vector3(v[0],v[1],v[2]);Require(SolarLayoutMath.IsFinite(r),"Nonfinite layout pose");return r; }
        public static Layout ReadLayout()
        {
            var c=JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            Require(c.schemaVersion==1&&c.targetCount==2000&&c.markers.Length==2000&&c.columns==50&&c.rows==20&&c.layers==2,"Expected schema 1 / exactly 50 x 20 x 2 authored markers");
            Require(c.markers.Select(m=>m.name).Distinct().Count()==2000&&c.markers.Select(m=>m.id).Distinct().Count()==2000,"Duplicate layout identity");
            foreach(var m in c.markers){Require(!string.IsNullOrWhiteSpace(m.id)&&m.name=="SPAWN_Small_"+m.id&&m.modelId=="FusionFrigate","Unowned model or marker identity");V(m.position);Require((V(m.scale)-Vector3.one).sqrMagnitude<.00001f,"Nonunit marker");Require(Vector3.Dot(V(m.forward),Vector3.forward)>.9999f&&Vector3.Dot(V(m.up),Vector3.up)>.9999f,"Formation directions differ");}
            return c;
        }
        static Transform Child(Transform p,string name)
        {var t=p.Find(name);if(t)return t;var g=new GameObject(name);g.transform.SetParent(p,false);Undo.RegisterCreatedObjectUndo(g,"Create owned formation branch");return g.transform;}
        static T Get<T>(GameObject g) where T:Component { return g.TryGetComponent<T>(out var c)?c:Undo.AddComponent<T>(g); }
        [MenuItem("DropletPrototype/Fleet 2000 Sun/1 Create or Reimport Saved Scene")]
        public static void Build()
        {
            Guard();Directory.CreateDirectory(Art);Directory.CreateDirectory(Evidence);
            string exported="ArtSource/Exports/Fleet2000Sun/FleetLayout_2000_Sun.json";
            Require(File.Exists(exported),"Run the Blender Fleet2000Sun exporter first");
            File.Copy(exported,LayoutPath,true);AssetDatabase.ImportAsset(LayoutPath,ImportAssetOptions.ForceSynchronousImport);
            var config=ReadLayout();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);Require(prefab!=null,"Existing FusionFrigate visual prefab missing");
            bool first=!File.Exists(ScenePath);if(first)Require(AssetDatabase.CopyAsset(Baseline,ScenePath),"Scene copy failed");
            var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath);
            var root=scene.GetRootGameObjects().Single(g=>g.GetComponent<FleetSceneRoot>());
            var mission=root.GetComponentInChildren<MissionController>();var generated=root.transform.Find("GeneratedFleet");Require(generated!=null,"GeneratedFleet owner missing");
            var old=generated.GetComponentsInChildren<ShipTarget>(true);Require(old.All(t=>!string.IsNullOrEmpty(t.targetId)&&t.targetId.StartsWith("SPAWN_")),"Unowned targets in generated fleet protected");
            Require(old.Select(t=>t.targetId).Distinct().Count()==old.Length,"Duplicate existing IDs; refused before scene mutation");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Import 2000 linked fleet poses");
            try
            {
            Undo.RecordObjects(new Object[]{mission,mission.motor,mission.motor.hitDetector,mission.score,mission.chaseCamera,mission.chaseCamera.GetComponent<Camera>(),root.GetComponentInChildren<DropletReflectionResponse>(),root.GetComponentInChildren<SolarLightingRig>(),root.GetComponentInChildren<HudPresenter>()},"Bind fleet scene dependencies");
            var existing=old.ToDictionary(t=>t.targetId);var retained=new HashSet<ShipTarget>();var targets=new List<ShipTarget>(2000);int created=0,updated=0;
            var effects=root.GetComponentInChildren<MissionEffects>();var pool=root.GetComponentInChildren<ReactorExplosionPool>();
            foreach(var pose in config.markers)
            {
                if(!existing.TryGetValue(pose.name,out var t))
                {var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,generated);Undo.RegisterCreatedObjectUndo(g,"Create independent ship identity");t=g.GetComponent<ShipTarget>();created++;}
                else {Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)==PrefabPath,"Different prefab on retained ID protected");updated++;}
                Undo.RecordObject(t,"Update stable identity");Undo.RecordObject(t.gameObject,"Update target name");Undo.RecordObject(t.transform,"Update Blender pose");
                t.name=pose.name;t.targetId=pose.name;
                var parent=Child(generated,pose.group??"Formation");if(t.transform.parent!=parent)Undo.SetTransformParent(t.transform,parent,"Group fleet layer");
                t.transform.SetPositionAndRotation(V(pose.position),Quaternion.LookRotation(V(pose.forward),V(pose.up)));t.transform.localScale=Vector3.one;
                var reactor=t.GetComponent<ReactorDestructionPresenter>();var legacy=t.GetComponent<DestructionPresenter>();
                Undo.RecordObject(reactor,"Bind existing reactor pool");Undo.RecordObject(legacy,"Bind existing effects");reactor.pool=pool;legacy.effects=effects;
                foreach(var o in new Object[]{t,t.transform,reactor,legacy}){EditorUtility.SetDirty(o);PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
                retained.Add(t);targets.Add(t);
            }
            foreach(var t in old)if(!retained.Contains(t))Undo.DestroyObjectImmediate(t.gameObject);
            var camera=mission.chaseCamera.GetComponent<Camera>();
            var owned=Child(root.transform,"GeneratedFleet2000Sun").gameObject;
            var manager=Get<FleetRenderManager>(owned);Undo.RecordObject(manager,"Configure fleet tiers");manager.chunkSize=1400;manager.Configure(targets.ToArray(),camera,mission.motor.transform);
            mission.motor.hitDetector.fleetRenderer=manager;
            string settingsPath=Art+"FleetMission_2000.asset";if(!File.Exists(settingsPath))Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(mission.settings),settingsPath),"Settings copy failed");
            var settings=AssetDatabase.LoadAssetAtPath<DropletSettings>(settingsPath);Undo.RecordObject(settings,"Adapt mission extent and time");settings.missionSeconds=5400;settings.boundaryWarningRadius=5900;settings.boundaryRadius=6500;EditorUtility.SetDirty(settings);
            Undo.RecordObject(mission,"Update mission membership");mission.targets=targets.ToArray();mission.settings=settings;mission.spawnPosition=new Vector3(0,8,0);mission.arenaCenter=new Vector3(93.5415f,65.564f,2501.823f);
            mission.motor.settings=settings;mission.motor.hitDetector.settings=settings;mission.score.settings=settings;mission.chaseCamera.settings=settings;
            var response=root.GetComponentInChildren<DropletReflectionResponse>();response.reactorSources=targets.Select(t=>t.visualRoot.transform.Find("FusionDriveEffects/LOD0/MainExhaust/MainExhaust_Core")).ToArray();
            var rig=root.GetComponentInChildren<SolarLightingRig>();rig.drives=targets.Select(t=>t.GetComponentInChildren<FusionDriveVisuals>(true)).ToArray();
            var hud=root.GetComponentInChildren<HudPresenter>();hud.locationLabel="SOLAR FLEET  /  50 x 20 x 2";
            var sun=Sun2000Authoring.Configure(scene,owned.transform,camera);
            var overview=Child(owned.transform,"Presentation View");Undo.RecordObject(overview,"Set presentation view");overview.SetPositionAndRotation(new Vector3(-6200,1000,1800),Quaternion.LookRotation(new Vector3(6700,-935,600),Vector3.up));
            var presentation=Get<FleetOverviewCamera>(owned);Undo.RecordObject(presentation,"Bind overview camera");presentation.mission=mission;presentation.view=camera;presentation.overview=overview;presentation.chase=mission.chaseCamera;
            camera.farClipPlane=Mathf.Max(camera.farClipPlane,25000);
            foreach(var c in new Object[]{mission,mission.motor,mission.motor.hitDetector,mission.chaseCamera,mission.score,response,rig,hud,manager,presentation,camera}){EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
            manager.RefreshNow();Inspect(created,updated);EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed");AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(500,65,2500),overview.rotation,7000);
            Debug.Log($"FLEET2000 saved: {created} created / {updated} retained at {ScenePath}");
            }
            catch { Undo.RevertAllDownToGroup(group);throw; }
        }
        [MenuItem("DropletPrototype/Fleet 2000 Sun/2 Inspect Saved Fleet")]
        public static void InspectMenu()=>Inspect(0,0);
        public static Audit Inspect(int created=0,int updated=0)
        {
            var m=Object.FindFirstObjectByType<MissionController>();Require(m!=null&&m.gameObject.scene.path==ScenePath,"Open the new scene first");var config=ReadLayout();var lookup=config.markers.ToDictionary(p=>p.name);var ts=m.targets;
            Require(ts.Length==2000&&ts.Select(t=>t.targetId).Distinct().Count()==2000,"Expected 2000 unique identities");
            var bounds=new Bounds(ts[0].transform.position,Vector3.zero);var allBounds=new Bounds[ts.Length];
            bool poses=true,units=true,refs=true;
            for(int i=0;i<ts.Length;i++)
            {var t=ts[i];bounds.Encapsulate(t.transform.position);poses&=Vector3.Distance(t.transform.position,V(lookup[t.targetId].position))<.01f&&Vector3.Dot(t.transform.forward,Vector3.forward)>.9999f;units&=Vector3.Distance(t.transform.lossyScale,Vector3.one)<.0001f;refs&=t.hitVolumes.Length==9&&t.hitVolumes.All(c=>c&&c.GetComponentInParent<ShipTarget>()==t)&&t.GetComponent<ReactorDestructionPresenter>().pool!=null;var lod=t.visualRoot.GetComponent<LODGroup>();var r=lod.GetLODs()[0].renderers.Where(r=>r.name.StartsWith("FusionFrigate_LOD")).ToArray();allBounds[i]=FusionFleetPipeline.BoundsOf(r);}
            float gap=float.MaxValue;for(int i=0;i<allBounds.Length;i++)for(int j=i+1;j<allBounds.Length;j++){Require(!allBounds[i].Intersects(allBounds[j]),"Intersecting hulls");var d=Vector3.Max(Vector3.zero,Vector3.Max(allBounds[i].min-allBounds[j].max,allBounds[j].min-allBounds[i].max));gap=Mathf.Min(gap,d.magnitude);}
            var report=new Audit{scene=m.gameObject.scene.path,unity=Application.unityVersion,layout=LayoutPath,targets=ts.Length,uniqueIds=ts.Select(t=>t.targetId).Distinct().Count(),colliders=ts.Sum(t=>t.hitVolumes.Length),unitRoots=units,posesMatch=poses,allReferences=refs,oldFleetAbsent=Object.FindObjectsByType<ShipTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==2000,min=bounds.min,max=bounds.max,shipSize=allBounds[0].size,minimumBoundsGap=gap,missionSeconds=m.settings.missionSeconds,boundary=m.settings.boundaryRadius,warning=m.settings.boundaryWarningRadius,created=created,updated=updated,ids=ts.Select(t=>t.targetId).ToArray(),uniqueMeshes=ts.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).Distinct().Count(),uniqueMaterials=ts.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().Count()};
            Require(ts.All(t=>Vector3.Distance(t.transform.position,m.arenaCenter)+32+400<m.settings.boundaryWarningRadius),"Target turning reserve outside warning boundary");
            Require(poses&&units&&refs&&report.oldFleetAbsent,"Scene audit failed");Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"scene-audit.json",JsonUtility.ToJson(report,true));return report;
        }
    }
}
