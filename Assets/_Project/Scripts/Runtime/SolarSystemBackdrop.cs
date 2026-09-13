using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    // Saved proxy meshes and all local rocks are authored in Edit mode. This component
    // updates only two celestial transforms and the sky centre; it creates no objects.
    [ExecuteAlways, DefaultExecutionOrder(200)]
    public sealed class SolarSystemBackdrop : MonoBehaviour
    {
        public TextAsset layoutJson;
        public Camera observer;
        public Transform sunProxy;
        public Transform earthProxy;
        public Transform skyProxy;
        public CombatScaleSettings combatScale;

        SolarLayoutData layout;
        TextAsset loadedAsset;
        Transform cachedSun, cachedEarth;
        Renderer[] sunRenderers = Array.Empty<Renderer>();
        Renderer[] earthRenderers = Array.Empty<Renderer>();

        public SolarLayoutData Layout
        {
            get { EnsureLayout(); return layout; }
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            ReloadLayout();
            ApplyMapping(observer);
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        void OnValidate() { loadedAsset = null; layout = null; }
        void LateUpdate() => ApplyMapping(observer);

        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // Never bind implicitly to SceneView, reflection, UI or preview cameras.
            // Explicit review cameras use ApplyMapping(camera) immediately before Render.
            if (camera == observer) ApplyMapping(camera);
        }

        public void ReloadLayout()
        {
            loadedAsset = layoutJson;
            layout = null;
            if (layoutJson == null) return;
            try
            {
                layout = JsonUtility.FromJson<SolarLayoutData>(layoutJson.text);
                if (combatScale != null && layout != null) layout.metersPerUnit = combatScale.metersPerUnityUnit;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Solar layout JSON is invalid; celestial proxies are hidden. " + exception.Message, this);
            }
        }

        void EnsureLayout()
        {
            if (loadedAsset != layoutJson || (layout == null && layoutJson != null && loadedAsset == null)) ReloadLayout();
        }

        void CacheRenderers()
        {
            if (cachedSun != sunProxy)
            {
                cachedSun = sunProxy;
                sunRenderers = sunProxy == null ? Array.Empty<Renderer>() : sunProxy.GetComponentsInChildren<Renderer>(true);
            }
            if (cachedEarth != earthProxy)
            {
                cachedEarth = earthProxy;
                earthRenderers = earthProxy == null ? Array.Empty<Renderer>() : earthProxy.GetComponentsInChildren<Renderer>(true);
            }
        }

        public void ApplyMapping(Camera camera)
        {
            EnsureLayout();
            CacheRenderers();
            if (camera == null) return; // An unbound prefab keeps its saved inspection pose.
            MapBody(layout == null ? null : layout.FindBody("sun"), sunProxy, sunRenderers, camera.transform.position);
            MapBody(layout == null ? null : layout.FindBody("earth"), earthProxy, earthRenderers, camera.transform.position);
            // The sky retains its authored world orientation and radius. Rocks do not move.
            if (skyProxy != null && SolarLayoutMath.IsFinite(camera.transform.position))
                skyProxy.position = camera.transform.position;
        }

        void MapBody(SolarBodyData body, Transform proxy, Renderer[] renderers, Vector3 cameraPosition)
        {
            if (proxy == null) return;
            bool valid = SolarLayoutMath.TryProjectBody(layout, body, cameraPosition, out SolarBodyProjection projection);
            foreach (Renderer renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = !valid;
            if (!valid) return;
            proxy.position = projection.worldPosition;
            // Export contract: prototype sphere radius = 1, unit-scale generated parents.
            proxy.localScale = Vector3.one * projection.proxyRadius;
        }
    }
}
