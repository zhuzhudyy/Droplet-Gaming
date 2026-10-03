using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>
    /// Art-directed angular size of the existing Sun proxy. The authoritative
    /// astronomical layout and all other celestial transforms remain untouched.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class SunDisplayRig : MonoBehaviour
    {
        public SolarSystemBackdrop backdrop;
        public SolarLightingRig lightingRig;
        public Camera observer;
        public Transform sunProxy;
        public Transform flareAnchor;
        public LensFlareComponentSRP lensFlare;
        public Renderer dropletRenderer;
        [Tooltip("Independent art direction; does not change radiusKm, AU positions or other bodies.")]
        [Range(1f, 55f)] public float displayAngularDiameter = 34f;
        [Tooltip("New calibrated scenes use physical radius/distance. Legacy scenes retain their saved art diameter.")]
        public bool usePhysicalAngularSize;
        [Min(.1f)] public float artisticSizeMultiplier = 2;
        [Min(1)] public float haloRadiusMultiplier = 6;
        public float EffectiveAngularDiameter { get; private set; }
        [Range(0f, 1f)] public float flareIntensity = .24f;
        [Tooltip("A compact photosphere sample makes a ship crossing the solar center suppress the optical flare.")]
        [Range(.01f, .5f)] public float flareOcclusionAngularRadius = .06f;
        public bool flareEnabled = true;
        [HideInInspector] public float disabledReflectionAngularRadius = .006f;

        public Vector3 SunDirection { get; private set; }
        public float DisplayRadiusUnits { get; private set; }
        public float PhysicalAngularDiameter { get; private set; }
        public float DisplayMultiplier => PhysicalAngularDiameter > 0f
            ? EffectiveAngularDiameter / PhysicalAngularDiameter : 0f;

        static readonly int SunRadiusId = Shader.PropertyToID("_SunAngularRadius");
        MaterialPropertyBlock properties;

        void OnEnable()
        {
            // SolarSystemBackdrop runs before this component (200 versus 300).
            // Its existing camera callback is followed by ours. We explicitly
            // repeat the mapping here so there is one correct final solar pose.
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            ApplyForCamera(BoundCamera);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            if (lensFlare != null) lensFlare.enabled = false;
            if (backdrop != null) backdrop.ApplyMapping(BoundCamera);
            SetReflectionRadius(disabledReflectionAngularRadius);
        }

        Camera BoundCamera => observer != null ? observer : backdrop != null ? backdrop.observer : null;
        void LateUpdate() => ApplyForCamera(BoundCamera);
        void OnValidate()
        {
            displayAngularDiameter = Mathf.Clamp(displayAngularDiameter, 1f, 55f);
            flareOcclusionAngularRadius = Mathf.Clamp(flareOcclusionAngularRadius, .01f, .5f);
        }

        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Review Game cameras get the same real projection. Reflection and
            // SceneView cameras must not silently become the gameplay observer.
            if (camera == BoundCamera || camera.cameraType == CameraType.Game) ApplyForCamera(camera);
        }

        public void ApplyForCamera(Camera camera)
        {
            if (camera == null || backdrop == null || sunProxy == null) return;
            backdrop.ApplyMapping(camera);
            var layout = backdrop.Layout;
            if (!SolarLayoutMath.TryProjectBody(layout, layout == null ? null : layout.FindBody("sun"),
                    camera.transform.position, out var projection))
            {
                if (lensFlare != null) lensFlare.enabled = false;
                return;
            }

            SunDirection = projection.direction;
            // Projection also supports an authored readability multiplier. Compute physical
            // angle directly in km so it is not accidentally applied twice.
            double ratio = layout.FindBody("sun").radiusKm / (projection.distanceAu * layout.auKm);
            PhysicalAngularDiameter = (float)(2 * System.Math.Asin(System.Math.Clamp(ratio, 0d, 1d)) * Mathf.Rad2Deg);
            EffectiveAngularDiameter = usePhysicalAngularSize
                ? Mathf.Clamp(PhysicalAngularDiameter * artisticSizeMultiplier, .001f, 55f)
                : Mathf.Clamp(displayAngularDiameter, 1f, 55f);
            float angularRadius = EffectiveAngularDiameter * .5f * Mathf.Deg2Rad;
            // The reused mesh has radius one. A sphere subtends asin(radius/d),
            // not atan(radius/d); this keeps the inspector angle meaningful.
            DisplayRadiusUnits = projection.proxyDistance * Mathf.Sin(angularRadius);
            sunProxy.SetPositionAndRotation(projection.worldPosition, sunProxy.rotation);
            sunProxy.localScale = Vector3.one * DisplayRadiusUnits;

            if (lightingRig != null && lightingRig.sunLight != null)
            {
                lightingRig.sunLight.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
                RenderSettings.sun = lightingRig.sunLight;
            }

            if (flareAnchor != null)
            {
                // Place the optical sample immediately in front of the existing
                // opaque solar surface. Attaching it to a Directional Light at
                // the far plane would make the Sun itself occlude its flare.
                float nearSurfaceDistance = Mathf.Max(10f,
                    projection.proxyDistance - DisplayRadiusUnits - Mathf.Max(5f, DisplayRadiusUnits * .002f));
                flareAnchor.position = camera.transform.position + SunDirection * nearSurfaceDistance;
                if (lensFlare != null)
                {
                    bool qualityAllows = lightingRig == null || lightingRig.postVolume == null || lightingRig.postVolume.enabled;
                    lensFlare.enabled = flareEnabled && qualityAllows;
                    lensFlare.intensity = flareIntensity;
                    lensFlare.occlusionRadius = nearSurfaceDistance * Mathf.Tan(flareOcclusionAngularRadius * Mathf.Deg2Rad);
                    lensFlare.occlusionOffset = 1f;
                    lensFlare.scale = (usePhysicalAngularSize ? haloRadiusMultiplier / 6f : 1f) * Mathf.Tan(angularRadius) / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) / .5f;
                }
            }

            SetReflectionRadius(angularRadius);
        }

        void SetReflectionRadius(float angularRadius)
        {
            if (dropletRenderer == null) return;
            // Preserve the existing bounded explosion/reactor MPB response.
            // Its shader already takes the world direction from GetMainLight.
            properties ??= new MaterialPropertyBlock();
            dropletRenderer.GetPropertyBlock(properties);
            properties.SetFloat(SunRadiusId, angularRadius);
            dropletRenderer.SetPropertyBlock(properties);
        }
    }
}
