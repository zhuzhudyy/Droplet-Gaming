using System;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>View-only reactor failure, subscribed after ShipTarget commits the kill.</summary>
    [DisallowMultipleComponent]
    public sealed class ReactorDestructionPresenter : MonoBehaviour
    {
        public ShipTarget target;
        public ReactorExplosionPool pool;
        public Transform reactorMarker;
        public GameObject visualRoot;
        public Renderer[] engineRenderers = Array.Empty<Renderer>();
        [Min(1)] public float explosionRadius = 16;

        public bool IsUnstable { get; private set; }
        public Vector3 ReactorPosition => reactorMarker != null ? reactorMarker.position : transform.position;
        ShipTarget boundTarget;
        MaterialPropertyBlock[] originalBlocks = Array.Empty<MaterialPropertyBlock>();
        MaterialPropertyBlock scratch;
        Renderer[] cachedEngineRenderers;
        GameObject cachedVisualRoot;
        bool canRevealVisual;
        static readonly int Instability = Shader.PropertyToID("_Instability");

        void OnEnable() => Bind();

        public void Bind()
        {
            if (target == null) target = GetComponent<ShipTarget>();
            if (visualRoot == null && target != null) visualRoot = target.visualRoot;
            if (boundTarget == target && scratch != null && cachedEngineRenderers == engineRenderers && cachedVisualRoot == visualRoot) return;
            EndInstability();
            Unbind();
            boundTarget = target;
            cachedEngineRenderers = engineRenderers; cachedVisualRoot = visualRoot;
            scratch = new MaterialPropertyBlock();
            int count = engineRenderers == null ? 0 : engineRenderers.Length;
            originalBlocks = new MaterialPropertyBlock[count];
            for (int i = 0; i < count; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (engineRenderers[i] != null) engineRenderers[i].GetPropertyBlock(originalBlocks[i]);
            }
            // Never reactivate gameplay objects. Malformed optional assets still
            // receive the pooled explosion, but cannot retain their intact view.
            canRevealVisual = visualRoot != null && visualRoot != gameObject &&
                !transform.IsChildOf(visualRoot.transform) &&
                visualRoot.GetComponentInChildren<Collider>(true) == null &&
                visualRoot.GetComponentInChildren<Rigidbody>(true) == null &&
                visualRoot.GetComponentInChildren<ShipTarget>(true) == null;
            if (boundTarget != null)
            {
                boundTarget.Destroyed += OnDestroyed;
                boundTarget.Restored += OnRestored;
            }
        }

        void OnDestroyed(ShipTarget ship)
        {
            EndInstability();
            if (pool != null && pool.isActiveAndEnabled) pool.TryPlay(this);
        }

        internal void BeginInstability()
        {
            if (target == null || !target.IsDestroyed) return;
            IsUnstable = true;
            if (canRevealVisual) visualRoot.SetActive(true);
            SetInstability(0.15f);
        }

        internal void SetInstability(float amount)
        {
            if (!IsUnstable || scratch == null || engineRenderers == null) return;
            for (int i = 0; i < engineRenderers.Length && i < originalBlocks.Length; i++)
            {
                Renderer renderer = engineRenderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(scratch);
                scratch.SetFloat(Instability, amount);
                renderer.SetPropertyBlock(scratch);
            }
        }

        public void EndInstability()
        {
            if (!IsUnstable) return;
            IsUnstable = false;
            for (int i = 0; engineRenderers != null && i < engineRenderers.Length && i < originalBlocks.Length; i++)
                if (engineRenderers[i] != null) engineRenderers[i].SetPropertyBlock(originalBlocks[i]);
            if (canRevealVisual && visualRoot != null && target != null && target.IsDestroyed)
                visualRoot.SetActive(false);
        }

        void OnRestored(ShipTarget ship) => EndInstability();
        void OnDisable() { EndInstability(); Unbind(); }
        void Unbind()
        {
            if (boundTarget == null) return;
            boundTarget.Destroyed -= OnDestroyed;
            boundTarget.Restored -= OnRestored;
            boundTarget = null;
        }
    }
}
