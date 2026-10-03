using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class EnhancementIntegration
    {
        public const string Full = "Assets/_Project/Scenes/FleetAssault_Enhanced.unity";
        public const string Small = "Assets/_Project/Scenes/FleetAssault_Enhanced_Small.unity";
        public const string Art = "Assets/_Project/Art/Enhancement/";
        public const string Evidence = "docs/verification/Enhancement-20260919/";
        [MenuItem("DropletPrototype/Enhancement/1 Integrate Small Safe Copy")]
        public static void IntegrateSmall() => Integrate(true);
        [MenuItem("DropletPrototype/Enhancement/2 Integrate Full Safe Copy")]
        public static void IntegrateFull() => Integrate(false);
        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Stop Play/compilation first.");
            for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene protected: "+SceneManager.GetSceneAt(i).path);
        }
        static T Copy<T>(T original,string name) where T:Object
        {
            var path=Art+name;var result=AssetDatabase.LoadAssetAtPath<T>(path);
            if(result!=null)return result;
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original),path))throw new IOException("Could not safely copy "+name);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        public static void Integrate(bool small)
        {
            Guard();Directory.CreateDirectory(Art);Directory.CreateDirectory(Evidence);
            string path=small?Small:Full;
            string source=small?NarrativeCombatBuilder.SmallScene:NarrativeCombatBuilder.FullScene;
            if(!File.Exists(path)&&!AssetDatabase.CopyAsset(source,path))throw new IOException("Scene copy failed.");
            var scene=EditorSceneManager.OpenScene(path);
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Integrate owned Enhancement scene");
            var mission=Object.FindAnyObjectByType<MissionController>();
            int expected=small?12:2000;
            if(mission.targets.Length!=expected||mission.targets.Select(s=>s.targetId).Distinct().Count()!=expected)throw new InvalidOperationException("Fleet identities changed.");
            string prefix=small?"Small":"Full";
            var scale=Copy(mission.combat.scale,prefix+"Scale.asset");Undo.RecordObject(scale,"Set reaction interval");
            scale.reactionDelaySeconds=new Vector2(1,3);EditorUtility.SetDirty(scale);
            var flight=Copy(mission.settings,prefix+"Flight.asset");flight.worldScale=scale;flight.ApplyWorldScale();EditorUtility.SetDirty(flight);
            Undo.RecordObject(mission,"Assign isolated flight settings");mission.settings=flight;mission.motor.settings=flight;mission.chaseCamera.settings=flight;
            mission.combat.scale=scale;mission.lasers.beamPool.scale=scale;
            var backdrop=Object.FindAnyObjectByType<SolarSystemBackdrop>();backdrop.combatScale=scale;backdrop.ReloadLayout();
            var sun=Object.FindAnyObjectByType<SunDisplayRig>();Undo.RecordObject(sun,"Calibrate physical solar size");
            sun.usePhysicalAngularSize=true;sun.artisticSizeMultiplier=2;sun.haloRadiusMultiplier=6;sun.flareIntensity=.15f;
            sun.flareOcclusionAngularRadius=.025f;
            var lighting=sun.lightingRig;
            lighting.settings=Copy(lighting.settings,"SolarLighting.asset");
            lighting.settings.sunIntensity=1.4f;EditorUtility.SetDirty(lighting.settings);
            var post=Copy(lighting.postVolume.sharedProfile,"SolarPost.asset");lighting.postVolume.sharedProfile=post;
            if(post.TryGet<ColorAdjustments>(out var color)){color.postExposure.overrideState=true;color.postExposure.value=0;}
            if(post.TryGet<Bloom>(out var bloom)){bloom.intensity.value=.18f;bloom.threshold.value=1.25f;bloom.scatter.value=.55f;}
            EditorUtility.SetDirty(post);
            var cam=mission.chaseCamera.GetComponent<Camera>();
            ConfigureLaser(mission,cam);
            var volume=cam.GetComponent<SolarScatteringVolume>();if(volume==null)volume=Undo.AddComponent<SolarScatteringVolume>(cam.gameObject);
            volume.sun=sun;volume.scatteringEnabled=true;volume.samples=16;volume.outerRadiusMultiplier=8;volume.opticalDepth=.035f;
            ConfigureRenderer(cam);
            bool englishReady=EnhancementVoiceAssets.Configure(mission);
            // Components are configured on the copied scene only; original asset references remain intact.
            sun.ApplyForCamera(cam);
            foreach(var root in scene.GetRootGameObjects())foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component!=null&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            File.WriteAllText(Evidence+prefix+"-integration.txt","Scene="+path+"\nIdentities="+expected+"\nPhysicalSolarDiameter="+sun.PhysicalAngularDiameter+"\nArtMultiplier="+sun.artisticSizeMultiplier+"\nDisplayDiameter="+sun.EffectiveAngularDiameter+"\nRadiusUU="+sun.DisplayRadiusUnits+"\nFOV="+cam.fieldOfView+"\nEnglishReady="+englishReady);
        }
        static void ConfigureLaser(MissionController mission,Camera camera)
        {
            var lasers=mission.lasers;var pool=lasers.beamPool;
            var settings=Copy(lasers.settings,"LaserWeapon.asset");
            settings.beamSeconds=.18f;settings.beamWidthMeters=12;settings.contactRadiusMeters=14;
            settings.minimumBeamPixels=1.8f;settings.maximumBeamWidthMeters=300;settings.contactParticleCount=3;
            settings.surfaceHighlightSeconds=.09f;settings.surfaceHighlightRadiusMeters=22;EditorUtility.SetDirty(settings);
            lasers.settings=pool.settings=settings;pool.viewCamera=camera;
            var pulse=AssetDatabase.LoadAssetAtPath<Material>(Art+"LaserPulse.mat");
            if(pulse==null){pulse=new Material(Shader.Find("DropletPrototype/NarrativeCombat/LaserPulse"));AssetDatabase.CreateAsset(pulse,Art+"LaserPulse.mat");}
            pool.material=pulse;
            var surface=lasers.dropletSurface;
            var response=mission.motor.GetComponent<LaserContactResponse>();if(response==null)response=Undo.AddComponent<LaserContactResponse>(mission.motor.gameObject);
            response.presentedMesh=surface.presentedMesh;response.targetRenderer=surface.presentedMesh.GetComponent<Renderer>();surface.contactResponse=response;
            var chrome=Copy(response.targetRenderer.sharedMaterial,"LaserChrome.mat");chrome.shader=Shader.Find("DropletPrototype/NarrativeCombat/LaserChrome");EditorUtility.SetDirty(chrome);
            response.targetRenderer.sharedMaterial=chrome;
        }
        static void ConfigureRenderer(Camera camera)
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new InvalidOperationException("Current URP pipeline required.");
            var serialized=new SerializedObject(pipeline);var list=serialized.FindProperty("m_RendererDataList");
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Art+"EnhancedRenderer.asset");
            if(data==null)
            {
                var source=list.GetArrayElementAtIndex(0).objectReferenceValue;
                if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),Art+"EnhancedRenderer.asset"))throw new IOException("Renderer copy failed");
                data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Art+"EnhancedRenderer.asset");
            }
            var feature=data.rendererFeatures.OfType<SolarScatteringFeature>().FirstOrDefault();
            if(feature==null){feature=ScriptableObject.CreateInstance<SolarScatteringFeature>();feature.name="Local solar corona (half resolution)";AssetDatabase.AddObjectToAsset(feature,data);data.rendererFeatures.Add(feature);}
            feature.scatteringShader=Shader.Find("DropletPrototype/Enhancement/LocalSolarScattering");
            if(feature.scatteringShader==null)throw new InvalidOperationException("Scattering shader not imported.");
            feature.Create();feature.SetActive(true);data.SetDirty();EditorUtility.SetDirty(feature);EditorUtility.SetDirty(data);
            int index=-1;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==data)index=i;
            if(index<0){index=list.arraySize;list.InsertArrayElementAtIndex(index);list.GetArrayElementAtIndex(index).objectReferenceValue=data;serialized.ApplyModifiedProperties();}
            var cameraData=camera.GetUniversalAdditionalCameraData();Undo.RecordObject(cameraData,"Select isolated renderer");cameraData.SetRenderer(index);cameraData.requiresDepthTexture=true;
        }
        [MenuItem("DropletPrototype/Enhancement/4 Build Windows")]
        public static void Build()
        {
            Guard();Directory.CreateDirectory("Builds/Windows-Enhanced-20260919");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Full,Small},locationPathName="Builds/Windows-Enhanced-20260919/DropletGaming.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Evidence+"build.txt",report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nSeconds="+report.summary.totalTime.TotalSeconds);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Enhanced player build failed");
        }
    }
}
