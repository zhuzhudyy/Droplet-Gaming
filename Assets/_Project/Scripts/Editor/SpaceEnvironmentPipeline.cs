// Staged outside Assets until Blender's reopened numeric and visual gates pass.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace DropletPrototype.Editor
{
    public static class SpaceEnvironmentPipeline
    {
        public const string Art="Assets/_Project/Art/Environment/SpaceEnvironment/";
        public const string ScenePath="Assets/_Project/Scenes/SpaceEnvironment_Review.unity";
        public const string PrefabPath="Assets/_Project/Prefabs/Environment/SpaceEnvironment.prefab";
        const string Source="ArtSource/Blender/SpaceEnvironment/";
        const string Evidence="docs/verification/SpaceEnvironment/";
        const string Owned="GeneratedEnvironment_v1";
        [Serializable] public class Model { public string assetId,file,shape; public int lod,triangles,vertices; public float[] dimensions; }
        [Serializable] public class Marker { public string name,id,assetId,group; public int materialVariant; public float[] position,forward,up,scale; }
        [Serializable] public class View { public string name;public float[] position,forward,up; public bool orthographic;public float orthoSize,fov,near,far; }
        [Serializable] public class Manifest {public Model[] models;public Marker[] markers;public View[] cameras;public string source_sha256; }
        [Serializable] class VisualGate { public bool passed; public string source_sha256; }
        static Manifest Read()=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"Exports/export-manifest.json"));
        static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void CleanSceneGuard()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play mode before environment authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Save unsaved scene work before environment authoring: "+SceneManager.GetSceneAt(i).path);
        }
        static void ConfigureModel(string path)
        {
            var m=(ModelImporter)AssetImporter.GetAtPath(path);m.globalScale=1;m.useFileScale=true;m.bakeAxisConversion=true;
            m.importAnimation=false;m.animationType=ModelImporterAnimationType.None;m.importBlendShapes=false;
            m.importCameras=false;m.importLights=false;m.addCollider=false;m.isReadable=false;m.generateSecondaryUV=false;
            m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.None;
            m.materialImportMode=ModelImporterMaterialImportMode.None;m.meshCompression=ModelImporterMeshCompression.Off;
            m.SaveAndReimport();
        }
        [MenuItem("DropletPrototype/Space Environment/1 Import Validated Exports")]
        public static void ImportAssets()
        {
            CleanSceneGuard();var data=Read();
            Require(File.Exists(Evidence+"blender-visual-review.json"),"Missing Blender visual gate.");
            var gate=JsonUtility.FromJson<VisualGate>(File.ReadAllText(Evidence+"blender-visual-review.json"));
            string sourceHash;using(var sha=System.Security.Cryptography.SHA256.Create())sourceHash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Source+"SpaceEnvironment.blend"))).Replace("-","").ToLowerInvariant();
            Require(gate.passed && gate.source_sha256==sourceHash && data.source_sha256==sourceHash,"Blender source changed or visual/export gate failed; reopen, inspect and export first.");
            Require(data.models.Sum(m=>m.triangles)<=60000,"Source mesh budget exceeded.");
            foreach(string s in new[]{"Models","Textures","Layout","Materials"})Directory.CreateDirectory(Art+s);
            // Calibration is imported first, in its own task-owned folder.
            string calibration=Art+"Layout/SpaceCalibration.fbx";
            File.Copy(Source+"Exports/SpaceCalibration.fbx",calibration,true);AssetDatabase.Refresh();ConfigureModel(calibration);ValidateCalibration(calibration);
            foreach(var model in data.models)File.Copy(Source+"Exports/"+model.file,Art+"Models/"+model.file,true);
            File.Copy(Source+"Exports/SpaceEnvironmentLayout.fbx",Art+"Layout/SpaceEnvironmentLayout.fbx",true);
            foreach(string f in Directory.GetFiles(Source+"Textures"))if(new[]{".png",".jpg"}.Contains(Path.GetExtension(f).ToLowerInvariant()))File.Copy(f,Art+"Textures/"+Path.GetFileName(f),true);
            AssetDatabase.Refresh();
            foreach(var m in data.models)ConfigureModel(Art+"Models/"+m.file);
            ConfigureModel(Art+"Layout/SpaceEnvironmentLayout.fbx");
            foreach(string f in Directory.GetFiles(Art+"Textures").Where(f=>f.EndsWith(".png")||f.EndsWith(".jpg")))
            {
                var t=(TextureImporter)AssetImporter.GetAtPath(f);t.textureType=TextureImporterType.Default;t.isReadable=false;t.mipmapEnabled=true;
                t.sRGBTexture=true;t.maxTextureSize=2048;t.wrapModeU=TextureWrapMode.Repeat;t.wrapModeV=TextureWrapMode.Clamp;
                t.textureCompression=TextureImporterCompression.Compressed;t.alphaSource=TextureImporterAlphaSource.None;
                var platform=t.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=2048;platform.format=TextureImporterFormat.DXT1;platform.compressionQuality=80;t.SetPlatformTextureSettings(platform);t.SaveAndReimport();
            }
            ValidateImported(data);Debug.Log("SpaceEnvironment: calibrated unique models and pose layout imported.");
        }
        static void ValidateCalibration(string path)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var all=asset.GetComponentsInChildren<Transform>(true);
            Transform Find(string n)=>all.Single(t=>t.name==n);
            void Near(Vector3 a,Vector3 b,string what)=>Require(Vector3.Distance(a,b)<.002f,"Calibration "+what+": "+a+" expected "+b);
            Near(Find("Cube_1m").position,new Vector3(-8,0,0),"cube pivot");Near(Find("Cube_1m").GetComponent<Renderer>().bounds.size,Vector3.one,"1 m dimensions");
            Near(Find("FRONT_TIP").position,new Vector3(0,0,3),"forward");Near(Find("UP_TIP").position,new Vector3(0,2.2f,0),"up");Near(Find("RIGHT_TIP").position,new Vector3(1.5f,0,0),"right");
            var pos=new[]{new Vector3(0,8,100),new Vector3(-20,15,140),new Vector3(30,-5,180)};var rot=new[]{Quaternion.identity,Quaternion.Euler(0,90,0),Quaternion.Euler(15,-35,0)};
            for(int i=0;i<3;i++){var t=Find("SPAWN_Small_00"+(i+1));Near(t.position,pos[i],"marker position");Near(t.forward,rot[i]*Vector3.forward,"marker forward");Near(t.up,rot[i]*Vector3.up,"marker up");}
            Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"calibration.txt","PASS: actual reimport; one-metre cube, asymmetric X/Y/Z, 3 translated/rotated full marker poses, tolerance 0.002. Same established FBX contract.\n");
        }
        static Dictionary<string,Transform> Poses(Manifest data)
        {
            var layout=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Layout/SpaceEnvironmentLayout.fbx");Require(layout!=null,"Missing layout FBX.");
            var poses=layout.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ENV_",StringComparison.Ordinal)).ToDictionary(t=>t.name);
            Require(poses.Count==data.markers.Length && data.markers.Select(m=>m.id).Distinct().Count()==data.markers.Length,"Layout IDs/count mismatch.");
            foreach(var m in data.markers)
            {
                Require(poses.ContainsKey(m.name),"Missing marker "+m.name);var t=poses[m.name];
                Require(Vector3.Distance(t.position,V(m.position))<.015f && Vector3.Distance(t.forward,V(m.forward))<.002f && Vector3.Distance(t.up,V(m.up))<.002f,"Converted layout pose mismatch: "+m.name);
                Require(Vector3.Distance(t.lossyScale,V(m.scale))<.015f && t.lossyScale.x>0 && t.localToWorldMatrix.determinant>0,"Invalid scale "+m.name);
                Require(data.models.Any(a=>a.assetId==m.assetId),"Unknown prototype "+m.assetId);
            }
            return poses;
        }
        static void ValidateImported(Manifest data)
        {
            foreach(var model in data.models)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/"+model.file);Require(asset!=null,"Missing model "+model.file);
                var filters=asset.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length==1,"Expected one shared prototype mesh: "+model.file);
                var mesh=filters[0].sharedMesh;Require(mesh!=null && Triangles(mesh)==model.triangles,"Imported triangle mismatch "+model.file);
                Require(!mesh.isReadable && mesh.HasVertexAttribute(VertexAttribute.Normal),"Read/write or normal contract "+model.file);
                Require(Vector3.Distance(filters[0].GetComponent<Renderer>().bounds.size,V(model.dimensions))<.02f,"Imported dimensions mismatch "+model.file);
            }
            Poses(data);
        }
        static long Triangles(Mesh m){long n=0;for(int i=0;i<m.subMeshCount;i++)n+=(long)m.GetIndexCount(i)/3;return n;}
        static Material Mat(string name,Color color,bool unlit,Texture texture=null)
        {
            string path=Art+"Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Simple Lit");Require(shader!=null,"Missing installed URP shader.");
            if(mat==null){mat=new Material(shader){name=name};AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.SetColor("_BaseColor",color);mat.SetTexture("_BaseMap",texture);mat.enableInstancing=true;
            if(mat.HasProperty("_SpecColor"))mat.SetColor("_SpecColor",Color.black);
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",0);
            if(mat.HasProperty("_SpecularHighlights"))mat.SetFloat("_SpecularHighlights",0);
            mat.DisableKeyword("_EMISSION");EditorUtility.SetDirty(mat);return mat;
        }
        static Transform Child(Transform parent,string name)
        {
            var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create environment object");return go.transform;
        }
        static int EnvironmentLayer()
        {
            int layer=LayerMask.NameToLayer("SpaceEnvironment");if(layer>=0)return layer;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var array=settings.FindProperty("layers");
            for(int i=9;i<32;i++)if(string.IsNullOrEmpty(array.GetArrayElementAtIndex(i).stringValue)){array.GetArrayElementAtIndex(i).stringValue="SpaceEnvironment";settings.ApplyModifiedProperties();return i;}
            throw new InvalidOperationException("No free environment layer.");
        }
        static int NeutralRendererIndex()
        {
            const string path=Art+"PreviewOnly/NeutralRenderer.asset";
            Directory.CreateDirectory(Art+"PreviewOnly");
            if(AssetDatabase.LoadMainAssetAtPath(path)==null)Require(AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset",path),"Cannot create task-owned neutral renderer.");
            var renderer=AssetDatabase.LoadMainAssetAtPath(path);var so=new SerializedObject(renderer);
            var features=so.FindProperty("m_RendererFeatures");var children=new List<UnityEngine.Object>();
            for(int i=0;i<features.arraySize;i++)children.Add(features.GetArrayElementAtIndex(i).objectReferenceValue);
            features.arraySize=0;var map=so.FindProperty("m_RendererFeatureMap");if(map!=null)map.arraySize=0;
            so.FindProperty("m_RenderingMode").intValue=0;so.ApplyModifiedPropertiesWithoutUndo();
            foreach(var feature in children)if(feature!=null&&AssetDatabase.GetAssetPath(feature)==path)UnityEngine.Object.DestroyImmediate(feature,true);
            EditorUtility.SetDirty(renderer);
            var pipeline=GraphicsSettings.currentRenderPipeline;Require(pipeline!=null,"Expected existing URP pipeline.");
            var pipelineSO=new SerializedObject(pipeline);var list=pipelineSO.FindProperty("m_RendererDataList");Require(list!=null,"Installed URP renderer registry unavailable.");
            for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)return i;
            int index=list.arraySize;list.arraySize++;list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;
            pipelineSO.ApplyModifiedProperties();return index;
        }
        [MenuItem("DropletPrototype/Space Environment/2 Create or Update Review")]
        public static void BuildReview()
        {
            CleanSceneGuard();var data=Read();ValidateImported(data);var poses=Poses(data);int layer=EnvironmentLayer();int neutralRenderer=NeutralRendererIndex();
            var mats=new Dictionary<string,Material>{
                {"Rock0",Mat("RockBasalt",new Color(.29f,.265f,.23f),false)},
                {"Rock1",Mat("RockSlate",new Color(.34f,.37f,.39f),false)},
                {"Sun",Mat("SunBase",new Color(1,.85f,.45f),true)},
                {"Earth",Mat("EarthBase",Color.white,false,AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/Earth_BaseColor_2048x1024.jpg"))},
                {"Sky",Mat("StarsBase",Color.white,true,AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/SpaceStars_BaseColor.png"))}};
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);bool first=prefab==null;
            var previousScene=SceneManager.GetActiveScene();var staging=first?EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive):default(Scene);
            if(first)SceneManager.SetActiveScene(staging);
            var root=first?new GameObject("SpaceEnvironment"):PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(root,"Update owned environment");var generated=Child(root.transform,Owned);
                var names=new HashSet<string>(data.markers.Select(m=>m.name));
                foreach(var stale in generated.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ENV_")&&!names.Contains(t.name)).ToArray())Undo.DestroyObjectImmediate(stale.gameObject);
                foreach(var marker in data.markers)
                {
                    var existing=generated.GetComponentsInChildren<Transform>(true).Where(x=>x.name==marker.name).ToArray();Require(existing.Length<=1,"Duplicate owned marker "+marker.name);
                    var parent=Child(generated,marker.group);var t=existing.Length==0?Child(parent,marker.name):existing[0];if(t.parent!=parent)Undo.SetTransformParent(t,parent,"Move owned environment instance");
                    var p=poses[marker.name];t.localPosition=p.position;t.localRotation=p.rotation;t.localScale=p.lossyScale;
                    var models=data.models.Where(m=>m.assetId==marker.assetId).OrderBy(m=>m.lod).ToArray();var renderers=new List<Renderer>();
                    foreach(var model in models)
                    {
                        string n="LOD"+model.lod;var child=t.Find(n);var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/"+model.file);
                        if(child!=null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)!=sourceModel){Undo.DestroyObjectImmediate(child.gameObject);child=null;}
                        if(child==null){child=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/"+model.file),t)).transform;child.name=n;Undo.RegisterCreatedObjectUndo(child.gameObject,"Add shared LOD prototype");}
                        child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                        var renderer=child.GetComponentInChildren<MeshRenderer>();
                        renderer.sharedMaterial=mats[marker.assetId.StartsWith("Rock")?"Rock"+marker.materialVariant:marker.assetId.StartsWith("Far")?"Rock0":marker.assetId];
                        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderers.Add(renderer);
                    }
                    if(models.Length>1)
                    {
                        var lod=t.GetComponent<LODGroup>();if(lod==null)lod=t.gameObject.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;lod.animateCrossFading=false;
                        // Lower levels remain resident; only drawing changes. No large-object early cull.
                        float[] levels=marker.assetId=="Earth"?new[]{.12f,.045f,.001f}:new[]{.07f,.016f,.0007f};
                        lod.SetLODs(renderers.Select((r,i)=>new LOD(levels[i],new[]{r})).ToArray());lod.RecalculateBounds();
                    }
                    else if(t.GetComponent<LODGroup>()!=null)Undo.DestroyObjectImmediate(t.GetComponent<LODGroup>());
                    foreach(Transform old in t.Cast<Transform>().ToArray())if(old.name.StartsWith("LOD")&&!models.Any(m=>old.name=="LOD"+m.lod))Undo.DestroyObjectImmediate(old.gameObject);
                }
                foreach(var t in generated.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=layer;GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);}
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{if(first){UnityEngine.Object.DestroyImmediate(root);SceneManager.SetActiveScene(previousScene);EditorSceneManager.CloseScene(staging,true);}else PrefabUtility.UnloadPrefabContents(root);}
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null?EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single):EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var instances=scene.GetRootGameObjects().Where(g=>PrefabUtility.GetCorrespondingObjectFromSource(g)==AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).ToArray();
            Require(instances.Length<=1,"Duplicate environment prefab roots.");
            if(instances.Length==0){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);Undo.RegisterCreatedObjectUndo(go,"Place environment prefab");}
            var preview=scene.GetRootGameObjects().SingleOrDefault(g=>g.name=="PreviewOnly_SpaceEnvironment_v1")??new GameObject("PreviewOnly_SpaceEnvironment_v1");
            foreach(var view in data.cameras)
            {
                var t=Child(preview.transform,view.name);var cam=t.GetComponent<Camera>();if(cam==null)cam=t.gameObject.AddComponent<Camera>();
                t.SetPositionAndRotation(V(view.position),Quaternion.LookRotation(V(view.forward),V(view.up)));cam.fieldOfView=view.fov;cam.aspect=16f/9;cam.orthographic=view.orthographic;cam.orthographicSize=view.orthoSize;
                cam.nearClipPlane=view.near;cam.farClipPlane=view.far;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.003f,.005f,.008f);
                cam.cullingMask=1<<layer;cam.allowHDR=false;cam.allowMSAA=false;cam.enabled=view.name=="ReferenceCamera";if(cam.enabled)cam.tag="MainCamera";
                var dataType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")).First(t=>t!=null);
                var additional=cam.GetComponent(dataType);if(additional==null)additional=cam.gameObject.AddComponent(dataType);
                var cameraSO=new SerializedObject(additional);cameraSO.FindProperty("m_RendererIndex").intValue=neutralRenderer;cameraSO.FindProperty("m_RenderPostProcessing").boolValue=false;
                cameraSO.FindProperty("m_RequiresDepthTextureOption").intValue=0;cameraSO.FindProperty("m_RequiresOpaqueTextureOption").intValue=0;cameraSO.ApplyModifiedPropertiesWithoutUndo();
            }
            var lightT=Child(preview.transform,"Neutral Inspection Light");var light=lightT.GetComponent<Light>();if(light==null)light=lightT.gameObject.AddComponent<Light>();
            light.type=LightType.Directional;light.color=Color.white;light.intensity=1;light.shadows=LightShadows.None;light.cullingMask=1<<layer;lightT.rotation=Quaternion.Euler(35,-35,0);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.4f,.4f);RenderSettings.reflectionIntensity=0;RenderSettings.fog=false;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Selection.activeGameObject=GameObject.Find("SpaceEnvironment");Debug.Log("SpaceEnvironment prefab and isolated review scene saved: "+data.markers.Length+" instances.");
        }
        [Serializable] public class MeshRecord {public string name,path;public int vertices;public long triangles,nativeBytes,gpuBufferBytes;public bool readable;}
        [Serializable] public class TextureRecord {public string name,path,format,graphicsFormat;public int width,height,mips;public long nativeBytes,blockCompressedMipBytes;public bool readable;}
        [Serializable] public class ViewRecord {public string camera;public long estimatedVisibleTriangles;public int lod0,lod1,lod2,culled;}
        [Serializable] public class Audit {public string context,unity,device;public bool passed;public int instances,uniqueMeshes,materials,textures,lodGroups,renderers;public long uniqueTriangles,meshNativeBytes,textureNativeBytes,meshGpuBufferBytes,textureMipEstimateBytes,editorAllocatedBytes,editorReservedBytes,wholeGraphicsDriverEstimateBytes;public MeshRecord[] mesh;public TextureRecord[] texture;public ViewRecord[] views;public string[] checks;}
        static long MeshBufferBytes(Mesh m){long n=0;for(int i=0;i<m.vertexBufferCount;i++)n+=(long)m.vertexCount*m.GetVertexBufferStride(i);for(int i=0;i<m.subMeshCount;i++)n+=(long)m.GetIndexCount(i)*(m.indexFormat==IndexFormat.UInt16?2:4);return n;}
        static long DxtMipBytes(int w,int h,int mips){long b=0;for(int i=0;i<mips;i++){b+=(long)((w+3)/4)*((h+3)/4)*8;w=Math.Max(1,w/2);h=Math.Max(1,h/2);}return b;}
        [MenuItem("DropletPrototype/Space Environment/3 Audit Saved Review")]
        public static void AuditReview()
        {
            var data=Read();ValidateImported(data);Require(SceneManager.GetActiveScene().path==ScenePath,"Open isolated review scene.");
            var root=GameObject.Find("SpaceEnvironment");Require(root!=null,"Missing environment root.");
            var markers=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ENV_")).ToArray();Require(markers.Length==data.markers.Length,"Instance count mismatch.");
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Require(materials.All(m=>m!=null&&AssetDatabase.Contains(m)&&AssetDatabase.GetAssetPath(m).StartsWith(Art+"Materials/")),"Materials must be shared persistent environment assets.");
            var textures=materials.Select(m=>m.GetTexture("_BaseMap")).OfType<Texture2D>().Distinct().ToArray();Require(textures.Length==2,"Missing material/texture.");
            foreach(var marker in data.markers)
            {
                var t=markers.Single(t=>t.name==marker.name);Require(Vector3.Distance(t.position,V(marker.position))<.015f&&Vector3.Distance(t.forward,V(marker.forward))<.002f&&Vector3.Distance(t.up,V(marker.up))<.002f&&Vector3.Distance(t.lossyScale,V(marker.scale))<.015f,"Saved full pose mismatch "+t.name);
                string materialName=marker.assetId.StartsWith("Rock")?(marker.materialVariant==0?"RockBasalt":"RockSlate"):marker.assetId.StartsWith("Far")?"RockBasalt":marker.assetId=="Sky"?"StarsBase":marker.assetId+"Base";
                var expectedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+materialName+".mat");
                foreach(var m in data.models.Where(m=>m.assetId==marker.assetId))
                {
                    var expected=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Models/"+m.file).GetComponentInChildren<MeshFilter>().sharedMesh;
                    var actual=t.Find("LOD"+m.lod).GetComponentInChildren<MeshFilter>().sharedMesh;Require(ReferenceEquals(actual,expected),"Mesh was copied per instance: "+t.name);
                    Require(ReferenceEquals(t.Find("LOD"+m.lod).GetComponentInChildren<Renderer>().sharedMaterial,expectedMaterial),"Per-instance or incorrect material: "+t.name);
                }
                var group=t.GetComponent<LODGroup>();int expectedLods=data.models.Count(m=>m.assetId==marker.assetId);
                if(expectedLods>1){Require(group!=null&&group.lodCount==expectedLods,"LOD count mismatch "+t.name);var levels=group.GetLODs();for(int i=0;i<levels.Length;i++)Require(levels[i].renderers.Length==1&&levels[i].renderers[0]==t.Find("LOD"+i).GetComponentInChildren<Renderer>(),"LOD renderer membership mismatch "+t.name);}
            }
            int layer=LayerMask.NameToLayer("SpaceEnvironment");var tuning=AssetDatabase.LoadAssetAtPath<DropletSettings>("Assets/_Project/Data/FleetMission.asset");Require((tuning.targetLayers.value&(1<<layer))==0,"Environment enters attack mask.");
            Require(root.GetComponentsInChildren<Collider>(true).Length==0 && root.GetComponentsInChildren<Rigidbody>(true).Length==0 && root.GetComponentsInChildren<ShipTarget>(true).Length==0,"Decoration acquired gameplay/physics.");
            Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0 && root.GetComponentsInChildren<Light>(true).Length==0 && root.GetComponentsInChildren<ReflectionProbe>(true).Length==0,"Prefab includes logic or lighting.");
            Require(renderers.All(r=>!r.isPartOfStaticBatch && GameObjectUtility.GetStaticEditorFlags(r.gameObject)==0),"Background static batching duplicates geometry.");
            Require(materials.Length<=6 && meshes.Length==data.models.Length,"Resource sharing budget mismatch.");
            var a=new Audit{context=EditorApplication.isPlaying?"Editor Play Mode, isolated review scene":"Editor Edit Mode, isolated review scene",unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,instances=markers.Length,uniqueMeshes=meshes.Length,materials=materials.Length,textures=textures.Length,lodGroups=root.GetComponentsInChildren<LODGroup>().Length,renderers=renderers.Length,
                mesh=meshes.Select(m=>new MeshRecord{name=m.name,path=AssetDatabase.GetAssetPath(m),vertices=m.vertexCount,triangles=Triangles(m),nativeBytes=Profiler.GetRuntimeMemorySizeLong(m),gpuBufferBytes=MeshBufferBytes(m),readable=m.isReadable}).ToArray(),
                texture=textures.Select(t=>new TextureRecord{name=t.name,path=AssetDatabase.GetAssetPath(t),format=t.format.ToString(),graphicsFormat=t.graphicsFormat.ToString(),width=t.width,height=t.height,mips=t.mipmapCount,nativeBytes=Profiler.GetRuntimeMemorySizeLong(t),blockCompressedMipBytes=DxtMipBytes(t.width,t.height,t.mipmapCount),readable=t.isReadable}).ToArray()};
            a.uniqueTriangles=a.mesh.Sum(m=>m.triangles);a.meshNativeBytes=a.mesh.Sum(m=>m.nativeBytes);a.textureNativeBytes=a.texture.Sum(t=>t.nativeBytes);a.meshGpuBufferBytes=a.mesh.Sum(m=>m.gpuBufferBytes);a.textureMipEstimateBytes=a.texture.Sum(t=>t.blockCompressedMipBytes);
            a.editorAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong();a.editorReservedBytes=Profiler.GetTotalReservedMemoryLong();a.wholeGraphicsDriverEstimateBytes=Profiler.GetAllocatedMemoryForGraphicsDriver();
            a.views=GameObject.Find("PreviewOnly_SpaceEnvironment_v1").GetComponentsInChildren<Camera>().Select(c=>EstimateView(root,c)).ToArray();
            a.checks=new[]{"All instance Mesh references equal imported shared objects by ReferenceEquals","All materials from five shared persistent assets","FBX poses match Blender world forward/up/position/scale","Read/Write disabled on all unique model/texture resources","No static batching, Colliders, Rigidbody, ShipTarget, MonoBehaviour, Lights or ReflectionProbes in environment prefab","LOD resources remain in memory: all levels included","View triangle values are analytical frustum/screen-height estimates, not GPU measured submitted triangles","Native object memory is Editor API measurement; GPU buffers+DXT1 mips are resource estimates; driver memory is whole Editor estimate"};
            Require(a.mesh.All(m=>m.nativeBytes>0)&&a.texture.All(t=>t.nativeBytes>0),"Native resource memory API unavailable; do not report zero as measured memory.");
            Require(a.uniqueTriangles<=60000,"Unique triangle budget exceeded");Require(a.meshNativeBytes+a.textureNativeBytes<64L*1024*1024,"Editor mesh/texture native resource budget exceeded");Require(a.texture.All(t=>!t.readable&&t.width<=2048&&t.height<=1024&&t.format=="DXT1"),"Texture import contract");
            a.passed=true;File.WriteAllText(Evidence+(EditorApplication.isPlaying?"unity-playmode-audit.json":"unity-audit.json"),JsonUtility.ToJson(a,true));Debug.Log("SpaceEnvironment audit PASS; unique triangles "+a.uniqueTriangles+", native Mesh+Texture "+(a.meshNativeBytes+a.textureNativeBytes)+" bytes (Editor only).");
        }
        static ViewRecord EstimateView(GameObject root,Camera cam)
        {
            var v=new ViewRecord{camera=cam.name};var planes=GeometryUtility.CalculateFrustumPlanes(cam);var grouped=new HashSet<Renderer>();
            foreach(var group in root.GetComponentsInChildren<LODGroup>())
            {
                var levels=group.GetLODs();foreach(var r in levels.SelectMany(l=>l.renderers))grouped.Add(r);
                float size=group.size*Mathf.Max(group.transform.lossyScale.x,group.transform.lossyScale.y,group.transform.lossyScale.z);
                float distance=Vector3.Distance(cam.transform.position,group.transform.TransformPoint(group.localReferencePoint));
                float h=QualitySettings.lodBias*size/(cam.orthographic?2*cam.orthographicSize:2*distance*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad/2));
                int chosen=Array.FindIndex(levels,l=>h>=l.screenRelativeTransitionHeight);
                if(chosen<0){v.culled++;continue;}bool any=false;
                foreach(var r in levels[chosen].renderers)if(GeometryUtility.TestPlanesAABB(planes,r.bounds)){v.estimatedVisibleTriangles+=Triangles(r.GetComponent<MeshFilter>().sharedMesh);any=true;}
                if(any){if(chosen==0)v.lod0++;else if(chosen==1)v.lod1++;else v.lod2++;}else v.culled++;
            }
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(!grouped.Contains(r)&&GeometryUtility.TestPlanesAABB(planes,r.bounds))v.estimatedVisibleTriangles+=Triangles(r.GetComponent<MeshFilter>().sharedMesh);
            return v;
        }
        [MenuItem("DropletPrototype/Space Environment/5 Capture Review Views")]
        public static void CaptureReviewViews()
        {
            Require(SceneManager.GetActiveScene().path==ScenePath,"Open isolated review scene for capture.");
            foreach(var camera in GameObject.Find("PreviewOnly_SpaceEnvironment_v1").GetComponentsInChildren<Camera>())Capture(camera,Evidence+"Unity-"+camera.name+".png");
            bool oldWire=GL.wireframe;try{GL.wireframe=true;Capture(GameObject.Find("ReferenceCamera").GetComponent<Camera>(),Evidence+"Unity-Wireframe.png");}finally{GL.wireframe=oldWire;}
        }
        public static void Capture(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1600,900,24);var old=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(image);}}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        [MenuItem("DropletPrototype/Space Environment/4 Reimport Twice and Reopen Check")]
        public static void ReimportAndReopenCheck()
        {
            CleanSceneGuard();Require(SceneManager.GetActiveScene().path==ScenePath,"Review scene must be open.");
            Require(GameObject.Find("ManualPreservationProbe")==null,"Test probe name already used; preserve existing manual object.");
            var sentinel=new GameObject("ManualPreservationProbe");sentinel.transform.position=new Vector3(7,11,13);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var paths=AssetDatabase.FindAssets("",new[]{Art,"Assets/_Project/Prefabs/Environment"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var before=paths.ToDictionary(p=>p,p=>AssetDatabase.AssetPathToGUID(p));
            for(int i=0;i<2;i++){ImportAssets();BuildReview();AuditReview();Require(GameObject.Find("ManualPreservationProbe")!=null,"External manual object removed.");}
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);AuditReview();
            Require(GameObject.Find("ManualPreservationProbe").transform.position==new Vector3(7,11,13),"Manual pose lost on reopen.");
            foreach(var pair in before)Require(AssetDatabase.AssetPathToGUID(pair.Key)==pair.Value,"GUID changed "+pair.Key);
            UnityEngine.Object.DestroyImmediate(GameObject.Find("ManualPreservationProbe"));EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            File.WriteAllText(Evidence+"repeat-import.txt","PASS: two complete model/layout reimports + owned prefab/review updates, then reopen; external manual root and transform preserved; all existing environment asset GUIDs stable; no duplicate IDs; actual mesh/material sharing rechecked. Temporary probe removed and review saved.\n");
        }
    }
}
