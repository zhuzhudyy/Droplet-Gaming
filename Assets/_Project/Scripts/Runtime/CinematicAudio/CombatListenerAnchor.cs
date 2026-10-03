using UnityEngine;

namespace DropletPrototype
{
    /// <summary>The scene's sole AudioListener lives here, independent of editorial camera cuts.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(150)]
    public sealed class CombatListenerAnchor : MonoBehaviour
    {
        public ChaseCamera chase;
        void LateUpdate()
        {
            if (chase == null || chase.target == null) return;
            if (chase.enabled || chase.settings == null)
            {
                transform.SetPositionAndRotation(chase.NormalPosition, chase.NormalRotation);
                return;
            }
            // Narrative temporarily disables ChaseCamera and drives its visible
            // transform. Hearing still follows the moving droplet's normal pose.
            var motor = chase.target;
            Vector3 player = motor.PresentedPosition;
            Quaternion heading = motor.PresentedRotation;
            Vector3 position = player + heading * new Vector3(0, chase.settings.cameraHeight, -chase.settings.cameraDistance);
            Quaternion rotation = Quaternion.LookRotation(player + heading * Vector3.forward * 35 - position, Vector3.up);
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
