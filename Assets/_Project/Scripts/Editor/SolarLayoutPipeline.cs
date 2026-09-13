using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class SolarLayoutPipeline
    {
        public const string Art="Assets/_Project/Art/Environment/SolarSystemLayout/";
        public const string Shared="Assets/_Project/Art/Environment/SpaceEnvironment/";
        public const string Source="ArtSource/Blender/SpaceEnvironment/SolarLayoutExports/";
        public const string Evidence="docs/verification/SolarSystemLayout/";
        public const string Prefab="Assets/_Project/Prefabs/Environment/SpaceEnvironment_SolarLayout.prefab";
        public const string Review="Assets/_Project/Scenes/SolarSystemLayout_Review.unity";
        public const string Playable="Assets/_Project/Scenes/FleetAssault_SolarLayout.unity";
        public const string Baseline="Assets/_Project/Scenes/FleetAssault.unity";
        public const string SettingsPath="Assets/_Project/Data/FleetMission_SolarLayout.asset";
        const string Owned="GeneratedSolarEnvironment_v1";
        [Serializable] public class Marker { public string name,id,assetId,group;public int materialVariant;public float[] position,forward,up,scale; }
        [Serializable] public class View {public string name;public float[] position,target;public float fov;}
        [Serializable] public class Manifest {public string source_sha256,config_sha256;public Marker[] markers;public View[] cameras;}
        [Serializable] class Gate {public bool passed;public string source_sha256;}
        public static void Require(bool b,string message){if(!b)throw new InvalidOperationException(message);}
        public static void Write(string file,object data){Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+file,Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));}
        public static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
        public static float[] A(Vector3 v)=>new[]{v.x,v.y,v.z};
        static T Component<T>(GameObject go) where T:Component {var c=go.GetComponent<T>();return c==null?go.AddComponent<T>():c;}
        static Manifest Read()=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
        static SolarLayoutData Data()=>JsonUtility.FromJson<SolarLayoutData>(File.ReadAllText(Source+"SolarLayoutConfig.json"));
        static string Hash(string file){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
        static void ValidateAuthority()
        {
            var m=Read();var g=JsonUtility.FromJson<Gate>(File.ReadAllText(Evidence+"blender-visual-review.json"));
            Require(g.passed&&g.source_sha256==m.source_sha256&&m.source_sha256==Hash("ArtSource/Blender/SpaceEnvironment/SpaceEnvironment_SolarLayout.blend"),"Blender source/visual gate is stale.");
            Require(m.config_sha256==Hash("Tools/Blender/SolarSystemLayout/layout_config.json")&&m.config_sha256==Hash(Source+"SolarLayoutConfig.json")&&m.config_sha256==Hash(Art+"SolarLayoutConfig.json")&&Hash(Source+"manifest.json")==Hash(Art+"manifest.json")&&Hash(Source+"SolarLayoutMarkers.fbx")==Hash(Art+"SolarLayoutMarkers.fbx"),"Authority/export/import mismatch; run validated Blender export, then Import first.");
        }
        public static void Guard()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene work: "+SceneManager.GetSceneAt(i).path);
        }
        static Transform Child(Transform parent,string name)
        {
            var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create solar layout");return go.transform;
        }
        static void ConfigureModel(string path)
        {
            var m=(ModelImporter)AssetImporter.GetAtPath(path);m.globalScale=1;m.useFileScale=true;m.bakeAxisConversion=true;
            m.importAnimation=false;m.animationType=ModelImporterAnimationType.None;m.importBlendShapes=false;m.importCameras=false;m.importLights=false;m.addCollider=false;m.isReadable=false;m.generateSecondaryUV=false;
            m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.None;m.materialImportMode=ModelImporterMaterialImportMode.None;m.SaveAndReimport();
        }
        [MenuItem("DropletPrototype/Solar Layout/1 Import Validated Blender Layout")]
        public static void Import()
        {
            Guard();var m=Read();var g=JsonUtility.FromJson<Gate>(File.ReadAllText(Evidence+"blender-visual-review.json"));
            Require(g.passed&&g.source_sha256==m.source_sha256&&m.source_sha256==Hash("ArtSource/Blender/SpaceEnvironment/SpaceEnvironment_SolarLayout.blend"),"Blender visual/source gate changed.");
            Require(m.config_sha256==Hash("Tools/Blender/SolarSystemLayout/layout_config.json")&&m.config_sha256==Hash(Source+"SolarLayoutConfig.json"),"Authority/export configuration mismatch.");
            Require(m.markers.Select(x=>x.id).Distinct().Count()==m.markers.Length,"Duplicate stable IDs.");
            Directory.CreateDirectory(Art);
            foreach(string f in new[]{"SolarLayoutMarkers.fbx","SolarLayoutConfig.json","manifest.json"})File.Copy(Source+f,Art+f,true);
            // No shared mesh, material or texture is rewritten by this importer.
            AssetDatabase.Refresh();ConfigureModel(Art+"SolarLayoutMarkers.fbx");
            ValidatePoses(m);Debug.Log("SOLAR Import PASS: "+m.markers.Length+" poses; existing shared resources retained.");
        }
        static Dictionary<string,Transform> ValidatePoses(Manifest data)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"SolarLayoutMarkers.fbx");Require(asset!=null,"Missing Blender marker asset.");
            var poses=asset.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_",StringComparison.Ordinal)).ToDictionary(t=>t.name);
            Require(poses.Count==data.markers.Length,"FBX marker count differs.");
            foreach(var m in data.markers)
            {
                Require(poses.ContainsKey(m.name),"Missing pose "+m.name);var t=poses[m.name];
                Require(Vector3.Distance(t.position,V(m.position))<.025f&&Vector3.Distance(t.forward,V(m.forward))<.002f&&Vector3.Distance(t.up,V(m.up))<.002f,"FBX basis/pose mismatch: "+m.id);
                Require(Vector3.Distance(t.lossyScale,V(m.scale))<.01f&&t.lossyScale.x>0&&t.localToWorldMatrix.determinant>0,"Invalid marker scale.");
            }
            // Reuse the independently calibrated cube/axis artifact, check the actual imported object.
            var cal=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"Layout/SpaceCalibration.fbx").GetComponentsInChildren<Transform>();
            Require(Vector3.Distance(cal.Single(t=>t.name=="Cube_1m").GetComponent<Renderer>().bounds.size,Vector3.one)<.002f,"Calibration units changed.");
            Require(Vector3.Distance(cal.Single(t=>t.name=="FRONT_TIP").position,new Vector3(0,0,3))<.002f,"Calibration forward changed.");
            return poses;
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Shared+"Materials/"+name+".mat");
        static Transform Model(Transform parent,string aid,int variant=0)
        {
            var rs=new List<Renderer>();int count=aid.StartsWith("Rock")||aid=="Earth"?3:1;
            for(int i=0;i<count;i++)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"Models/"+aid+"_LOD"+i+".fbx");Require(asset!=null,"Missing shared model "+aid);
                var child=parent.Find("LOD"+i);
                if(child!=null&&PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)!=asset){Undo.DestroyObjectImmediate(child.gameObject);child=null;}
                if(child==null){child=((GameObject)PrefabUtility.InstantiatePrefab(asset,parent)).transform;child.name="LOD"+i;Undo.RegisterCreatedObjectUndo(child.gameObject,"Attach shared model");}
                child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var renderer=child.GetComponentInChildren<MeshRenderer>();
                renderer.sharedMaterial=Mat(aid.StartsWith("Rock")?(variant==0?"RockBasalt":"RockSlate"):aid=="Sun"?"SunBase":aid=="Earth"?"EarthBase":"StarsBase");
                Require(renderer.sharedMaterial!=null,"Missing shared material.");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;rs.Add(renderer);
            }
            if(count==3)
            {
                var lod=Component<LODGroup>(parent.gameObject);lod.fadeMode=LODFadeMode.None;lod.animateCrossFading=false;
                var thresholds=aid=="Earth"?new[]{.12f,.045f,0f}:new[]{.07f,.016f,.0007f};lod.SetLODs(rs.Select((r,i)=>new LOD(thresholds[i],new[]{r})).ToArray());lod.RecalculateBounds();
            }
            return parent;
        }
        static SolarSystemBackdrop UpdatePrefab(Manifest manifest,SolarLayoutData data)
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);Scene previous=SceneManager.GetActiveScene();Scene temp=default;
            if(existing==null){temp=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(temp);}
            var root=existing==null?new GameObject("SpaceEnvironment_SolarLayout"):PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(root,"Update solar owned subtree");var generated=Child(root.transform,Owned);var poses=ValidatePoses(manifest);var names=new HashSet<string>(manifest.markers.Select(m=>m.name));
                foreach(var stale in generated.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_")&&!names.Contains(t.name)).ToArray())Undo.DestroyObjectImmediate(stale.gameObject);
                foreach(var m in manifest.markers)
                {
                    var parent=Child(generated,m.group);var found=generated.GetComponentsInChildren<Transform>(true).Where(t=>t.name==m.name).ToArray();Require(found.Length<=1,"Duplicate saved marker "+m.id);
                    var t=found.Length==0?Child(parent,m.name):found[0];if(t.parent!=parent)Undo.SetTransformParent(t,parent,"Update environment partition");
                    var pose=poses[m.name];t.localPosition=pose.position;t.localRotation=pose.rotation;t.localScale=pose.lossyScale;Model(t,m.assetId,m.materialVariant);
                }
                var b=Component<SolarSystemBackdrop>(root);b.layoutJson=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"SolarLayoutConfig.json");b.observer=null;
                b.sunProxy=Model(Child(generated,"SunProxy"),"Sun");b.earthProxy=Model(Child(generated,"EarthProxy"),"Earth");b.skyProxy=Model(Child(generated,"SkyProxy"),"Sky");b.skyProxy.localScale=Vector3.one*data.skyRadius;
                b.ReloadLayout();var cg=new GameObject("Temporary mapping camera");try{var c=cg.AddComponent<Camera>();c.transform.position=new Vector3(0,10.2f,-8);b.ApplyMapping(c);}finally{UnityEngine.Object.DestroyImmediate(cg);}
                int layer=LayerMask.NameToLayer("SpaceEnvironment");Require(layer>=0,"Existing environment layer unavailable.");
                foreach(var t in generated.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=layer;GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);}
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally {if(existing==null){UnityEngine.Object.DestroyImmediate(root);SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(temp,true);}else PrefabUtility.UnloadPrefabContents(root);}
            return AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<SolarSystemBackdrop>();
        }
        static int NeutralRenderer()
        {
            var so=new SerializedObject(GraphicsSettings.currentRenderPipeline);var list=so.FindProperty("m_RendererDataList");var target=AssetDatabase.LoadMainAssetAtPath(Shared+"PreviewOnly/NeutralRenderer.asset");
            for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==target)return i;
            throw new InvalidOperationException("Previously installed neutral renderer is missing; no automatic pipeline mutation.");
        }
        static SolarSystemBackdrop EnsureEnvironment(Scene scene)
        {
            var roots=scene.GetRootGameObjects().Where(g=>g.GetComponent<SolarSystemBackdrop>()!=null).ToArray();Require(roots.Length<=1,"Multiple solar environments.");
            if(roots.Length==0){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);Undo.RegisterCreatedObjectUndo(go,"Add solar environment");return go.GetComponent<SolarSystemBackdrop>();}
            return roots[0].GetComponent<SolarSystemBackdrop>();
        }
        [MenuItem("DropletPrototype/Solar Layout/2 Save Prefab and Review")]
        public static void BuildReview()
        {
            Guard();ValidateAuthority();var manifest=Read();var data=Data();UpdatePrefab(manifest,data);
            Scene sc=File.Exists(Review)?EditorSceneManager.OpenScene(Review,OpenSceneMode.Single):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var b=EnsureEnvironment(sc);var rig=sc.GetRootGameObjects().SingleOrDefault(g=>g.name=="PreviewOnly_SolarLayout");
            if(rig==null){rig=new GameObject("PreviewOnly_SolarLayout");Undo.RegisterCreatedObjectUndo(rig,"Create review rig");}
            foreach(var v in manifest.cameras)
            {
                var t=Child(rig.transform,v.name);var c=Component<Camera>(t.gameObject);c.transform.SetPositionAndRotation(V(v.position),Quaternion.LookRotation(V(v.target)-V(v.position),Vector3.up));
                c.fieldOfView=v.fov;c.nearClipPlane=data.nearClip;c.farClipPlane=data.farClip;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;c.allowHDR=false;c.allowMSAA=false;c.enabled=v.name=="SpawnForward";
                var urp=Component<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(c.gameObject);urp.SetRenderer(NeutralRenderer());urp.renderPostProcessing=false;
                if(c.enabled){c.tag="MainCamera";b.observer=c;}
            }
            var light=Child(rig.transform,"Neutral inspection light");var l=Component<Light>(light.gameObject);l.type=LightType.Directional;l.color=Color.white;l.intensity=1.4f;l.shadows=LightShadows.None;light.rotation=Quaternion.Euler(35,-25,0);
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.15f,.15f);
            b.ApplyMapping(b.observer);EditorSceneManager.MarkSceneDirty(sc);EditorSceneManager.SaveScene(sc,Review);AssetDatabase.SaveAssets();Debug.Log("SOLAR review/prefab saved in Edit mode.");
        }
        [MenuItem("DropletPrototype/Solar Layout/3 Save Playable Variant")]
        public static void BuildPlayable()
        {
            Guard();ValidateAuthority();Require(File.Exists(Prefab),"Build the validated environment first.");var data=Data();
            if(!File.Exists(Playable))Require(AssetDatabase.CopyAsset(Baseline,Playable),"Unable to create scene copy.");
            var sc=EditorSceneManager.OpenScene(Playable,OpenSceneMode.Single);var fleet=sc.GetRootGameObjects().Single(g=>g.GetComponent<FleetSceneRoot>()!=null);
            var env=fleet.transform.Find("Environment");Require(env!=null,"Existing environment ownership root missing.");
            // Only these two explicitly authored old background objects are replaced. Keep its lights and any manual children.
            foreach(string name in new[]{"Earth - distant non-target","Starfield - single static mesh"}){var t=env.Find(name);if(t!=null)Undo.DestroyObjectImmediate(t.gameObject);}
            var b=EnsureEnvironment(sc);var m=fleet.GetComponentInChildren<MissionController>();var original=AssetDatabase.LoadAssetAtPath<DropletSettings>("Assets/_Project/Data/FleetMission.asset");var settings=AssetDatabase.LoadAssetAtPath<DropletSettings>(SettingsPath);
            if(settings==null){settings=UnityEngine.Object.Instantiate(original);settings.name="FleetMission_SolarLayout";AssetDatabase.CreateAsset(settings,SettingsPath);}
            settings.boundaryRadius=data.boundaryRadius;settings.boundaryWarningRadius=data.warningRadius;EditorUtility.SetDirty(settings);
            Undo.RecordObject(m,"Bind solar boundary settings");m.settings=settings;m.motor.settings=settings;m.motor.hitDetector.settings=settings;m.score.settings=settings;m.chaseCamera.settings=settings;
            var c=m.chaseCamera.GetComponent<Camera>();c.farClipPlane=data.farClip;b.observer=c;b.ReloadLayout();b.ApplyMapping(c);
            m.GetComponent<PlayerOptions>().mission=m;var hud=fleet.GetComponentInChildren<HudPresenter>();hud.locationLabel="MAIN BELT  /  TRAINING SORTIE";
            foreach(var component in new UnityEngine.Object[]{m,m.motor,m.motor.hitDetector,m.score,m.chaseCamera,c,b,hud}){EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            Require(m.targets.Length==40,"Combat target count changed.");EditorSceneManager.MarkSceneDirty(sc);EditorSceneManager.SaveScene(sc);AssetDatabase.SaveAssets();Debug.Log("SOLAR playable variant saved: "+settings.boundaryRadius+" metres; combat unchanged.");
        }
        [MenuItem("DropletPrototype/Solar Layout/0 Capture Current Baseline")]
        public static void CaptureBaseline()
        {
            Guard();var m=UnityEngine.Object.FindFirstObjectByType<MissionController>();Require(m!=null,"Open FleetAssault first.");var c=m.chaseCamera.GetComponent<Camera>();
            Write("baseline-fleet.json",new {scene=m.gameObject.scene.path,settings=JsonUtility.ToJson(m.settings),spawn=A(m.spawnPosition),center=A(m.arenaCenter),targets=m.targets.Select(t=>new {t.targetId,position=A(t.transform.position),rotation=new[]{t.transform.rotation.x,t.transform.rotation.y,t.transform.rotation.z,t.transform.rotation.w},scale=A(t.transform.localScale)}),camera=new{position=A(c.transform.position),rotation=A(c.transform.eulerAngles),c.fieldOfView,c.nearClipPlane,c.farClipPlane}});
            Capture(c,"Before-Fleet-Spawn.png");Debug.Log("SOLAR baseline saved.");
        }
        public static void Capture(Camera c,string file)
        {
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);var old=c.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory(Evidence);File.WriteAllBytes(Evidence+file,image.EncodeToPNG());}
            finally{c.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
        [MenuItem("DropletPrototype/Solar Layout/5 Capture Review Views")]
        public static void CaptureReview()
        {
            Guard();Require(SceneManager.GetActiveScene().path==Review,"Open solar review first.");var b=UnityEngine.Object.FindFirstObjectByType<SolarSystemBackdrop>();var original=b.observer;var cameras=GameObject.Find("PreviewOnly_SolarLayout").GetComponentsInChildren<Camera>();
            foreach(var c in cameras){b.observer=c;b.ApplyMapping(c);Capture(c,"Unity-"+c.name+".png");}b.observer=original;b.ApplyMapping(original);
            Write("review-camera-poses.json",cameras.Select(c=>new{c.name,position=A(c.transform.position),rotation=A(c.transform.eulerAngles),c.fieldOfView,width=1280,height=720}));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Debug.Log("SOLAR eight actual camera renders saved.");
        }
        [MenuItem("DropletPrototype/Solar Layout/4 Audit Current Scene")]
        public static void Audit()
        {
            var b=UnityEngine.Object.FindFirstObjectByType<SolarSystemBackdrop>();Require(b!=null,"Open a solar scene.");var manifest=Read();var poses=ValidatePoses(manifest);var generated=b.transform.Find(Owned);var rs=generated.GetComponentsInChildren<MeshRenderer>(true);var meshes=rs.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();var mats=rs.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();var textures=mats.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).Where(t=>t!=null).Distinct().ToArray();
            foreach(var r in rs)Require(r.sharedMaterial!=null&&!r.isPartOfStaticBatch&&AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh).StartsWith(Shared),"Unshared resource or batching copy.");
            foreach(var record in manifest.markers)
            {
                var t=generated.GetComponentsInChildren<Transform>(true).Single(x=>x.name==record.name);var p=poses[record.name];Require(Vector3.Distance(t.position,p.position)<.025f&&Quaternion.Angle(t.rotation,p.rotation)<.02f,"Saved pose changed "+record.id);
                for(int i=0;i<3;i++){var actual=t.Find("LOD"+i).GetComponentInChildren<MeshFilter>().sharedMesh;var expected=AssetDatabase.LoadAssetAtPath<GameObject>(Shared+"Models/"+record.assetId+"_LOD"+i+".fbx").GetComponentInChildren<MeshFilter>().sharedMesh;Require(ReferenceEquals(actual,expected),"Per-instance mesh duplication.");}
            }
            Require(generated.GetComponentsInChildren<Collider>(true).Length==0&&generated.GetComponentsInChildren<Rigidbody>(true).Length==0&&generated.GetComponentsInChildren<ShipTarget>(true).Length==0,"Environment affects gameplay queries.");
            Require(generated.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("POSE_")).Count()==manifest.markers.Length,"Duplicate instances.");
            long meshBytes=meshes.Sum(x=>Profiler.GetRuntimeMemorySizeLong(x)),textureBytes=textures.Sum(x=>Profiler.GetRuntimeMemorySizeLong(x));Require(meshBytes>0&&textureBytes>0&&meshBytes+textureBytes<64L*1024*1024,"Resource memory budget.");
            var m=UnityEngine.Object.FindFirstObjectByType<MissionController>();if(m!=null){Require(m.settings.boundaryRadius==Data().boundaryRadius&&m.settings.boundaryWarningRadius==Data().warningRadius,"Wrong playable boundary.");Require((m.settings.targetLayers.value&(1<<LayerMask.NameToLayer("SpaceEnvironment")))==0,"Environment in hit mask.");Require(m.targets.Length==40,"Fleet count changed.");}
            Write(EditorApplication.isPlaying?"unity-playmode-audit.json":"unity-audit-"+(m==null?"review":"playable")+".json",new {passed=true,context=EditorApplication.isPlaying?"Editor Play Mode":"Editor Edit Mode",unity=Application.unityVersion,scene=b.gameObject.scene.path,instances=manifest.markers.Length+3,rocks=manifest.markers.Length,lodGroups=generated.GetComponentsInChildren<LODGroup>().Length,uniqueMeshes=meshes.Length,uniqueTriangles=meshes.Sum(x=>(long)x.GetIndexCount(0)/3),materials=mats.Length,textures=textures.Length,meshNativeBytes=meshBytes,textureNativeBytes=textureBytes,meshTextureMiB=(meshBytes+textureBytes)/1048576.0,mesh=meshes.Select(x=>new{x.name,path=AssetDatabase.GetAssetPath(x),vertices=x.vertexCount,triangles=(long)x.GetIndexCount(0)/3,bytes=Profiler.GetRuntimeMemorySizeLong(x),x.isReadable}),texture=textures.Select(x=>new{x.name,x.width,x.height,bytes=Profiler.GetRuntimeMemorySizeLong(x),format=x is Texture2D t?t.format.ToString():"other",mips=x.mipmapCount}),allocatedEditorBytes=Profiler.GetTotalAllocatedMemoryLong(),reservedEditorBytes=Profiler.GetTotalReservedMemoryLong(),graphicsDriverWholeEditorEstimate=Profiler.GetAllocatedMemoryForGraphicsDriver(),notes="All LOD resources included. Editor native resource API, not attributable VRAM or Player memory. File sizes not used."});Debug.Log("SOLAR resource/pose audit PASS: "+meshes.Length+" unique meshes, "+(meshBytes+textureBytes)+" bytes Mesh+Texture.");
        }
    }
}
