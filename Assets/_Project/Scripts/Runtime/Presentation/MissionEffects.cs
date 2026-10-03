using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>Fixed-budget optional impact presentation. No physics or score authority.</summary>
    public sealed class MissionEffects : MonoBehaviour
    {
        public EffectSettings settings;
        public MissionController mission;
        public DropletMotor motor;
        public ChaseCamera chaseCamera;
        public Material flashMaterial;
        public Material sparkMaterial;
        public GameObject[] wreckPrefabs;
        public AudioClip[] impactClips;
        [Tooltip("Disable when the event audio director owns world impacts; flight sound is independent.")]
        public bool worldAudioEnabled = true;
        public AudioClip flightClip;
        public UnityEngine.Audio.AudioMixerGroup outputMixerGroup;

        public int ActiveEffectCount { get; private set; }
        public int ActiveAudioCount { get; private set; }
        public int PoolInstanceCount { get; private set; }
        public int DroppedEffectCount { get; private set; }
        public float ElapsedSimulationTime { get; private set; }
        public int Capacity => settings == null || Quality == EffectQuality.Off ? 0 : Mathf.Min(slots.Length,
            Mathf.Clamp(Quality == EffectQuality.High ? settings.highEffectCapacity : settings.lowEffectCapacity, 0, 32));
        // Audio remains available when optional visual effects are disabled.
        public int AudioCapacity => settings == null ? 0 : Mathf.Min(sounds.Length,
            Mathf.Clamp(Quality == EffectQuality.Low ? settings.lowAudioCapacity : settings.highAudioCapacity, 0, 12));
        public EffectQuality Quality
        {
            get => qualityAssigned ? quality : settings != null ? settings.initialQuality : EffectQuality.High;
            set
            {
                if (Quality == value && qualityAssigned) return;
                qualityAssigned = true; quality = value;
                ResetEffects();
            }
        }

        sealed class Fragment
        {
            public Transform transform;
            public Vector3 position, scale, velocity, axis, center;
            public Quaternion rotation;
        }
        sealed class Wreck
        {
            public GameObject root;
            public Fragment[] fragments;
        }
        sealed class Slot
        {
            public GameObject root, flash, spark;
            public MeshRenderer flashRenderer, sparkRenderer;
            public Wreck[] variants;
            public Wreck wreck;
            public float age;
            public bool active;
        }
        sealed class Sound
        {
            public AudioSource source;
            public float remaining;
            public bool active, flight;
        }
        Slot[] slots = Array.Empty<Slot>();
        Sound[] sounds = Array.Empty<Sound>();
        GameObject poolRoot;
        Mesh flashMesh, sparkMesh;
        MaterialPropertyBlock colors;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MissionController boundMission;
        EffectQuality quality;
        bool qualityAssigned, initialized;

        void OnEnable() { Bind(); if (settings != null) InitializePool(); }
        void Start() { InitializePool(); }
        void Bind()
        {
            if (boundMission == mission) return;
            if (boundMission != null)
            {
                boundMission.Restarted -= ResetEffects;
                boundMission.StateChanged -= OnStateChanged;
            }
            boundMission = mission;
            if (boundMission != null)
            {
                boundMission.Restarted += ResetEffects;
                boundMission.StateChanged += OnStateChanged;
            }
        }
        public void InitializePool()
        {
            Bind();
            if (initialized || settings == null) return;
            colors = new MaterialPropertyBlock();
            initialized = true;
            poolRoot = new GameObject("__MissionEffectPool");
            poolRoot.layer = 2;
            poolRoot.transform.SetParent(transform, false);
            flashMesh = CreateFlashMesh(); sparkMesh = CreateSparkMesh();
            int count = Mathf.Clamp(Mathf.Max(settings.highEffectCapacity, settings.lowEffectCapacity), 0, 32);
            slots = new Slot[count];
            for (int i = 0; i < count; i++)
            {
                var slot = new Slot { root = new GameObject("Impact_" + i.ToString("00")) };
                slot.root.layer = 2; slot.root.transform.SetParent(poolRoot.transform, false);
                slot.flash = CreateMeshObject("Flash", slot.root.transform, flashMesh, flashMaterial, out slot.flashRenderer);
                slot.spark = CreateMeshObject("DirectionalSparks", slot.root.transform, sparkMesh, sparkMaterial, out slot.sparkRenderer);
                int variants = wreckPrefabs != null ? wreckPrefabs.Length : 0;
                slot.variants = new Wreck[variants];
                for (int k = 0; k < variants; k++)
                {
                    GameObject prefab = wreckPrefabs[k];
                    if (prefab == null) continue;
                    // Reject unsafe presentation assets before instantiation. The pool
                    // therefore never contributes collision volumes or target identity.
                    if (prefab.GetComponentInChildren<Collider>(true) != null ||
                        prefab.GetComponentInChildren<Rigidbody>(true) != null ||
                        prefab.GetComponentInChildren<ShipTarget>(true) != null)
                    {
                        if (i == 0) Debug.LogWarning("Wreck prefab must contain only presentation geometry: " + prefab.name, this);
                        continue;
                    }
                    GameObject clone = Instantiate(prefab, slot.root.transform, false);
                    clone.name = "Wreck_" + k;
                    foreach (var node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
                    var renderers = clone.GetComponentsInChildren<MeshRenderer>(true);
                    var wreck = new Wreck { root = clone, fragments = new Fragment[renderers.Length] };
                    for (int f = 0; f < renderers.Length; f++)
                    {
                        Transform part = renderers[f].transform;
                        renderers[f].shadowCastingMode = ShadowCastingMode.Off;
                        renderers[f].receiveShadows = false;
                        var filter = part.GetComponent<MeshFilter>();
                        wreck.fragments[f] = new Fragment { transform = part, position = part.localPosition,
                            rotation = part.localRotation, scale = part.localScale,
                            center = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero };
                    }
                    clone.SetActive(false); slot.variants[k] = wreck;
                }
                slot.root.SetActive(false); slots[i] = slot;
            }
            count = Mathf.Clamp(Mathf.Max(settings.highAudioCapacity, settings.lowAudioCapacity), 0, 12);
            sounds = new Sound[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Sound_" + i.ToString("00")); go.layer = 2;
                go.transform.SetParent(poolRoot.transform, false);
                var source = go.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = outputMixerGroup;
                source.playOnAwake = false; source.spatialBlend = .65f;
                source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 15; source.maxDistance = 200;
                source.dopplerLevel = 0; source.priority = 160;
                sounds[i] = new Sound { source = source };
            }
            PoolInstanceCount = poolRoot.GetComponentsInChildren<Transform>(true).Length;
        }

        public bool TryPlay(ShipTarget target, int wreckKind, Vector3 scale)
        {
            if (!isActiveAndEnabled || target == null || settings == null || Quality == EffectQuality.Off ||
                (mission != null && mission.State != MissionState.Playing)) return false;
            InitializePool();
            ShipHitContext hit = target.LastHit;
            Vector3 direction = hit.direction.sqrMagnitude > .00001f ? hit.direction.normalized : target.transform.forward;
            bool played = false;
            int capacity = Capacity;
            for (int i = 0; i < capacity; i++)
            {
                Slot slot = slots[i]; if (slot.active) continue;
                slot.active = true; slot.age = 0; ActiveEffectCount++;
                slot.root.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
                slot.root.transform.localScale = scale;
                slot.wreck = wreckKind >= 0 && wreckKind < slot.variants.Length ? slot.variants[wreckKind] : null;
                if (slot.wreck != null)
                {
                    slot.wreck.root.SetActive(true);
                    for (int f = 0; f < slot.wreck.fragments.Length; f++)
                    {
                        Fragment part = slot.wreck.fragments[f];
                        part.transform.localPosition = part.position; part.transform.localRotation = part.rotation;
                        part.transform.localScale = part.scale;
                        Vector3 localDirection = part.transform.parent.InverseTransformDirection(direction);
                        Vector3 fromHull = part.transform.parent.InverseTransformDirection(part.transform.TransformPoint(part.center) - target.transform.position);
                        Vector3 outward = fromHull.sqrMagnitude > .01f ? fromHull.normalized :
                            new Vector3(Mathf.Sin(f * 2.39f), .25f, Mathf.Cos(f * 2.39f)).normalized;
                        part.velocity = outward * settings.outwardSpeed + localDirection * settings.directionalSpeed;
                        part.axis = new Vector3(Mathf.Sin(f * 1.7f + 1), .4f, Mathf.Cos(f * 1.2f)).normalized;
                    }
                }
                Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .99f ? Vector3.right : Vector3.up;
                slot.flash.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(direction, up));
                slot.flash.transform.localScale = Vector3.one * settings.flashSize;
                slot.spark.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(direction, up));
                slot.spark.transform.localScale = new Vector3(1, 1, 7);
                slot.flash.SetActive(flashMaterial != null);
                slot.spark.SetActive(sparkMaterial != null && Quality == EffectQuality.High);
                ApplyColor(slot.flashRenderer, new Color(1.6f, .36f, .055f, .95f));
                ApplyColor(slot.sparkRenderer, new Color(1.8f, .65f, .16f, .85f));
                slot.root.SetActive(true); played = true; break;
            }
            if (!played) DroppedEffectCount++;
            if (PlayImpact(hit.point, wreckKind)) played = true;
            if (played && settings.cameraFeedback && chaseCamera != null)
                chaseCamera.AddImpulse(direction, settings.cameraImpulse);
            return played;
        }

        bool PlayImpact(Vector3 point, int kind)
        {
            if (!worldAudioEnabled) return false;
            if (impactClips == null || impactClips.Length == 0) return false;
            AudioClip clip = impactClips[Mathf.Clamp(kind == 0 ? 0 : 1, 0, impactClips.Length - 1)];
            if (clip == null) return false;
            for (int i = 0; i < AudioCapacity; i++)
            {
                Sound sound = sounds[i];
                // An impact can reclaim the quiet flight bed; it cannot steal
                // another impact or exceed the total source concurrency limit.
                if (sound.active && !sound.flight) continue;
                if (!sound.active) ActiveAudioCount++;
                sound.source.Stop(); sound.flight = false; sound.active = true;
                sound.source.transform.position = point;
                sound.source.clip = clip; sound.source.loop = false; sound.source.spatialBlend = .65f;
                sound.source.pitch = 1; sound.source.volume = settings.masterVolume * settings.impactVolume;
                sound.remaining = clip.length; sound.source.Play(); return true;
            }
            return false;
        }
        void Update()
        {
            if (!initialized) InitializePool();
            if (!initialized || settings == null ||
                Time.deltaTime <= 0 || (mission != null && mission.State == MissionState.Paused)) return;
            float dt = Time.deltaTime; ElapsedSimulationTime += dt;
            if (Quality != EffectQuality.Off) foreach (Slot slot in slots)
            {
                if (!slot.active) continue;
                slot.age += dt;
                if (slot.age >= settings.wreckLifetime) { Release(slot); continue; }
                float flash = 1 - slot.age / Mathf.Max(.01f, settings.flashLifetime);
                float fire = Mathf.Clamp01(1 - slot.age / 1.1f);
                slot.flash.SetActive(fire > 0 && flashMaterial != null);
                if (flash > 0)
                {
                    slot.flash.transform.localScale = Vector3.one * settings.flashSize * (1.3f - flash * .3f);
                    ApplyColor(slot.flashRenderer, new Color(1.6f, .36f, .055f, flash * .95f));
                }
                else if (fire > 0)
                {
                    float flicker = .82f + Mathf.Sin(slot.age * 73f) * .12f;
                    slot.flash.transform.localScale = Vector3.one * settings.flashSize * .24f * fire * flicker;
                    ApplyColor(slot.flashRenderer, new Color(1.1f, .27f, .035f, fire * .5f));
                }
                float spark = 1 - slot.age / Mathf.Max(.01f, settings.sparkLifetime);
                slot.spark.SetActive(spark > 0 && sparkMaterial != null && Quality == EffectQuality.High);
                if (spark > 0) ApplyColor(slot.sparkRenderer, new Color(1.8f, .65f, .16f, spark * .85f));
                if (slot.wreck == null) continue;
                float shrink = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(settings.wreckLifetime * .65f, settings.wreckLifetime, slot.age));
                foreach (Fragment part in slot.wreck.fragments)
                {
                    part.transform.localPosition = part.position + part.velocity * slot.age;
                    part.transform.localRotation = Quaternion.AngleAxis(slot.age * settings.tumbleDegreesPerSecond, part.axis) * part.rotation;
                    part.transform.localScale = part.scale * shrink;
                }
            }
            foreach (Sound sound in sounds)
            {
                if (!sound.active || sound.flight) continue;
                sound.remaining -= dt;
                sound.source.volume = settings.masterVolume * settings.impactVolume;
                if (sound.remaining <= 0) Stop(sound);
            }
            UpdateFlightSound();
        }
        void UpdateFlightSound()
        {
            bool allowed = flightClip != null && motor != null && motor.settings != null && settings.flightVolume > 0 &&
                mission != null && mission.State == MissionState.Playing && motor.Speed > 4 && AudioCapacity > 0;
            Sound flight = null;
            foreach (Sound sound in sounds)
                if (sound.active && sound.flight) { flight = sound; break; }
            if (!allowed) { if (flight != null) Stop(flight); return; }
            if (flight == null)
            {
                for (int i = AudioCapacity - 1; i >= 0; i--) if (!sounds[i].active) { flight = sounds[i]; break; }
                if (flight == null) return;
                flight.active = true; flight.flight = true; ActiveAudioCount++;
                flight.source.clip = flightClip; flight.source.loop = true; flight.source.spatialBlend = 0;
                flight.source.Play();
            }
            flight.source.transform.position = motor.PresentedPosition;
            float speedRatio = Mathf.Clamp01(motor.Speed /
                Mathf.Max(0.01f, motor.settings.maxCruiseSpeed * motor.settings.boostMultiplier));
            flight.source.volume = settings.masterVolume * settings.flightVolume * (.15f + .85f * speedRatio);
            flight.source.pitch = .72f + speedRatio * .60f;
        }
        void OnStateChanged(MissionState state)
        {
            foreach (Sound sound in sounds)
            {
                if (!sound.active) continue;
                if (state == MissionState.Paused) sound.source.Pause();
                else if (state == MissionState.Playing) sound.source.UnPause();
                else if (sound.flight) Stop(sound);
            }
        }
        void Stop(Sound sound)
        {
            sound.source.Stop(); sound.source.clip = null; sound.active = false; sound.flight = false;
            sound.remaining = 0; ActiveAudioCount = Mathf.Max(0, ActiveAudioCount - 1);
        }
        void Release(Slot slot)
        {
            slot.active = false; slot.age = 0; slot.root.SetActive(false);
            if (slot.wreck != null) slot.wreck.root.SetActive(false);
            slot.wreck = null; ActiveEffectCount = Mathf.Max(0, ActiveEffectCount - 1);
        }
        public void ResetEffects()
        {
            foreach (Slot slot in slots) if (slot.active) Release(slot);
            foreach (Sound sound in sounds) if (sound.active) Stop(sound);
            ActiveEffectCount = 0; ActiveAudioCount = 0; DroppedEffectCount = 0; ElapsedSimulationTime = 0;
        }
        void OnDisable()
        {
            if (boundMission != null)
            {
                boundMission.Restarted -= ResetEffects;
                boundMission.StateChanged -= OnStateChanged;
            }
            boundMission = null; ResetEffects();
        }
        void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot);
            if (flashMesh != null) Destroy(flashMesh);
            if (sparkMesh != null) Destroy(sparkMesh);
        }
        void ApplyColor(MeshRenderer renderer, Color color)
        {
            colors.Clear(); colors.SetColor(BaseColor, color); colors.SetColor(ColorId, color);
            renderer.SetPropertyBlock(colors);
        }
        static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name); go.layer = 2; go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go;
        }
        static Mesh CreateFlashMesh()
        {
            // Three crossed soft quads remain visible from any approach; no collider.
            var mesh = new Mesh { name = "PooledImpactFlash" };
            var vertices = new Vector3[12]; var uv = new Vector2[12]; var tint = new Color[12]; var indices = new int[18];
            for (int p = 0; p < 3; p++)
            {
                Vector3 right = p == 2 ? Vector3.forward : Vector3.right, up = p == 1 ? Vector3.forward : Vector3.up;
                int b = p * 4; vertices[b] = -right-up; vertices[b+1] = right-up; vertices[b+2] = right+up; vertices[b+3] = -right+up;
                uv[b]=Vector2.zero;uv[b+1]=Vector2.right;uv[b+2]=Vector2.one;uv[b+3]=Vector2.up;
                for(int j=0;j<4;j++)tint[b+j]=Color.white;
                int k=p*6;indices[k]=b;indices[k+1]=b+1;indices[k+2]=b+2;indices[k+3]=b;indices[k+4]=b+2;indices[k+5]=b+3;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=tint;mesh.triangles=indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh CreateSparkMesh()
        {
            var vertices = new Vector3[18]; var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                Vector3 side = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[i * 3] = side * -.035f;
                vertices[i * 3 + 1] = side * .035f;
                vertices[i * 3 + 2] = side * (1 + i % 2 * .4f) + Vector3.forward * (i % 2 == 0 ? 1 : -.6f);
                triangles[i * 3] = i * 3; triangles[i * 3 + 1] = i * 3 + 1; triangles[i * 3 + 2] = i * 3 + 2;
            }
            var mesh = new Mesh { name = "PooledDirectionalSparks" }; mesh.vertices = vertices; mesh.triangles = triangles;
            var tint = new Color[vertices.Length]; for (int i = 0; i < tint.Length; i++) tint[i] = Color.white; mesh.colors = tint;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
