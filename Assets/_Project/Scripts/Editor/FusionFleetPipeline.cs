using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace DropletPrototype.Editor
{
    public static class FusionFleetPipeline
    {
        public const string ScenePath="Assets/_Project/Scenes/FleetAssault_Expanded.unity";
        public const string Baseline="Assets/_Project/Scenes/FleetAssault_SolarLayout.unity";
        public const string PrefabPath="Assets/_Project/Prefabs/Fleet/FusionFrigate.prefab";
        public const string Art="Assets/_Project/Art/Models/Ships/FusionFrigate/";
        public const string LayoutArt="Assets/_Project/Art/Models/FleetExpansion/";
        public const string SettingsPath="Assets/_Project/Data/FleetMission_Expanded.asset";
        public const string Authority="Tools/Blender/FleetExpansion/layout_config.json";
        public const string Evidence="docs/verification/FleetExpansion/";
        [Serializable] public class Marker {public string name,id,group; public float[] position,forward,up,scale;}
        [Serializable] public class Config {public int originalCount,targetCount,seed;public float shipLength,missionDurationSeconds;public Marker[] markers;}
        [Serializable] public class ModelReport { public string unity;public int autoLodGroups,uniqueMeshes,uniqueMaterials,renderers,transforms,sockets;public bool readable;public LodReport[] lods;public string[] materials; }
        [Serializable] public class LodReport {public int level,renderers,meshes;public long triangles;public Vector3 min,max,size;}
        [Serializable] public class LayoutReport { public int count,originalCount,seed,squadrons,colliders,renderers,uniqueMesh,uniqueMaterial;public Vector3 centerMin,centerMax,centerSize,visualSize;public float nearestMin,nearestMedian,nearestMax,shipLength,nearestInLengths,groupClearance,boundary,warning,maxTargetRadius,missionSeconds;public string signature;public long meshNativeBytes,materialNativeBytes; }
        public static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Guard()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play before fleet authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene work is protected. Save it before using fleet authoring.");
            Require(PrefabStageUtility.GetCurrentPrefabStage()==null,"Close the Prefab Stage before fleet authoring.");
        }
        public static Vector3 V(float[] v)=>new Vector3(v[0],v[1],v[2]);
        static string Hash(string p){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        static void CopyIfChanged(string source,string dest)
        {
            Require(File.Exists(source),"Missing source "+source);Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if(!File.Exists(dest)||Hash(source)!=Hash(dest)){File.Copy(source,dest,true);AssetDatabase.ImportAsset(dest,ImportAssetOptions.ForceSynchronousImport);}
        }
        static Transform Child(Transform parent,string name)
        {
            var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create "+name);return go.transform;
        }
        static void Configure(string path,bool material)
        {
            var i=(ModelImporter)AssetImporter.GetAtPath(path);Require(i!=null,"Not an imported model "+path);
            i.globalScale=1;i.useFileScale=true;i.bakeAxisConversion=true;i.preserveHierarchy=true;
            i.importCameras=false;i.importLights=false;i.importAnimation=false;i.animationType=ModelImporterAnimationType.None;i.importBlendShapes=false;
            i.addCollider=false;i.isReadable=false;i.importVisibility=false;i.importNormals=ModelImporterNormals.Import;
            i.materialImportMode=material?ModelImporterMaterialImportMode.ImportStandard:ModelImporterMaterialImportMode.None;
            if(material)foreach(string name in new[]{"FF_Armor","FF_Structure","FF_EngineMetal","FF_BlueGray"})i.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+name+".mat"));
            i.SaveAndReimport();
        }
        static void Material(string name,Color color,float metal,float roughness)
        {
            string path=Art+"Materials/"+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",1-roughness);m.enableInstancing=false;
            AssetDatabase.CreateAsset(m,path);
        }
        [MenuItem("DropletPrototype/Fleet Expansion/1 Import Fusion Frigate")]
        public static void ImportModel()
        {
            Guard();Directory.CreateDirectory(Art+"Materials");AssetDatabase.Refresh();
            Material("FF_Armor",new Color(.390f,.430f,.455f),.50f,.42f);
            Material("FF_Structure",new Color(.063f,.083f,.103f),.45f,.52f);
            Material("FF_EngineMetal",new Color(.235f,.285f,.325f),.78f,.34f);
            Material("FF_BlueGray",new Color(.075f,.250f,.345f),.35f,.32f);
            CopyIfChanged("ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx",Art+"FusionFrigate.fbx");
            Configure(Art+"FusionFrigate.fbx",true);AssetDatabase.SaveAssets();
            InspectModel();Debug.Log("FUSION model imported and measured.");
        }
        public static Transform[] ModelLods(GameObject model)=>model.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="FusionFrigate_LOD0"||t.name=="FusionFrigate_LOD1"||t.name=="FusionFrigate_LOD2").OrderBy(t=>t.name).ToArray();
        public static long Triangles(Mesh mesh){long n=0;for(int i=0;i<mesh.subMeshCount;i++)n+=mesh.GetIndexCount(i)/3;return n;}
        public static Bounds BoundsOf(IEnumerable<Renderer> rs){var a=rs.ToArray();Require(a.Length>0,"Empty geometry");var b=a[0].bounds;foreach(var r in a)b.Encapsulate(r.bounds);return b;}
        public static ModelReport InspectModel()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"FusionFrigate.fbx");Require(asset!=null,"Import FusionFrigate first.");
            var temp=UnityEngine.Object.Instantiate(asset);temp.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var lods=ModelLods(temp);Require(lods.Length==3,"Expected three named LOD roots.");
                var rs=temp.GetComponentsInChildren<MeshRenderer>(true);var mats=rs.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var report=new ModelReport{unity=Application.unityVersion,autoLodGroups=temp.GetComponentsInChildren<LODGroup>(true).Length,uniqueMeshes=temp.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct().Count(),uniqueMaterials=mats.Length,renderers=rs.Length,transforms=temp.GetComponentsInChildren<Transform>(true).Length,sockets=temp.GetComponentsInChildren<Transform>(true).Count(t=>t.name.Contains("Exhaust")),readable=((ModelImporter)AssetImporter.GetAtPath(Art+"FusionFrigate.fbx")).isReadable,materials=mats.Select(m=>m==null?"MISSING":AssetDatabase.GetAssetPath(m)).ToArray(),lods=lods.Select((t,i)=>{var renderers=t.GetComponentsInChildren<MeshRenderer>(true);var b=BoundsOf(renderers);return new LodReport{level=i,renderers=renderers.Length,meshes=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(),triangles=renderers.Sum(r=>Triangles(r.GetComponent<MeshFilter>().sharedMesh)),min=b.min,max=b.max,size=b.size};}).ToArray()};
                File.WriteAllText(Evidence+"unity-model.json",JsonUtility.ToJson(report,true));
                foreach(var l in report.lods)Require(Vector3.Distance(l.size,new Vector3(7.38f,6.211535f,22.14f))<.01f,"Unexpected imported axes or dimensions: "+l.size);
                Require(report.sockets==5 && mats.All(m=>m!=null&&m.shader.name=="Universal Render Pipeline/Lit"),"Missing sockets/shared URP materials.");
                return report;
            }finally{UnityEngine.Object.DestroyImmediate(temp);}
        }
        [MenuItem("DropletPrototype/Fleet Expansion/2 Save Gameplay Prefab")]
        public static void SavePrefab()
        {
            Guard();InspectModel();bool exists=File.Exists(PrefabPath);
            var preview=exists?default(Scene):EditorSceneManager.NewPreviewScene();
            var root=exists?PrefabUtility.LoadPrefabContents(PrefabPath):new GameObject("FusionFrigate");
            if(!exists)SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                root.transform.localScale=Vector3.one;var visual=Child(root.transform,"VisualRoot");
                // Only these tool-owned branches are replaced; other authored children survive.
                foreach(string name in new[]{"VisualRoot","HitVolumes","Sockets"}){var t=root.transform.Find(name);if(t!=null)foreach(Transform child in t.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);}
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"FusionFrigate.fbx");var model=UnityEngine.Object.Instantiate(asset,visual,false);model.name="FusionFrigate_Model";
                foreach(var old in model.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(old);
                foreach(var t in model.GetComponentsInChildren<Transform>(true)){t.gameObject.SetActive(true);GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);}
                var lodRoots=ModelLods(model);var sockets=Child(root.transform,"Sockets");
                // Unity's imported Empty basis faces +Z despite the verified Blender socket contract.
                // Preserve the measured attachment position; normalize all five sockets to local +Z aft.
                foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Exhaust")).ToArray()){t.SetParent(sockets,true);t.localRotation=Quaternion.LookRotation(Vector3.back,Vector3.up);t.localScale=Vector3.one;}
                var group=visual.GetComponent<LODGroup>();if(group==null)group=visual.gameObject.AddComponent<LODGroup>();
                group.fadeMode=LODFadeMode.None;group.animateCrossFading=false;
                var heights=new[]{.12f,.035f,.001f};group.SetLODs(lodRoots.Select((t,i)=>new LOD(heights[i],t.GetComponentsInChildren<Renderer>(true))).ToArray());group.RecalculateBounds();
                var hits=Child(root.transform,"HitVolumes");
                void Box(string n,Vector3 p,Vector3 size){var go=new GameObject(n);go.transform.SetParent(hits,false);go.layer=LayerMask.NameToLayer("ShipTarget");var b=go.AddComponent<BoxCollider>();b.center=p;b.size=size;b.isTrigger=true;}
                // Tapered hull in three coarse pieces; four auxiliary cylinders share two tight pairs.
                Box("Aft hull",new Vector3(0,-.10f,-3.3f),new Vector3(7.20f,2.8f,7.0f));
                Box("Fore hull",new Vector3(0,-.05f,3.1f),new Vector3(5.5f,2.55f,5.8f));
                Box("Bow",new Vector3(0,-.05f,8.45f),new Vector3(2.80f,1.7f,5.24f));
                Box("Bridge",new Vector3(0,1.74f,-.53f),new Vector3(2.48f,1.04f,3.54f));
                var mainGo=new GameObject("Main engine");mainGo.transform.SetParent(hits,false);mainGo.layer=LayerMask.NameToLayer("ShipTarget");var main=mainGo.AddComponent<CapsuleCollider>();main.direction=2;main.center=new Vector3(0,-.05f,-8.4f);main.radius=1.68f;main.height=5.34f;main.isTrigger=true;
                // One oriented capsule for each major auxiliary engine; independent of LOD.
                foreach(var p in new[]{new Vector3(-2.5f,.15f,-8.68913f),new Vector3(2.5f,.15f,-8.68913f),new Vector3(-1.62f,-2f,-8.68913f),new Vector3(1.62f,-2f,-8.68913f)})
                {var go=new GameObject("Auxiliary engine");go.transform.SetParent(hits,false);go.layer=LayerMask.NameToLayer("ShipTarget");var c=go.AddComponent<CapsuleCollider>();c.direction=2;c.center=p;c.radius=.735f;c.height=3.72f;c.isTrigger=true;}
                var target=root.GetComponent<ShipTarget>();if(target==null)target=root.AddComponent<ShipTarget>();target.visualRoot=visual.gameObject;target.hitVolumes=hits.GetComponentsInChildren<Collider>(true);target.targetId="";
                var presenter=root.GetComponent<DestructionPresenter>();if(presenter==null)presenter=root.AddComponent<DestructionPresenter>();presenter.target=target;presenter.effects=null;presenter.wreckKind=0;presenter.wreckScale=Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }finally{if(exists)PrefabUtility.UnloadPrefabContents(root);else EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.SaveAssets();Debug.Log("FUSION gameplay prefab saved.");
        }
        public static Config ReadConfig()
        {
            var c=JsonUtility.FromJson<Config>(File.ReadAllText(Authority));Require(c.originalCount>0&&c.targetCount>c.originalCount&&c.markers.Length==c.targetCount,"Invalid expansion counts.");Require(c.markers.Select(m=>m.name).Distinct().Count()==c.targetCount,"Duplicate marker names.");return c;
        }
        [MenuItem("DropletPrototype/Fleet Expansion/3 Create or Update Expanded Fleet")]
        public static void Build()
        {
            Guard();var config=ReadConfig();Require(File.Exists(PrefabPath),"Save and validate the single gameplay prefab first.");
            Require(File.Exists(Evidence+"single-ship-play.json"),"Run the single-ship Play check before expanding.");
            CopyIfChanged("ArtSource/Exports/FleetExpansion/FleetLayout_Expanded.fbx",LayoutArt+"FleetLayout_Expanded.fbx");Configure(LayoutArt+"FleetLayout_Expanded.fbx",false);
            CopyIfChanged(Authority,LayoutArt+"layout_config.json");
            var layout=AssetDatabase.LoadAssetAtPath<GameObject>(LayoutArt+"FleetLayout_Expanded.fbx");var markers=layout.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("SPAWN_")).ToDictionary(t=>t.name);
            foreach(var marker in config.markers){Require(markers.ContainsKey(marker.name),"FBX missing "+marker.name);var t=markers[marker.name];Require(Vector3.Distance(t.position,V(marker.position))<.025f&&Vector3.Distance(t.forward,V(marker.forward))<.001f&&Vector3.Distance(t.up,V(marker.up))<.001f,"Marker pose mismatch "+marker.name);}
            Require(markers.Count==config.targetCount,"Unexpected extra FBX markers.");
            var ids=new HashSet<string>();foreach(var t in markers.Values){var parts=t.name.Split('_');Require(parts.Length==3&&parts[1]=="Small"&&ids.Add(parts[2]),"Invalid type/duplicate marker identity.");Require((t.lossyScale-Vector3.one).sqrMagnitude<.00001f&&t.localToWorldMatrix.determinant>0,"Non-unit marker scale.");}
            bool first=!File.Exists(ScenePath);if(first)Require(AssetDatabase.CopyAsset(Baseline,ScenePath),"Unable to create playable copy.");
            var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var root=scene.GetRootGameObjects().Single(g=>g.GetComponent<FleetSceneRoot>()!=null);var generated=root.transform.Find("GeneratedFleet");Require(generated!=null,"Existing fleet ownership group missing.");
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Update expanded fleet");Undo.RegisterFullObjectHierarchyUndo(root,"Update expanded fleet references");
            var existing=generated.GetComponentsInChildren<ShipTarget>(true);Require(existing.All(t=>!string.IsNullOrEmpty(t.targetId)&&t.targetId.StartsWith("SPAWN_")),"Unowned targets in GeneratedFleet; preserve and resolve before regeneration.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            // The original importer owns only SPAWN identities. Remove incompatible source-prefab instances explicitly within that same subtree.
            foreach(var t in existing)if(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=prefab)Undo.DestroyObjectImmediate(t.gameObject);
            var targets=FleetLayoutImporter.Import(layout,generated,prefab,prefab,prefab);
            var lookup=config.markers.ToDictionary(m=>m.name);
            foreach(var t in targets){var parent=Child(generated,lookup[t.targetId].group);if(t.transform.parent!=parent)Undo.SetTransformParent(t.transform,parent,"Group squadron");PrefabUtility.RecordPrefabInstancePropertyModifications(t.transform);}
            var m=root.GetComponentInChildren<MissionController>();var old=AssetDatabase.LoadAssetAtPath<DropletSettings>("Assets/_Project/Data/FleetMission_SolarLayout.asset");
            var settings=AssetDatabase.LoadAssetAtPath<DropletSettings>(SettingsPath);
            if(settings==null){AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(old),SettingsPath);settings=AssetDatabase.LoadAssetAtPath<DropletSettings>(SettingsPath);}
            Undo.RecordObject(settings,"Update expanded mission settings");settings.missionSeconds=config.missionDurationSeconds;settings.boundaryRadius=Mathf.Max(old.boundaryRadius,settings.boundaryRadius);settings.boundaryWarningRadius=Mathf.Max(old.boundaryWarningRadius,settings.boundaryWarningRadius);EditorUtility.SetDirty(settings);
            m.settings=settings;m.targets=targets;m.motor.settings=settings;m.motor.hitDetector.settings=settings;m.chaseCamera.settings=settings;m.score.settings=settings;
            var effects=root.GetComponentInChildren<MissionEffects>();effects.wreckPrefabs=Array.Empty<GameObject>();
            foreach(var t in targets){var p=t.GetComponent<DestructionPresenter>();p.effects=effects;PrefabUtility.RecordPrefabInstancePropertyModifications(p);}
            // This optional old G09 benchmark assumes old ship IDs and 30-60 targets. New validation has a separate explicit entry point.
            var legacy=root.GetComponentInChildren<FleetBenchmark>();if(legacy!=null)Undo.DestroyObjectImmediate(legacy);
            foreach(var obj in new UnityEngine.Object[]{m,m.motor,m.motor.hitDetector,m.chaseCamera,m.score,effects}){EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
            Validate(scene,config);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);Selection.activeGameObject=generated.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(100,60,700),Quaternion.Euler(24,-24,0),1100);
            Debug.Log("EXPANDED FLEET saved "+targets.Length+" independent targets.");
        }
        [MenuItem("DropletPrototype/Fleet Expansion/4 Validate Fleet")]
        public static void ValidateMenu(){Guard();Require(SceneManager.GetActiveScene().path==ScenePath,"Open FleetAssault_Expanded first.");Validate(SceneManager.GetActiveScene(),ReadConfig());Debug.Log("EXPANDED FLEET validation passed.");}
        public static LayoutReport Validate(Scene scene,Config config)
        {
            var ts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ShipTarget>(true)).OrderBy(t=>t.targetId).ToArray();
            Require(ts.Length==config.targetCount&&ts.Select(t=>t.targetId).Distinct().Count()==ts.Length,"Target count/identity mismatch.");
            var m=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MissionController>(true)).Single();Require(m.targets.Length==ts.Length&&new HashSet<ShipTarget>(m.targets).SetEquals(ts),"Incomplete mission membership.");
            var centers=new Bounds(ts[0].transform.position,Vector3.zero);var bounds=new Bounds[ts.Length];var nearest=new float[ts.Length];
            var byId=config.markers.ToDictionary(x=>x.name);
            for(int i=0;i<ts.Length;i++)
            {
                var t=ts[i];Require(Vector3.Distance(t.transform.position,V(byId[t.targetId].position))<.025f,"Saved pose mismatch "+t.name);Require((t.transform.lossyScale-Vector3.one).sqrMagnitude<.00001f,"Non-unit ship scale.");
                var groups=t.GetComponentsInChildren<LODGroup>(true);Require(groups.Length==1&&groups[0].lodCount==3,"Duplicate/missing LODGroup.");
                Require(t.hitVolumes.All(c=>c!=null&&c.GetComponentInParent<ShipTarget>()==t&&c.transform.IsChildOf(t.transform)&&!c.transform.IsChildOf(t.visualRoot.transform)),"Hit identity/LOD separation failed.");
                bounds[i]=BoundsOf(groups[0].GetLODs()[0].renderers);centers.Encapsulate(t.transform.position);
                nearest[i]=ts.Where(o=>o!=t).Min(o=>Vector3.Distance(t.transform.position,o.transform.position));
                Require(Vector3.Distance(t.transform.position,m.arenaCenter)+bounds[i].extents.magnitude+config.shipLength*6<m.settings.boundaryWarningRadius,"Target lacks reachable turning margin.");
            }
            for(int i=0;i<bounds.Length;i++)for(int j=i+1;j<bounds.Length;j++)Require(!bounds[i].Intersects(bounds[j]),"Interpenetrating ships.");
            var squad=ts.GroupBy(t=>byId[t.targetId].group).Select(g=>{var b=new Bounds(g.First().transform.position,Vector3.zero);foreach(var t in g)b.Encapsulate(BoundsOf(t.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers));return b;}).ToArray();float clear=float.MaxValue;
            for(int i=0;i<squad.Length;i++)for(int j=i+1;j<squad.Length;j++){var a=squad[i];var b=squad[j];var delta=Vector3.Max(Vector3.zero,Vector3.Max(a.min-b.max,b.min-a.max));clear=Mathf.Min(clear,delta.magnitude);}
            Require(clear>=config.shipLength*6,"Insufficient squadron clearance.");Array.Sort(nearest);
            var allBounds=bounds[0];foreach(var b in bounds)allBounds.Encapsulate(b);
            var renderers=ts.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).ToArray();var meshes=ts.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).Distinct().ToArray();var mats=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Require(mats.Length==4&&meshes.Length==19,"Per-instance resource duplication or missing model parts.");
            var report=new LayoutReport{count=ts.Length,originalCount=config.originalCount,seed=config.seed,squadrons=squad.Length,colliders=ts.Sum(t=>t.hitVolumes.Length),renderers=renderers.Length,uniqueMesh=meshes.Length,uniqueMaterial=mats.Length,centerMin=centers.min,centerMax=centers.max,centerSize=centers.size,visualSize=allBounds.size,nearestMin=nearest.First(),nearestMedian=(nearest[(nearest.Length-1)/2]+nearest[nearest.Length/2])*.5f,nearestMax=nearest.Last(),shipLength=config.shipLength,nearestInLengths=nearest[nearest.Length/2]/config.shipLength,groupClearance=clear,boundary=m.settings.boundaryRadius,warning=m.settings.boundaryWarningRadius,maxTargetRadius=ts.Max(t=>Vector3.Distance(t.transform.position,m.arenaCenter)),missionSeconds=m.settings.missionSeconds,signature=string.Join("|",ts.Select(t=>t.targetId+":"+t.transform.position.ToString("F4"))),meshNativeBytes=meshes.Sum(x=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(x)),materialNativeBytes=mats.Sum(x=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(x))};
            File.WriteAllText(Evidence+"unity-layout.json",JsonUtility.ToJson(report,true));return report;
        }
    }
}
