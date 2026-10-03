using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>Bounded presentation only. A displayed snapshot never applies damage.</summary>
    [DisallowMultipleComponent]
    public sealed class LaserBeamPool : MonoBehaviour
    {
        public Material material;
        public LaserWeaponSettings settings;
        public CombatScaleSettings scale;
        public AudioClip shotAudio;
        [Tooltip("Disable when the event audio director owns world laser playback.")]
        public bool worldAudioEnabled = true;
        public UnityEngine.Audio.AudioMixerGroup outputMixerGroup;
        public Camera viewCamera;
        public bool effectsEnabled = true;
        public int ActiveCount { get; private set; }
        public int PeakCount { get; private set; }
        public int DroppedVisualCount { get; private set; }
        public int Capacity => slots.Length;
        sealed class Slot
        {
            public GameObject root;
            public LineRenderer incident, reflected;
            public Transform contact;
            public MeshRenderer contactRenderer;
            public ParticleSystem sparks;
            public readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[5];
            public Vector3 incidentStart, incidentEnd, reflectedEnd, normal;
            public float remaining, duration;
            public int contactFrame, contactFrameAge;
            public bool bounced;
        }
        Slot[] slots = Array.Empty<Slot>();
        Material fallbackMaterial;
        Mesh contactMesh;
        AudioClip generatedAudio;
        AudioSource audioSource;
        float soundCooldown;
        MaterialPropertyBlock colorBlock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ContactShape = Shader.PropertyToID("_ContactShape");
        // World-space contact is an impact flash, not a marker that hangs in space
        // for the beam's whole afterimage. The mesh-local glint has its own lifetime.
        const float ContactSeconds = .025f;
        const int ContactFrames = 2;

        void Start() => InitializePool();
        public void InitializePool()
        {
            if (slots.Length > 0 || settings == null || scale == null) return;
            colorBlock = new MaterialPropertyBlock();
            if (material == null)
            {
                var shader = Shader.Find("DropletPrototype/NarrativeCombat/LaserPulse");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return;
                fallbackMaterial = new Material(shader) { name = "RuntimeLaserFallback" };
                fallbackMaterial.SetColor(BaseColor, Color.white); material = fallbackMaterial;
            }
            contactMesh = CreateContactMesh();
            slots = new Slot[Mathf.Clamp(settings.visualBeamBudget, 1, 32)];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new Slot { root = new GameObject("LaserSnapshot_" + i) };
                slot.root.layer = 2; slot.root.transform.SetParent(transform, false);
                slot.incident = CreateLine(slot.root.transform, "Incident"); slot.reflected = CreateLine(slot.root.transform, "Reflected");
                var contact = new GameObject("SurfaceContact"); contact.layer = 2; contact.transform.SetParent(slot.root.transform, false);
                contact.AddComponent<MeshFilter>().sharedMesh = contactMesh;
                slot.contactRenderer = contact.AddComponent<MeshRenderer>(); slot.contactRenderer.sharedMaterial = material;
                slot.contactRenderer.shadowCastingMode = ShadowCastingMode.Off; slot.contactRenderer.receiveShadows = false;
                slot.contact = contact.transform; slot.sparks = CreateSparks(slot.root.transform);
                slots[i] = slot; slot.root.SetActive(false);
            }
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = outputMixerGroup;
            audioSource.spatialBlend = 0; audioSource.volume = settings.audioVolume;
            if (shotAudio == null) { generatedAudio = GenerateShotAudio(); shotAudio = generatedAudio; }
        }
        LineRenderer CreateLine(Transform parent, string name)
        {
            var node = new GameObject(name); node.layer = 2; node.transform.SetParent(parent, false);
            var line = node.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
            line.positionCount = 2; line.widthMultiplier = scale.MetersToUnits(settings.beamWidthMeters);
            line.numCapVertices = 2; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.alignment = LineAlignment.View; line.textureMode = LineTextureMode.Stretch; return line;
        }

        ParticleSystem CreateSparks(Transform parent)
        {
            var node = new GameObject("ContactSparklets"); node.layer = 2; node.transform.SetParent(parent, false);
            var system = node.AddComponent<ParticleSystem>();
            var main = system.main; main.playOnAwake = false; main.loop = false;
            main.maxParticles = 5; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            SetColor(renderer, Color.white, true);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        public bool Show(Vector3 start, Vector3 contact, Vector3 reflectedEnd, bool bounced,
            Transform contactAnchor = null, Vector3 contactLocal = default, Vector3 contactNormal = default)
        {
            if (!effectsEnabled) return false;
            InitializePool();
            if (settings == null || scale == null) return false;
            Slot slot = null; foreach (var candidate in slots) if (candidate.remaining <= 0) { slot = candidate; break; }
            if (slot == null) { DroppedVisualCount++; return false; }
            slot.remaining = slot.duration = Mathf.Max(.001f, settings.beamSeconds); slot.bounced = bounced;
            slot.contactFrame = Time.frameCount; slot.contactFrameAge = 0;
            slot.incidentStart = start; slot.incidentEnd = contact; slot.reflectedEnd = reflectedEnd;
            slot.normal = contactNormal.sqrMagnitude > .00001f ? contactNormal.normalized : (start - contact).normalized;
            // The legacy anchor arguments remain source-compatible, but a short
            // flash is a frozen complete path. Moving one endpoint would invent
            // a new reflected ray without checking occlusion or the surface.
            slot.incident.startColor = slot.incident.endColor = Color.white;
            slot.reflected.startColor = slot.reflected.endColor = Color.white;
            slot.reflected.enabled = bounced;
            slot.contact.localScale = Vector3.one * scale.MetersToUnits(settings.contactRadiusMeters) * 2;
            slot.root.SetActive(true); Present(slot);
            ActiveCount++; PeakCount = Mathf.Max(PeakCount, ActiveCount);
            if (worldAudioEnabled && soundCooldown <= 0 && audioSource != null && !audioSource.isPlaying && shotAudio != null)
            {
                audioSource.volume = settings.audioVolume; audioSource.clip = shotAudio; audioSource.Play();
                soundCooldown = settings.audioCooldownSeconds;
            }
            return true;
        }
        void SetColor(Renderer renderer, Color color, bool contact = false)
        {
            colorBlock.Clear(); colorBlock.SetColor(BaseColor, color);
            colorBlock.SetFloat(ContactShape, contact ? 1 : 0); renderer.SetPropertyBlock(colorBlock);
        }
        public void Step(float simulationDelta)
        {
            if (simulationDelta <= 0) return;
            soundCooldown = Mathf.Max(0, soundCooldown - simulationDelta);
            foreach (var slot in slots)
            {
                if (slot.remaining <= 0) continue;
                slot.remaining = Mathf.Max(0, slot.remaining - simulationDelta);
                if (slot.remaining <= 0 || !effectsEnabled) { slot.remaining = 0; slot.root.SetActive(false); ActiveCount--; }
            }
        }
        void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            foreach (var slot in slots) if (slot.remaining > 0) Present(slot);
            if (audioSource != null)
            {
                if (Time.timeScale <= 0) audioSource.Pause(); else audioSource.UnPause();
            }
        }
        void Present(Slot slot)
        {
            if (slot.contactFrame != Time.frameCount)
            {
                // A paused image keeps its exact transient state. Repeated
                // presentation calls in one frame never spend another frame.
                if (Time.timeScale > 0) slot.contactFrameAge++;
                slot.contactFrame = Time.frameCount;
            }
            float life = Mathf.Clamp01(slot.remaining / slot.duration);
            float alpha = Mathf.SmoothStep(0, 1, life / .75f);
            Color incident = settings.incidentColor, reflected = settings.reflectedColor;
            incident.a = reflected.a = alpha;
            SetColor(slot.incident, incident); SetColor(slot.reflected, reflected);
            float contactLife = Mathf.Clamp01(1 - (slot.duration - slot.remaining) / ContactSeconds);
            bool showContact = slot.contactFrameAge < ContactFrames && contactLife > 0;
            Color contactColor = slot.bounced ? reflected : incident;
            contactColor.a = contactLife * contactLife;
            SetColor(slot.contactRenderer, contactColor, true);
            slot.contactRenderer.enabled = showContact;
            slot.incident.SetPosition(0, slot.incidentStart); slot.incident.SetPosition(1, slot.incidentEnd);
            slot.reflected.SetPosition(0, slot.incidentEnd); slot.reflected.SetPosition(1, slot.reflectedEnd);
            slot.incident.widthMultiplier = slot.reflected.widthMultiplier = 1;
            slot.incident.startWidth = BeamWidth(slot.incidentStart); slot.incident.endWidth = BeamWidth(slot.incidentEnd);
            slot.reflected.startWidth = BeamWidth(slot.incidentEnd); slot.reflected.endWidth = BeamWidth(slot.reflectedEnd);
            slot.contact.position = slot.incidentEnd;
            if (viewCamera != null) slot.contact.rotation = viewCamera.transform.rotation;
            if (showContact) PresentSparks(slot, contactLife);
            else if (slot.sparks.particleCount > 0) slot.sparks.Clear(false);
        }
        float BeamWidth(Vector3 position)
        {
            float physical = scale.MetersToUnits(settings.beamWidthMeters);
            if (viewCamera == null || settings.minimumBeamPixels <= 0) return physical;
            float depth = Mathf.Max(viewCamera.nearClipPlane, Vector3.Dot(position - viewCamera.transform.position, viewCamera.transform.forward));
            float unitsPerPixel = viewCamera.orthographic ? viewCamera.orthographicSize * 2 / Mathf.Max(1, viewCamera.pixelHeight)
                : 2 * depth * Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Max(1, viewCamera.pixelHeight);
            return Mathf.Clamp(unitsPerPixel * settings.minimumBeamPixels, physical,
                Mathf.Max(physical, scale.MetersToUnits(settings.maximumBeamWidthMeters)));
        }
        void PresentSparks(Slot slot, float life)
        {
            int count = slot.bounced ? Mathf.Clamp(settings.contactParticleCount, 0, 5) : 0;
            Vector3 tangent = Vector3.Cross(slot.normal, Mathf.Abs(slot.normal.y) > .9f ? Vector3.right : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(slot.normal, tangent);
            float radius = scale.MetersToUnits(settings.contactRadiusMeters);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f;
                Vector3 direction = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle) + slot.normal * .6f).normalized;
                slot.particles[i].position = slot.incidentEnd + direction * radius * (1 - life) * 3;
                slot.particles[i].startSize = radius * .34f * life;
                Color color = settings.reflectedColor; color.a = life * life;
                slot.particles[i].startColor = color;
                slot.particles[i].startLifetime = slot.particles[i].remainingLifetime = 10;
            }
            slot.sparks.SetParticles(slot.particles, count);
            slot.sparks.Pause(false);
        }
        public void ResetEffects()
        {
            foreach (var slot in slots)
            { slot.remaining = 0; slot.sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); slot.root.SetActive(false); }
            ActiveCount = PeakCount = DroppedVisualCount = 0; soundCooldown = 0;
            if (audioSource != null) audioSource.Stop();
        }
        void OnDisable() => ResetEffects();
        void OnDestroy()
        {
            if (fallbackMaterial != null) Destroy(fallbackMaterial);
            if (contactMesh != null) Destroy(contactMesh);
            if (generatedAudio != null) Destroy(generatedAudio);
        }
        static Mesh CreateContactMesh()
        {
            var vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            var mesh = new Mesh { name = "PooledLaserContact" };
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static AudioClip GenerateShotAudio()
        {
            const int rate = 22050; int samples = Mathf.RoundToInt(rate * .085f); var data = new float[samples];
            uint noise = 0x6325u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate; noise = noise * 1664525u + 1013904223u;
                data[i] = Mathf.Exp(-t * 60) * (.28f * Mathf.Sin(2 * Mathf.PI * (1900 * t - 4400 * t * t)) + .045f * ((noise & 65535) / 32768f - 1));
            }
            var clip = AudioClip.Create("OriginalLaserPulse", samples, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}
