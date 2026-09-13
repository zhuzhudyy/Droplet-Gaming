using UnityEngine;

namespace DropletPrototype
{
    // The saved overview is used only on the Ready screen; flight retains the existing chase camera.
    [DefaultExecutionOrder(150)]
    public sealed class FleetOverviewCamera : MonoBehaviour
    {
        public MissionController mission;
        public Camera view;
        public ChaseCamera chase;
        public Transform overview;
        bool showing;
        void LateUpdate()
        {
            if(mission==null||view==null||overview==null||chase==null)return;
            bool ready=mission.State==MissionState.Ready;
            if(ready){view.transform.SetPositionAndRotation(overview.position,overview.rotation);showing=true;}
            else if(showing){chase.ResetCamera();showing=false;}
        }
        void OnDisable(){if(showing&&chase!=null)chase.ResetCamera();showing=false;}
    }
}
