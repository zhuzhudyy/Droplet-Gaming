using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DropletPrototype
{
    public enum CombatShotFrequency { Off, Low, Standard }
    public enum CombatShotKind { Attacker, DropletContact, Penetration, Explosion }

    /// <summary>Consumes real combat notifications; never changes the motor, damage, clock or weapon state.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(90)]
    public sealed class ShotDirector : MonoBehaviour
    {
        // Ordinary notifications are much more frequent than one-shot reactor
        // events. Share their lottery and give each opportunity a smaller weight.
        const float OrdinaryProbabilityFactor = .25f;
        const float OrdinarySampleSeconds = 1.5f;
        const float ExplosionMinimumFacingWeight = .9f;
        public MissionController mission;
        public FleetCombatSimulation simulation;
        public ChaseCamera chase;
        public FleetRenderManager fleetRenderer;
        public CombatShotFrequency frequency = CombatShotFrequency.Standard;
        [Tooltip("Independent Input System binding; Space remains the flight brake.")]
        public string returnBinding = "<Keyboard>/q";
        public string frequencyBinding = "<Keyboard>/f8";
        public bool showControls = true;
        [Min(100)] public float maximumEventDistance = 4000;
        [Range(20, 300)] public float strongTurnDegreesPerSecond = 65;
        public int randomSeed = 190926;

        public bool Active { get; private set; }
        public CombatShotKind CurrentKind { get; private set; }
        public CombatShotKind LastShotKind { get; private set; }
        public long LastShotEventId { get; private set; }
        public int StartedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int PendingCount => hasCandidate ? 1 : 0;
        public float RemainingSeconds => Active ? Mathf.Max(0, duration - elapsed) : 0;
        public float NextEligibleTime => nextEligibleTime;
        public event Action<CombatEvent, CombatShotKind> ShotStarted;
        public event Action ShotEnded;

        FleetCombatSimulation boundSimulation;
        MissionController boundMission;
        DropletMotor boundMotor;
        InputAction returnAction, frequencyAction;
        System.Random random;
        CombatEvent candidate, activeEvent;
        CombatShotKind candidateKind;
        ShipTarget activeSubject;
        string activeSubjectId;
        readonly HashSet<long> seen = new HashSet<long>();
        readonly Queue<long> seenOrder = new Queue<long>();
        readonly Dictionary<CombatShotKind, float> lastKinds = new Dictionary<CombatShotKind, float>();
        readonly Dictionary<CombatShotKind, float> nextSamples = new Dictionary<CombatShotKind, float>();
        readonly Dictionary<string, float> lastSubjects = new Dictionary<string, float>(StringComparer.Ordinal);
        bool hasCandidate, hasRotation;
        int candidateFrame, generation;
        float candidateScore, nextEligibleTime, elapsed, duration, turnSuppressedUntil;
        float nextOrdinarySample;
        int ordinaryOpportunityFrame = -1;
        double ordinaryOpportunityRoll;
        float activeAttackerSide = 1;
        Quaternion previousMotorRotation;
        Vector3 activeSubjectLocalPoint;

        public void Configure(MissionController controller, FleetCombatSimulation combat, ChaseCamera camera, FleetRenderManager renderer)
        {
            Unbind(); mission = controller; simulation = combat; chase = camera; fleetRenderer = renderer;
            if (isActiveAndEnabled) Bind();
        }
        void OnEnable() { CreateActions(); Bind(); }
        void OnDisable()
        {
            StopShot(true); Unbind();
            returnAction?.Dispose(); frequencyAction?.Dispose(); returnAction = frequencyAction = null;
        }
        void CreateActions()
        {
            returnAction?.Dispose(); frequencyAction?.Dispose();
            returnAction = new InputAction("Return from combat close-up", InputActionType.Button, returnBinding);
            frequencyAction = new InputAction("Combat close-up frequency", InputActionType.Button, frequencyBinding);
            returnAction.Enable(); frequencyAction.Enable();
        }
        public void RebindReturn(string binding)
        {
            if (string.IsNullOrWhiteSpace(binding)) return;
            returnBinding = binding;
            if (isActiveAndEnabled) CreateActions();
        }
        public void SetFrequency(CombatShotFrequency value)
        {
            frequency = value;
            if (value == CombatShotFrequency.Off) { hasCandidate = false; StopShot(false); }
        }
        void Bind()
        {
            if (simulation == null || chase == null) return;
            boundSimulation = simulation; boundMission = mission; boundMotor = chase.target;
            boundSimulation.EventRaised += ConsiderEvent; boundSimulation.SimulationReset += ResetDirector;
            if (boundMission != null) boundMission.StateChanged += OnMissionState;
            if (boundMotor != null) boundMotor.Teleported += OnTeleport;
            ResetDirector();
        }
        void Unbind()
        {
            if (boundSimulation != null) { boundSimulation.EventRaised -= ConsiderEvent; boundSimulation.SimulationReset -= ResetDirector; }
            if (boundMission != null) boundMission.StateChanged -= OnMissionState;
            if (boundMotor != null) boundMotor.Teleported -= OnTeleport;
            boundSimulation = null; boundMission = null; boundMotor = null;
        }
        public void ResetDirector()
        {
            StopShot(true); seen.Clear(); seenOrder.Clear(); lastKinds.Clear(); nextSamples.Clear(); lastSubjects.Clear();
            hasCandidate = hasRotation = false; StartedCount = RejectedCount = ExpiredCount = 0;
            LastShotEventId = 0; generation = simulation != null ? simulation.Generation : 0;
            random = new System.Random(randomSeed ^ generation);
            nextEligibleTime = (simulation != null ? simulation.SimulatedTime : 0) + .75f;
            turnSuppressedUntil = nextOrdinarySample = 0;
            ordinaryOpportunityFrame = -1;
        }
        void OnTeleport() { hasCandidate = false; hasRotation = false; StopShot(true); }
        void OnMissionState(MissionState state)
        { if (state != MissionState.Playing) { hasCandidate = false; StopShot(true); } }

        bool CanPresent => frequency != CombatShotFrequency.Off && simulation != null && chase != null &&
            chase.isActiveAndEnabled && chase.target != null && Time.timeScale > 0 &&
            (mission == null || mission.State == MissionState.Playing);

        /// <summary>Public for integration probes, with exactly the same validity and probability policy as real notifications.</summary>
        public void ConsiderEvent(CombatEvent value)
        {
            if (!TryKind(value.kind, out var kind)) return;
            // The legacy damage bus also calls laser hull damage HullPenetrated.
            // Only the motor's actual sweep is a droplet fly-through shot.
            if (kind == CombatShotKind.Penetration && value.source != DamageSource.Penetration) return;
            if (!CanPresent || Active || value.generation != simulation.Generation || value.eventId <= 0 ||
                simulation.SimulatedTime < nextEligibleTime || simulation.SimulatedTime < turnSuppressedUntil)
            { RejectedCount++; return; }
            if (!seen.Add(value.eventId)) { RejectedCount++; return; }
            seenOrder.Enqueue(value.eventId);
            while (seenOrder.Count > 256) seen.Remove(seenOrder.Dequeue());
            float age = simulation.SimulatedTime - value.simulationTime;
            if (age < -.001f || age > MaximumAge(kind)) { ExpiredCount++; return; }
            if (!ValidSubject(value, kind)) { RejectedCount++; return; }
            Vector3 eventPoint = value.PositionAtOrigin(simulation.AccumulatedOriginOffset);
            float distance = Vector3.Distance(chase.target.PresentedPosition, eventPoint);
            if (distance > maximumEventDistance) { RejectedCount++; return; }
            float now = simulation.SimulatedTime;
            bool ordinary = kind == CombatShotKind.Attacker || kind == CombatShotKind.DropletContact;
            // A hundred beams per second must not get a hundred lottery tickets.
            // These gates only discard notifications; they never schedule future cuts.
            if ((lastKinds.TryGetValue(kind, out float recent) && now - recent < 32) ||
                (nextSamples.TryGetValue(kind, out float nextSample) && now < nextSample) ||
                (ordinary && now < nextOrdinarySample && ordinaryOpportunityFrame != Time.frameCount))
            { RejectedCount++; return; }
            bool newOrdinaryOpportunity = ordinary && now >= nextOrdinarySample;
            if (newOrdinaryOpportunity)
            { nextOrdinarySample = now + OrdinarySampleSeconds; ordinaryOpportunityFrame = Time.frameCount; }
            else if (!ordinary) nextSamples[kind] = now + .2f;
            float weight = kind == CombatShotKind.Explosion ? 1.4f : kind == CombatShotKind.Penetration ? 1.15f : kind == CombatShotKind.DropletContact ? .85f : .65f;
            if (ordinary) weight *= OrdinaryProbabilityFactor;
            // A nearby reactor that is already unstable deserves a chance to
            // produce its real explosion event. This only reduces the current
            // common event's lottery weight; it never reserves or queues a cut.
            if (ordinary && NearbyPendingExplosion()) weight *= .18f;
            weight *= Mathf.Lerp(1, .3f, Mathf.Clamp01(distance / maximumEventDistance));
            Vector3 delta = eventPoint - chase.NormalPosition;
            float facing = delta.sqrMagnitude > .01f ? Vector3.Dot(chase.NormalRotation * Vector3.forward, delta.normalized) : 1;
            // A delayed blast often happens behind the still-moving droplet.
            // Its new oblique composition is independently tested for occlusion,
            // so being behind the normal view must not halve its importance.
            weight *= Mathf.Lerp(kind == CombatShotKind.Explosion ? ExplosionMinimumFacingWeight : .45f,
                1, Mathf.InverseLerp(-.4f, .7f, facing));
            if (lastKinds.TryGetValue(kind, out float lastKind) && simulation.SimulatedTime - lastKind < 45) weight *= .25f;
            string id = SubjectId(value, kind);
            if (!string.IsNullOrEmpty(id) && lastSubjects.TryGetValue(id, out float lastSubject) && simulation.SimulatedTime - lastSubject < 90) weight *= .12f;
            float chance = Mathf.Clamp01(weight * (frequency == CombatShotFrequency.Low ? .18f : .48f));
            if (random == null) random = new System.Random(randomSeed);
            if (newOrdinaryOpportunity) ordinaryOpportunityRoll = random.NextDouble();
            // FireRay emits WeaponFired immediately before DropletContact. They
            // share ONE draw, allowing the clearer/high-weight contact to compete
            // in that same frame instead of always losing to callback order.
            double roll = ordinary ? ordinaryOpportunityRoll : random.NextDouble();
            if (roll > chance) { RejectedCount++; return; }
            // Only the strongest notification of THIS rendered frame is retained. No historical queue.
            if (hasCandidate && candidateFrame == Time.frameCount && weight <= candidateScore) return;
            candidate = value; candidateKind = kind; candidateScore = weight;
            candidateFrame = Time.frameCount; hasCandidate = true;
        }

        bool NearbyPendingExplosion()
        {
            if (simulation.PendingCount == 0 || simulation.targets == null) return false;
            Vector3 player = chase.target.PresentedPosition;
            foreach (var ship in simulation.targets)
            {
                if (ship == null || ship.IsResolved || ship.DamageState != ShipDamageState.FatalPending) continue;
                float remaining = ship.ExplosionAt - simulation.SimulatedTime;
                if (remaining >= 0 && remaining <= 5 && (ship.transform.position - player).sqrMagnitude <= 1600 * 1600) return true;
            }
            return false;
        }

        void Update()
        {
            if (returnAction != null && returnAction.WasPressedThisFrame()) ReturnImmediately();
            if (frequencyAction != null && frequencyAction.WasPressedThisFrame())
                SetFrequency((CombatShotFrequency)(((int)frequency + 1) % 3));
        }
        void LateUpdate() => TickPresentation(Time.deltaTime);

        public void TickPresentation(float deltaTime)
        {
            if (simulation != null && generation != simulation.Generation) ResetDirector();
            if (!CanPresent) { hasCandidate = false; StopShot(true); return; }
            float now = simulation.SimulatedTime;
            Quaternion rotation = chase.target.transform.rotation;
            // A fixed-step rotation can land inside a much shorter render frame.
            // Use the interval which actually produced that movement, so high FPS
            // cannot turn moderate steering into an apparent abrupt turn.
            float rotationInterval = Mathf.Max(deltaTime, chase.target.LastSimulationSeconds);
            if (hasRotation && rotationInterval > .0001f && Quaternion.Angle(previousMotorRotation, rotation) / rotationInterval > strongTurnDegreesPerSecond)
                turnSuppressedUntil = now + .65f;
            previousMotorRotation = rotation; hasRotation = true;
            if (now < turnSuppressedUntil) { hasCandidate = false; StopShot(false); return; }
            if (!Active && hasCandidate)
            {
                hasCandidate = false;
                if (candidateFrame != Time.frameCount || now - candidate.simulationTime > MaximumAge(candidateKind)) ExpiredCount++;
                else TryStart(candidate, candidateKind);
            }
            if (!Active) return;
            elapsed += Mathf.Max(0, deltaTime);
            if (elapsed >= duration || !ValidSubject(activeEvent, CurrentKind)) { StopShot(false); return; }
            if (!TryCompose(activeEvent, CurrentKind, out var position, out var rotationToSubject, out float fov))
            { StopShot(false); return; }
            chase.SetCinematicPose(position, rotationToSubject, fov);
        }

        bool TryStart(CombatEvent value, CombatShotKind kind)
        {
            if (!ValidSubject(value, kind) || !TryCompose(value, kind, out var position, out var rotation, out float fov))
            { RejectedCount++; return false; }
            activeEvent = value; CurrentKind = LastShotKind = kind; LastShotEventId = value.eventId;
            activeSubject = Subject(value, kind); activeSubjectId = SubjectId(value, kind);
            activeSubjectLocalPoint = activeSubject != null ? activeSubject.transform.InverseTransformPoint(value.PositionAtOrigin(simulation.AccumulatedOriginOffset)) : Vector3.zero;
            if (activeSubject != null) activeSubject.Restored += OnSubjectRestored;
            elapsed = 0; duration = kind == CombatShotKind.DropletContact ? .65f : kind == CombatShotKind.Explosion ? 1.1f : .85f;
            Active = true; StartedCount++;
            lastKinds[kind] = simulation.SimulatedTime;
            if (!string.IsNullOrEmpty(activeSubjectId)) lastSubjects[activeSubjectId] = simulation.SimulatedTime;
            nextEligibleTime = simulation.SimulatedTime + (float)(12 + random.NextDouble() * 8) * (frequency == CombatShotFrequency.Low ? 1.7f : 1);
            fleetRenderer?.PinCinematicSubject(kind == CombatShotKind.Explosion ? null : activeSubject, chase.target.transform);
            chase.SetCinematicPose(position, rotation, fov);
            // Optional audio emphasis may subscribe; it must never replay this event's sound.
            ShotStarted?.Invoke(value, kind); return true;
        }
        void OnSubjectRestored(ShipTarget subject) => StopShot(true);
        public void ReturnImmediately()
        { hasCandidate = false; StopShot(false); }
        void StopShot(bool immediate)
        {
            bool wasActive = Active; Active = false;
            if (activeSubject != null) activeSubject.Restored -= OnSubjectRestored;
            activeSubject = null; activeSubjectId = null;
            fleetRenderer?.ClearCinematicSubject();
            if (chase != null && (wasActive || immediate)) chase.ReturnToChase(immediate ? 0 : .24f);
            if (wasActive) ShotEnded?.Invoke();
        }

        bool ValidSubject(CombatEvent value, CombatShotKind kind)
        {
            if (simulation == null || value.generation != simulation.Generation) return false;
            ShipTarget subject = Subject(value, kind);
            if (kind == CombatShotKind.DropletContact) return chase != null && chase.target != null;
            if (subject == null || subject.targetId != SubjectId(value, kind) || subject.CombatSimulation != simulation || subject.IsEscaped) return false;
            if (kind == CombatShotKind.Explosion) return subject.IsDestroyed && subject.DamageState == ShipDamageState.Exploded;
            if (subject.IsResolved) return false;
            return kind != CombatShotKind.Penetration || subject.DamageState == ShipDamageState.FatalPending;
        }
        bool TryCompose(CombatEvent value, CombatShotKind kind, out Vector3 position, out Quaternion rotation, out float fov)
        {
            Vector3 eventPoint = value.PositionAtOrigin(simulation.AccumulatedOriginOffset);
            Vector3 player = chase.target.PresentedPosition;
            Vector3 forward = chase.target.PresentedRotation * Vector3.forward;
            Vector3 right = chase.target.PresentedRotation * Vector3.right;
            Vector3 up = Vector3.up;
            ShipTarget subject = Subject(value, kind);
            Vector3 focus;
            fov = 55;
            if (kind == CombatShotKind.DropletContact)
            {
                Vector3 normal = value.normal.sqrMagnitude > .1f ? value.normal.normalized : -forward;
                focus = player + forward * .45f;
                position = player + normal * 4.5f + right * 3.3f + up * 1.6f - forward * 1.7f;
                fov = 52;
            }
            else if (kind == CombatShotKind.Penetration)
            {
                float shotAge = Active && activeEvent.eventId == value.eventId ? elapsed : 0;
                focus = Vector3.Lerp(player + forward * 4, subject.transform.position,
                    .3f * Mathf.Clamp01(1 - shotAge / .25f));
                position = player - forward * 15 + right * 18 + up * 7;
                fov = 68;
            }
            else if (kind == CombatShotKind.Attacker)
            {
                Vector3 shipForward = subject.transform.forward;
                Vector3 shipRight = subject.transform.right;
                float radius = 30;
                if (fleetRenderer != null && fleetRenderer.TryGetTargetBounds(subject, out var bounds)) radius = Mathf.Clamp(bounds.extents.magnitude, 8, 80);
                Vector3 muzzle = Active && subject == activeSubject ? subject.transform.TransformPoint(activeSubjectLocalPoint) : eventPoint;
                focus = Vector3.Lerp(subject.transform.position, muzzle, .55f);
                Vector3 center = subject.transform.position + up * radius * .65f + shipForward * radius * .85f;
                float side = Active && subject == activeSubject ? activeAttackerSide :
                    RenderSettings.sun != null && Vector3.Dot(shipRight, -RenderSettings.sun.transform.forward) < 0 ? -1 : 1;
                position = center + shipRight * radius * 1.8f * side;
                if (!Active)
                {
                    // Prefer the lit side, but accept the other authored side if
                    // the preferred line of sight is blocked. Never add a light.
                    if (!ClearLineOfSight(position, focus, subject))
                    { side = -side; position = center + shipRight * radius * 1.8f * side; }
                    activeAttackerSide = side;
                }
                // Once chosen, keep the side for this shot. New occlusion aborts
                // instead of snapping 180 degrees to the opposite side mid-shot.
                fov = 57;
            }
            else
            {
                // The hull is already resolved. Follow the immutable/rebased explosion position, never a pooled visual.
                Vector3 towardPlayer = player - eventPoint;
                if (towardPlayer.sqrMagnitude < 1) towardPlayer = -forward;
                if (towardPlayer.magnitude < 180)
                {
                    focus = Vector3.Lerp(eventPoint, player, .3f);
                    Vector3 side = Vector3.Cross(up, towardPlayer.normalized);
                    if (side.sqrMagnitude < .1f) side = right;
                    position = focus + side.normalized * 100 + up * 35 - towardPlayer.normalized * 25;
                }
                else
                {
                    focus = eventPoint;
                    position = eventPoint + towardPlayer.normalized * 100 + right * 45 + up * 30;
                }
                fov = 62;
            }
            Vector3 delta = focus - position;
            rotation = delta.sqrMagnitude > .01f ? Quaternion.LookRotation(delta.normalized, up) : Quaternion.identity;
            if (!Finite(position) || delta.sqrMagnitude < 4) return false;
            // Uses the authoritative broad phase too: disabled far-hull colliders still occlude a proposed shot.
            return ClearLineOfSight(position, focus, subject);
        }
        bool ClearLineOfSight(Vector3 position, Vector3 focus, ShipTarget ignoredSubject)
        {
            Vector3 delta = focus - position;
            return delta.sqrMagnitude >= 4 && !simulation.RaycastShips(position, delta.normalized,
                Mathf.Max(0, delta.magnitude - .25f), out _, ignoredSubject);
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        static ShipTarget Subject(CombatEvent value, CombatShotKind kind) => kind == CombatShotKind.Attacker ? value.speaker : kind == CombatShotKind.DropletContact ? null : value.subject;
        static string SubjectId(CombatEvent value, CombatShotKind kind) => kind == CombatShotKind.Attacker ? value.attackerId : kind == CombatShotKind.DropletContact ? "Droplet" : value.targetId;
        static float MaximumAge(CombatShotKind kind) => kind == CombatShotKind.Attacker || kind == CombatShotKind.DropletContact ? .14f : .24f;
        static bool TryKind(CombatEventKind kind, out CombatShotKind result)
        {
            switch (kind)
            {
                case CombatEventKind.WeaponFired: result = CombatShotKind.Attacker; return true;
                case CombatEventKind.DropletContact: result = CombatShotKind.DropletContact; return true;
                case CombatEventKind.HullPenetrated: result = CombatShotKind.Penetration; return true;
                case CombatEventKind.ShipExploded: result = CombatShotKind.Explosion; return true;
                default: result = default; return false;
            }
        }
        void OnGUI()
        {
            if (!showControls || mission == null || mission.State == MissionState.Narrative) return;
            string mode = frequency == CombatShotFrequency.Off ? "OFF" : frequency == CombatShotFrequency.Low ? "LOW" : "STANDARD";
            string binding = returnAction != null ? returnAction.GetBindingDisplayString() : "Q";
            GUI.Label(new Rect(Screen.width - 390, Screen.height - 30, 385, 25),
                "Close-ups " + mode + " [F8]" + (Active ? "  ·  Return [" + binding + "]" : ""));
        }
    }
}
