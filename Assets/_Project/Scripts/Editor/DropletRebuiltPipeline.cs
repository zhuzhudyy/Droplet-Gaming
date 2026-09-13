using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit new-asset authoring and inspection only; no automatic generator or runtime changes.</summary>
    public static class DropletRebuiltPipeline
    {
        public const string Model="Assets/_Project/Art/Models/Droplet_Rebuilt/Droplet_Rebuilt.fbx";
        public const string Prefab="Assets/_Project/Prefabs/Player/Droplet_Rebuilt.prefab";
        public const string TestScene="Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity";
        public const string ReviewScene="Assets/_Project/Scenes/Droplet_Rebuilt_Review.unity";
        public const string Baseline="Assets/_Project/Scenes/FleetAssault_Lighting.unity";
        public const string Evidence="docs/verification/Droplet_Rebuilt/";
        public const string MaterialPath="Assets/_Project/Art/LightingUpgrade/MetalDroplet.mat";
        public static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void Write(string name,object value){Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+name,JsonUtility.ToJson(value,true));}
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
        static void Guard(){Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Stop Play/compilation first.");Require(PrefabStageUtility.GetCurrentPrefabStage()==null,"Prefab stage protected.");for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene protected: "+SceneManager.GetSceneAt(i).path);}
        public static T[] Components<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true)).ToArray();
        public static Transform Marker(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        [Serializable]public class AssetReport
        {public string version,hierarchy;public Vector3 size,center,head,tail;public int triangles,renderers,inward,zero;public float headZoneRadius,tailZoneRadius,seamNormalDegrees;public bool calibration,unitTransforms,noAnimation,noColliders;}
        public static AssetReport InspectAsset()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Model);Require(asset!=null,"Missing rebuilt FBX.");
            var filters=asset.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length==1,"One surface required.");var f=filters[0];var mesh=f.sharedMesh;var mat=f.transform.localToWorldMatrix;
            var ps=mesh.vertices.Select(p=>mat.MultiplyPoint3x4(p)).ToArray();var ns=mesh.normals.Select(n=>mat.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            var b=new Bounds(ps[0],Vector3.zero);foreach(var p in ps)b.Encapsulate(p);
            var tr=mesh.triangles;int inward=0,zero=0;
            for(int i=0;i<tr.Length;i+=3){int a=tr[i],c=tr[i+1],d=tr[i+2];var face=Vector3.Cross(ps[c]-ps[a],ps[d]-ps[a]);if(face.sqrMagnitude<1e-24f)zero++;if(Vector3.Dot(face,ns[a]+ns[c]+ns[d])<=0)inward++;}
            float seam=0;foreach(var g in ps.Select((p,i)=>new{p,i}).GroupBy(x=>x.p))foreach(var a in g)foreach(var c in g)seam=Mathf.Max(seam,Vector3.Angle(ns[a.i],ns[c.i]));
            var r=new AssetReport{version=Application.unityVersion,size=b.size,center=b.center,head=ps.OrderByDescending(p=>p.z).First(),tail=ps.OrderBy(p=>p.z).First(),triangles=tr.Length/3,renderers=asset.GetComponentsInChildren<Renderer>(true).Length,inward=inward,zero=zero,seamNormalDegrees=seam,
                headZoneRadius=ps.Where(p=>p.z>1.12f).Max(p=>new Vector2(p.x,p.y).magnitude),tailZoneRadius=ps.Where(p=>p.z< -1.12f).Max(p=>new Vector2(p.x,p.y).magnitude),
                hierarchy=string.Join("\n",asset.GetComponentsInChildren<Transform>(true).Select(t=>t.name+" p="+t.localPosition+" r="+t.localEulerAngles+" s="+t.localScale)),
                unitTransforms=asset.GetComponentsInChildren<Transform>(true).All(t=>t.localPosition==Vector3.zero&&Quaternion.Angle(t.localRotation,Quaternion.identity)<.001f&&t.localScale==Vector3.one),
                noAnimation=!((ModelImporter)AssetImporter.GetAtPath(Model)).importAnimation,noColliders=asset.GetComponentsInChildren<Collider>(true).Length==0};
            var calibration=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Fleet/calibration.fbx");var nodes=calibration.GetComponentsInChildren<Transform>(true);
            r.calibration=Vector3.Distance(nodes.Single(t=>t.name=="Cube_1m").GetComponent<Renderer>().bounds.size,Vector3.one)<.002f&&Vector3.Distance(nodes.Single(t=>t.name=="FRONT_TIP").position,new Vector3(0,0,3))<.002f&&Vector3.Distance(nodes.Single(t=>t.name=="UP_TIP").position,new Vector3(0,2.2f,0))<.002f;
            Require(Vector3.Distance(b.size,new Vector3(.8f,.8f,2.4f))<.0001f&&b.center.magnitude<.0001f,"Wrong dimensions or pivot.");
            Require(r.headZoneRadius>r.tailZoneRadius*3&&r.head.z>r.tail.z,"Actual mesh head/tail reversed.");Require(r.triangles==7040&&inward==0&&zero==0&&seam<.1f,"Bad geometry/normals.");Require(r.unitTransforms&&r.calibration,"Unexpected conversion transforms.");return r;
        }
        [MenuItem("DropletPrototype/Rebuilt Droplet/0 Diagnose Existing Scene")]
        public static void Diagnose()
        {
            Guard();var m=Object.FindAnyObjectByType<MissionController>();var v=m.motor.visualRoot;
            var f=v.GetComponentsInChildren<MeshFilter>(true).Single();var ps=f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p)).ToArray();var local=ps.Select(p=>m.motor.transform.InverseTransformPoint(p)).ToArray();
            var report=new Diagnosis{scene=SceneManager.GetActiveScene().path,mesh=AssetDatabase.GetAssetPath(f.sharedMesh),hierarchy=string.Join("\n",m.motor.transform.GetComponentsInChildren<Transform>(true).Select(t=>t.name+" p="+t.localPosition+" r="+t.localEulerAngles+" s="+t.localScale+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().Name)))),renderers=v.GetComponentsInChildren<Renderer>(true).Length,animations=v.GetComponentsInChildren<Animator>(true).Length,positiveRadius=local.Where(p=>p.z>1.176f).Max(p=>new Vector2(p.x,p.y).magnitude),negativeRadius=local.Where(p=>p.z< -1.176f).Max(p=>new Vector2(p.x,p.y).magnitude),hitRadius=m.settings.hitRadius};Write("unity-previous-diagnosis.json",report);Debug.Log(JsonUtility.ToJson(report));
        }
        [Serializable]class Diagnosis{public string scene,mesh,hierarchy;public int renderers,animations;public float positiveRadius,negativeRadius,hitRadius;}
        [MenuItem("DropletPrototype/Rebuilt Droplet/1 Import")]
        public static void Import()
        {
            Guard();Folder(Path.GetDirectoryName(Model).Replace('\\','/'));string source="ArtSource/Exports/Droplet/Droplet_Rebuilt/Droplet_Rebuilt.fbx";
            if(!File.Exists(Model))File.Copy(source,Model);else Require(File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(Model)),"Existing FBX differs; protect it.");
            AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);var i=(ModelImporter)AssetImporter.GetAtPath(Model);
            i.globalScale=1;i.useFileScale=true;i.bakeAxisConversion=true;i.importAnimation=false;i.animationType=ModelImporterAnimationType.None;i.importCameras=false;i.importLights=false;i.addCollider=false;i.isReadable=false;i.meshCompression=ModelImporterMeshCompression.Off;i.importNormals=ModelImporterNormals.Import;i.importTangents=ModelImporterTangents.CalculateMikk;i.materialImportMode=ModelImporterMaterialImportMode.None;i.SaveAndReimport();Write("unity-import.json",InspectAsset());Debug.Log("REBUILT_IMPORT_PASS");
        }
        static void AddMarkers(Transform parent,Mesh mesh)
        {
            foreach(var item in new[]{("HeadMarker",new Vector3(0,0,mesh.bounds.max.z)),("TailMarker",new Vector3(0,0,mesh.bounds.min.z)),("ForwardMarker",new Vector3(0,0,mesh.bounds.max.z+.5f))})
            {var go=new GameObject(item.Item1);Undo.RegisterCreatedObjectUndo(go,"Create endpoint inspection marker");go.transform.SetParent(parent,false);go.transform.localPosition=item.Item2;go.layer=2;}
        }
        [MenuItem("DropletPrototype/Rebuilt Droplet/2 Create Safe Playable Copy")]
        public static void CreatePlayable()
        {
            Guard();InspectAsset();Require(!File.Exists(TestScene)&&!File.Exists(Prefab),"Rebuilt assets already exist; open for review, do not silently overwrite.");
            Require(AssetDatabase.CopyAsset(Baseline,TestScene),"Copy stable scene failed.");var s=EditorSceneManager.OpenScene(TestScene,OpenSceneMode.Single);var m=Components<MissionController>(s).Single();var v=m.motor.visualRoot;var f=v.GetComponentsInChildren<MeshFilter>(true).Single();
            Require(f.transform!=v&&f.transform.childCount==0&&f.GetComponents<Component>().All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"Unexpected gameplay content on mesh child.");
            Require(f.transform.localScale==Vector3.one&&f.transform.localPosition==Vector3.zero&&Quaternion.Angle(f.transform.localRotation,Quaternion.identity)<.001f,"Investigate existing mesh transform before replacing.");
            int colliders=Components<Collider>(s).Length;var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentInChildren<MeshFilter>().sharedMesh;
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Rebuilt droplet in copied scene");Undo.RecordObject(f,"Replace visible mesh only");f.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(f);AddMarkers(f.transform,mesh);
            var visual=new GameObject("Droplet_Rebuilt");try{var child=new GameObject("Surface");child.transform.SetParent(visual.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=f.GetComponent<Renderer>().sharedMaterial;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;AddMarkers(child.transform,mesh);foreach(var t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;PrefabUtility.SaveAsPrefabAsset(visual,Prefab);}finally{Object.DestroyImmediate(visual);}
            Require(Components<Collider>(s).Length==colliders&&v.GetComponentsInChildren<Renderer>(true).Length==1,"Unexpected collider/renderer change.");
            EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);Write("unity-scene.json",new SceneReport{path=TestScene,baseline=Baseline,ships=m.targets.Length,colliders=colliders,hitRadius=m.settings.hitRadius,missionSeconds=m.settings.missionSeconds,root=m.motor.name,settings=AssetDatabase.GetAssetPath(m.settings),material=AssetDatabase.GetAssetPath(f.GetComponent<Renderer>().sharedMaterial),visualScale=v.localScale});
            Debug.Log("REBUILT_PLAYABLE_SAVED");
        }
        [Serializable]class SceneReport{public string path,baseline,root,settings,material;public int ships,colliders;public float hitRadius,missionSeconds;public Vector3 visualScale;}
        [MenuItem("DropletPrototype/Rebuilt Droplet/3 Create Review Scene")]
        public static void CreateReview()
        {
            Guard();Require(!File.Exists(ReviewScene),"Review exists; preserve it.");var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Droplet_Rebuilt_Inspection_Only");Undo.RegisterCreatedObjectUndo(root,"Create isolated review scene");
            var vis=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));vis.transform.SetParent(root.transform,false);
            Folder("Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection");var gray=new Material(Shader.Find("Universal Render Pipeline/Lit"));gray.SetColor("_BaseColor",new Color(.42f,.42f,.42f));gray.SetFloat("_Smoothness",.4f);AssetDatabase.CreateAsset(gray,"Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Gray.mat");var grayRenderer=vis.GetComponentInChildren<Renderer>();Undo.RecordObject(grayRenderer,"Assign inspection gray material");grayRenderer.sharedMaterial=gray;PrefabUtility.RecordPrefabInstancePropertyModifications(grayRenderer);
            var mirror=new Material(Shader.Find("Universal Render Pipeline/Lit"));mirror.SetColor("_BaseColor",new Color(.85f,.85f,.85f));mirror.SetFloat("_Metallic",1);mirror.SetFloat("_Smoothness",.965f);AssetDatabase.CreateAsset(mirror,"Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Mirror.mat");
            var cube=new Cubemap(128,TextureFormat.RGBA32,false);cube.name="InspectionStripes";
            for(int face=0;face<6;face++){var colors=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float u=2*(x+.5f)/128-1,v=2*(y+.5f)/128-1;Vector3 d;switch((CubemapFace)face){case CubemapFace.PositiveX:d=new Vector3(1,-v,-u);break;case CubemapFace.NegativeX:d=new Vector3(-1,-v,u);break;case CubemapFace.PositiveY:d=new Vector3(u,1,v);break;case CubemapFace.NegativeY:d=new Vector3(u,-1,-v);break;case CubemapFace.PositiveZ:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break;}float val=Mathf.SmoothStep(.04f,.7f,Mathf.InverseLerp(-.08f,.08f,Mathf.Sin(d.normalized.z*13)));colors[y*128+x]=new Color(val,val,val,1);}cube.SetPixels(colors,(CubemapFace)face);}cube.Apply();AssetDatabase.CreateAsset(cube,"Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Stripes.cubemap");
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.24f,.24f,.24f);
            var light=new GameObject("InspectionLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(30,-35,0);light.transform.SetParent(root.transform,false);
            var cam=new GameObject("InspectionCamera").AddComponent<Camera>();cam.tag="MainCamera";cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.065f,.08f);cam.orthographic=true;cam.orthographicSize=1.6f;cam.transform.SetParent(root.transform,false);cam.transform.SetPositionAndRotation(new Vector3(4,1.3f,2.2f),Quaternion.LookRotation(new Vector3(-4,-1.3f,-2.2f)));cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s,ReviewScene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(ReviewScene,OpenSceneMode.Single);Require(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single().sharedMaterial==gray,"Gray override lost on reopen.");Debug.Log("REBUILT_REVIEW_SAVED_AND_REOPENED");
        }
        [MenuItem("DropletPrototype/Rebuilt Droplet/4 Capture Review")]
        public static async void CaptureReview()
        {
            Require(SceneManager.GetActiveScene().path==ReviewScene&&!Application.isPlaying,"Open review scene outside Play.");var r=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Single();var cam=Camera.main;var original=r.sharedMaterial;var p=cam.transform.position;var q=cam.transform.rotation;
            var mirror=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Mirror.mat");
            try{var dirs=new[]{new Vector3(5,0,0),new Vector3(4,1.3f,2.2f),new Vector3(-4,1,-2),new Vector3(1,.4f,5),new Vector3(1,.4f,-5)};for(int i=0;i<dirs.Length;i++){cam.transform.SetPositionAndRotation(dirs[i],Quaternion.LookRotation(-dirs[i]));r.sharedMaterial=original;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();await Task.Delay(300);Capture(cam,"Unity-Gray-"+i);r.sharedMaterial=mirror;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();await Task.Delay(300);Capture(cam,"Unity-Mirror-"+i);}}finally{r.sharedMaterial=original;cam.transform.SetPositionAndRotation(p,q);}Debug.Log("REBUILT_REVIEW_CAPTURED");
        }
        [MenuItem("DropletPrototype/Rebuilt Droplet/7 Refine Inspection Only")]
        public static void RefineInspection()
        {
            Guard();var gray=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Gray.mat");Undo.RecordObject(gray,"Disable reflections for neutral inspection");gray.SetFloat("_Smoothness",.2f);gray.SetFloat("_SpecularHighlights",0);gray.SetFloat("_EnvironmentReflections",0);gray.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");gray.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");EditorUtility.SetDirty(gray);AssetDatabase.SaveAssetIfDirty(gray);
            var mirror=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Models/Droplet_Rebuilt/Inspection/Mirror.mat");Undo.RecordObject(mirror,"Use continuous inspection environment without cubemap sampling artifacts");mirror.shader=Shader.Find("DropletPrototype/Inspection/RebuiltAnalyticMirror");Require(mirror.shader!=null,"Missing inspection mirror shader.");EditorUtility.SetDirty(mirror);AssetDatabase.SaveAssetIfDirty(mirror);
            EditorSceneManager.OpenScene(ReviewScene,OpenSceneMode.Single);CaptureReview();
        }
        public static void Capture(Camera c,string name)
        {
            var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var active=RenderTexture.active;var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(Evidence+name+".png",texture.EncodeToPNG());}
            finally{RenderTexture.active=active;Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(rt);}
        }
        [MenuItem("DropletPrototype/Rebuilt Droplet/5 Open Playable")]
        public static void OpenPlayable(){Guard();EditorSceneManager.OpenScene(TestScene,OpenSceneMode.Single);}
    }
}
