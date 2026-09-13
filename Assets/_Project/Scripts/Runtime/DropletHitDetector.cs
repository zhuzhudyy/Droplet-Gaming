using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropletPrototype
{
    public sealed class DropletHitDetector : MonoBehaviour
    {
        public DropletSettings settings;
        public FleetRenderManager fleetRenderer;
        public FleetCombatSimulation combat;
        Collider[] overlaps = new Collider[8];
        RaycastHit[] sweeps = new RaycastHit[8];
        readonly List<Candidate> candidates = new List<Candidate>(16);
        readonly HashSet<ShipTarget> seen = new HashSet<ShipTarget>();
        public int BufferGrowthCount { get; private set; }
        struct Candidate { public ShipTarget ship; public float distance; public Vector3 point; }

        // The caller supplies each actual traveled segment BEFORE applying its endpoint.
        public int SweepSegment(Vector3 from, Vector3 to, float speed = 0, float startFraction = 0, float endFraction = 1)
        {
            if (settings == null) return 0;
            if (combat != null)
            {
                if (combat.mission != null && combat.mission.State != MissionState.Playing) return 0;
                return combat.SweepSegment(from, to, settings.hitRadius, speed, startFraction, endFraction);
            }
            if (fleetRenderer != null) fleetRenderer.PrepareSweep(from, to, settings.hitRadius);
            candidates.Clear(); seen.Clear();
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(from, settings.hitRadius, overlaps, settings.targetLayers, QueryTriggerInteraction.Collide);
                if (count < overlaps.Length) break;
                if (overlaps.Length >= 4096)
                {
                    overlaps = Physics.OverlapSphere(from, settings.hitRadius, settings.targetLayers, QueryTriggerInteraction.Collide);
                    count = overlaps.Length; break;
                }
                Array.Resize(ref overlaps, overlaps.Length * 2); BufferGrowthCount++;
            }
            for (int i = 0; i < count; i++) Add(overlaps[i], 0, overlaps[i].ClosestPoint(from));
            Vector3 delta = to - from; float distance = delta.magnitude;
            if (distance > .000001f)
            {
                while (true)
                {
                    count = Physics.SphereCastNonAlloc(from, settings.hitRadius, delta / distance, sweeps, distance, settings.targetLayers, QueryTriggerInteraction.Collide);
                    if (count < sweeps.Length) break;
                    if (sweeps.Length >= 4096)
                    {
                        sweeps = Physics.SphereCastAll(from, settings.hitRadius, delta / distance, distance, settings.targetLayers, QueryTriggerInteraction.Collide);
                        count = sweeps.Length; break;
                    }
                    Array.Resize(ref sweeps, sweeps.Length * 2); BufferGrowthCount++;
                }
                for (int i = 0; i < count; i++) Add(sweeps[i].collider, sweeps[i].distance, sweeps[i].point);
            }
            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            int destroyed = 0;
            foreach (var candidate in candidates)
                if (seen.Add(candidate.ship) && candidate.ship.TryDestroy(new ShipHitContext(candidate.point,
                    distance > .000001f ? delta / distance : transform.forward, speed))) destroyed++;
            return destroyed;
        }
        void Add(Collider collider, float distance, Vector3 point)
        {
            if (collider == null) return;
            var ship = collider.GetComponentInParent<ShipTarget>();
            if (ship != null && !ship.IsDestroyed) candidates.Add(new Candidate { ship = ship, distance = distance, point = point });
        }
        public void ResetQueries() { candidates.Clear(); seen.Clear(); BufferGrowthCount = 0; }
    }
}
