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
    /// <summary>
    /// Called by the Fleet 2000 scene importer, never by an import callback.
    /// Only the destination scene and this task's own resource folder are edited.
    /// The caller owns the outer Undo group and scene save/validation transaction.
    /// </summary>
    public static class Sun2000Authoring
    {
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault_2000_Sun.unity";
        public const string Art = "Assets/_Project/Art/Fleet2000Sun/";
        public const string SunMaterialPath = Art + "SolarPhotosphere.mat";
        public const string PostProfilePath = Art + "SunPost.asset";
        public const string LightingProfilePath = Art + "SunLighting.asset";
        public const string FlareDataPath = Art + "SunLensFlare.asset";
        public const string DropletMaterialPath = Art + "PerfectChromeSun.mat";

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static T Component<T>(GameObject target) where T : Component
        {
            var found = target.GetComponent<T>();
            return found != null ? found : Undo.AddComponent<T>(target);
        }

        static Transform Child(Transform parent, string name)
        {
            var result = parent.Find(name);
            if (result != null) return result;
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, "Create Fleet 2000 sunlight object");
            return child.transform;
        }

        static void Changed(Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static T CopyAsset<T>(T source, string destination) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(destination);
            if (asset != null) return asset;
            Require(source != null, "Required source is missing for " + destination);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            Require(!string.IsNullOrEmpty(sourcePath), "Expected persistent source for " + destination);
            Require(AssetDatabase.CopyAsset(sourcePath, destination), "Could not copy " + sourcePath);
            asset = AssetDatabase.LoadAssetAtPath<T>(destination);
            Require(asset != null, "Copied asset did not load: " + destination);
            return asset;
        }

        static Material CreateSunMaterial()
        {
            var shader = Shader.Find("DropletPrototype/Fleet2000Sun/SolarPhotosphere");
            Require(shader != null, "Import the SolarPhotosphere shader before configuring the scene.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(SunMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SolarPhotosphere" };
                AssetDatabase.CreateAsset(material, SunMaterialPath);
            }
            Undo.RecordObject(material, "Configure owned solar photosphere");
            material.shader = shader;
            material.SetColor("_CoreColor", new Color(2.4f, 1.9f, 1.2f));
            material.SetColor("_RimColor", new Color(1.65f, .68f, .14f));
            material.SetFloat("_Granulation", .35f);
            material.SetFloat("_CellScale", 62f);
            material.SetFloat("_EvolutionSpeed", .014f);
            Changed(material);
            return material;
        }

        static LensFlareDataSRP CreateFlareData()
        {
            var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(FlareDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                data.name = "Occluded warm-gold solar optics";
                AssetDatabase.CreateAsset(data, FlareDataPath);
            }
            Undo.RecordObject(data, "Configure owned SRP flare data");
            data.elements = new[]
            {
                new LensFlareDataElementSRP
                {
                    flareType = SRPLensFlareType.Circle, blendMode = SRPLensFlareBlendMode.Additive,
                    tint = new Color(1f, .73f, .30f, 1f), localIntensity = .48f,
                    uniformScale = 7.5f, fallOff = .85f, edgeOffset = .8f,
                    position = 0f, modulateByLightColor = false
                },
                new LensFlareDataElementSRP
                {
                    flareType = SRPLensFlareType.Circle, blendMode = SRPLensFlareBlendMode.Additive,
                    tint = new Color(1f, .90f, .64f, 1f), localIntensity = .16f,
                    uniformScale = 2.9f, sizeXY = new Vector2(2.4f, .022f),
                    fallOff = .8f, edgeOffset = .7f, position = 0f, modulateByLightColor = false
                },
                new LensFlareDataElementSRP
                {
                    flareType = SRPLensFlareType.Polygon, blendMode = SRPLensFlareBlendMode.Additive,
                    tint = new Color(.58f, .68f, .75f, 1f), localIntensity = .08f,
                    uniformScale = .50f, sideCount = 6, sdfRoundness = .12f,
                    fallOff = .85f, edgeOffset = .65f, position = .72f, modulateByLightColor = false
                }
            };
            Changed(data);
            return data;
        }

        /// <summary>
        /// Configure the copied destination after its existing fleet has been
        /// replaced. Returns the saved component for presentation/diagnostic views.
        /// This method deliberately does not save scenes or alter pipeline assets.
        /// </summary>
        public static SunDisplayRig Configure(Scene scene, Transform ownedRoot, Camera observer = null)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Stop Play/compilation before configuring sunlight.");
            Require(scene.IsValid() && scene.isLoaded && scene.path == ScenePath,
                "Sun authoring is confined to the copied FleetAssault_2000_Sun scene.");
            Require(ownedRoot != null && ownedRoot.gameObject.scene == scene, "Provide the task-owned scene subtree.");
            var backdrops = InScene<SolarSystemBackdrop>(scene);
            var rigs = InScene<SolarLightingRig>(scene);
            Require(backdrops.Length == 1 && rigs.Length == 1, "Expected one preserved solar backdrop and lighting rig.");
            var backdrop = backdrops[0];
            var lighting = rigs[0];
            observer = observer != null ? observer : backdrop.observer;
            Require(observer != null && backdrop.sunProxy != null && lighting.sunLight != null,
                "The source scene must retain its observer, Sun proxy and main light.");
            Require(lighting.settings != null && lighting.postVolume != null && lighting.postVolume.sharedProfile != null,
                "The source lighting and post-processing profiles are required.");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline != null && pipeline.supportDataDrivenLensFlare,
                "The installed URP asset must already support Data Driven Lens Flare; no global pipeline mutation is performed.");

            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var sunMaterial = CreateSunMaterial();
            var oldCoronaRoot = backdrop.sunProxy.Find("SolarCorona");
            foreach (var renderer in backdrop.sunProxy.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, "Configure Sun display renderer");
                bool oldCorona = oldCoronaRoot != null && (renderer.transform == oldCoronaRoot ||
                    renderer.transform.IsChildOf(oldCoronaRoot));
                if (oldCorona) renderer.enabled = false;
                else
                {
                    renderer.enabled = true;
                    renderer.sharedMaterial = sunMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                Changed(renderer);
            }

            Undo.RecordObject(lighting, "Assign owned sunlight settings");
            var profile = CopyAsset(lighting.settings, LightingProfilePath);
            Undo.RecordObject(profile, "Tune neutral sunlight");
            profile.sunIntensity = 3.25f;
            profile.sunColor = new Color(1f, .985f, .955f);
            profile.ambientStrength = .26f;
            profile.bloomIntensity = .24f;
            lighting.settings = profile;

            // Preserve the single sliced local probe and explosion reflections.
            // Disable only other Directional Lights in this destination scene.
            foreach (var light in InScene<Light>(scene))
                if (light.type == LightType.Directional)
                {
                    Undo.RecordObject(light, "Unify directional sunlight");
                    light.enabled = light == lighting.sunLight;
                    Changed(light);
                }

            var post = CopyAsset(lighting.postVolume.sharedProfile, PostProfilePath);
            Undo.RecordObject(lighting.postVolume, "Assign owned sunlight post-processing");
            if (!post.TryGet<Bloom>(out var bloom))
            {
                bloom = post.Add<Bloom>();
                AssetDatabase.AddObjectToAsset(bloom, post);
            }
            Undo.RecordObject(bloom, "Tune restrained warm halo");
            bloom.active = true;
            bloom.intensity.Override(.24f);
            bloom.threshold.Override(1.3f);
            bloom.scatter.Override(.50f);
            bloom.clamp.Override(10f);
            bloom.tint.Override(new Color(1f, .93f, .81f));
            if (!post.TryGet<Tonemapping>(out var tone))
            {
                tone = post.Add<Tonemapping>();
                AssetDatabase.AddObjectToAsset(tone, post);
            }
            Undo.RecordObject(tone, "Preserve ACES highlight rolloff");
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);
            lighting.postVolume.sharedProfile = post;
            lighting.postVolume.isGlobal = true;
            lighting.postVolume.priority = 25f;
            RenderSettings.fog = false;

            var node = Child(ownedRoot, "Sun display and occluded optics");
            var display = Component<SunDisplayRig>(node.gameObject);
            Undo.RecordObject(display, "Bind independent solar display");
            var anchor = Child(node, "Solar front-surface flare sample");
            var flare = Component<LensFlareComponentSRP>(anchor.gameObject);
            Undo.RecordObject(flare, "Configure true SRP depth-occluded flare");
            flare.lensFlareData = CreateFlareData();
            flare.useOcclusion = true;
            flare.sampleCount = 32;
            flare.allowOffScreen = false;
            flare.attenuationByLightShape = false;
            // The flare anchor has no Light. Its position is the photosphere's
            // front surface, not a Directional Light's far-plane projection.
            flare.lightOverride = null;
            flare.maxAttenuationDistance = 50000f;
            flare.maxAttenuationScale = 50000f;
            flare.distanceAttenuationCurve = AnimationCurve.Constant(0f, 1f, 1f);
            flare.scaleByDistanceCurve = AnimationCurve.Constant(0f, 1f, 1f);
            flare.radialScreenAttenuationCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.8f, 1f), new Keyframe(1.1f, 0f));
            display.backdrop = backdrop;
            display.lightingRig = lighting;
            display.observer = observer;
            display.sunProxy = backdrop.sunProxy;
            display.flareAnchor = anchor;
            display.lensFlare = flare;
            display.displayAngularDiameter = 34f;
            display.flareIntensity = .24f;
            display.flareOcclusionAngularRadius = .06f;
            display.flareEnabled = true;

            var response = InScene<DropletReflectionResponse>(scene).SingleOrDefault();
            if (response != null && response.targetRenderer != null)
            {
                display.dropletRenderer = response.targetRenderer;
                var originalChrome = display.dropletRenderer.sharedMaterial;
                Require(originalChrome != null && originalChrome.HasProperty("_SunAngularRadius"),
                    "Preserve the source PerfectChrome shader with solar reflection support.");
                if (AssetDatabase.GetAssetPath(originalChrome) != DropletMaterialPath)
                    display.disabledReflectionAngularRadius = originalChrome.GetFloat("_SunAngularRadius");
                var chrome = CopyAsset(originalChrome, DropletMaterialPath);
                Undo.RecordObject(chrome, "Match extended solar reflection size");
                chrome.SetFloat("_SunAngularRadius", display.displayAngularDiameter * .5f * Mathf.Deg2Rad);
                Undo.RecordObject(display.dropletRenderer, "Assign owned solar chrome variant");
                display.dropletRenderer.sharedMaterial = chrome;
                Changed(chrome);
                Changed(display.dropletRenderer);
            }

            Undo.RecordObject(observer, "Enable HDR solar post-processing");
            var cameraData = observer.GetUniversalAdditionalCameraData();
            Undo.RecordObject(cameraData, "Enable camera depth for SRP flare occlusion");
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            observer.allowHDR = true;
            lighting.ApplyLighting();
            Undo.RecordObject(backdrop.sunProxy, "Save independent solar display pose");
            Undo.RecordObject(anchor, "Save solar optical sample pose");
            display.ApplyForCamera(observer);
            foreach (var target in new Object[] { lighting, profile, post, bloom, tone, lighting.postVolume,
                         display, flare, anchor, backdrop.sunProxy, observer, cameraData }) Changed(target);
            EditorSceneManager.MarkSceneDirty(scene);
            return display;
        }
    }
}
