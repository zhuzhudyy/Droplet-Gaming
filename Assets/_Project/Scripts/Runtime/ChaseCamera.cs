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
        public void AddImpulse(Vector3 direction, float strength)
        {
            if (!ReducedMotion) impulse = Vector3.ClampMagnitude(impulse + direction.normalized * strength, .18f);
        }
        public void ResetCamera()
        {
            if (target == null || settings == null) return;
            impulse = Vector3.zero;
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
            transform.position = settings.worldScale != null ? desired : Vector3.Lerp(transform.position, desired, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(position + rotation * Vector3.forward * 35 - transform.position, Vector3.up), blend);
            GetComponent<Camera>().fieldOfView = settings.fieldOfView;
        }
    }
}
