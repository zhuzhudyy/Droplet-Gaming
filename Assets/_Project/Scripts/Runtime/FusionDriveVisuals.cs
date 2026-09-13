using System;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>
    /// Optional, already-authored engine presentation. ShipTarget owns the enclosing
    /// VisualRoot lifetime; the existing LODGroup owns visibility. No lights, material
    /// instances, physics, Update callbacks or per-ship animation state are required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FusionDriveVisuals : MonoBehaviour
    {
        public Renderer[] coreRenderers = Array.Empty<Renderer>();
        public Renderer[] detailRenderers = Array.Empty<Renderer>();
        [SerializeField] EffectQuality quality = EffectQuality.High;

        public EffectQuality Quality => quality;

        public void Configure(Renderer[] cores, Renderer[] details)
        {
            coreRenderers = cores ?? Array.Empty<Renderer>();
            detailRenderers = details ?? Array.Empty<Renderer>();
            ApplyQuality();
        }

        public void SetQuality(EffectQuality value)
        {
            quality = value;
            ApplyQuality();
        }

        void OnEnable() => ApplyQuality();

        void ApplyQuality()
        {
            // Do not toggle Renderer.enabled: it would compete with native LODGroup
            // decisions. forceRenderingOff is an additional presentation-only mask.
            SetSuppressed(coreRenderers, quality == EffectQuality.Off);
            SetSuppressed(detailRenderers, quality != EffectQuality.High);
        }

        static void SetSuppressed(Renderer[] renderers, bool suppressed)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = suppressed;
        }
    }
}
