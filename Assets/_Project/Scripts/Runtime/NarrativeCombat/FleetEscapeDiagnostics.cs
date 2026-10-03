using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropletPrototype
{
    public enum FleetThreatCause { NearbyPenetration, NearbyExplosion, FleetLoss, ExplicitRetreat }

    public readonly struct FleetThreatNotice
    {
        public readonly ShipTarget ship, source;
        public readonly FleetThreatCause cause;
        public readonly float time, reactionAt;
        public readonly int generation;
        public FleetThreatNotice(ShipTarget ship, ShipTarget source, FleetThreatCause cause, float time, float reactionAt, int generation)
        { this.ship = ship; this.source = source; this.cause = cause; this.time = time; this.reactionAt = reactionAt; this.generation = generation; }
    }

    /// <summary>
    /// Optional, bounded observations of normal threat events. Positions include the
    /// accumulated origin shift, so camera movement and rebasing cannot fake flight.
    /// This component never requests retreat or changes a target, camera or input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FleetEscapeDiagnostics : MonoBehaviour
    {
        public FleetCombatSimulation simulation;
        public bool captureEnabled;
        public bool writeConsole;
        [Range(1, 256)] public int maximumTrackedShips = 64;
        public IReadOnlyList<Sample> Samples => samples;

        [Serializable]
        public sealed class Sample
        {
            public string shipId, sourceId, cause, behavior, damage;
            public int generation, checkpointSeconds;
            public float simulationTime, secondsSinceThreat, reactionDelay, speedMetersPerSecond;
            public Vector3 authoritativePositionMeters, transformPositionMeters, renderedPositionMeters;
            public Vector3 displacementMeters;
            public float transformErrorMeters, renderedErrorMeters;
            public bool instanced, renderPoseAvailable, colliderEnabled, resolved;
        }
        sealed class Tracked
        {
            public FleetThreatNotice notice;
            public Vector3 initialAbsolutePosition;
            public int nextCheckpoint = 3;
        }
        [Serializable] sealed class Report { public string coordinateSpace = "Absolute meters including accumulated origin shifts"; public Sample[] samples; }
        readonly List<Sample> samples = new List<Sample>(192);
        readonly List<Tracked> tracked = new List<Tracked>(64);
        readonly HashSet<ShipTarget> seen = new HashSet<ShipTarget>();
        FleetCombatSimulation bound;

        void OnEnable() { Bind(); }
        public void Bind()
        {
            Unbind();
            if (simulation == null) simulation = GetComponent<FleetCombatSimulation>();
            bound = simulation;
            if (bound == null) return;
            bound.ThreatScheduled += OnThreat;
            bound.StepCompleted += Observe;
            bound.SimulationReset += Clear;
        }
        public void Clear() { samples.Clear(); tracked.Clear(); seen.Clear(); }
        void OnThreat(FleetThreatNotice notice)
        {
            if (!captureEnabled || tracked.Count >= Mathf.Clamp(maximumTrackedShips, 1, 256) ||
                notice.ship == null || !seen.Add(notice.ship)) return;
            // Explicit diagnostic requests are recorded distinctly and can never
            // stand in for NearbyPenetration/NearbyExplosion acceptance evidence.
            var item = new Tracked { notice = notice, initialAbsolutePosition = Absolute(notice.ship.transform.position) };
            tracked.Add(item); Capture(item, 0);
        }
        void Observe()
        {
            if (!captureEnabled || bound == null) return;
            foreach (var item in tracked)
            {
                if (item.nextCheckpoint > 10 || item.notice.ship == null) continue;
                float age = bound.SimulatedTime - item.notice.time;
                while (item.nextCheckpoint <= 10 && age + .0001f >= item.nextCheckpoint)
                {
                    Capture(item, item.nextCheckpoint);
                    item.nextCheckpoint = item.nextCheckpoint == 3 ? 10 : 11;
                }
            }
        }
        void Capture(Tracked item, int checkpoint)
        {
            var ship = item.notice.ship;
            if (!bound.TryGetAuthoritativePose(ship, out var authority, out _)) return;
            var rendered = ship.transform.position;
            bool instanced = false, available = false;
            if (bound.fleetRenderer != null && bound.fleetRenderer.TryGetPresentationPose(ship, out var matrix, out instanced))
            { rendered = matrix.GetColumn(3); available = true; }
            bool colliderEnabled = false;
            if (ship.hitVolumes != null) foreach (var collider in ship.hitVolumes)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy) { colliderEnabled = true; break; }
            var sample = new Sample
            {
                shipId = ship.targetId, sourceId = item.notice.source != null ? item.notice.source.targetId : "",
                cause = item.notice.cause.ToString(), generation = item.notice.generation,
                behavior = ship.BehaviorState.ToString(), damage = ship.DamageState.ToString(), checkpointSeconds = checkpoint,
                simulationTime = bound.SimulatedTime, secondsSinceThreat = bound.SimulatedTime - item.notice.time,
                reactionDelay = item.notice.reactionAt - item.notice.time,
                speedMetersPerSecond = Meters(ship.Velocity.magnitude),
                authoritativePositionMeters = Meters(Absolute(authority)),
                transformPositionMeters = Meters(Absolute(ship.transform.position)),
                renderedPositionMeters = Meters(Absolute(rendered)),
                displacementMeters = Meters(Absolute(authority) - item.initialAbsolutePosition),
                transformErrorMeters = Meters(Vector3.Distance(authority, ship.transform.position)),
                renderedErrorMeters = Meters(Vector3.Distance(authority, rendered)),
                instanced = instanced, renderPoseAvailable = available, colliderEnabled = colliderEnabled, resolved = ship.IsResolved
            };
            samples.Add(sample);
            if (writeConsole) Debug.Log("FleetEscape " + JsonUtility.ToJson(sample), this);
        }
        Vector3 Absolute(Vector3 position) => position + bound.AccumulatedOriginOffset;
        float Meters(float value) => bound.scale != null ? bound.scale.UnitsToMeters(value) : value * 100;
        Vector3 Meters(Vector3 value) => bound.scale != null ? bound.scale.UnitsToMeters(value) : value * 100;
        public string ToJson() => JsonUtility.ToJson(new Report { samples = samples.ToArray() }, true);
        void Unbind()
        {
            if (bound != null)
            {
                bound.ThreatScheduled -= OnThreat;
                bound.StepCompleted -= Observe;
                bound.SimulationReset -= Clear;
            }
            bound = null;
        }
        void OnDisable() { Unbind(); }
    }
}
