using System;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>
    /// Supplies a bounded, world-space reflection approximation only to the unique
    /// player's chrome. The explosion pool is the sole explosion timing authority;
    /// this component owns no kill state, lights, materials or capture cameras.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class DropletReflectionResponse : MonoBehaviour
    {
        public Renderer targetRenderer;
        public MissionController mission;
        public ReactorExplosionPool explosionPool;
        [Tooltip("Authored main reactor markers. Inactive/destroyed ship markers are ignored.")]
        public Transform[] reactorSources = Array.Empty<Transform>();
        [Min(.1f)] public float reactorSourceRadius = 4f;
        [Range(0f, 4f)] public float reactorSourceStrength = 1.8f;
        [Min(1f)] public float reactorMaximumDistance = 200f;
        [Min(.02f)] public float reactorSelectionInterval = .12f;
        public Color reactorColor = new Color(.22f, .61f, 1f);
        public bool reactorReflections = true;
        public bool explosionReflections = true;

        public int ActiveExplosionSources { get; private set; }
        public int ActiveReactorSources { get; private set; }
        public float PeakExplosionStrength { get; private set; }

        const int BurstCapacity = 4, ReactorCapacity = 3;
        static readonly int BurstPositionsId = Shader.PropertyToID("_BurstPositions");
        static readonly int BurstColorsId = Shader.PropertyToID("_BurstColors");
        static readonly int BurstCountId = Shader.PropertyToID("_BurstCount");
        static readonly int ReactorPositionsId = Shader.PropertyToID("_ReactorPositions");
        static readonly int ReactorColorsId = Shader.PropertyToID("_ReactorColors");
        static readonly int ReactorCountId = Shader.PropertyToID("_ReactorCount");
        readonly Vector4[] burstPositions = new Vector4[BurstCapacity];
        readonly Vector4[] burstColors = new Vector4[BurstCapacity];
        readonly Vector4[] reactorPositions = new Vector4[ReactorCapacity];
        readonly Vector4[] reactorColors = new Vector4[ReactorCapacity];
        readonly Transform[] selectedReactors = new Transform[ReactorCapacity];
        readonly Renderer[] selectedReactorRenderers = new Renderer[ReactorCapacity];
        readonly float[] selectedDistances = new float[ReactorCapacity];
        MaterialPropertyBlock properties;
        MissionController subscribedMission;
        float nextReactorSelection;

        void OnEnable()
        {
            properties ??= new MaterialPropertyBlock();
            subscribedMission = mission;
            if (subscribedMission != null) subscribedMission.Restarted += ResetResponse;
            ResetResponse();
        }

        void OnDisable()
        {
            if (subscribedMission != null) subscribedMission.Restarted -= ResetResponse;
            subscribedMission = null;
            ResetResponse();
        }

        void LateUpdate()
        {
            if (targetRenderer == null) return;
            ActiveExplosionSources = explosionReflections && explosionPool != null && explosionPool.isActiveAndEnabled
                ? Mathf.Clamp(explosionPool.CopyReflectionSources(burstPositions, burstColors), 0, BurstCapacity) : 0;
            PeakExplosionStrength = 0;
            for (int i = 0; i < ActiveExplosionSources; i++)
                PeakExplosionStrength = Mathf.Max(PeakExplosionStrength, burstPositions[i].w);
            // Every used array element is replaced, and unused elements are zeroed
            // even though the shader also respects the count. Nothing accumulates.
            for (int i = ActiveExplosionSources; i < BurstCapacity; i++)
                burstPositions[i] = burstColors[i] = Vector4.zero;

            if (Time.time >= nextReactorSelection)
            {
                SelectNearbyReactors();
                nextReactorSelection = Time.time + Mathf.Max(.02f, reactorSelectionInterval);
            }
            ActiveReactorSources = 0;
            if (reactorReflections)
                for (int i = 0; i < ReactorCapacity; i++)
                {
                    Transform source = selectedReactors[i];
                    if (source == null || !source.gameObject.activeInHierarchy ||
                        (selectedReactorRenderers[i] != null && selectedReactorRenderers[i].forceRenderingOff)) continue;
                    Vector3 position = source.position;
                    // Runtime source motion does not wait for the selection timer.
                    reactorPositions[ActiveReactorSources] = new Vector4(position.x, position.y, position.z, reactorSourceStrength);
                    reactorColors[ActiveReactorSources] = new Vector4(reactorColor.r, reactorColor.g, reactorColor.b, reactorSourceRadius);
                    ActiveReactorSources++;
                }
            for (int i = ActiveReactorSources; i < ReactorCapacity; i++)
                reactorPositions[i] = reactorColors[i] = Vector4.zero;
            ApplyProperties();
        }

        void SelectNearbyReactors()
        {
            Array.Clear(selectedReactors, 0, ReactorCapacity);
            Array.Clear(selectedReactorRenderers, 0, ReactorCapacity);
            for (int i = 0; i < ReactorCapacity; i++) selectedDistances[i] = float.PositiveInfinity;
            if (!reactorReflections || reactorSources == null) return;
            Vector3 position = targetRenderer.bounds.center;
            float limitSquared = reactorMaximumDistance * reactorMaximumDistance;
            // A fixed top-three insertion list, once per .12 s, avoids a per-frame
            // sort, FindObjects scan, allocations or one Update on each of 120 ships.
            foreach (Transform source in reactorSources)
            {
                if (source == null || !source.gameObject.activeInHierarchy) continue;
                float distance = (source.position - position).sqrMagnitude;
                if (distance > limitSquared || distance >= selectedDistances[ReactorCapacity - 1]) continue;
                // The native LODGroup may cull this particular LOD0 core while a
                // co-located lower LOD still emits. Only the presentation quality
                // mask suppresses the source, never isVisible or Renderer.enabled.
                source.TryGetComponent(out Renderer sourceRenderer);
                if (sourceRenderer != null && sourceRenderer.forceRenderingOff) continue;
                for (int slot = 0; slot < ReactorCapacity; slot++)
                {
                    if (distance >= selectedDistances[slot]) continue;
                    for (int move = ReactorCapacity - 1; move > slot; move--)
                    {
                        selectedDistances[move] = selectedDistances[move - 1];
                        selectedReactors[move] = selectedReactors[move - 1];
                        selectedReactorRenderers[move] = selectedReactorRenderers[move - 1];
                    }
                    selectedDistances[slot] = distance;
                    selectedReactors[slot] = source;
                    selectedReactorRenderers[slot] = sourceRenderer;
                    break;
                }
            }
        }

        public void ResetResponse()
        {
            ActiveExplosionSources = ActiveReactorSources = 0;
            PeakExplosionStrength = 0;
            nextReactorSelection = 0;
            Array.Clear(burstPositions, 0, BurstCapacity);
            Array.Clear(burstColors, 0, BurstCapacity);
            Array.Clear(reactorPositions, 0, ReactorCapacity);
            Array.Clear(reactorColors, 0, ReactorCapacity);
            Array.Clear(selectedReactors, 0, ReactorCapacity);
            Array.Clear(selectedReactorRenderers, 0, ReactorCapacity);
            ApplyProperties();
        }

        void ApplyProperties()
        {
            if (targetRenderer == null) return;
            properties ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(properties);
            properties.SetVectorArray(BurstPositionsId, burstPositions);
            properties.SetVectorArray(BurstColorsId, burstColors);
            properties.SetFloat(BurstCountId, ActiveExplosionSources);
            properties.SetVectorArray(ReactorPositionsId, reactorPositions);
            properties.SetVectorArray(ReactorColorsId, reactorColors);
            properties.SetFloat(ReactorCountId, ActiveReactorSources);
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
