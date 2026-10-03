using System;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>A bounded, mesh-local optical afterglow. Never owns damage, lights,
    /// movement, or the world-space pulse path. Coexists with reactor reflection MPBs.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(410)]
    public sealed class LaserContactResponse : MonoBehaviour
    {
        public Renderer targetRenderer;
        public Transform presentedMesh;
        public bool effectsEnabled = true;
        public int ActiveCount { get; private set; }
        const int Capacity = 4;
        struct Contact
        {
            public Vector3 localPoint, localIncoming;
            public Color color;
            public float radius, remaining, duration;
        }
        readonly Contact[] contacts = new Contact[Capacity];
        readonly Vector4[] positions = new Vector4[Capacity];
        readonly Vector4[] colors = new Vector4[Capacity];
        readonly Vector4[] incoming = new Vector4[Capacity];
        static readonly int PositionsId = Shader.PropertyToID("_LaserContactPositions");
        static readonly int ColorsId = Shader.PropertyToID("_LaserContactColors");
        static readonly int IncomingId = Shader.PropertyToID("_LaserContactIncoming");
        static readonly int CountId = Shader.PropertyToID("_LaserContactCount");
        MaterialPropertyBlock properties;

        public void RegisterHit(DropletSurfaceHit hit, Vector3 worldIncoming, Matrix4x4 surfaceMatrix,
            Color color, float radius, float seconds)
        {
            if (!effectsEnabled || targetRenderer == null || presentedMesh == null) return;
            int slot = 0;
            for (int i = 1; i < Capacity; i++)
                if (contacts[i].remaining < contacts[slot].remaining) slot = i;
            contacts[slot] = new Contact
            {
                localPoint = hit.localPoint,
                localIncoming = surfaceMatrix.inverse.MultiplyVector(worldIncoming).normalized,
                color = color, radius = Mathf.Max(.001f, radius),
                remaining = Mathf.Max(.001f, seconds), duration = Mathf.Max(.001f, seconds)
            };
            RefreshResponse();
        }

        public void Step(float simulationDelta)
        {
            if (simulationDelta <= 0) return;
            for (int i = 0; i < Capacity; i++)
                contacts[i].remaining = Mathf.Max(0, contacts[i].remaining - simulationDelta);
            RefreshResponse();
        }

        void LateUpdate() => RefreshResponse();
        public void RefreshResponse()
        {
            ActiveCount = 0;
            if (effectsEnabled && presentedMesh != null)
                for (int i = 0; i < Capacity; i++)
                {
                    var contact = contacts[i];
                    if (contact.remaining <= 0) continue;
                    Vector3 world = presentedMesh.TransformPoint(contact.localPoint);
                    Vector3 toSource = -presentedMesh.localToWorldMatrix.MultiplyVector(contact.localIncoming).normalized;
                    float decay = contact.remaining / contact.duration;
                    positions[ActiveCount] = new Vector4(world.x, world.y, world.z, contact.radius);
                    colors[ActiveCount] = new Vector4(contact.color.r, contact.color.g, contact.color.b, decay * decay);
                    incoming[ActiveCount] = new Vector4(toSource.x, toSource.y, toSource.z, 0);
                    ActiveCount++;
                }
            for (int i = ActiveCount; i < Capacity; i++)
                positions[i] = colors[i] = incoming[i] = Vector4.zero;
            ApplyProperties();
        }

        void ApplyProperties()
        {
            if (targetRenderer == null) return;
            properties ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(properties);
            properties.SetVectorArray(PositionsId, positions);
            properties.SetVectorArray(ColorsId, colors);
            properties.SetVectorArray(IncomingId, incoming);
            properties.SetFloat(CountId, ActiveCount);
            targetRenderer.SetPropertyBlock(properties);
        }
        public void ResetResponse()
        {
            Array.Clear(contacts, 0, Capacity); Array.Clear(positions, 0, Capacity);
            Array.Clear(colors, 0, Capacity); Array.Clear(incoming, 0, Capacity);
            ActiveCount = 0; ApplyProperties();
        }
        void OnDisable() => ResetResponse();
    }
}
