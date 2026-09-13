using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>
    /// Saved ShipTargets keep the authoritative identity, pose and lifetime.
    /// Close targets use their complete prefab; distant targets share LOD1/2 meshes.
    /// No target, mesh, material, light or effect is created during a tier transition.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(800)]
    public sealed class FleetRenderManager : MonoBehaviour
    {
        public ShipTarget[] targets = Array.Empty<ShipTarget>();
        public Camera gameplayCamera;
        public Transform droplet;
        [Min(10)] public float nearDistance = 600;
        [Min(20)] public float middleDistance = 1800;
        [Min(100)] public float chunkSize = 1400;
        [Min(0)] public float transitionHysteresis = 50;
        public bool streamInteraction = true;

        public int TargetCount => entries.Length;
        public int AliveCount { get { UpdateCounters(); return aliveCount; } }
        public int FullPrefabCount { get { UpdateCounters(); return fullPrefabCount; } }
        public int InstancedShipCount { get { UpdateCounters(); return instancedShipCount; } }
        public int InteractionShipCount { get { UpdateCounters(); return interactionShipCount; } }
        public int VisibleInstancedShips { get; private set; }
        public int LastDrawCalls { get; private set; }
        public int LastLod1Ships { get; private set; }
        public int LastLod2Ships { get; private set; }
        public int LastSubmittedPartInstances { get; private set; }
        public int Lod1DrawGroups => parts[0].Length;
        public int Lod2DrawGroups => parts[1].Length;
        public int LastSweepActivated { get; private set; }
        public int ChunkCount => chunks.Count;
        public bool InstancingAvailable { get; private set; }
        [NonSerialized] public bool MeasureCpuCost;
        public double RefreshCpuMilliseconds { get; private set; }
        public double RenderCpuMilliseconds { get; private set; }

        // Unity 6000.5 uses objectToWorld + worldToObject by default: 511 is safe
        // without requiring assumeuniformscaling on every shared shader.
        const int BatchCapacity = 511;
        static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("Fleet2000.RefreshTiers");
        static readonly ProfilerMarker SweepMarker = new ProfilerMarker("Fleet2000.PrepareSweep");
        static readonly ProfilerMarker RenderMarker = new ProfilerMarker("Fleet2000.RenderInstanced");

        sealed class Entry
        {
            public ShipTarget target;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public Bounds localBounds;
            public float radius;
            public Renderer[] renderers;
            public bool[] originalMasks;
            public bool full, interaction, initialized;
            public int sweptFrame = -1, renderLod;
        }
        sealed class Chunk
        {
            public readonly List<int> entries = new List<int>();
            public Bounds bounds;
        }
        sealed class Part
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] localMatrices = Array.Empty<Matrix4x4>();
            public int submesh, layer;
            public uint renderingLayerMask;
            public ShadowCastingMode shadowCasting;
            public bool receiveShadows;
            public byte qualityMask;
        }
        Entry[] entries = Array.Empty<Entry>();
        readonly List<Chunk> chunks = new List<Chunk>();
        readonly Dictionary<ShipTarget, Entry> lookup = new Dictionary<ShipTarget, Entry>();
        readonly Matrix4x4[] matrices = new Matrix4x4[BatchCapacity];
        readonly Plane[] planes = new Plane[6];
        Part[][] parts = { Array.Empty<Part>(), Array.Empty<Part>() };
        FusionDriveVisuals driveQualitySource;
        const byte CoreQualityMask = 1, DetailQualityMask = 2;
        bool rebuilding;
        bool countersDirty;
        int aliveCount, fullPrefabCount, instancedShipCount, interactionShipCount;

        public void Configure(ShipTarget[] fleet, Camera view, Transform player)
        {
            targets = fleet ?? Array.Empty<ShipTarget>();
            gameplayCamera = view;
            droplet = player;
            RebuildCache();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += RenderCamera;
            RebuildCache();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderCamera;
            ReleaseTargets();
            entries = Array.Empty<Entry>();
            chunks.Clear(); lookup.Clear();
            aliveCount = fullPrefabCount = instancedShipCount = interactionShipCount = 0;
            countersDirty = false;
            VisibleInstancedShips = LastDrawCalls = LastLod1Ships = LastLod2Ships = LastSubmittedPartInstances = 0;
            driveQualitySource = null;
        }

        /// <summary>Explicit after layout edits/import. Does not discover or create targets.</summary>
        public void RebuildCache()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
                ReleaseTargets();
                lookup.Clear(); chunks.Clear();
                var unique = new List<Entry>(targets == null ? 0 : targets.Length);
                var cells = new Dictionary<Vector3Int, Chunk>();
                parts = new[] { Array.Empty<Part>(), Array.Empty<Part>() };
                driveQualitySource = null;
                InstancingAvailable = SystemInfo.supportsInstancing;
                if (targets != null) foreach (var target in targets)
                {
                    if (target == null || lookup.ContainsKey(target)) continue;
                    var renderers = target.visualRoot != null
                        ? target.visualRoot.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
                    var entry = new Entry
                    {
                        target = target, matrix = target.transform.localToWorldMatrix,
                        renderers = renderers, originalMasks = new bool[renderers.Length],
                        bounds = WorldBounds(target, renderers)
                    };
                    entry.radius = entry.bounds.extents.magnitude;
                    entry.localBounds = TransformBounds(entry.bounds, target.transform.worldToLocalMatrix);
                    for (int i = 0; i < renderers.Length; i++) entry.originalMasks[i] = renderers[i].forceRenderingOff;
                    if (unique.Count == 0)
                    {
                        driveQualitySource = target.GetComponentInChildren<FusionDriveVisuals>(true);
                        parts[0] = ReadParts(target, 1);
                        parts[1] = ReadParts(target, 2);
                        InstancingAvailable &= parts[0].Length > 0 && parts[1].Length > 0;
                    }
                    var point = entry.bounds.center / Mathf.Max(100, chunkSize);
                    var key = new Vector3Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), Mathf.FloorToInt(point.z));
                    if (!cells.TryGetValue(key, out var chunk))
                    {
                        chunk = new Chunk { bounds = entry.bounds };
                        cells.Add(key, chunk); chunks.Add(chunk);
                    }
                    else chunk.bounds.Encapsulate(entry.bounds);
                    chunk.entries.Add(unique.Count);
                    unique.Add(entry); lookup.Add(target, entry);
                    target.Destroyed += OnDestroyed;
                    target.Escaped += OnDestroyed;
                    target.Restored += OnRestored;
                }
                entries = unique.ToArray();
                if (entries.Length > 0 && !InstancingAvailable)
                    Debug.LogWarning("Fleet2000: instancing unavailable or LOD1/2 material instancing disabled; keeping complete interactive prefabs.", this);
                RefreshNow();
            }
            finally { rebuilding = false; }
        }

        Part[] ReadParts(ShipTarget source, int level)
        {
            if (source.visualRoot == null) return Array.Empty<Part>();
            var group = source.visualRoot.GetComponentInChildren<LODGroup>(true);
            if (group == null) return Array.Empty<Part>();
            var lods = group.GetLODs();
            if (lods.Length <= level) return Array.Empty<Part>();
            var result = new List<Part>();
            var groups = new Dictionary<(Mesh, Material, int, int, uint, ShadowCastingMode, bool, byte), Part>();
            var localMatrixBuilders = new Dictionary<Part, List<Matrix4x4>>();
            foreach (var renderer in lods[level].renderers)
            {
                if (renderer == null || !(renderer is MeshRenderer)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                byte qualityMask = 0;
                if (driveQualitySource != null)
                {
                    if (driveQualitySource.coreRenderers != null && Array.IndexOf(driveQualitySource.coreRenderers, renderer) >= 0) qualityMask |= CoreQualityMask;
                    if (driveQualitySource.detailRenderers != null && Array.IndexOf(driveQualitySource.detailRenderers, renderer) >= 0) qualityMask |= DetailQualityMask;
                }
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    if (i >= materials.Length || materials[i] == null || !materials[i].enableInstancing)
                    { InstancingAvailable = false; continue; }
                    var key = (filter.sharedMesh, materials[i], i, renderer.gameObject.layer,
                        renderer.renderingLayerMask, renderer.shadowCastingMode, renderer.receiveShadows, qualityMask);
                    if (!groups.TryGetValue(key, out var part))
                    {
                        part = new Part
                        {
                            mesh = filter.sharedMesh, material = materials[i], submesh = i,
                            layer = renderer.gameObject.layer, renderingLayerMask = renderer.renderingLayerMask,
                            shadowCasting = renderer.shadowCastingMode, receiveShadows = renderer.receiveShadows,
                            qualityMask = qualityMask
                        };
                        groups.Add(key, part); result.Add(part);
                        localMatrixBuilders.Add(part, new List<Matrix4x4>());
                    }
                    // A frigate's four auxiliary engines and five cores already share
                    // geometry. Their different transforms belong in the same draw,
                    // alongside the matching parts from other ships in this chunk.
                    localMatrixBuilders[part].Add(source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                }
            }
            foreach (var part in result) part.localMatrices = localMatrixBuilders[part].ToArray();
            return result.ToArray();
        }

        void ReleaseTargets()
        {
            foreach (var entry in entries)
            {
                var target = entry.target;
                if (target == null) continue;
                target.Destroyed -= OnDestroyed; target.Restored -= OnRestored;
                target.Escaped -= OnDestroyed;
                for (int i = 0; i < entry.renderers.Length; i++)
                    if (entry.renderers[i] != null) entry.renderers[i].forceRenderingOff = entry.originalMasks[i];
                // Fail open: disabling this optional renderer cannot remove live gameplay.
                if (!target.IsResolved)
                {
                    if (target.visualRoot != null) target.visualRoot.SetActive(true);
                    if (target.hitVolumes != null) foreach (var collider in target.hitVolumes)
                        if (collider != null) collider.enabled = true;
                }
            }
        }

        void LateUpdate() => RefreshNow();

        public void RefreshNow()
        {
            if (!isActiveAndEnabled) return;
            long costStart = MeasureCpuCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            using (RefreshMarker.Auto())
            {
                RefreshAuthoritativePoses();
                bool playing = Application.IsPlaying(gameObject);
                foreach (var entry in entries)
                {
                    if (entry.target == null || entry.target.IsResolved) continue;
                    bool full = !InstancingAvailable || WantsFull(entry) || (playing && entry.sweptFrame == Time.frameCount);
                    bool interaction = !playing || !streamInteraction || full;
                    ApplyState(entry, full, interaction);
                }
                countersDirty = true;
            }
            if (MeasureCpuCost) RefreshCpuMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - costStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>Update both instanced matrices and culling bounds from the same moving targets used by collision.</summary>
        public void RefreshAuthoritativePoses()
        {
            bool dynamic = false;
            foreach (var entry in entries)
            {
                if (entry.target == null || entry.target.CombatSimulation == null) continue;
                dynamic = true;
                entry.matrix = entry.target.transform.localToWorldMatrix;
                entry.bounds = TransformBounds(entry.localBounds, entry.matrix);
                entry.radius = entry.bounds.extents.magnitude;
            }
            if (!dynamic) return;
            foreach (var chunk in chunks)
            {
                bool first = true;
                foreach (int index in chunk.entries)
                {
                    var entry = entries[index];
                    if (entry.target == null || entry.target.IsResolved) continue;
                    if (first) { chunk.bounds = entry.bounds; first = false; }
                    else chunk.bounds.Encapsulate(entry.bounds);
                }
            }
            countersDirty = true;
        }

        bool WantsFull(Entry entry)
        {
            if (droplet == null && gameplayCamera == null) return true;
            float distance = Mathf.Max(10, nearDistance) + (entry.full ? Mathf.Max(0, transitionHysteresis) : 0);
            // Include the complete hull, so a close bow cannot remain streamed out.
            float sqr = distance * distance;
            return (droplet != null && entry.bounds.SqrDistance(droplet.position) <= sqr) ||
                (gameplayCamera != null && entry.bounds.SqrDistance(gameplayCamera.transform.position) <= sqr);
        }

        void ApplyState(Entry entry, bool full, bool interaction)
        {
            var target = entry.target;
            if (target == null || target.IsResolved) return;
            if (!entry.initialized || entry.full != full)
            {
                if (Application.IsPlaying(gameObject))
                {
                    if (target.visualRoot != null) target.visualRoot.SetActive(full);
                }
                else
                {
                    // Editor previews do not serialize hidden VisualRoots/collider states.
                    for (int i = 0; i < entry.renderers.Length; i++)
                        if (entry.renderers[i] != null)
                            entry.renderers[i].forceRenderingOff = !full || entry.originalMasks[i];
                }
                entry.full = full;
            }
            if (!entry.initialized || entry.interaction != interaction)
            {
                if (target.hitVolumes != null) foreach (var collider in target.hitVolumes)
                    if (collider != null) collider.enabled = interaction;
                entry.interaction = interaction;
            }
            entry.initialized = true;
            countersDirty = true;
        }

        /// <summary>
        /// Called synchronously BEFORE overlap and cast queries for every actual motor
        /// segment. The broad phase covers arbitrarily long paths and initial overlap;
        /// the existing precise compound-collider query remains the only hit authority.
        /// </summary>
        public void PrepareSweep(Vector3 from, Vector3 to, float radius)
        {
            LastSweepActivated = 0;
            if (!isActiveAndEnabled || !Application.IsPlaying(gameObject)) return;
            using (SweepMarker.Auto())
            {
                bool changed = false;
                radius = Mathf.Max(0, radius);
                foreach (var chunk in chunks)
                {
                    float chunkRadius = chunk.bounds.extents.magnitude + radius;
                    if (DistanceToSegmentSquared(chunk.bounds.center, from, to) > chunkRadius * chunkRadius) continue;
                    foreach (int index in chunk.entries)
                    {
                        var entry = entries[index];
                        if (entry.target == null || entry.target.IsResolved) continue;
                        float expandedRadius = entry.radius + radius + .01f;
                        if (DistanceToSegmentSquared(entry.bounds.center, from, to) > expandedRadius * expandedRadius) continue;
                        entry.sweptFrame = Time.frameCount;
                        if (!entry.interaction) { changed = true; LastSweepActivated++; }
                        ApplyState(entry, true, true);
                    }
                }
                // Even with autoSyncTransforms off, re-enabled compound shapes must
                // participate in the very next query, including a zero-length path.
                if (changed) Physics.SyncTransforms();
            }
        }

        public bool IsInstanced(ShipTarget target) => target != null && !target.IsResolved &&
            lookup.TryGetValue(target, out var entry) && entry.initialized && !entry.full && InstancingAvailable;

        public bool IsInteractionActive(ShipTarget target) => target != null && !target.IsResolved &&
            lookup.TryGetValue(target, out var entry) && entry.interaction;

        void OnDestroyed(ShipTarget target)
        {
            if (!lookup.TryGetValue(target, out var entry)) return;
            entry.interaction = false; entry.sweptFrame = -1;
            // The target's IsDestroyed flag excludes it from every render submission
            // immediately. Its existing reactor presenter may show 0.16 s instability.
            countersDirty = true;
        }

        void OnRestored(ShipTarget target)
        {
            if (!lookup.TryGetValue(target, out var entry)) return;
            entry.initialized = false; entry.sweptFrame = -1; entry.full = false;
            bool full = !InstancingAvailable || WantsFull(entry);
            ApplyState(entry, full, !Application.IsPlaying(gameObject) || !streamInteraction || full);
        }

        void UpdateCounters()
        {
            if (!countersDirty) return;
            aliveCount = fullPrefabCount = instancedShipCount = interactionShipCount = 0;
            foreach (var entry in entries)
            {
                if (entry.target == null || entry.target.IsResolved) continue;
                aliveCount++;
                if (entry.full) fullPrefabCount++; else instancedShipCount++;
                if (entry.interaction) interactionShipCount++;
            }
            countersDirty = false;
        }

        void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled || !InstancingAvailable || camera == null || camera.cameraType == CameraType.Preview) return;
            long costStart = MeasureCpuCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            using (RenderMarker.Auto())
            {
                bool record = camera == gameplayCamera;
                if (record) VisibleInstancedShips = LastDrawCalls = LastLod1Ships = LastLod2Ships = LastSubmittedPartInstances = 0;
                // SolarLightingRig applies this same existing quality to every drive,
                // including inactive/destroyed source visuals. Do not infer quality
                // from forceRenderingOff: editor preview also uses that mask.
                EffectQuality quality = driveQualitySource != null ? driveQualitySource.Quality : EffectQuality.High;
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                Vector3 cameraPosition = camera.transform.position;
                float middleSquared = Mathf.Max(nearDistance, middleDistance) * Mathf.Max(nearDistance, middleDistance);
                foreach (var chunk in chunks)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, chunk.bounds)) continue;
                    int mid = 0, far = 0;
                    foreach (int index in chunk.entries)
                    {
                        var entry = entries[index]; entry.renderLod = -1;
                        if (entry.target == null || entry.target.IsResolved || entry.full ||
                            !entry.target.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, entry.bounds)) continue;
                        entry.renderLod = entry.bounds.SqrDistance(cameraPosition) < middleSquared ? 0 : 1;
                        if (entry.renderLod == 0) mid++; else far++;
                    }
                    if (record) { VisibleInstancedShips += mid + far; LastLod1Ships += mid; LastLod2Ships += far; }
                    for (int level = 0; level < 2; level++)
                    {
                        if ((level == 0 ? mid : far) == 0) continue;
                        foreach (var part in parts[level])
                        {
                            if ((camera.cullingMask & (1 << part.layer)) == 0) continue;
                            if (((part.qualityMask & CoreQualityMask) != 0 && quality == EffectQuality.Off) ||
                                ((part.qualityMask & DetailQualityMask) != 0 && quality != EffectQuality.High)) continue;
                            int count = 0;
                            var parameters = new RenderParams(part.material)
                            {
                                camera = camera, layer = part.layer, renderingLayerMask = part.renderingLayerMask,
                                worldBounds = chunk.bounds, receiveShadows = part.receiveShadows,
                                shadowCastingMode = level == 0 ? part.shadowCasting : ShadowCastingMode.Off,
                                lightProbeUsage = LightProbeUsage.Off,
                                // Match the outdoor prefab: blend local captures back into the shared sky reflection.
                                reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox
                            };
                            foreach (int index in chunk.entries)
                            {
                                var entry = entries[index];
                                if (entry.renderLod != level) continue;
                                var localMatrices = part.localMatrices;
                                for (int localIndex = 0; localIndex < localMatrices.Length; localIndex++)
                                {
                                    matrices[count++] = MultiplyAffine(in entry.matrix, in localMatrices[localIndex]);
                                    if (count < BatchCapacity) continue;
                                    Graphics.RenderMeshInstanced(parameters, part.mesh, part.submesh, matrices, count);
                                    if (record) { LastDrawCalls++; LastSubmittedPartInstances += count; }
                                    count = 0;
                                }
                            }
                            if (count > 0)
                            {
                                Graphics.RenderMeshInstanced(parameters, part.mesh, part.submesh, matrices, count);
                                if (record) { LastDrawCalls++; LastSubmittedPartInstances += count; }
                            }
                        }
                    }
                }
            }
            if (MeasureCpuCost) RenderCpuMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - costStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public static float DistanceToSegmentSquared(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < .00000001f) return (point - from).sqrMagnitude;
            float t = Mathf.Clamp01(Vector3.Dot(point - from, segment) / lengthSquared);
            return (point - (from + segment * t)).sqrMagnitude;
        }

        /// <summary>Compose two Transform-derived affine matrices. Their bottom
        /// rows are (0,0,0,1), including nonuniform/negative scale and shear.
        /// This preserves the full geometry without doing the unused fourth-row
        /// work of the general-purpose projective 4x4 operator for every part.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 MultiplyAffine(in Matrix4x4 left, in Matrix4x4 right)
        {
            Matrix4x4 result;
            result.m00 = left.m00 * right.m00 + left.m01 * right.m10 + left.m02 * right.m20;
            result.m01 = left.m00 * right.m01 + left.m01 * right.m11 + left.m02 * right.m21;
            result.m02 = left.m00 * right.m02 + left.m01 * right.m12 + left.m02 * right.m22;
            result.m03 = left.m00 * right.m03 + left.m01 * right.m13 + left.m02 * right.m23 + left.m03;
            result.m10 = left.m10 * right.m00 + left.m11 * right.m10 + left.m12 * right.m20;
            result.m11 = left.m10 * right.m01 + left.m11 * right.m11 + left.m12 * right.m21;
            result.m12 = left.m10 * right.m02 + left.m11 * right.m12 + left.m12 * right.m22;
            result.m13 = left.m10 * right.m03 + left.m11 * right.m13 + left.m12 * right.m23 + left.m13;
            result.m20 = left.m20 * right.m00 + left.m21 * right.m10 + left.m22 * right.m20;
            result.m21 = left.m20 * right.m01 + left.m21 * right.m11 + left.m22 * right.m21;
            result.m22 = left.m20 * right.m02 + left.m21 * right.m12 + left.m22 * right.m22;
            result.m23 = left.m20 * right.m03 + left.m21 * right.m13 + left.m22 * right.m23 + left.m23;
            result.m30 = result.m31 = result.m32 = 0;
            result.m33 = 1;
            return result;
        }

        static Bounds WorldBounds(ShipTarget target, Renderer[] renderers)
        {
            var bounds = new Bounds(target.transform.position, Vector3.zero);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    bounds.Encapsulate(TransformBounds(filter.sharedMesh.bounds, renderer.transform.localToWorldMatrix));
            }
            if (target.hitVolumes != null) foreach (var collider in target.hitVolumes)
            {
                if (collider == null) continue;
                if (collider is BoxCollider box)
                    bounds.Encapsulate(TransformBounds(new Bounds(box.center, box.size), box.transform.localToWorldMatrix));
                else if (collider.enabled && collider.gameObject.activeInHierarchy) bounds.Encapsulate(collider.bounds);
            }
            return bounds;
        }

        static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(local.center);
            var x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(center, extents * 2);
        }
    }
}
