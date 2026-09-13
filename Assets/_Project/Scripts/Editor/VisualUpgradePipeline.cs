using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class VisualUpgradePipeline
    {
        public const string Baseline="Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity";
        public const string ScenePath="Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity";
        public const string ShipPath="Assets/_Project/Prefabs/Fleet/FusionFrigate_VisualUpgrade.prefab";
        public const string EnvironmentPath="Assets/_Project/Prefabs/Environment/SpaceEnvironment_VisualUpgrade.prefab";
        public const string Art="Assets/_Project/Art/VisualUpgrade/";
        public const string Evidence="docs/verification/VisualUpgrade/";
        public const float ShipScale=2.6f;
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Guard(){Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Stop Play and compilation first.");Require(PrefabStageUtility.GetCurrentPrefabStage()==null,"Close Prefab stage first.");for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene protected: "+SceneManager.GetSceneAt(i).path);}
        static T Get<T>(GameObject g)where T:Component{var c=g.GetComponent<T>();return c?c:Undo.AddComponent<T>(g);}
        static GameObject Child(Transform p,string n){var t=p.Find(n);if(t)return t.gameObject;var g=new GameObject(n);g.transform.SetParent(p,false);Undo.RegisterCreatedObjectUndo(g,"Create visual upgrade object");return g;}
        static T Copy<T>(string source,string dest)where T:Object{if(!File.Exists(dest))Require(AssetDatabase.CopyAsset(source,dest),"Copy failed: "+dest);return AssetDatabase.LoadAssetAtPath<T>(dest);}
        static Material Mat(string name,string source,string shader=null)
        {
            Material m=Copy<Material>(source,Art+name+".mat");Undo.RecordObject(m,"Tune owned visual material");m.name=name;if(shader!=null){var s=Shader.Find(shader);Require(s!=null,"Missing shader "+shader);m.shader=s;}m.enableInstancing=true;EditorUtility.SetDirty(m);mats[name]=m;return m;
        }
        static void CreateMaterials()
        {
            string old=LightingUpgradePipeline.Art;
            var drop=Mat("PerfectChrome",old+"MetalDroplet.mat","DropletPrototype/VisualUpgrade/PerfectChrome");drop.SetColor("_BaseColor",new Color(.86f,.88f,.90f));drop.SetFloat("_Metallic",1);drop.SetFloat("_Smoothness",.985f);drop.SetFloat("_ExplosionReflectionStrength",2.6f);drop.SetFloat("_ReactorReflectionStrength",1.1f);drop.SetFloat("_ReflectionStrength",1.45f);drop.SetFloat("_SunReflectionStrength",2.8f);
            foreach(var n in new[]{"FF_Armor","FF_Structure","FF_EngineMetal","FF_BlueGray","RockBasalt","RockSlate","EarthBase"})Mat(n,old+n+".mat","DropletPrototype/VisualUpgrade/FleetSurface");
            mats["FF_Armor"].SetColor("_BaseColor",new Color(.52f,.56f,.59f));mats["FF_Armor"].SetFloat("_Metallic",.9f);mats["FF_Armor"].SetFloat("_Smoothness",.73f);mats["FF_Armor"].SetFloat("_Variation",.27f);
            mats["FF_Structure"].SetFloat("_Smoothness",.30f);mats["FF_EngineMetal"].SetFloat("_Smoothness",.86f);
            var core=Mat("FusionCore",old+"FusionCore.mat","DropletPrototype/VisualUpgrade/ReactorCore");core.SetColor("_CoreColor",new Color(7.4f,8.5f,9.4f));core.SetColor("_EdgeColor",new Color(.08f,.9f,2.7f));core.SetFloat("_NoiseAmount",.23f);core.SetFloat("_PulseAmount",.055f);
            Mat("FusionShell",old+"FusionShell.mat").SetColor("_Color",new Color(.09f,.9f,2.25f));Mat("FusionLiner",old+"FusionLiner.mat").SetColor("_Color",new Color(.12f,.72f,1.65f));
            Mat("SunBase",old+"SunBase.mat","DropletPrototype/VisualUpgrade/SolarStar").SetColor("_BaseColor",new Color(4.5f,2.8f,1.15f));Mat("SolarCorona",old+"SolarCorona.mat").SetColor("_BaseColor",new Color(.85f,.36f,.08f));
            foreach(var name in new[]{"ReactorFire","PenetrationFlash"})
            {var m=Mat(name,old+"FusionShell.mat","DropletPrototype/VisualUpgrade/ReactorFire");m.SetFloat("_Kind",name=="ReactorFire"?0:1);m.SetColor("_BaseColor",Color.white);}
            Mat("HotDebris",old+"FF_EngineMetal.mat","DropletPrototype/VisualUpgrade/FleetSurface");
        }
        static void Rebind(GameObject root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {var a=r.sharedMaterials;for(int i=0;i<a.Length;i++)if(a[i]&&mats.TryGetValue(a[i].name,out var m))a[i]=m;r.sharedMaterials=a;}
        }
        static void ShipPrefab()
        {
            Copy<GameObject>(LightingUpgradePipeline.PrefabPath,ShipPath);var g=PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(g,"Scale owned frigate visual and hit coverage");
                g.name="FusionFrigate_VisualUpgrade";g.transform.localScale=Vector3.one;
                foreach(var name in new[]{"VisualRoot","HitVolumes","Sockets"}){var t=g.transform.Find(name);Require(t!=null,"Missing ship child: "+name);t.localScale=Vector3.one*ShipScale;}
                Rebind(g);var marker=Child(g.transform.Find("Sockets"),"ReactorDetonationOrigin").transform;marker.localPosition=new Vector3(0,-.05f,-7f);marker.localRotation=Quaternion.identity;marker.localScale=Vector3.one;
                var target=g.GetComponent<ShipTarget>();var view=Get<ReactorDestructionPresenter>(g);view.target=target;view.visualRoot=target.visualRoot;view.reactorMarker=marker;view.explosionRadius=16f;view.engineRenderers=g.GetComponentInChildren<FusionDriveVisuals>(true).coreRenderers;
                target.visualRoot.GetComponent<LODGroup>().RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(g,ShipPath);
            }finally{PrefabUtility.UnloadPrefabContents(g);}
        }
        static void EnvironmentPrefab()
        {
            Copy<GameObject>(LightingUpgradePipeline.EnvironmentPath,EnvironmentPath);var g=PrefabUtility.LoadPrefabContents(EnvironmentPath);
            try{Rebind(g);PrefabUtility.SaveAsPrefabAsset(g,EnvironmentPath);}finally{PrefabUtility.UnloadPrefabContents(g);}
        }
        static void ChromeEnvironment(Vector3 sun)
        {
            string path=Art+"CombatEnvironment.asset";var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(!cube){cube=new Cubemap(256,TextureFormat.RGBAHalf,true){name="Combat HDR reflection environment"};AssetDatabase.CreateAsset(cube,path);}
            var plane=new Vector3(.13f,.91f,.39f).normalized;
            for(int f=0;f<6;f++)
            {
                var pixels=new Color[256*256];
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    float u=2*(x+.5f)/256-1,v=2*(y+.5f)/256-1;
                    var d=f==0?new Vector3(1,-v,-u):f==1?new Vector3(-1,-v,u):f==2?new Vector3(u,1,v):f==3?new Vector3(u,-1,-v):f==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);d.Normalize();
                    float a=Mathf.Max(0,Vector3.Dot(d,sun));
                    // An authored low-frequency HDR field: brighter neutral unresolved
                    // galactic/ecliptic illumination, dark gaps, and an aligned solar disc.
                    // No detailed ship or explosion is painted into this environment.
                    float band=Mathf.Exp(-Mathf.Pow(Vector3.Dot(d,plane)*8,2));
                    float cloud=.65f+.35f*Mathf.Sin(d.x*8+d.z*3)*Mathf.Sin(d.z*7-d.y*4);
                    float scatter=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.4f,.65f,.6f).normalized)),7);
                    pixels[y*256+x]=new Color(.018f,.022f,.029f)+new Color(.38f,.40f,.43f)*band*cloud+new Color(.13f,.14f,.15f)*scatter+new Color(16,15.5f,14.7f)*Mathf.Pow(a,18000)+new Color(.32f,.29f,.24f)*Mathf.Pow(a,1100);
                }
                cube.SetPixels(pixels,(CubemapFace)f);
            }
            cube.Apply(true,false);EditorUtility.SetDirty(cube);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;
            var sky=Mat("CombatReflectionSky",LightingUpgradePipeline.Art+"SolarReflectionSky.mat");sky.SetTexture("_Tex",cube);RenderSettings.skybox=sky;
        }
        static void DropletPrefab()
        {
            string path="Assets/_Project/Prefabs/Player/Droplet_VisualUpgrade.prefab";Copy<GameObject>("Assets/_Project/Prefabs/Player/Droplet_Rebuilt.prefab",path);
            var g=PrefabUtility.LoadPrefabContents(path);try{foreach(var r in g.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=mats["PerfectChrome"];PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
        }
        [MenuItem("DropletPrototype/Visual Upgrade/1 Create or Update Upgrade Scene")]
        public static void Build()
        {
            Guard();Directory.CreateDirectory(Art);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();CreateMaterials();ShipPrefab();EnvironmentPrefab();DropletPrefab();
            Copy<SceneAsset>(Baseline,ScenePath);var scene=SceneManager.GetActiveScene().path==ScenePath?SceneManager.GetActiveScene():EditorSceneManager.OpenScene(ScenePath);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Author visual upgrade scene");
            var root=scene.GetRootGameObjects().Single(g=>g.GetComponent<FleetSceneRoot>());Undo.RegisterFullObjectHierarchyUndo(root,"Bind upgraded combat presentation");
            var mission=root.GetComponentInChildren<MissionController>();var effects=root.GetComponentInChildren<MissionEffects>();var camera=mission.chaseCamera.GetComponent<Camera>();
            var owned=Child(root.transform,"GeneratedVisualUpgrade");var pool=Get<ReactorExplosionPool>(owned);pool.mission=mission;pool.focus=mission.motor.transform;pool.viewCamera=camera;pool.legacyEffects=effects;pool.fireMaterial=mats["ReactorFire"];pool.flashMaterial=mats["PenetrationFlash"];pool.debrisMaterial=mats["HotDebris"];
            var audioSettings=Copy<EffectSettings>(AssetDatabase.GetAssetPath(effects.settings),Art+"CombatEffects.asset");audioSettings.highEffectCapacity=0;audioSettings.lowEffectCapacity=0;effects.settings=audioSettings;EditorUtility.SetDirty(audioSettings);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath);var targets=new List<ShipTarget>();
            foreach(var old in mission.targets)
            {
                ShipTarget t=old;string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject);
                Require(path==LightingUpgradePipeline.PrefabPath||path==ShipPath,"Unowned target protected: "+path);
                if(path!=ShipPath){var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.transform.parent);Undo.RegisterCreatedObjectUndo(g,"Create visual upgrade frigate");g.name=old.name;g.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);g.transform.localScale=old.transform.localScale;t=g.GetComponent<ShipTarget>();t.targetId=old.targetId;Undo.DestroyObjectImmediate(old.gameObject);}
                var p=t.GetComponent<ReactorDestructionPresenter>();p.pool=pool;t.GetComponent<DestructionPresenter>().effects=effects;targets.Add(t);
                foreach(var c in new Object[]{t,p,t.transform,t.GetComponent<DestructionPresenter>()}){EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
            }
            Require(targets.Count==120,"Expected preserved 120 targets");mission.targets=targets.ToArray();
            var b=scene.GetRootGameObjects().Select(g=>g.GetComponent<SolarSystemBackdrop>()).Single(x=>x);
            if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject)!=EnvironmentPath){Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b.gameObject)==LightingUpgradePipeline.EnvironmentPath,"Unowned backdrop protected");var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath),scene);Undo.RegisterCreatedObjectUndo(g,"Create upgraded environment instance");Undo.DestroyObjectImmediate(b.gameObject);b=g.GetComponent<SolarSystemBackdrop>();}
            b.observer=camera;b.ReloadLayout();b.ApplyMapping(camera);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
            var r=mission.motor.visualRoot.GetComponentsInChildren<Renderer>(true).Single();r.sharedMaterial=mats["PerfectChrome"];r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            var response=Get<DropletReflectionResponse>(owned);response.targetRenderer=r;response.mission=mission;response.explosionPool=pool;response.reactorSources=targets.Select(t=>t.visualRoot.transform.Find("FusionDriveEffects/LOD0/MainExhaust/MainExhaust_Core")).ToArray();response.reactorSourceRadius=4;response.reactorSourceStrength=1.8f;
            var rig=root.GetComponentInChildren<SolarLightingRig>();var lighting=Copy<LightingQualityProfile>(LightingUpgradePipeline.ProfilePath,Art+"CombatLighting.asset");lighting.sunIntensity=2.6f;lighting.sunColor=new Color(1,.98f,.95f);lighting.ambientStrength=.085f;lighting.bloomIntensity=.19f;rig.settings=lighting;rig.backdrop=b;rig.drives=targets.Select(t=>t.GetComponentInChildren<FusionDriveVisuals>(true)).ToArray();
            rig.postVolume.sharedProfile=Copy<VolumeProfile>(AssetDatabase.GetAssetPath(rig.postVolume.sharedProfile),Art+"CombatPost.asset");if(rig.postVolume.sharedProfile.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.19f);bloom.threshold.Override(1.25f);bloom.scatter.Override(.52f);bloom.clamp.Override(8);EditorUtility.SetDirty(bloom);}rig.localProbe.cullingMask=~(1<<2);rig.ApplyLighting();RenderSettings.reflectionIntensity=1.2f;ChromeEnvironment(-rig.sunLight.transform.forward);
            foreach(var c in new Object[]{mission,effects,pool,response,rig,lighting,rig.postVolume,rig.localProbe})EditorUtility.SetDirty(c);
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Scene save failed");AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
            Inspect();Debug.Log("VISUAL UPGRADE SAVED: "+ScenePath);
        }
        [Serializable]public class Audit
        {public string scene,unity,pipeline;public int ships,colliders,lights,probes,shipRenderers,sharedShipMaterials;public Vector3 oldShipSize,newShipSize,dropletSize;public float scale,oldLengthRatio,newLengthRatio,minCenterDistance,medianNearestDistance,minBoundsGap;public bool unitRoots,markersAssigned,poolAssigned;public string[] lods;}
        static Bounds HullBounds(ShipTarget t)
        {var rs=t.visualRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("FusionFrigate_LOD")).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        [MenuItem("DropletPrototype/Visual Upgrade/2 Inspect Saved Scene")]
        public static void Inspect()
        {
            var m=Object.FindFirstObjectByType<MissionController>();Require(m!=null,"No mission");var ts=m.targets;var near=new List<float>();float gap=float.MaxValue;
            foreach(var t in ts){float n=float.MaxValue;foreach(var other in ts){if(t==other)continue;float d=Vector3.Distance(t.transform.position,other.transform.position);n=Mathf.Min(n,d);var a=HullBounds(t);var b=HullBounds(other);Vector3 separation=Vector3.Max(Vector3.zero,Vector3.Max(a.min-b.max,b.min-a.max));gap=Mathf.Min(gap,separation.magnitude);}near.Add(n);}near.Sort();
            var sample=ts[0];var localShip=PrefabUtility.LoadPrefabContents(ShipPath);Vector3 size;try{size=HullBounds(localShip.GetComponent<ShipTarget>()).size;}finally{PrefabUtility.UnloadPrefabContents(localShip);}
            var ds=m.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;var lod=sample.visualRoot.GetComponent<LODGroup>();
            var aReport=new Audit{scene=SceneManager.GetActiveScene().path,unity=Application.unityVersion,pipeline=GraphicsSettings.currentRenderPipeline.name,ships=ts.Length,colliders=ts.Sum(t=>t.hitVolumes.Length),lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.enabled),probes=Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,shipRenderers=sample.visualRoot.GetComponentsInChildren<Renderer>(true).Length,sharedShipMaterials=sample.visualRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().Count(),oldShipSize=size/ShipScale,newShipSize=size,dropletSize=ds,scale=ShipScale,oldLengthRatio=size.z/ShipScale/ds.z,newLengthRatio=size.z/ds.z,minCenterDistance=near[0],medianNearestDistance=near[near.Count/2],minBoundsGap=gap,unitRoots=ts.All(t=>t.transform.localScale==Vector3.one),markersAssigned=ts.All(t=>t.GetComponent<ReactorDestructionPresenter>().reactorMarker!=null),poolAssigned=ts.All(t=>t.GetComponent<ReactorDestructionPresenter>().pool!=null),lods=lod.GetLODs().Select(l=>l.screenRelativeTransitionHeight+" / "+l.renderers.Length+" renderers").ToArray()};
            Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"scene-audit.json",JsonUtility.ToJson(aReport,true));Debug.Log(JsonUtility.ToJson(aReport));
        }
    }
}
