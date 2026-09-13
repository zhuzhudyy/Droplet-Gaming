using UnityEngine;

namespace DropletPrototype
{
    public sealed class DropletMotor : MonoBehaviour
    {
        public DropletSettings settings;
        public DropletInput input;
        public Transform visualRoot;
        public DropletHitDetector hitDetector;
        public MissionController mission;
        public event System.Action Teleported;
        bool simulationEnabled = true;
        public bool SimulationEnabled
        {
            get => simulationEnabled;
            set
            {
                simulationEnabled = value;
                if (!value)
                {
                    previousPosition = transform.position; previousRotation = transform.rotation;
                    if (visualRoot != null) visualRoot.SetPositionAndRotation(previousPosition, previousRotation);
                }
            }
        }
        public float Speed { get; private set; }
        public float CruiseSpeed { get; private set; }
        public float MeasuredSpeed { get; private set; }
        public float LastSimulationSeconds { get; private set; }
        Vector3 previousPosition;
        Quaternion previousRotation;
        float yaw, pitch;
        public Vector3 PresentedPosition => Vector3.Lerp(previousPosition, transform.position, Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime));
        public Quaternion PresentedRotation => Quaternion.Slerp(previousRotation, transform.rotation, Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime));

        void Awake() { if (settings != null) ResetPose(transform.position, transform.rotation); }
        void FixedUpdate()
        {
            if (mission != null) return; // Mission supplies the clamped final step.
            if (!SimulationEnabled || settings == null || (input != null && !input.GameplayEnabled)) return;
            Simulate(Time.fixedDeltaTime, input != null ? input.ReadStep(Time.fixedDeltaTime, settings) : default);
        }
        public void Simulate(float dt, FlightCommand command)
        {
            if (!SimulationEnabled || dt <= 0 || settings == null) return;
            previousPosition = transform.position; previousRotation = transform.rotation;
            CruiseSpeed = Mathf.Clamp(CruiseSpeed + command.throttle * settings.speedAdjustment * dt, 0, settings.maxCruiseSpeed);
            float targetSpeed = command.brake ? 0 : CruiseSpeed * (command.boost ? settings.boostMultiplier : 1);
            Speed = Mathf.MoveTowards(Speed, targetSpeed, (command.brake ? settings.brakeStrength : settings.acceleration) * dt);
            Vector2 degrees = Vector2.ClampMagnitude(command.look * settings.mouseSensitivity, settings.turnDegreesPerSecond * dt);
            float newPitch = Mathf.Clamp(pitch + degrees.y * (settings.invertY ? 1 : -1), -settings.pitchLimit, settings.pitchLimit);
            float dy = degrees.x, dp = newPitch - pitch;
            int segments = Mathf.Max(1, Mathf.CeilToInt(degrees.magnitude / 4));
            for (int i = 0; i < segments; i++)
            {
                Quaternion direction = Quaternion.Euler(pitch + dp / segments * .5f, yaw + dy / segments * .5f, 0);
                Vector3 next = transform.position + direction * new Vector3(command.strafe * settings.strafeSpeed, 0, Speed) * (dt / segments);
                BeforeSegment(transform.position, next, i / (float)segments, (i + 1f) / segments);
                pitch += dp / segments; yaw += dy / segments;
                transform.SetPositionAndRotation(next, Quaternion.Euler(pitch, yaw, 0));
            }
            LastSimulationSeconds = dt;
            MeasuredSpeed = Vector3.Distance(previousPosition, transform.position) / dt;
        }
        void BeforeSegment(Vector3 from, Vector3 to, float startFraction, float endFraction)
        { if (hitDetector != null) hitDetector.SweepSegment(from, to, Speed, startFraction, endFraction); }
        public void HoldSimulationPose()
        {
            previousPosition = transform.position; previousRotation = transform.rotation;
            MeasuredSpeed = 0; LastSimulationSeconds = 0;
        }
        public void ResetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            previousPosition = position; previousRotation = rotation;
            yaw = rotation.eulerAngles.y; pitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
            CruiseSpeed = Speed = settings != null ? settings.initialSpeed : 0;
            MeasuredSpeed = 0; LastSimulationSeconds = 0;
            if (visualRoot != null) visualRoot.SetPositionAndRotation(position, rotation);
            input?.ResetInput();
            hitDetector?.ResetQueries();
            Teleported?.Invoke();
        }
        void LateUpdate()
        {
            if (visualRoot != null && SimulationEnabled) visualRoot.SetPositionAndRotation(PresentedPosition, PresentedRotation);
        }
    }
}
