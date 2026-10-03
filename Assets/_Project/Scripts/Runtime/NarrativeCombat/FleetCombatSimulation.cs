using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace DropletPrototype
{
    public readonly struct FleetRayHit
    {
        public readonly ShipTarget ship;
        public readonly Vector3 point, normal;
        public readonly float distance;
        public FleetRayHit(ShipTarget ship, Vector3 point, Vector3 normal, float distance)
        { this.ship = ship; this.point = point; this.normal = normal; this.distance = distance; }
    }

    /// <summary>
    /// One fixed-step authority for saved ShipTargets. Collider-derived hull geometry,
    /// trajectories, damage deadlines and render transforms survive every LOD change.
    /// No Update, per-ship coroutine, or pooled-view callback advances game state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FleetCombatSimulation : MonoBehaviour
    {
        public MissionController mission;
        public CombatScaleSettings scale;
        public ShipTarget[] targets = Array.Empty<ShipTarget>();
        public Transform threat;
        public FleetRenderManager fleetRenderer;
        public Vector3 evacuationCenter;
        public float SimulatedTime { get; private set; }
        public int Generation { get; private set; }
        public int PendingCount { get; private set; }
        public int EscapedCount { get; private set; }
        public int IntactCount { get; private set; }
        public int ExplodedCount { get; private set; }
        public int LastSweepCandidateCount { get; private set; }
        public int LastSweepSubsteps { get; private set; }
        public int TotalCount => entries.Length;
        public bool StepOpen => stepOpen;
        public bool CombatActive => mission == null || mission.State == MissionState.Playing;
        [NonSerialized] public bool MeasureCpuCost;
        public double BeginStepCpuMilliseconds { get; private set; }
        public double EndStepCpuMilliseconds { get; private set; }
        public event Action<CombatEvent> EventRaised;
        public event Action SimulationReset;
        public event Action<FleetThreatNotice> ThreatScheduled;
        public event Action StepCompleted;
        public Vector3 AccumulatedOriginOffset { get; private set; }

        sealed class Shape
        {
            public Bounds localBounds;
            public Matrix4x4 shapeFromRoot;
            public float inverseMinimumScale;
        }
        sealed class Entry
        {
            public ShipTarget ship;
            public Vector3 initialPosition, rootScale, start, end, velocity, escapeDirection;
            public Quaternion initialRotation, startRotation, endRotation;
            public Shape[] shapes;
            public float radius, panicAt, breakUntil, decisionAt, maximumSpeed, unstableAt;
            public bool unstableRaised;
            public string lastAttackerId;
            public uint seed;
            public int queryStamp;
        }
        readonly struct Contact
        {
            public readonly Entry entry;
            public readonly float fraction;
            public readonly Vector3 point;
            public Contact(Entry entry, float fraction, Vector3 point)
            { this.entry = entry; this.fraction = fraction; this.point = point; }
        }
        Entry[] entries = Array.Empty<Entry>();
        readonly Dictionary<ShipTarget, Entry> lookup = new Dictionary<ShipTarget, Entry>();
        readonly Dictionary<Vector3Int, List<Entry>> cells = new Dictionary<Vector3Int, List<Entry>>();
        readonly List<List<Entry>> cellPool = new List<List<Entry>>();
        readonly List<Entry> sweepCandidates = new List<Entry>(64), rayCandidates = new List<Entry>(64), neighborCandidates = new List<Entry>(64);
        readonly List<Contact> contacts = new List<Contact>(32);
        bool stepOpen, posesCommitted, fleetRetreatIssued;
        float stepDuration;
        int queryStamp, usedCells;
        long penetrationSequence;
        uint eventSequence;
        static readonly ProfilerMarker StepMarker = new ProfilerMarker("NarrativeCombat.FleetStep");
        static readonly ProfilerMarker SweepMarker = new ProfilerMarker("NarrativeCombat.RelativeSweep");

        public void Configure(ShipTarget[] fleet, Transform player, CombatScaleSettings settings)
        {
            foreach (var old in entries) if (old.ship != null && old.ship.CombatSimulation == this) old.ship.CombatSimulation = null;
            targets = fleet ?? Array.Empty<ShipTarget>(); threat = player; scale = settings;
            AccumulatedOriginOffset = Vector3.zero;
            lookup.Clear();
            var created = new List<Entry>(targets.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ship in targets)
            {
                if (ship == null || lookup.ContainsKey(ship)) continue;
                if (string.IsNullOrWhiteSpace(ship.targetId)) ship.targetId = "NC-" + created.Count.ToString("0000");
                if (!ids.Add(ship.targetId)) Debug.LogError("Duplicate stable ship ID: " + ship.targetId, ship);
                var entry = new Entry
                {
                    ship = ship, initialPosition = ship.transform.position, initialRotation = ship.transform.rotation,
                    rootScale = ship.transform.lossyScale, seed = StableSeed(ship.targetId, scale != null ? scale.randomSeed : 73191)
                };
                entry.shapes = ReadShapes(ship, out entry.radius);
                created.Add(entry); lookup.Add(ship, entry); ship.CombatSimulation = this;
            }
            entries = created.ToArray();
            ResetSimulation();
        }

        public void ResetSimulation()
        {
            Generation++; SimulatedTime = 0; PendingCount = EscapedCount = ExplodedCount = 0;
            eventSequence = 0;
            IntactCount = entries.Length; stepOpen = posesCommitted = false; fleetRetreatIssued = false; penetrationSequence = 0;
            foreach (var e in entries)
            {
                if (e.ship == null) continue;
                e.ship.transform.SetPositionAndRotation(e.initialPosition, e.initialRotation);
                e.ship.ResetTarget(); e.start = e.end = e.initialPosition; e.startRotation = e.endRotation = e.initialRotation;
                e.velocity = Vector3.zero; e.escapeDirection = Vector3.zero;
                e.panicAt = float.PositiveInfinity; e.breakUntil = 0;
                e.decisionAt = Unit(e.seed, 2) * .5f; e.unstableRaised = false;
                e.lastAttackerId = string.Empty;
                e.maximumSpeed = Units(Mathf.Lerp(scale != null ? scale.fleeMinMetersPerSecond : 2000,
                    scale != null ? scale.fleeMaxMetersPerSecond : 8000, Unit(e.seed, 3)));
            }
            RebuildSpatialIndex();
            if (fleetRenderer != null) fleetRenderer.RefreshAuthoritativePoses();
            SimulationReset?.Invoke();
        }

        public void BeginStep(float dt)
        {
            bool measureCpu = MeasureCpuCost;
            long cpuStarted = measureCpu ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            try
            {
            if (dt <= 0 || !CombatActive) return;
            if (entries.Length == 0 && targets != null && targets.Length > 0) Configure(targets, threat, scale);
            if (stepOpen) throw new InvalidOperationException("EndStep must close the preceding fleet step.");
            using (StepMarker.Auto())
            {
                stepOpen = true; posesCommitted = false; stepDuration = dt;
                Vector3 threatPosition = threat != null ? threat.position : evacuationCenter;
                foreach (var e in entries)
                {
                    var ship = e.ship; if (ship == null) continue;
                    e.start = e.end = ship.transform.position; e.startRotation = e.endRotation = ship.transform.rotation;
                    if (ship.IsResolved) continue;
                    if (ship.DamageState == ShipDamageState.FatalPending)
                    {
                        // Inertia and a small deterministic tumble, independent of the optional view.
                        e.velocity *= Mathf.Exp(-dt * .025f);
                        e.end = e.start + e.velocity * dt;
                        e.endRotation = e.startRotation * Quaternion.Euler(dt * (1 + Unit(e.seed, 4) * 2), 0, dt * (2 + Unit(e.seed, 5) * 3));
                        ship.Velocity = e.velocity; continue;
                    }
                    if (SimulatedTime >= e.panicAt && (ship.BehaviorState == ShipBehaviorState.Formation || ship.BehaviorState == ShipBehaviorState.Engaging))
                    {
                        ship.BehaviorState = ShipBehaviorState.BreakingFormation;
                        e.breakUntil = SimulatedTime + .7f + Unit(e.seed, 7) * 1.5f;
                        e.decisionAt = 0;
                        EmitEvent(CombatEventKind.RetreatOrdered, ship, ship, e.start);
                    }
                    if (ship.BehaviorState == ShipBehaviorState.BreakingFormation && SimulatedTime >= e.breakUntil)
                        ship.BehaviorState = ShipBehaviorState.Fleeing;
                    if (SimulatedTime >= e.decisionAt)
                    {
                        float near = Units(scale != null ? scale.nearDecisionRangeMeters : 200000);
                        bool close = (e.start - threatPosition).sqrMagnitude < near * near;
                        e.decisionAt = SimulatedTime + (close ? (scale != null ? scale.nearDecisionInterval : .12f) : (scale != null ? scale.farDecisionInterval : .6f));
                        Decide(e, threatPosition);
                    }
                    bool fleeing = ship.BehaviorState == ShipBehaviorState.BreakingFormation || ship.BehaviorState == ShipBehaviorState.Fleeing;
                    Vector3 desired = fleeing ? e.escapeDirection : e.startRotation * Vector3.forward;
                    if (desired.sqrMagnitude < .0001f) desired = e.startRotation * Vector3.forward;
                    float turn = (scale != null ? scale.fleeTurnDegreesPerSecond : 16) * dt;
                    if (fleeing) e.endRotation = Quaternion.RotateTowards(e.startRotation, Quaternion.LookRotation(desired.normalized, Vector3.up), turn);
                    float wantedSpeed = fleeing ? e.maximumSpeed : ship.BehaviorState == ShipBehaviorState.Engaging ? Units(scale != null ? scale.engagementMetersPerSecond : 250) : 0;
                    float acceleration = Units(scale != null ? scale.fleeAccelerationMetersPerSecondSquared : 1400);
                    Vector3 desiredVelocity = e.endRotation * Vector3.forward * wantedSpeed;
                    e.velocity = Vector3.MoveTowards(e.velocity, desiredVelocity, acceleration * dt);
                    e.end = e.start + e.velocity * dt; ship.Velocity = e.velocity;
                }
                RebuildSpatialIndex();
            }
            }
            finally
            {
                if (measureCpu) BeginStepCpuMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - cpuStarted) *
                    (1000d / System.Diagnostics.Stopwatch.Frequency);
            }
        }

        public void EndStep(float dt)
        {
            bool measureCpu = MeasureCpuCost;
            long cpuStarted = measureCpu ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            try
            {
            if (!stepOpen) return;
            float elapsed = stepDuration;
            SimulatedTime += elapsed;
            // Publish every pose before emitting explosions or nearby-threat
            // queries. Previously observers later in the array still had their
            // previous-frame Transform while earlier ships used their endpoint.
            foreach (var e in entries)
            {
                var ship = e.ship; if (ship == null || ship.IsResolved) continue;
                ship.transform.SetPositionAndRotation(e.end, e.endRotation);
            }
            posesCommitted = true;
            if (fleetRenderer != null) fleetRenderer.RefreshAuthoritativePoses();
            foreach (var e in entries)
            {
                var ship = e.ship; if (ship == null || ship.IsResolved) continue;
                if (ship.DamageState == ShipDamageState.FatalPending)
                {
                    if (!e.unstableRaised && SimulatedTime >= e.unstableAt)
                    { e.unstableRaised = true; EmitEvent(CombatEventKind.ReactorUnstable, ship, ship, e.end, ship.LastHit.source); }
                    if (SimulatedTime + .000001f >= ship.ExplosionAt)
                    {
                        if (ship.CommitExplosion(ship.LastHit))
                        {
                            PendingCount--; ExplodedCount++;
                            EmitEvent(CombatEventKind.CommunicationInterrupted, ship, ship, e.end, ship.LastHit.source);
                            EmitEvent(CombatEventKind.ShipExploded, FindWitness(e), ship, e.end, ship.LastHit.source);
                            PanicNearby(e.end, ship, FleetThreatCause.NearbyExplosion);
                        }
                    }
                }
                else if (ship.BehaviorState == ShipBehaviorState.Fleeing &&
                    (e.end - evacuationCenter).sqrMagnitude >= EscapeRadius * EscapeRadius && ship.MarkEscaped())
                {
                    IntactCount--; EscapedCount++;
                    EmitEvent(CombatEventKind.ShipEscaped, ship, ship, e.end);
                }
            }
            stepOpen = false;
            var completedHandlers = StepCompleted;
            if (completedHandlers != null) foreach (Action callback in completedHandlers.GetInvocationList())
                try { callback(); } catch (Exception exception) { Debug.LogException(exception, this); }
            }
            finally
            {
                if (measureCpu) EndStepCpuMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - cpuStarted) *
                    (1000d / System.Diagnostics.Stopwatch.Frequency);
            }
        }

        void Decide(Entry e, Vector3 threatPosition)
        {
            if (e.ship.BehaviorState == ShipBehaviorState.Formation || e.ship.BehaviorState == ShipBehaviorState.Engaging)
            {
                float range = Units(scale != null ? scale.engagementRangeMeters : 250000);
                e.ship.BehaviorState = (e.start - threatPosition).sqrMagnitude <= range * range ? ShipBehaviorState.Engaging : ShipBehaviorState.Formation;
                return;
            }
            Vector3 outward = e.start - evacuationCenter;
            if (outward.sqrMagnitude < 1) outward = new Vector3(Unit(e.seed, 11) - .5f, (Unit(e.seed, 12) - .5f) * .5f, 1);
            Vector3 away = e.start - threatPosition;
            if (away.sqrMagnitude < .001f) away = outward;
            // The outward component always reaches a finite spherical evacuation boundary.
            Vector3 direction = outward.normalized * 1.5f + away.normalized;
            float avoidance = Units(scale != null ? scale.avoidanceDistanceMeters : 16000);
            Query(new Bounds(e.start, Vector3.one * avoidance * 2), neighborCandidates);
            Vector3 separation = Vector3.zero;
            foreach (var other in neighborCandidates)
            {
                if (other == e || other.ship == null || other.ship.IsResolved) continue;
                Vector3 delta = e.start - other.start; float sqr = delta.sqrMagnitude;
                if (sqr > .001f && sqr < avoidance * avoidance) separation += delta.normalized * (1 - Mathf.Sqrt(sqr) / avoidance);
            }
            e.escapeDirection = (direction + Vector3.ClampMagnitude(separation, .5f)).normalized;
        }

        public bool ApplyDamage(ShipTarget ship, ShipHitContext hit, DamageSource source, long attackId, string attackerId = null)
        {
            if (!CombatActive || ship == null || !lookup.TryGetValue(ship, out var e) || ship.DamageState != ShipDamageState.Intact || ship.IsResolved) return false;
            float min = scale != null ? Mathf.Max(.01f, scale.explosionDelaySeconds.x) : 2;
            float max = scale != null ? Mathf.Max(min, scale.explosionDelaySeconds.y) : 5;
            float delay = Mathf.Lerp(min, max, Unit(e.seed, 19));
            var sourced = new ShipHitContext(hit.point, hit.direction.normalized, hit.speed, source, attackId);
            if (!ship.MarkFatal(sourced, SimulatedTime + delay)) return false;
            e.lastAttackerId = attackerId ?? (source == DamageSource.Penetration ? "Droplet" : string.Empty);
            e.unstableAt = SimulatedTime + delay * .45f; e.unstableRaised = false;
            // A ship at rest gets a small physical impact impulse; a fleeing ship retains its inertia.
            e.velocity += sourced.direction * Units(120); ship.Velocity = e.velocity;
            IntactCount--; PendingCount++;
            EmitEvent(CombatEventKind.HullPenetrated, ship, ship, hit.point, source, hit.direction,
                attackerId: attackerId ?? (source == DamageSource.Penetration ? "Droplet" : string.Empty));
            EmitEvent(CombatEventKind.RescueRequested, ship, ship, hit.point, source);
            PanicNearby(ship.transform.position, ship, FleetThreatCause.NearbyPenetration);
            float threshold = scale != null ? scale.fleetLossRetreatFraction : .08f;
            if (!fleetRetreatIssued && (PendingCount + ExplodedCount) >= Mathf.Max(1, entries.Length * threshold))
            {
                fleetRetreatIssued = true;
                // The reaction window already spreads departures. Adding another
                // random delay here previously stretched a normal response to 7.9s.
                foreach (var other in entries) SchedulePanic(other, ship, FleetThreatCause.FleetLoss);
            }
            return true;
        }

        public void RequestRetreat(ShipTarget ship, float delay = 0)
        {
            if (ship != null && lookup.TryGetValue(ship, out var e))
                SetPanicDeadline(e, SimulatedTime + Mathf.Max(0, delay), null, FleetThreatCause.ExplicitRetreat);
        }

        public void SetShipVelocity(ShipTarget ship, Vector3 velocity)
        { if (ship != null && lookup.TryGetValue(ship, out var e)) { e.velocity = velocity; ship.Velocity = velocity; } }

        void PanicNearby(Vector3 position, ShipTarget source, FleetThreatCause cause)
        {
            float radius = Units(scale != null ? scale.nearbyPanicRadiusMeters : 175000);
            Query(new Bounds(position, Vector3.one * radius * 2), neighborCandidates);
            foreach (var e in neighborCandidates)
                if ((e.ship.transform.position - position).sqrMagnitude <= radius * radius) SchedulePanic(e, source, cause);
        }
        void SchedulePanic(Entry e, ShipTarget source, FleetThreatCause cause)
        {
            Vector2 reaction = scale != null ? scale.NormalReactionDelay : new Vector2(1, 3);
            SetPanicDeadline(e, SimulatedTime + Mathf.Lerp(reaction.x, reaction.y, Unit(e.seed, 23)), source, cause);
        }
        void SetPanicDeadline(Entry e, float deadline, ShipTarget source, FleetThreatCause cause)
        {
            if (e.ship == null || e.ship.IsResolved || e.ship.DamageState != ShipDamageState.Intact ||
                e.ship.BehaviorState == ShipBehaviorState.BreakingFormation || e.ship.BehaviorState == ShipBehaviorState.Fleeing ||
                deadline >= e.panicAt) return;
            e.panicAt = deadline;
            var handlers = ThreatScheduled;
            if (handlers == null) return;
            var notice = new FleetThreatNotice(e.ship, source, cause, SimulatedTime, deadline, Generation);
            foreach (Action<FleetThreatNotice> callback in handlers.GetInvocationList())
                try { callback(notice); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
        public bool TryGetAuthoritativePose(ShipTarget ship, out Vector3 position, out Quaternion rotation)
        {
            if (ship != null && lookup.TryGetValue(ship, out var e))
            {
                bool proposed = stepOpen && !posesCommitted;
                position = proposed ? e.start : e.end; rotation = proposed ? e.startRotation : e.endRotation; return true;
            }
            position = default; rotation = Quaternion.identity; return false;
        }
        ShipTarget FindWitness(Entry victim)
        {
            float radius = Units(scale != null ? scale.nearbyPanicRadiusMeters : 175000);
            Query(new Bounds(victim.end, Vector3.one * radius * 2), neighborCandidates);
            ShipTarget selected = null; float best = float.PositiveInfinity;
            foreach (var e in neighborCandidates)
            {
                if (e == victim || e.ship == null || e.ship.IsResolved || e.ship.DamageState != ShipDamageState.Intact) continue;
                float distance = (e.end - victim.end).sqrMagnitude;
                if (distance < best) { best = distance; selected = e.ship; }
            }
            return selected;
        }
        public void EmitEvent(CombatEventKind kind, ShipTarget speaker, ShipTarget subject, Vector3 position,
            DamageSource source = DamageSource.Penetration, Vector3 direction = default, Vector3 normal = default,
            string attackerId = null, string targetId = null)
        {
            var stateOwner = subject != null ? subject : speaker;
            if (attackerId == null && stateOwner != null && lookup.TryGetValue(stateOwner, out var stateEntry) &&
                (kind == CombatEventKind.HullPenetrated || kind == CombatEventKind.ReactorUnstable ||
                 kind == CombatEventKind.RescueRequested || kind == CombatEventKind.ShipExploded ||
                 kind == CombatEventKind.CommunicationInterrupted)) attackerId = stateEntry.lastAttackerId;
            if (direction.sqrMagnitude < .000001f && stateOwner != null)
                direction = stateOwner.DamageState == ShipDamageState.Intact ? stateOwner.transform.forward : stateOwner.LastHit.direction;
            long id = ((long)Generation << 32) | ++eventSequence;
            var value = new CombatEvent(kind, speaker, subject, position, source, SimulatedTime, Generation, id,
                attackerId ?? (speaker != null ? speaker.targetId : string.Empty),
                targetId ?? (subject != null ? subject.targetId : string.Empty), direction, normal, AccumulatedOriginOffset);
            var handlers = EventRaised;
            if (handlers == null) return;
            foreach (Action<CombatEvent> callback in handlers.GetInvocationList())
                try { callback(value); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        public int SweepSegment(Vector3 from, Vector3 to, float radius, float speed,
            float segmentStartFraction = 0, float segmentEndFraction = 1)
        {
            if (!CombatActive) return 0;
            using (SweepMarker.Auto())
            {
                contacts.Clear(); LastSweepSubsteps = 0;
                var path = new Bounds(from, Vector3.zero); path.Encapsulate(to); path.Expand(radius * 2 + .02f);
                Query(path, sweepCandidates); LastSweepCandidateCount = sweepCandidates.Count;
                foreach (var e in sweepCandidates)
                {
                    if (e.ship == null || e.ship.IsResolved || e.ship.DamageState != ShipDamageState.Intact) continue;
                    float a = stepOpen ? Mathf.Clamp01(segmentStartFraction) : 1;
                    float b = stepOpen ? Mathf.Clamp01(segmentEndFraction) : 1;
                    float turn = Quaternion.Angle(e.startRotation, e.endRotation) * Mathf.Abs(b - a);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(turn / 3), 1, 64);
                    LastSweepSubsteps += steps;
                    float first = float.PositiveInfinity;
                    for (int step = 0; step < steps; step++)
                    {
                        float t0 = step / (float)steps, t1 = (step + 1f) / steps;
                        float f0 = Mathf.Lerp(a, b, t0), f1 = Mathf.Lerp(a, b, t1);
                        Vector3 p0 = Vector3.Lerp(from, to, t0), p1 = Vector3.Lerp(from, to, t1);
                        // Both endpoints use one midpoint orientation: relative translation
                        // is then a straight segment even at arbitrarily high player speed.
                        // Padding covers the hull's rotation around that fixed orientation.
                        Quaternion middleRotation = Quaternion.Slerp(e.startRotation, e.endRotation, (f0 + f1) * .5f);
                        Matrix4x4 root0 = Matrix4x4.TRS(Vector3.Lerp(e.start, e.end, f0), middleRotation, e.rootScale).inverse;
                        Matrix4x4 root1 = Matrix4x4.TRS(Vector3.Lerp(e.start, e.end, f1), middleRotation, e.rootScale).inverse;
                        // Inflated hull bounds cover the curved surface swept between sampled rotations.
                        float turnPadding = e.radius * Mathf.Sin(Mathf.Min(90, turn / steps) * Mathf.Deg2Rad);
                        foreach (var shape in e.shapes)
                        {
                            Vector3 localFrom = shape.shapeFromRoot.MultiplyPoint3x4(root0.MultiplyPoint3x4(p0));
                            Vector3 localTo = shape.shapeFromRoot.MultiplyPoint3x4(root1.MultiplyPoint3x4(p1));
                            if (SegmentBox(localFrom, localTo, shape.localBounds, (radius + turnPadding) * shape.inverseMinimumScale, out float t, out _))
                                first = Mathf.Min(first, Mathf.Lerp(t0, t1, t));
                        }
                    }
                    if (!float.IsPositiveInfinity(first)) contacts.Add(new Contact(e, first, Vector3.Lerp(from, to, first)));
                }
                contacts.Sort((x, y) => x.fraction.CompareTo(y.fraction));
                int damaged = 0; Vector3 direction = (to - from).sqrMagnitude > .0000001f ? (to - from).normalized : threat != null ? threat.forward : Vector3.forward;
                long attack = ++penetrationSequence;
                foreach (var contact in contacts)
                    if (ApplyDamage(contact.entry.ship, new ShipHitContext(contact.point, direction, speed), DamageSource.Penetration, attack)) damaged++;
                return damaged;
            }
        }

        public bool RaycastShips(Vector3 origin, Vector3 direction, float maximumDistance, out FleetRayHit hit, ShipTarget ignore = null)
        {
            hit = default;
            if (maximumDistance <= 0 || direction.sqrMagnitude < .000001f) return false;
            direction.Normalize(); Vector3 to = origin + direction * maximumDistance;
            var bounds = new Bounds(origin, Vector3.zero); bounds.Encapsulate(to); bounds.Expand(.02f);
            Query(bounds, rayCandidates);
            float nearest = 1; ShipTarget selected = null; Vector3 normal = Vector3.zero;
            foreach (var e in rayCandidates)
            {
                if (e.ship == null || e.ship == ignore || e.ship.IsResolved) continue;
                Matrix4x4 inverseRoot = e.ship.transform.worldToLocalMatrix;
                foreach (var shape in e.shapes)
                {
                    Matrix4x4 inverse = shape.shapeFromRoot * inverseRoot;
                    if (!SegmentBox(inverse.MultiplyPoint3x4(origin), inverse.MultiplyPoint3x4(to), shape.localBounds, 0, out float t, out Vector3 localNormal) || t > nearest) continue;
                    nearest = t; selected = e.ship; normal = inverse.transpose.MultiplyVector(localNormal).normalized;
                }
            }
            if (selected == null) return false;
            hit = new FleetRayHit(selected, origin + direction * (maximumDistance * nearest), normal, maximumDistance * nearest); return true;
        }

        public void ResetSweepHistory()
        {
            stepOpen = false;
            foreach (var e in entries)
            {
                if (e.ship == null) continue;
                e.start = e.end = e.ship.transform.position; e.startRotation = e.endRotation = e.ship.transform.rotation;
            }
            RebuildSpatialIndex();
        }
        public void ShiftOrigin(Vector3 offset)
        {
            AccumulatedOriginOffset += offset;
            evacuationCenter -= offset;
            foreach (var e in entries)
            {
                e.initialPosition -= offset; e.start -= offset; e.end -= offset;
                if (e.ship != null) e.ship.transform.position -= offset;
            }
            ResetSweepHistory();
            if (fleetRenderer != null) fleetRenderer.RefreshAuthoritativePoses();
        }

        float Units(float meters) => scale != null ? scale.MetersToUnits(meters) : meters / 100;
        float EscapeRadius => Units(scale != null ? scale.escapeRadiusMeters : 1800000);
        float CellSize => Mathf.Max(10, Units(scale != null ? scale.spatialCellMeters : 100000));
        Vector3Int Cell(Vector3 p) { float size = CellSize; return new Vector3Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size)); }
        void RebuildSpatialIndex()
        {
            cells.Clear(); usedCells = 0;
            foreach (var e in entries)
            {
                if (e.ship == null || e.ship.IsResolved) continue;
                Bounds bound = new Bounds(e.start, Vector3.one * e.radius * 2); bound.Encapsulate(new Bounds(e.end, Vector3.one * e.radius * 2));
                Vector3Int min = Cell(bound.min), max = Cell(bound.max);
                for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
                {
                    var key = new Vector3Int(x, y, z);
                    if (!cells.TryGetValue(key, out var list))
                    {
                        if (usedCells == cellPool.Count) cellPool.Add(new List<Entry>(8));
                        list = cellPool[usedCells++]; list.Clear(); cells.Add(key, list);
                    }
                    list.Add(e);
                }
            }
        }
        void Query(Bounds bounds, List<Entry> result)
        {
            result.Clear(); int stamp = ++queryStamp;
            if (stamp == int.MaxValue) { queryStamp = stamp = 1; foreach (var e in entries) e.queryStamp = 0; }
            Vector3Int min = Cell(bounds.min), max = Cell(bounds.max);
            long volume = ((long)max.x - min.x + 1) * ((long)max.y - min.y + 1) * ((long)max.z - min.z + 1);
            // Arbitrarily long diagnostic sweeps remain complete without visiting empty space.
            if (volume > 4096)
            {
                foreach (var e in entries)
                {
                    if (e.ship == null || e.ship.IsResolved) continue;
                    Bounds ship = new Bounds(e.start, Vector3.one * e.radius * 2); ship.Encapsulate(new Bounds(e.end, Vector3.one * e.radius * 2));
                    if (ship.Intersects(bounds)) result.Add(e);
                }
                return;
            }
            for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
            {
                if (!cells.TryGetValue(new Vector3Int(x, y, z), out var list)) continue;
                foreach (var e in list) if (e.queryStamp != stamp) { e.queryStamp = stamp; result.Add(e); }
            }
        }

        static Shape[] ReadShapes(ShipTarget ship, out float radius)
        {
            var shapes = new List<Shape>(); radius = 1;
            if (ship.hitVolumes != null) foreach (var collider in ship.hitVolumes)
            {
                if (collider == null) continue;
                Bounds local;
                if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
                else if (collider is CapsuleCollider capsule)
                { Vector3 size = Vector3.one * capsule.radius * 2; size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]); local = new Bounds(capsule.center, size); }
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else continue;
                Matrix4x4 rootFromShape = ship.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                Vector3 s = collider.transform.lossyScale;
                shapes.Add(new Shape { localBounds = local, shapeFromRoot = rootFromShape.inverse,
                    inverseMinimumScale = 1 / Mathf.Max(.0001f, Mathf.Min(Mathf.Abs(s.x), Mathf.Min(Mathf.Abs(s.y), Mathf.Abs(s.z)))) });
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    radius = Mathf.Max(radius, (collider.transform.TransformPoint(p) - ship.transform.position).magnitude);
                }
            }
            return shapes.ToArray();
        }

        // Slab intersection, including initial overlap. Inflated boxes conservatively
        // approximate a sphere at compound-box edges; original hit-volume sizes stay intact.
        public static bool SegmentBox(Vector3 from, Vector3 to, Bounds bounds, float inflation, out float fraction, out Vector3 normal)
        {
            Vector3 min = bounds.min - Vector3.one * inflation, max = bounds.max + Vector3.one * inflation, delta = to - from;
            float enter = 0, exit = 1; normal = delta.sqrMagnitude > .000001f ? -delta.normalized : Vector3.forward;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .0000001f)
                { if (from[axis] < min[axis] || from[axis] > max[axis]) { fraction = 0; return false; } continue; }
                float t0 = (min[axis] - from[axis]) / delta[axis], t1 = (max[axis] - from[axis]) / delta[axis];
                float sign = -1;
                if (t0 > t1) { float swap = t0; t0 = t1; t1 = swap; sign = 1; }
                if (t0 > enter) { enter = t0; normal = Vector3.zero; normal[axis] = sign; }
                exit = Mathf.Min(exit, t1);
                if (enter > exit) { fraction = 0; return false; }
            }
            fraction = enter; return enter <= 1 && exit >= 0;
        }
        static uint StableSeed(string id, int seed)
        { unchecked { uint value = 2166136261u ^ (uint)seed; foreach (char c in id) { value ^= c; value *= 16777619; } return value; } }
        static float Unit(uint seed, uint channel)
        { unchecked { uint value = seed ^ (channel * 747796405u); value ^= value >> 16; value *= 2246822519u; value ^= value >> 13; return (value & 0x00ffffff) / 16777215f; } }
        void OnDisable() { stepOpen = false; Generation++; }
        void OnDestroy()
        { foreach (var e in entries) if (e.ship != null && e.ship.CombatSimulation == this) e.ship.CombatSimulation = null; }
    }
}
