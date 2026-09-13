using UnityEngine;

namespace DropletPrototype
{
    public enum DamageSource { Penetration, DirectLaser, ReflectedLaser }
    public readonly struct ShipHitContext
    {
        public readonly Vector3 point, direction;
        public readonly float speed;
        public readonly DamageSource source;
        public readonly long attackId;
        public ShipHitContext(Vector3 point, Vector3 direction, float speed)
            : this(point, direction, speed, DamageSource.Penetration, 0) { }
        public ShipHitContext(Vector3 point, Vector3 direction, float speed, DamageSource source, long attackId)
        { this.point = point; this.direction = direction; this.speed = speed; this.source = source; this.attackId = attackId; }
    }
}
