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
            public Transform contactAnchor;
            public Vector3 contactLocal, incidentStart, incidentEnd, reflectedEnd;
            public float remaining;
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

        void Start() => InitializePool();
        public void InitializePool()
        {
            if (slots.Length > 0 || settings == null || scale == null) return;
            colorBlock = new MaterialPropertyBlock();
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return;
                fallbackMaterial = new Material(shader) { name = "RuntimeLaserFallback" };
                fallbackMaterial.SetColor(BaseColor, Color.white * 3); material = fallbackMaterial;
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
                slot.contact = contact.transform; slots[i] = slot; slot.root.SetActive(false);
            }
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0; audioSource.volume = settings.audioVolume;
            if (shotAudio == null) { generatedAudio = GenerateShotAudio(); shotAudio = generatedAudio; }
        }
        LineRenderer CreateLine(Transform parent, string name)
        {
            var node = new GameObject(name); node.layer = 2; node.transform.SetParent(parent, false);
            var line = node.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
            line.positionCount = 2; line.widthMultiplier = scale.MetersToUnits(settings.beamWidthMeters);
            line.numCapVertices = 2; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.alignment = LineAlignment.View; return line;
        }

        public bool Show(Vector3 start, Vector3 contact, Vector3 reflectedEnd, bool bounced,
            Transform contactAnchor = null, Vector3 contactLocal = default)
        {
            if (!effectsEnabled) return false;
            InitializePool();
            Slot slot = null; foreach (var candidate in slots) if (candidate.remaining <= 0) { slot = candidate; break; }
            if (slot == null) { DroppedVisualCount++; return false; }
            slot.remaining = settings.beamSeconds; slot.bounced = bounced;
            slot.incidentStart = start; slot.incidentEnd = contact; slot.reflectedEnd = reflectedEnd;
            slot.contactAnchor = bounced ? contactAnchor : null; slot.contactLocal = contactLocal;
            slot.incident.startColor = slot.incident.endColor = settings.incidentColor;
            slot.reflected.startColor = slot.reflected.endColor = settings.reflectedColor;
            SetColor(slot.incident, settings.incidentColor); SetColor(slot.reflected, settings.reflectedColor);
            SetColor(slot.contactRenderer, (bounced ? settings.reflectedColor : settings.incidentColor) * 2);
            slot.reflected.enabled = bounced;
            slot.contact.localScale = Vector3.one * scale.MetersToUnits(settings.contactRadiusMeters) * 2;
            slot.root.SetActive(true); Present(slot);
            ActiveCount++; PeakCount = Mathf.Max(PeakCount, ActiveCount);
            if (soundCooldown <= 0 && audioSource != null && !audioSource.isPlaying && shotAudio != null)
            {
                audioSource.volume = settings.audioVolume; audioSource.clip = shotAudio; audioSource.Play();
                soundCooldown = settings.audioCooldownSeconds;
            }
            return true;
        }
        void SetColor(Renderer renderer, Color color)
        { colorBlock.Clear(); colorBlock.SetColor(BaseColor, color); renderer.SetPropertyBlock(colorBlock); }
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
            foreach (var slot in slots) if (slot.remaining > 0) Present(slot);
            if (audioSource != null)
            {
                if (Time.timeScale <= 0) audioSource.Pause(); else audioSource.UnPause();
            }
        }
        void Present(Slot slot)
        {
            Vector3 point = slot.contactAnchor != null ? slot.contactAnchor.TransformPoint(slot.contactLocal) : slot.incidentEnd;
            slot.incident.SetPosition(0, slot.incidentStart); slot.incident.SetPosition(1, point);
            slot.reflected.SetPosition(0, point); slot.reflected.SetPosition(1, slot.reflectedEnd + (point - slot.incidentEnd));
            slot.contact.position = point;
        }
        public void ResetEffects()
        {
            foreach (var slot in slots) { slot.remaining = 0; slot.contactAnchor = null; slot.root.SetActive(false); }
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
            var vertices = new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down };
            var mesh = new Mesh { name = "PooledLaserContact" };
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0,2,1, 0,3,2, 0,4,3, 0,1,4, 5,1,2, 5,2,3, 5,3,4, 5,4,1 };
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
