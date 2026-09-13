using UnityEngine;

namespace DropletPrototype
{
    /// <summary>A short optional wake. It never supplies a movement path.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class DropletTrail : MonoBehaviour
    {
        public DropletMotor motor;
        public MissionController mission;
        public MissionEffects effects;
        public EffectSettings settings;
        public Material trailMaterial;
        TrailRenderer trail;
        DropletMotor boundMotor;
        MissionController boundMission;
        public int PositionCount => trail != null ? trail.positionCount : 0;

        void OnEnable() { Bind(); }
        void Start() { Initialize(); }
        public void Initialize()
        {
            Bind();
            if (trail != null || motor == null || settings == null) return;
            var root = new GameObject("DropletWake");
            root.layer = 2;
            root.transform.SetParent(transform, false);
            trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = settings.trailLifetime;
            trail.minVertexDistance = .4f;
            trail.widthMultiplier = settings.trailWidth;
            trail.startColor = new Color(.55f, .83f, 1f, .65f);
            trail.endColor = new Color(.12f, .28f, .5f, 0);
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
        }
        void Bind()
        {
            if (boundMotor != motor)
            {
                if (boundMotor != null) boundMotor.Teleported -= Clear;
                boundMotor = motor;
                if (boundMotor != null) boundMotor.Teleported += Clear;
            }
            if (boundMission != mission)
            {
                if (boundMission != null) boundMission.Restarted -= Clear;
                boundMission = mission;
                if (boundMission != null) boundMission.Restarted += Clear;
            }
        }
        void LateUpdate()
        {
            if (trail == null) Initialize();
            if (trail == null || motor == null || settings == null) return;
            bool allowed = settings.enableTrails && trailMaterial != null &&
                (effects == null ? settings.initialQuality != EffectQuality.Off : effects.Quality != EffectQuality.Off);
            bool playing = mission == null || mission.State == MissionState.Playing;
            trail.emitting = allowed && playing && motor.Speed >= settings.trailMinimumSpeed;
            if (!allowed) { trail.Clear(); return; }
            if (mission != null && mission.State == MissionState.Paused) return;
            trail.transform.SetPositionAndRotation(motor.PresentedPosition + motor.PresentedRotation * new Vector3(0, 0, -1.2f), motor.PresentedRotation);
        }
        public void Clear() { if (trail != null) { trail.emitting = false; trail.Clear(); } }
        void OnDisable()
        {
            if (boundMotor != null) boundMotor.Teleported -= Clear;
            if (boundMission != null) boundMission.Restarted -= Clear;
            boundMotor = null; boundMission = null; Clear();
        }
    }
}
