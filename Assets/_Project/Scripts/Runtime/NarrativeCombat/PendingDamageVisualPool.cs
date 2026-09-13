using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype
{
    /// <summary>Bounded view of pending hulls; never owns a damage deadline or score.</summary>
    [DisallowMultipleComponent]
    public sealed class PendingDamageVisualPool : MonoBehaviour
    {
        public FleetCombatSimulation simulation;
        public ReactorExplosionPool reactorPool;
        public Transform focus;
        public Camera viewCamera;
        public Material flashMaterial, fireMaterial, debrisMaterial;
        [Range(1, 12)] public int budget = 12;
        public int ActiveEffectCount { get; private set; }
        public int DroppedEffectCount { get; private set; }
        public int PoolInstanceCount { get; private set; }
        sealed class View { public Transform transform; public MeshRenderer renderer; }
        sealed class Slot
        {
            public ShipTarget ship;
            public Transform root;
            public View flash, wound, reactor;
            public View[] shards;
            public Vector3 localPoint, localDirection;
            public Transform reactorMarker;
            public float born, deadline;
        }
        Slot[] slots;
        FleetCombatSimulation bound;
        Mesh quad, shard;
        MaterialPropertyBlock properties;
        static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColor = Shader.PropertyToID("_BaseColor");

        void OnEnable() => Bind();
        void Start() { Bind(); InitializePool(); }
        void Bind()
        {
            if (bound == simulation) return;
            if (bound != null) { bound.EventRaised -= OnEvent; bound.SimulationReset -= ResetEffects; }
            bound = simulation;
            if (bound != null) { bound.EventRaised += OnEvent; bound.SimulationReset += ResetEffects; }
        }
        public void InitializePool()
        {
            if (slots != null) return;
            if (reactorPool != null)
            {
                if (flashMaterial == null) flashMaterial = reactorPool.flashMaterial;
                if (fireMaterial == null) fireMaterial = reactorPool.fireMaterial;
                if (debrisMaterial == null) debrisMaterial = reactorPool.debrisMaterial;
            }
            properties = new MaterialPropertyBlock();
            quad = new Mesh { name = "PendingDamageQuad" };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 }; quad.RecalculateNormals(); quad.RecalculateBounds();
            shard = new Mesh { name = "PendingArmourFragment" };
            shard.vertices = new[] { new Vector3(-.5f, 0, -.2f), new Vector3(.5f, 0, -.2f), new Vector3(0, .18f, .3f), new Vector3(0, -.13f, .35f) };
            shard.triangles = new[] { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 }; shard.RecalculateNormals(); shard.RecalculateBounds();
            slots = new Slot[12];
            for (int i = 0; i < slots.Length; i++)
            {
                var root = new GameObject("PendingHullDamage_" + i.ToString("00")).transform;
                root.SetParent(transform, false); root.gameObject.layer = 2;
                var slot = new Slot { root = root, shards = new View[3] };
                slot.flash = MakeView("LocalPenetrationFlash", root, quad, flashMaterial);
                slot.wound = MakeView("ExposedHullEmber", root, quad, fireMaterial);
                slot.reactor = MakeView("ReactorInstability", root, quad, fireMaterial);
                for (int s = 0; s < slot.shards.Length; s++) slot.shards[s] = MakeView("ArmourShard_" + s, root, shard, debrisMaterial);
                root.gameObject.SetActive(false); slots[i] = slot;
            }
            PoolInstanceCount = 12 * 7;
        }
        View MakeView(string label, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(label); go.layer = 2; go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.enabled = material != null;
            return new View { transform = go.transform, renderer = renderer };
        }
        void OnEvent(CombatEvent value)
        {
            if (value.kind != CombatEventKind.HullPenetrated || value.subject == null) return;
            if (reactorPool != null && reactorPool.Quality == EffectQuality.Off) return;
            InitializePool();
            int limit = Mathf.Min(budget, reactorPool != null && reactorPool.Quality == EffectQuality.Low ? 6 : 12);
            Slot selected = null; float farthest = -1;
            Vector3 observer = focus != null ? focus.position : viewCamera != null ? viewCamera.transform.position : transform.position;
            float incoming = (value.position - observer).sqrMagnitude;
            for (int i = 0; i < limit; i++)
            {
                var candidate = slots[i];
                if (candidate.ship == null) { selected = candidate; break; }
                float distance = (candidate.ship.transform.position - observer).sqrMagnitude;
                if (distance > farthest) { farthest = distance; selected = candidate; }
            }
            if (selected == null || (selected.ship != null && incoming > farthest)) { DroppedEffectCount++; return; }
            var ship = value.subject;
            selected.ship = ship; selected.born = simulation != null ? simulation.SimulatedTime : 0;
            selected.deadline = ship.ExplosionAt;
            selected.localPoint = ship.transform.InverseTransformPoint(value.position);
            selected.localDirection = ship.transform.InverseTransformDirection(ship.LastHit.direction);
            var presenter = ship.GetComponent<ReactorDestructionPresenter>();
            selected.reactorMarker = presenter != null ? presenter.reactorMarker : null;
            selected.root.gameObject.SetActive(true);
            Animate(selected);
        }
        void Update()
        {
            Bind(); if (slots == null || simulation == null) return;
            ActiveEffectCount = 0;
            foreach (var slot in slots)
            {
                if (slot.ship == null) continue;
                if (slot.ship.DamageState != ShipDamageState.FatalPending || (reactorPool != null && reactorPool.Quality == EffectQuality.Off))
                { slot.ship = null; slot.root.gameObject.SetActive(false); continue; }
                Animate(slot); ActiveEffectCount++;
            }
        }
        void Animate(Slot slot)
        {
            float now = simulation != null ? simulation.SimulatedTime : 0;
            float age = Mathf.Max(0, now - slot.born), progress = Mathf.Clamp01(age / Mathf.Max(.01f, slot.deadline - slot.born));
            Vector3 point = slot.ship.transform.TransformPoint(slot.localPoint);
            Vector3 direction = slot.ship.transform.TransformDirection(slot.localDirection).normalized;
            if (direction.sqrMagnitude < .001f) direction = slot.ship.transform.forward;
            Quaternion billboard = viewCamera != null ? viewCamera.transform.rotation : Quaternion.LookRotation(-direction);
            float flash = Mathf.Clamp01(1 - age / .16f);
            slot.flash.transform.SetPositionAndRotation(point, billboard);
            slot.flash.transform.localScale = new Vector3(5, 1.5f, 1) * (1 + age * 2);
            Tint(slot.flash, new Color(3.5f, 5, 7, flash));
            slot.wound.transform.SetPositionAndRotation(point + direction * .1f, billboard);
            slot.wound.transform.localScale = new Vector3(2.8f, .8f, 1);
            Tint(slot.wound, new Color(2.1f, .3f, .035f, .75f));
            slot.reactor.transform.SetPositionAndRotation(slot.reactorMarker != null ? slot.reactorMarker.position : slot.ship.transform.position, billboard);
            slot.reactor.transform.localScale = Vector3.one * (2 + progress * 3);
            Tint(slot.reactor, new Color(.3f, 1.6f + progress, 3, progress * (.35f + .2f * Mathf.Sin(now * 33))));
            for (int i = 0; i < slot.shards.Length; i++)
            {
                Vector3 spread = Quaternion.AngleAxis(i * 120, direction) * Vector3.Cross(direction, Mathf.Abs(direction.y) > .8f ? Vector3.right : Vector3.up);
                slot.shards[i].transform.SetPositionAndRotation(point + (direction * 4 + spread * 3) * age,
                    Quaternion.AngleAxis(age * (60 + i * 40), spread));
                slot.shards[i].transform.localScale = Vector3.one * Mathf.Clamp01(1 - age / 1.2f) * .5f;
            }
        }
        void Tint(View view, Color color)
        {
            if (view.renderer.sharedMaterial == null) return;
            view.renderer.enabled = color.a > .001f;
            properties.Clear(); properties.SetColor(ColorId, color); properties.SetColor(BaseColor, color);
            view.renderer.SetPropertyBlock(properties);
        }
        public void ResetEffects()
        {
            if (slots != null) foreach (var slot in slots) { slot.ship = null; slot.root.gameObject.SetActive(false); }
            ActiveEffectCount = DroppedEffectCount = 0;
        }
        void OnDisable()
        {
            if (bound != null) { bound.EventRaised -= OnEvent; bound.SimulationReset -= ResetEffects; }
            bound = null; ResetEffects();
        }
        void OnDestroy() { if (quad != null) Destroy(quad); if (shard != null) Destroy(shard); }
    }
}
