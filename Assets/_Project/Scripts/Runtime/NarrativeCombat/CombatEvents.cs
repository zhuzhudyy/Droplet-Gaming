using UnityEngine;

namespace DropletPrototype
{
    public enum CombatEventKind
    {
        AttackIneffective, LaserReflected, HullPenetrated, ReactorUnstable, ShipExploded,
        RescueRequested, RetreatOrdered, ShipEscaped, CommunicationInterrupted,
        WeaponFired, DropletContact
    }

    public readonly struct CombatEvent
    {
        public readonly CombatEventKind kind;
        public readonly ShipTarget speaker, subject;
        public readonly Vector3 position;
        public readonly DamageSource source;
        public readonly float simulationTime;
        public readonly int generation;
        public readonly long eventId;
        public readonly string attackerId, targetId;
        public readonly Vector3 direction, normal, originOffset;
        public readonly ShipDamageState damageState;
        public readonly ShipBehaviorState behaviorState;
        public CombatEvent(CombatEventKind kind, ShipTarget speaker, ShipTarget subject,
            Vector3 position, DamageSource source, float simulationTime, int generation)
            : this(kind, speaker, subject, position, source, simulationTime, generation, 0,
                speaker != null ? speaker.targetId : string.Empty,
                subject != null ? subject.targetId : string.Empty, Vector3.zero, Vector3.zero, Vector3.zero) { }

        public CombatEvent(CombatEventKind kind, ShipTarget speaker, ShipTarget subject,
            Vector3 position, DamageSource source, float simulationTime, int generation, long eventId,
            string attackerId, string targetId, Vector3 direction, Vector3 normal, Vector3 originOffset)
        {
            this.kind = kind; this.speaker = speaker; this.subject = subject; this.position = position;
            this.source = source; this.simulationTime = simulationTime; this.generation = generation;
            this.eventId = eventId; this.attackerId = attackerId ?? string.Empty; this.targetId = targetId ?? string.Empty;
            this.direction = direction; this.normal = normal; this.originOffset = originOffset;
            var stateOwner = subject != null ? subject : speaker;
            damageState = stateOwner != null ? stateOwner.DamageState : ShipDamageState.Intact;
            behaviorState = stateOwner != null ? stateOwner.BehaviorState : ShipBehaviorState.Formation;
        }

        public Vector3 PositionAtOrigin(Vector3 currentOriginOffset) => position + originOffset - currentOriginOffset;
    }

}
