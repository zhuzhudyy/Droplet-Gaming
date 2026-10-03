using UnityEngine;

namespace DropletPrototype
{
    [DefaultExecutionOrder(100)]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public DropletMotor target;
        public DropletSettings settings;
        public bool ReducedMotion = true;
        Vector3 impulse;
        Camera view;
        bool normalPoseInitialized, cinematic;
        Vector3 cinematicPosition, returnOffset;
        Quaternion cinematicRotation, returnRotation = Quaternion.identity;
        float cinematicFov, returnRemaining, returnDuration, returnFov;
        public Vector3 NormalPosition { get; private set; }
        public Quaternion NormalRotation { get; private set; } = Quaternion.identity;
        public bool CinematicActive => cinematic;

        // Presentation only: the motor continues to use its own yaw/pitch basis.
        public void SetCinematicPose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            cinematic = true; cinematicPosition = position; cinematicRotation = rotation;
            cinematicFov = Mathf.Clamp(fieldOfView, 30, 85); returnRemaining = 0;
        }
        public void ReturnToChase(float seconds = .24f)
        {
            if (!cinematic && returnRemaining <= 0) return;
            cinematic = false;
            returnDuration = returnRemaining = Mathf.Max(0, seconds);
            returnOffset = transform.position - NormalPosition;
            returnRotation = transform.rotation * Quaternion.Inverse(NormalRotation);
            if (view == null) view = GetComponent<Camera>();
            returnFov = view != null ? view.fieldOfView : 65;
            if (seconds <= 0 && normalPoseInitialized)
            {
                transform.SetPositionAndRotation(NormalPosition, NormalRotation);
                if (view != null && settings != null) view.fieldOfView = settings.fieldOfView;
            }
        }
        public void AddImpulse(Vector3 direction, float strength)
        {
            if (!ReducedMotion) impulse = Vector3.ClampMagnitude(impulse + direction.normalized * strength, .18f);
        }
        public void ResetCamera()
        {
            if (target == null || settings == null) return;
            impulse = Vector3.zero;
            cinematic = false; returnRemaining = 0; normalPoseInitialized = false;
            Apply(1, target.transform.position, target.transform.rotation);
        }
        void Start() { ResetCamera(); }
        void LateUpdate()
        {
            if (target == null || settings == null || Time.timeScale == 0) return;
            Apply(1 - Mathf.Exp(-settings.cameraSmoothing * Time.deltaTime), target.PresentedPosition, target.PresentedRotation);
            if (!ReducedMotion) transform.position += impulse;
            impulse = Vector3.Lerp(impulse, Vector3.zero, 1 - Mathf.Exp(-18 * Time.deltaTime));
        }
        void Apply(float blend, Vector3 position, Quaternion rotation)
        {
            Vector3 desired = position + rotation * new Vector3(0, settings.cameraHeight, -settings.cameraDistance);
            // At kilometres/second, smoothing the absolute world position would
            // trail by hundreds of hull lengths. Keep the local chase offset.
            if (!normalPoseInitialized)
            { NormalPosition = desired; NormalRotation = rotation; normalPoseInitialized = true; }
            NormalPosition = settings.worldScale != null ? desired : Vector3.Lerp(NormalPosition, desired, blend);
            NormalRotation = Quaternion.Slerp(NormalRotation, Quaternion.LookRotation(position + rotation * Vector3.forward * 35 - NormalPosition, Vector3.up), blend);
            if (view == null) view = GetComponent<Camera>();
            if (cinematic)
            {
                transform.SetPositionAndRotation(cinematicPosition, cinematicRotation);
                if (view != null) view.fieldOfView = cinematicFov;
            }
            else if (returnRemaining > 0)
            {
                returnRemaining = Mathf.Max(0, returnRemaining - Time.unscaledDeltaTime);
                float weight = Mathf.SmoothStep(0, 1, returnRemaining / Mathf.Max(.001f, returnDuration));
                transform.SetPositionAndRotation(NormalPosition + returnOffset * weight,
                    Quaternion.Slerp(Quaternion.identity, returnRotation, weight) * NormalRotation);
                if (view != null) view.fieldOfView = Mathf.Lerp(settings.fieldOfView, returnFov, weight);
            }
            else
            {
                transform.SetPositionAndRotation(NormalPosition, NormalRotation);
                if (view != null) view.fieldOfView = settings.fieldOfView;
            }
        }
    }
}
