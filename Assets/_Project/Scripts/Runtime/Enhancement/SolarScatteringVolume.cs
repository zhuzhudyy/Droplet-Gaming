using UnityEngine;

namespace DropletPrototype
{
    /// <summary>Local corona only; does not fill the vacuum or control exposure.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SolarScatteringVolume : MonoBehaviour
    {
        public SunDisplayRig sun;
        public bool scatteringEnabled = true;
        [Range(8, 32)] public int samples = 16;
        [Range(1.2f, 20)] public float outerRadiusMultiplier = 8;
        [Range(0, .2f)] public float opticalDepth = .035f;
        public Color scatteringColor = new Color(1, .8f, .52f);
        readonly Plane[] frustum = new Plane[6];
        public bool IsVisible(Camera camera)
        {
            if (!scatteringEnabled || sun == null || sun.sunProxy == null || camera == null) return false;
            if (sun.observer != null && sun.observer != camera) return false;
            if (sun.lightingRig != null && sun.lightingRig.missionEffects != null && sun.lightingRig.missionEffects.Quality == EffectQuality.Off) return false;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            float radius = sun.DisplayRadiusUnits * outerRadiusMultiplier;
            foreach (var plane in frustum) if (plane.GetDistanceToPoint(sun.sunProxy.position) < -radius) return false;
            return true;
        }
    }
}
