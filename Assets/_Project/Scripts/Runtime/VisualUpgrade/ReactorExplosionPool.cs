using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>
    /// Bounded collider-free reactor VFX. Gameplay commits immediately; visual
    /// instability, blast and directional reflection share one simulation clock.
    /// All geometry, arrays, renderers and the two lights are prewarmed once.
    /// </summary>
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class ReactorExplosionPool : MonoBehaviour
    {
        public MissionController mission;
        public MissionEffects legacyEffects;
        public EffectSettings settings;
        public Transform focus;
        public Camera viewCamera;
        public Material fireMaterial, flashMaterial, debrisMaterial;
        [Range(.08f, .3f)] public float instabilityDuration = .16f;
        [Range(1.5f, 4)] public float lifetime = 2.7f;
        [Range(0, 2)] public int lightBudget = 2;
        [Min(0)] public float lightIntensity = 22;

        public int ActiveEffectCount { get; private set; }
        public int PeakActiveEffectCount { get; private set; }
        public int ActiveLightCount { get; private set; }
        public int PoolInstanceCount { get; private set; }
        public int DroppedEffectCount { get; private set; }
        public int ReclaimedEffectCount { get; private set; }
        public float ElapsedSimulationTime { get; private set; }
        public int Capacity => Quality == EffectQuality.Off ? 0 : Quality == EffectQuality.Low ? 6 : 12;
        public EffectQuality Quality
        {
            get => qualityAssigned ? quality : legacyEffects != null ? legacyEffects.Quality :
                settings != null ? settings.initialQuality : EffectQuality.High;
            set { qualityAssigned = true; quality = value; lastQuality = value; ResetEffects(); }
        }

        const int SlotCount = 12, LobeCount = 5, ShardCount = 8;
        sealed class View
        {
            public Transform transform;
            public MeshRenderer renderer;
        }
        sealed class Slot
        {
            public GameObject root;
            public ReactorDestructionPresenter owner;
            public View impact, ring, sparks, jet, debris;
            public View[] lobes;
            public Mesh shardMesh;
            public Vector3[] shardVertices;
            public Vector3 source, hitPoint, direction;
            public Quaternion rotation;
            public float age, radius, priority, seed;
            public bool active;
        }
        Slot[] slots = Array.Empty<Slot>();
        readonly Light[] lights = new Light[2];
        readonly int[] bestIndices = new int[4];
        readonly float[] bestScores = new float[4];
        MaterialPropertyBlock properties;
        GameObject poolRoot;
        Mesh quadMesh, ringMesh, sparkMesh;
        MissionController boundMission;
        EffectQuality quality, lastQuality;
        bool initialized, qualityAssigned;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int Age = Shader.PropertyToID("_Age");
        static readonly int Seed = Shader.PropertyToID("_Seed");
        static readonly Vector3[] ShardShape = { new Vector3(-.5f, 0, -.2f), new Vector3(.6f, 0, -.3f),
            new Vector3(.1f, .16f, .2f), new Vector3(0, -.1f, .75f) };

        void OnEnable() => Bind();
        void Start() => InitializePool();
        void Bind()
        {
            if (boundMission == mission) return;
            if (boundMission != null) boundMission.Restarted -= ResetEffects;
            boundMission = mission;
            if (boundMission != null) boundMission.Restarted += ResetEffects;
        }

        public void InitializePool()
        {
            Bind();
            if (initialized) return;
            initialized = true; lastQuality = Quality;
            properties = new MaterialPropertyBlock();
            if (viewCamera == null) viewCamera = Camera.main;
            if (focus == null && mission != null && mission.motor != null) focus = mission.motor.transform;
            poolRoot = new GameObject("__ReactorExplosionPool");
            poolRoot.layer = 2; poolRoot.transform.SetParent(transform, false);
            quadMesh = CreateQuad(); ringMesh = CreateRing(); sparkMesh = CreateSparks();
            slots = new Slot[SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new Slot { root = new GameObject("ReactorBurst_" + i.ToString("00")),
                    lobes = new View[LobeCount], shardVertices = new Vector3[ShardCount * 4] };
                slot.root.layer = 2; slot.root.transform.SetParent(poolRoot.transform, false);
                slot.impact = MakeView("PenetrationFlash", slot.root.transform, quadMesh, flashMaterial);
                slot.ring = MakeView("ReactorShockRing", slot.root.transform, ringMesh, flashMaterial);
                slot.sparks = MakeView("BallisticHotSparks", slot.root.transform, sparkMesh, flashMaterial);
                slot.jet = MakeView("ForwardHullRupture", slot.root.transform, quadMesh, fireMaterial);
                for (int l = 0; l < LobeCount; l++)
                    slot.lobes[l] = MakeView("FireLobe_" + l, slot.root.transform, quadMesh, fireMaterial);
                slot.shardMesh = CreateShardMesh(slot.shardVertices);
                slot.debris = MakeView("EightArmourFragments", slot.root.transform, slot.shardMesh, debrisMaterial);
                slot.root.SetActive(false); slots[i] = slot;
            }
            for (int i = 0; i < lights.Length; i++)
            {
                var go = new GameObject("SharedReactorLight_" + i); go.layer = 2;
                go.transform.SetParent(poolRoot.transform, false);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point; light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel; light.enabled = false;
                light.bounceIntensity = 0; lights[i] = light;
            }
            PoolInstanceCount = poolRoot.GetComponentsInChildren<Transform>(true).Length;
        }

        public bool TryPlay(ReactorDestructionPresenter presenter)
        {
            if (!isActiveAndEnabled || presenter == null || presenter.target == null ||
                !presenter.target.IsDestroyed || Quality == EffectQuality.Off) return false;
            InitializePool();
            if (lastQuality != Quality) { ResetEffects(); lastQuality = Quality; }
            // Idempotence also holds if an optional listener is inadvertently
            // wired twice; one committed ship can own only one live slot.
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].active && slots[i].owner == presenter) return false;
            Vector3 source = presenter.ReactorPosition;
            float priority = Priority(source);
            int selected = -1;
            float least = float.MaxValue;
            for (int i = 0; i < Capacity; i++)
            {
                Slot candidate = slots[i];
                if (!candidate.active) { selected = i; break; }
                float retention = Priority(candidate.source) * (candidate.age < .9f ? 1.15f : .65f);
                if (retention < least) { least = retention; selected = i; }
            }
            if (selected < 0 || (slots[selected].active && priority <= least))
            { DroppedEffectCount++; return false; }
            Slot slot = slots[selected];
            if (slot.active) { Release(slot); ReclaimedEffectCount++; }
            ShipHitContext hit = presenter.target.LastHit;
            slot.owner = presenter; slot.source = source; slot.hitPoint = hit.point;
            slot.direction = hit.direction.sqrMagnitude > .0001f ? hit.direction.normalized : presenter.transform.forward;
            slot.rotation = presenter.transform.rotation; slot.radius = Mathf.Clamp(presenter.explosionRadius, 2, 30);
            slot.age = 0; slot.seed = selected * 1.371f + source.x * .013f + source.z * .007f;
            slot.active = true; slot.priority = priority;
            slot.root.transform.SetPositionAndRotation(source, slot.rotation);
            slot.root.transform.localScale = Vector3.one;
            slot.root.SetActive(true); presenter.BeginInstability();
            ActiveEffectCount++; PeakActiveEffectCount = Mathf.Max(PeakActiveEffectCount, ActiveEffectCount);
            Animate(slot); UpdateLights(); return true;
        }

        void Update()
        {
            if (!initialized) InitializePool();
            if (lastQuality != Quality) { ResetEffects(); lastQuality = Quality; }
            if (Quality == EffectQuality.Off || Time.deltaTime <= 0 ||
                (mission != null && mission.State == MissionState.Paused)) return;
            ElapsedSimulationTime += Time.deltaTime;
            foreach (Slot slot in slots)
            {
                if (!slot.active) continue;
                if (slot.owner == null || slot.owner.target == null || !slot.owner.target.IsDestroyed)
                { Release(slot); continue; }
                slot.age += Time.deltaTime;
                if (slot.age >= lifetime) { Release(slot); continue; }
                Animate(slot);
            }
            UpdateLights();
        }

        void Animate(Slot slot)
        {
            float age = slot.age, burst = age - instabilityDuration, radius = slot.radius;
            Quaternion billboard = viewCamera != null ? viewCamera.transform.rotation : slot.rotation;
            float impact = Mathf.Clamp01(1 - age / .085f);
            Show(slot.impact, impact > 0 && flashMaterial != null);
            slot.impact.transform.SetPositionAndRotation(slot.hitPoint, billboard);
            slot.impact.transform.localScale = new Vector3(radius * .48f, radius * .12f, 1) * (1.4f - impact * .4f);
            Tint(slot.impact, new Color(4.5f, 6, 8, impact * .85f), age, slot.seed);
            if (burst < 0)
            {
                float instability = Mathf.Clamp01(age / Mathf.Max(.01f, instabilityDuration));
                slot.owner.SetInstability(instability * (1.2f + .25f * Mathf.Sin(age * 145)));
                Show(slot.lobes[0], fireMaterial != null);
                slot.lobes[0].transform.SetPositionAndRotation(slot.source, billboard);
                slot.lobes[0].transform.localScale = Vector3.one * radius * (.12f + instability * .2f);
                Tint(slot.lobes[0], new Color(.6f, 2.6f, 5, .25f + instability * .45f), age, slot.seed);
                for (int i = 1; i < LobeCount; i++) Show(slot.lobes[i], false);
                Show(slot.ring, false); Show(slot.sparks, false); Show(slot.jet, false); Show(slot.debris, false);
                return;
            }
            slot.owner.EndInstability();
            float blast = Mathf.Clamp01(1 - burst / .85f);
            float remnant = Mathf.Clamp01(1 - burst / Mathf.Max(.2f, lifetime - instabilityDuration));
            float expansion = 1 - Mathf.Exp(-burst * 12);
            for (int i = 0; i < LobeCount; i++)
            {
                bool detailed = Quality == EffectQuality.High || i < 3;
                float delay = i * .024f, t = Mathf.Max(0, burst - delay);
                float fade = Mathf.Clamp01(1 - t / (i == 0 ? 1.15f : 1.8f));
                Show(slot.lobes[i], detailed && burst >= delay && fade > 0 && fireMaterial != null);
                // Separate the torn orange shell from a much smaller white core.
                // Broad overlapping HDR billboards otherwise merge into one
                // smooth overexposed sphere, erasing the actual plasma texture.
                Vector3 drift = i == 0 ? Vector3.zero :
                    new Vector3(Mathf.Sin(i * 2.4f) * .68f, Mathf.Cos(i * 2.4f) * .53f, (i - 1) * .26f);
                slot.lobes[i].transform.SetPositionAndRotation(slot.source + slot.rotation * drift * radius * expansion,
                    billboard * Quaternion.AngleAxis(i * 71 + slot.seed * 17, Vector3.forward));
                float swell = (.2f + .72f * (1 - Mathf.Exp(-t * 9))) * (i == 0 ? .42f : .55f);
                slot.lobes[i].transform.localScale = new Vector3(1.05f + .1f * Mathf.Sin(i * 2), .72f + i * .065f, 1) * radius * swell;
                Color initial = i == 0 ? new Color(3.1f, 2.15f, .95f, 1) : new Color(2.3f, .75f, .13f, 1);
                Color cooled = i == 0 ? new Color(1.8f, .26f, .018f, 1) : new Color(1.35f, .12f, .012f, 1);
                Color hot = Color.Lerp(initial, cooled, Mathf.Clamp01(t * 2.8f + i * .035f));
                hot.a = fade * (i == 0 ? .58f : .43f);
                Tint(slot.lobes[i], hot, t, slot.seed + i * 1.73f);
            }
            float ring = Mathf.Clamp01(1 - burst / .7f);
            Show(slot.ring, ring > 0 && flashMaterial != null);
            slot.ring.transform.localPosition = Vector3.zero;
            slot.ring.transform.localRotation = Quaternion.identity;
            slot.ring.transform.localScale = Vector3.one * radius * (.15f + burst * 3.1f);
            Tint(slot.ring, new Color(2.0f, .72f, .18f, ring * ring * .46f), burst, slot.seed);
            Show(slot.jet, blast > 0 && fireMaterial != null);
            slot.jet.transform.SetPositionAndRotation(slot.source + slot.rotation * Vector3.forward * radius * expansion * .58f, billboard);
            // A long hull-axis rupture reads as a reactor driven blast, distinct
            // from the short penetration flash at the actual collision point.
            Vector3 projected = viewCamera != null ? viewCamera.transform.InverseTransformDirection(slot.rotation * Vector3.forward) : Vector3.right;
            slot.jet.transform.rotation = billboard * Quaternion.AngleAxis(Mathf.Atan2(projected.y, projected.x) * Mathf.Rad2Deg, Vector3.forward);
            slot.jet.transform.localScale = new Vector3(radius * (.6f + expansion * .9f), radius * .28f, 1);
            Tint(slot.jet, new Color(2.3f, .62f, .1f, blast * .34f), burst, slot.seed + 13);
            Show(slot.sparks, burst < .9f && flashMaterial != null);
            slot.sparks.transform.localPosition = Vector3.zero;
            slot.sparks.transform.localScale = Vector3.one * radius * (.25f + burst * 2.6f);
            slot.sparks.transform.localRotation = Quaternion.AngleAxis(slot.seed * 23, Vector3.forward);
            Tint(slot.sparks, new Color(3.4f, 1.5f, .32f, Mathf.Clamp01(1 - burst / .9f)), burst, slot.seed);
            Show(slot.debris, debrisMaterial != null && burst > .02f);
            AnimateShards(slot, burst, remnant);
        }

        void AnimateShards(Slot slot, float burst, float remnant)
        {
            float shrink = Mathf.Clamp01(remnant * 4);
            for (int i = 0; i < ShardCount; i++)
            {
                Vector3 direction = new Vector3(Mathf.Sin(i * 2.399f), Mathf.Cos(i * 2.399f), .25f + .35f * Mathf.Sin(i * 4.13f)).normalized;
                Vector3 center = direction * slot.radius * (.1f + burst * (.6f + (i % 3) * .12f));
                center += Vector3.forward * slot.radius * burst * .34f;
                Quaternion spin = Quaternion.AngleAxis(burst * (39 + i * 13), direction);
                float size = slot.radius * (.065f + (i % 3) * .025f) * shrink;
                for (int v = 0; v < 4; v++) slot.shardVertices[i * 4 + v] = center + spin * ShardShape[v] * size;
            }
            slot.shardMesh.vertices = slot.shardVertices;
            slot.shardMesh.RecalculateNormals(); slot.shardMesh.RecalculateBounds();
        }

        float Priority(Vector3 source)
        {
            Vector3 observer = focus != null ? focus.position : viewCamera != null ? viewCamera.transform.position : transform.position;
            float weight = 1 / (1 + (source - observer).sqrMagnitude / 3600);
            if (viewCamera != null)
            {
                Vector3 viewport = viewCamera.WorldToViewportPoint(source);
                if (viewport.z > 0 && viewport.x > -.1f && viewport.x < 1.1f && viewport.y > -.1f && viewport.y < 1.1f)
                    weight *= 2;
            }
            return weight;
        }

        float Strength(Slot slot)
        {
            float burst = slot.age - instabilityDuration;
            if (burst < 0) return .12f + .48f * Mathf.Clamp01(slot.age / Mathf.Max(.01f, instabilityDuration));
            // One authoritative envelope is also sampled by the chrome shader;
            // no independently scheduled callbacks can leave stale orange light.
            return 1.8f * Mathf.Exp(-burst * 4) + .2f * Mathf.Clamp01(1 - burst / Mathf.Max(.1f, lifetime - instabilityDuration));
        }
        static Color SourceColor(Slot slot, float instability)
            => slot.age < instability ? new Color(.25f, .68f, 1) :
                Color.Lerp(new Color(1, .82f, .5f), new Color(1, .22f, .035f), Mathf.Clamp01((slot.age - instability) * 2));

        int SelectSources(int limit)
        {
            for (int i = 0; i < 4; i++) { bestIndices[i] = -1; bestScores[i] = -1; }
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].active) continue;
                float score = Strength(slots[i]) * Priority(slots[i].source);
                for (int rank = 0; rank < limit; rank++)
                {
                    if (score <= bestScores[rank]) continue;
                    for (int shift = limit - 1; shift > rank; shift--)
                    { bestScores[shift] = bestScores[shift - 1]; bestIndices[shift] = bestIndices[shift - 1]; }
                    bestScores[rank] = score; bestIndices[rank] = i; count = Mathf.Min(limit, count + 1); break;
                }
            }
            return count;
        }

        /// <summary>Nonallocating top-four world-space sources: position/strength and RGB/radius.</summary>
        public int CopyReflectionSources(Vector4[] positionsAndStrength, Vector4[] colorsAndRadius)
        {
            if (positionsAndStrength == null || colorsAndRadius == null) return 0;
            int limit = Mathf.Min(4, Mathf.Min(positionsAndStrength.Length, colorsAndRadius.Length));
            for (int i = 0; i < positionsAndStrength.Length; i++) positionsAndStrength[i] = Vector4.zero;
            for (int i = 0; i < colorsAndRadius.Length; i++) colorsAndRadius[i] = Vector4.zero;
            if (!initialized || !isActiveAndEnabled || Quality == EffectQuality.Off || limit == 0) return 0;
            int count = SelectSources(limit);
            for (int i = 0; i < count; i++)
            {
                Slot slot = slots[bestIndices[i]]; Color color = SourceColor(slot, instabilityDuration);
                positionsAndStrength[i] = new Vector4(slot.source.x, slot.source.y, slot.source.z, Strength(slot));
                colorsAndRadius[i] = new Vector4(color.r, color.g, color.b, slot.radius * 4);
            }
            return count;
        }

        void UpdateLights()
        {
            ActiveLightCount = 0;
            int count = SelectSources(Mathf.Clamp(Quality == EffectQuality.High ? lightBudget : Quality == EffectQuality.Low ? Mathf.Min(1, lightBudget) : 0, 0, 2));
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i]; if (light == null) continue;
                bool active = i < count;
                light.enabled = active;
                if (!active) continue;
                Slot slot = slots[bestIndices[i]];
                light.transform.position = slot.source;
                light.color = SourceColor(slot, instabilityDuration);
                light.intensity = lightIntensity * Strength(slot);
                light.range = slot.radius * 4;
                ActiveLightCount++;
            }
        }

        void Release(Slot slot)
        {
            if (slot.owner != null) slot.owner.EndInstability();
            slot.owner = null; slot.active = false; slot.age = 0;
            slot.root.SetActive(false); ActiveEffectCount = Mathf.Max(0, ActiveEffectCount - 1);
        }
        public void ResetEffects()
        {
            foreach (Slot slot in slots) if (slot.active) Release(slot);
            foreach (Light light in lights) if (light != null) light.enabled = false;
            ActiveEffectCount = 0; ActiveLightCount = 0; PeakActiveEffectCount = 0;
            DroppedEffectCount = 0; ReclaimedEffectCount = 0; ElapsedSimulationTime = 0;
        }
        void OnDisable()
        {
            ResetEffects();
            if (boundMission != null) boundMission.Restarted -= ResetEffects;
            boundMission = null;
        }
        void OnDestroy()
        {
            foreach (Slot slot in slots) if (slot.shardMesh != null) Destroy(slot.shardMesh);
            if (quadMesh != null) Destroy(quadMesh);
            if (ringMesh != null) Destroy(ringMesh);
            if (sparkMesh != null) Destroy(sparkMesh);
            if (poolRoot != null) Destroy(poolRoot);
        }
        static void Show(View view, bool shown) => view.renderer.enabled = shown;
        void Tint(View view, Color color, float age, float seed)
        {
            properties.Clear(); properties.SetColor(BaseColor, color); properties.SetColor(ColorId, color);
            properties.SetFloat(Age, age); properties.SetFloat(Seed, seed); view.renderer.SetPropertyBlock(properties);
        }
        static View MakeView(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name); go.layer = 2; go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return new View { transform = go.transform, renderer = renderer };
        }
        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "ReactorSoftLobeShared" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh CreateRing()
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2]; var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length]; var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                float rupture = 1 + .022f * Mathf.Sin(angle * 7) + .013f * Mathf.Sin(angle * 13);
                vertices[i * 2] = radial * rupture * .95f; vertices[i * 2 + 1] = radial * rupture;
                // Radial sprite shaders also render this authored geometric band:
                // sample their bright centre; its silhouette is the ring mesh.
                uv[i * 2] = uv[i * 2 + 1] = new Vector2(.5f, .5f);
                // Broken sectors and varying band strength remove the rigid
                // planetary-ring silhouette while retaining a readable shock.
                float sector = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Sin(angle * 5 + .4f) + .32f) * 2));
                colors[i * 2] = colors[i * 2 + 1] = new Color(1, 1, 1, sector * (.65f + .35f * Mathf.Sin(angle * 3) * Mathf.Sin(angle * 3)));
                if (i == segments) continue;
                int k = i * 6, v = i * 2;
                triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 3;
                triangles[k + 3] = v; triangles[k + 4] = v + 3; triangles[k + 5] = v + 2;
            }
            var mesh = new Mesh { name = "ReactorShockRingShared", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh CreateSparks()
        {
            const int count = 22;
            var vertices = new Vector3[count * 3]; var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length]; var triangles = new int[vertices.Length];
            for (int i = 0; i < count; i++)
            {
                Vector3 radial = new Vector3(Mathf.Sin(i * 2.399f), Mathf.Cos(i * 2.399f), Mathf.Sin(i * 1.721f) * .7f).normalized;
                Vector3 side = Vector3.Cross(radial, Vector3.forward).normalized * .009f;
                float reach = .6f + (i % 4) * .13f;
                int v = i * 3;
                vertices[v] = radial * reach - side; vertices[v + 1] = radial * reach + side;
                vertices[v + 2] = radial * reach * .46f;
                colors[v] = colors[v + 1] = Color.white; colors[v + 2] = new Color(1, .35f, .06f, 0);
                uv[v] = uv[v + 1] = uv[v + 2] = new Vector2(.5f, .5f);
                triangles[v] = v; triangles[v + 1] = v + 1; triangles[v + 2] = v + 2;
            }
            var mesh = new Mesh { name = "Reactor22SparksShared", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh CreateShardMesh(Vector3[] vertices)
        {
            var triangles = new int[ShardCount * 12];
            int[] tetra = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };
            for (int i = 0; i < ShardCount; i++) for (int t = 0; t < 12; t++) triangles[i * 12 + t] = i * 4 + tetra[t];
            var mesh = new Mesh { name = "PooledEightArmourFragments", vertices = vertices, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }
    }
}
