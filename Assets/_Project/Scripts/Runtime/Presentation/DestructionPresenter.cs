using System;
using UnityEngine;

namespace DropletPrototype
{
    /// <summary>Optional view of a target's already-committed destruction.</summary>
    public sealed class DestructionPresenter : MonoBehaviour
    {
        public ShipTarget target;
        public MissionEffects effects;
        [Range(0, 2)] public int wreckKind;
        public Vector3 wreckScale = Vector3.one;
        ShipTarget boundTarget;
        bool reportedError;

        void OnEnable() { Bind(); }
        public void Bind()
        {
            if (target == null) target = GetComponent<ShipTarget>();
            if (boundTarget == target) return;
            if (boundTarget != null) boundTarget.Destroyed -= OnDestroyed;
            boundTarget = target;
            if (boundTarget != null) boundTarget.Destroyed += OnDestroyed;
        }
        void OnDestroyed(ShipTarget ship)
        {
            // The target and score have their own state; a malformed optional asset
            // must not interrupt other subscribers, including the mission.
            try { if (effects != null && effects.isActiveAndEnabled) effects.TryPlay(ship, wreckKind, wreckScale); }
            catch (Exception exception)
            {
                if (reportedError) return;
                reportedError = true;
                Debug.LogWarning("Optional impact presentation was omitted: " + exception.Message, this);
            }
        }
        void OnDisable()
        {
            if (boundTarget != null) boundTarget.Destroyed -= OnDestroyed;
            boundTarget = null;
        }
    }
}
