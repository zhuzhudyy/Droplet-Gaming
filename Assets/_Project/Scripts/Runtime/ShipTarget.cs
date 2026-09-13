using System;
using UnityEngine;

namespace DropletPrototype
{
    public enum ShipDamageState { Intact, FatalPending, Exploded }
    public enum ShipBehaviorState { Formation, Engaging, BreakingFormation, Fleeing, Escaped }
    public sealed class ShipTarget : MonoBehaviour
    {
        public string targetId;
        public GameObject visualRoot;
        public Collider[] hitVolumes;
        public bool IsDestroyed { get; private set; }
        public ShipDamageState DamageState { get; internal set; }
        public ShipBehaviorState BehaviorState { get; internal set; }
        public FleetCombatSimulation CombatSimulation { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public float ExplosionAt { get; internal set; }
        public bool IsEscaped => BehaviorState == ShipBehaviorState.Escaped;
        public bool IsResolved => IsDestroyed || IsEscaped;
        public bool CanAttack => DamageState == ShipDamageState.Intact && !IsEscaped &&
            (BehaviorState == ShipBehaviorState.Formation || BehaviorState == ShipBehaviorState.Engaging);
        public ShipHitContext LastHit { get; private set; }
        public event Action<ShipTarget> Destroyed;
        public event Action<ShipTarget> Restored;
        public event Action<ShipTarget> FatalDamage;
        public event Action<ShipTarget> Escaped;

        public bool TryDestroy()
            => TryDestroy(new ShipHitContext(transform.position, transform.forward, 0));
        public bool TryDestroy(ShipHitContext hit)
        {
            if (CombatSimulation != null) return CombatSimulation.ApplyDamage(this, hit, hit.source, hit.attackId);
            return CommitExplosion(hit);
        }
        internal bool MarkFatal(ShipHitContext hit, float explodeAt)
        {
            if (DamageState != ShipDamageState.Intact || IsResolved) return false;
            DamageState = ShipDamageState.FatalPending; LastHit = hit; ExplosionAt = explodeAt;
            Notify(FatalDamage); return true;
        }
        internal bool CommitExplosion(ShipHitContext hit)
        {
            if (IsResolved) return false;
            IsDestroyed = true;
            DamageState = ShipDamageState.Exploded;
            LastHit = hit;
            if (hitVolumes != null)
                foreach (var volume in hitVolumes) if (volume != null) volume.enabled = false;
            if (visualRoot != null) visualRoot.SetActive(false);
            Notify(Destroyed);
            return true;
        }
        internal bool MarkEscaped()
        {
            if (IsResolved || DamageState != ShipDamageState.Intact) return false;
            BehaviorState = ShipBehaviorState.Escaped;
            if (hitVolumes != null) foreach (var volume in hitVolumes) if (volume != null) volume.enabled = false;
            if (visualRoot != null) visualRoot.SetActive(false);
            Notify(Escaped); return true;
        }
        public void ResetTarget()
        {
            IsDestroyed = false;
            DamageState = ShipDamageState.Intact; BehaviorState = ShipBehaviorState.Formation;
            Velocity = Vector3.zero; ExplosionAt = 0;
            LastHit = default;
            if (hitVolumes != null)
                foreach (var volume in hitVolumes) if (volume != null) volume.enabled = true;
            if (visualRoot != null) visualRoot.SetActive(true);
            Notify(Restored);
        }
        // Optional presentation faults cannot suppress another subscriber's score or later hits.
        void Notify(Action<ShipTarget> handlers)
        {
            if (handlers == null) return;
            foreach (Action<ShipTarget> callback in handlers.GetInvocationList())
                try { callback(this); } catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
