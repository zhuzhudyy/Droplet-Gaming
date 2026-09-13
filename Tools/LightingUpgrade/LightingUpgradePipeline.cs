using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class LightingUpgradePipeline
    {
        public const string ScenePath="Assets/_Project/Scenes/FleetAssault_Lighting.unity";
        public const string PrefabPath="Assets/_Project/Prefabs/Fleet/FusionFrigate_Lighting.prefab";
        public const string EnvironmentPath="Assets/_Project/Prefabs/Environment/SpaceEnvironment_Lighting.prefab";
        public const string Art="Assets/_Project/Art/LightingUpgrade/";
        public const string ProfilePath="Assets/_Project/Data/LightingQuality.asset";
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static void Require(bool v,string m){if(!v)throw new InvalidOperationException(m);}
        static T Get<T>(GameObject go)where T:Component{var c=go.GetComponent<T>();return c==null?Undo.AddComponent<T>(go):c;}
        static GameObject Child(Transform p,string n){var t=p.Find(n);if(t!=null)return t.gameObject;var go=new GameObject(n);go.transform.SetParent(p,false);Undo.RegisterCreatedObjectUndo(go,"Create lighting object");return go;}
        static Material Mat(string name,string shader)
        {
            string path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var s=Shader.Find(shader);Require(s!=null,"Shader unavailable "+shader);
            if(mat==null){mat=new Material(s){name=name};AssetDatabase.CreateAsset(mat,path);}else{Undo.RecordObject(mat,"Tune lighting material");mat.shader=s;}
            mat.enableInstancing=true;materials[name]=mat;EditorUtility.SetDirty(mat);return mat;
        }
        static Material Surface(string name,Color color,float metal,float smooth,float variation,float kind=0)
        {var m=Mat(name,"DropletPrototype/Lighting/SpaceSurface");m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Variation",variation);m.SetFloat("_SurfaceKind",kind);return m;}
        static void CreateMaterials()
        {
            Surface("FF_Armor",new Color(.48f,.53f,.58f),.82f,.65f,.10f);
            Surface("FF_Structure",new Color(.075f,.09f,.11f),.65f,.38f,.10f);
            Surface("FF_EngineMetal",new Color(.31f,.37f,.42f),.98f,.84f,.13f);
            var window=Surface("FF_BlueGray",new Color(.04f,.18f,.26f),.55f,.7f,.025f);window.SetColor("_EmissionColor",new Color(.015f,.20f,.36f));
            Surface("RockBasalt",new Color(.29f,.265f,.235f),0,.12f,.42f,1);
            Surface("RockSlate",new Color(.215f,.24f,.265f),0,.10f,.38f,1);
            var earth=Surface("EarthBase",Color.white,0,.25f,0,2);earth.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(SolarLayoutPipeline.Shared+"Textures/Earth_BaseColor_2048x1024.jpg"));
            var sun=Mat("SunBase","DropletPrototype/Lighting/SolarDisc");sun.SetColor("_BaseColor",new Color(5,3.15f,1.1f));
            var corona=Mat("SolarCorona","DropletPrototype/Lighting/SolarCorona");corona.SetColor("_BaseColor",new Color(.65f,.28f,.07f));
            var drop=Mat("MetalDroplet","Universal Render Pipeline/Lit");drop.SetColor("_BaseColor",new Color(.86f,.93f,1));drop.SetFloat("_Metallic",1);drop.SetFloat("_Smoothness",.985f);
            Mat("FusionCore","DropletPrototype/FusionDriveCore");Mat("FusionShell","DropletPrototype/FusionDriveShell");
            var liner=Mat("FusionLiner","DropletPrototype/FusionDriveShell");liner.SetColor("_Color",new Color(.12f,.65f,1.3f,1));liner.SetFloat("_Opacity",.55f);liner.SetFloat("_RimPower",.7f);liner.SetFloat("_CenterFill",0);liner.SetFloat("_NoiseAmount",.05f);
        }
        static void Rebind(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&materials.TryGetValue(mats[i].name,out var replacement))mats[i]=replacement;
                r.sharedMaterials=mats;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;
                if(mats.Any(m=>m!=null&&m.name.StartsWith("Rock"))){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
            }
        }
        static Mesh Sphere()=>AssetDatabase.LoadAssetAtPath<GameObject>(SolarLayoutPipeline.Shared+"Models/Earth_LOD2.fbx").GetComponentInChildren<MeshFilter>(true).sharedMesh;
        static void SavePrefabs(LightingQualityProfile profile)
        {
            if(!File.Exists(PrefabPath))Require(AssetDatabase.CopyAsset(FusionFleetPipeline.PrefabPath,PrefabPath),"Copy ship prefab failed");
            var ship=PrefabUtility.LoadPrefabContents(PrefabPath);
            try{Rebind(ship);FusionDriveAuthoring.Attach(ship.transform.Find("VisualRoot"),ship.transform.Find("Sockets"),Sphere(),materials["FusionCore"],materials["FusionShell"],materials["FusionLiner"]);PrefabUtility.SaveAsPrefabAsset(ship,PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(ship);}
            // The original macro configuration remains the authority; this named display copy only adds presentation multipliers.
            var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(SolarLayoutPipeline.Art+"SolarLayoutConfig.json"));
            foreach(var body in json["bodies"]){string id=((string)body["id"]).ToLowerInvariant();if(id=="sun")body["readabilityMultiplier"]=profile.solarDisplayMultiplier;if(id=="earth")body["readabilityMultiplier"]=profile.earthDisplayMultiplier;}
            File.WriteAllText(Art+"SolarDisplayConfig.json",json.ToString());AssetDatabase.ImportAsset(Art+"SolarDisplayConfig.json",ImportAssetOptions.ForceSynchronousImport);
            if(!File.Exists(EnvironmentPath))Require(AssetDatabase.CopyAsset(SolarLayoutPipeline.Prefab,EnvironmentPath),"Copy environment failed");
            var env=PrefabUtility.LoadPrefabContents(EnvironmentPath);
            try
            {
                Rebind(env);var b=env.GetComponent<SolarSystemBackdrop>();b.layoutJson=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"SolarDisplayConfig.json");b.ReloadLayout();
                var halo=Child(b.sunProxy,"SolarCorona");halo.transform.localScale=Vector3.one*5;Get<MeshFilter>(halo).sharedMesh=Sphere();var r=Get<MeshRenderer>(halo);r.sharedMaterial=materials["SolarCorona"];r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.reflectionProbeUsage=ReflectionProbeUsage.Off;halo.layer=b.sunProxy.gameObject.layer;
                PrefabUtility.SaveAsPrefabAsset(env,EnvironmentPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(env);}
        }
        static Cubemap CreateReflection(Vector3 sun)
        {
            string path=Art+"SolarEnvironment.asset";var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(cube==null){cube=new Cubemap(256,TextureFormat.RGBAHalf,true){name="Solar aligned HDR environment"};AssetDatabase.CreateAsset(cube,path);}
            int size=cube.width;
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=2*(x+.5f)/size-1,v=2*(y+.5f)/size-1;
                    Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);d.Normalize();
                    float alignment=Vector3.Dot(d,sun);float disc=Mathf.Pow(Mathf.Max(0,alignment),18000);float haze=Mathf.Pow(Mathf.Max(0,alignment),1500);
                    float milky=Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(d.y+.28f)*3.5f),6);
                    pixels[y*size+x]=new Color(.036f,.05f,.075f)*(1+milky*.75f)+new Color(15,12.8f,9.4f)*disc+new Color(.18f,.12f,.055f)*haze;
                }
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true,false);EditorUtility.SetDirty(cube);return cube;
        }
        [MenuItem("DropletPrototype/Lighting Upgrade/1 Create or Update Lighting Scene")]
        public static void Build()
        {
            SolarLayoutPipeline.Guard();Require(PrefabStageUtility.GetCurrentPrefabStage()==null,"Close Prefab stage before authoring");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(LightingUpgradeInspection.Evidence);AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<LightingQualityProfile>(ProfilePath);if(profile==null){profile=ScriptableObject.CreateInstance<LightingQualityProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
            CreateMaterials();SavePrefabs(profile);
            if(!File.Exists(ScenePath))Require(AssetDatabase.CopyAsset(FusionFleetPipeline.ScenePath,ScenePath),"Copy stable scene failed");
            var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Upgrade lighting scene");
            var root=scene.GetRootGameObjects().Single(g=>g.GetComponent<FleetSceneRoot>()!=null);Undo.RegisterFullObjectHierarchyUndo(root,"Bind upgraded scene presentation");
            var mission=root.GetComponentInChildren<MissionController>();var effects=root.GetComponentInChildren<MissionEffects>();
            var targets=new List<ShipTarget>();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach(var old in mission.targets)
            {
                if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject)==PrefabPath){targets.Add(old);continue;}
                Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject)==FusionFleetPipeline.PrefabPath,"Unowned ship instance protected");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.transform.parent);Undo.RegisterCreatedObjectUndo(go,"Create upgraded ship");go.name=old.name;go.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);go.transform.localScale=old.transform.localScale;
                var t=go.GetComponent<ShipTarget>();t.targetId=old.targetId;go.GetComponent<DestructionPresenter>().effects=effects;PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<DestructionPresenter>());targets.Add(t);Undo.DestroyObjectImmediate(old.gameObject);
            }
            mission.targets=targets.ToArray();Require(targets.Count==120,"Must preserve all 120 targets");
            var b=scene.GetRootGameObjects().Select(g=>g.GetComponent<SolarSystemBackdrop>()).Single(x=>x!=null);
            if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject)!=EnvironmentPath)
            {var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath),scene);Undo.RegisterCreatedObjectUndo(replacement,"Create lighting environment");Undo.DestroyObjectImmediate(b.gameObject);b=replacement.GetComponent<SolarSystemBackdrop>();}
            var camera=mission.chaseCamera.GetComponent<Camera>();b.observer=camera;b.ReloadLayout();b.ApplyMapping(camera);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
            foreach(var r in mission.motor.visualRoot.GetComponentsInChildren<Renderer>(true)){r.sharedMaterial=materials["MetalDroplet"];r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;r.gameObject.layer=2;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            var env=root.transform.Find("Environment");foreach(var light in env.GetComponentsInChildren<Light>(true)){light.enabled=false;EditorUtility.SetDirty(light);}
            var owned=Child(root.transform,"GeneratedLightingUpgrade");var rig=Get<SolarLightingRig>(owned);rig.backdrop=b;rig.settings=profile;rig.mission=mission;rig.missionEffects=effects;rig.drives=targets.Select(t=>t.GetComponentInChildren<FusionDriveVisuals>(true)).ToArray();
            var lightGo=Child(owned.transform,"Sun Key - physical direction");var key=Get<Light>(lightGo);key.type=LightType.Directional;key.shadows=LightShadows.Soft;key.shadowStrength=.95f;key.shadowBias=.035f;key.shadowNormalBias=.3f;rig.sunLight=key;
            SolarLayoutMath.TryProjectBody(b.Layout,b.Layout.FindBody("sun"),camera.transform.position,out var sun);var cube=CreateReflection(sun.direction);
            RenderSettings.fog=false;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.reflectionIntensity=1;
            var sky=Mat("SolarReflectionSky","Skybox/Cubemap");sky.SetTexture("_Tex",cube);sky.SetFloat("_Exposure",1);sky.SetColor("_Tint",Color.gray);RenderSettings.skybox=sky;
            var probe=Get<ReflectionProbe>(Child(owned.transform,"Nearby reflections - one sliced capture"));probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.hdr=true;probe.boxProjection=false;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.cullingMask=~(1<<2);probe.nearClipPlane=.3f;probe.farClipPlane=220;probe.size=Vector3.one*500;probe.blendDistance=80;probe.intensity=1;probe.importance=2;rig.localProbe=probe;
            var volumeProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Art+"SolarPost.asset");if(volumeProfile==null){volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(volumeProfile,Art+"SolarPost.asset");}
            if(!volumeProfile.TryGet<Bloom>(out var bloom)){bloom=volumeProfile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,volumeProfile);}bloom.active=true;bloom.intensity.Override(profile.bloomIntensity);bloom.threshold.Override(1.35f);bloom.scatter.Override(.55f);bloom.clamp.Override(7);
            if(!volumeProfile.TryGet<Tonemapping>(out var tone)){tone=volumeProfile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,volumeProfile);}tone.active=true;tone.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(volumeProfile);EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(tone);
            var volume=Get<Volume>(Child(owned.transform,"Solar bloom - restrained"));volume.isGlobal=true;volume.priority=20;volume.sharedProfile=volumeProfile;rig.postVolume=volume;
            var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.Skybox;rig.ApplyLighting();
            foreach(var o in new UnityEngine.Object[]{rig,mission,b,camera,extra,key,probe,volume})EditorUtility.SetDirty(o);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);Debug.Log("LIGHTING SCENE SAVED: "+ScenePath);
        }
    }
}
