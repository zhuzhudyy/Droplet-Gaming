using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropletPrototype
{
    public struct LaserShotResult
    {
        public bool reflected, damageApplied;
        public ShipTarget hitShip;
        public Vector3 contactPoint, contactNormal, reflectedDirection, endPoint;
        public long attackId;
    }

    /// <summary>One fleet scheduler; authoritative hull queries include disabled-LOD
    /// geometry. The mission advances this only during formal combat.</summary>
    [DisallowMultipleComponent]
    public sealed class FleetLaserDirector : MonoBehaviour
    {
        public FleetCombatSimulation simulation;
        public LaserWeaponSettings settings;
        public DropletReflectiveSurface dropletSurface;
        public LaserBeamPool beamPool;
        public int ShotsFired { get; private set; }
        public int ReflectionCount { get; private set; }
        public int ReflectedDamageCount { get; private set; }
        public int LastRayQueries { get; private set; }
        public int PeakRayQueries { get; private set; }
        public int ActiveWeaponCount => tactical.Count;
        public LaserShotResult LastShot { get; private set; }
        sealed class Weapon
        {
            public ShipTarget ship;
            public Transform muzzle;
            public Vector3 direction;
            public double nextDecision, nextShot, lastAim;
            public bool ineffectiveReported, reflectionReported;
        }
        Weapon[] weapons = Array.Empty<Weapon>();
        readonly List<int> tactical = new List<int>(256);
        readonly Dictionary<Transform, Quaternion> socketRestRotations = new Dictionary<Transform, Quaternion>();
        double nextSelection;
        int cursor;
        long nextAttackId;

        public void ResetWeapons()
        {
            var fleet = simulation != null ? simulation.targets : null;
            weapons = fleet == null ? Array.Empty<Weapon>() : new Weapon[fleet.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                var ship = fleet[i]; if (ship == null) continue;
                // Muzzles are authored on the stable root, independent of LOD.
                Transform muzzle = ship.transform.Find("LaserMuzzle");
                if (muzzle != null)
                {
                    if (!socketRestRotations.TryGetValue(muzzle, out var rest))
                    { rest = muzzle.localRotation; socketRestRotations.Add(muzzle, rest); }
                    muzzle.localRotation = rest;
                }
                weapons[i] = new Weapon { ship = ship, muzzle = muzzle, direction = muzzle != null ? muzzle.forward : ship.transform.forward,
                    nextShot = .4 + StablePhase(ship.targetId) * 2.3, lastAim = 0 };
            }
            tactical.Clear(); nextSelection = 0; cursor = 0; nextAttackId = 0;
            ShotsFired = ReflectionCount = ReflectedDamageCount = LastRayQueries = PeakRayQueries = 0;
            LastShot = default; beamPool?.ResetEffects();
        }
        static float StablePhase(string id)
        { uint hash = 2166136261; if (id != null) foreach (char c in id) hash = (hash ^ c) * 16777619; return (hash & 65535) / 65535f; }

        public void Step(float dt)
        {
            if (dt <= 0 || simulation == null || simulation.scale == null || settings == null || dropletSurface == null) return;
            beamPool?.Step(dt); LastRayQueries = 0;
            if (weapons.Length != (simulation.targets?.Length ?? 0)) ResetWeapons();
            double now = simulation.SimulatedTime;
            if (now >= nextSelection)
            {
                tactical.Clear(); float selectionRange = simulation.scale.MetersToUnits(settings.rangeMeters) * 1.1f;
                Vector3 aim = dropletSurface.AimPoint;
                for (int i = 0; i < weapons.Length; i++)
                { var w = weapons[i]; if (w != null && w.ship != null && w.muzzle != null && w.ship.CanAttack && (w.ship.transform.position - aim).sqrMagnitude <= selectionRange * selectionRange) tactical.Add(i); }
                nextSelection = now + .25; if (tactical.Count > 0) cursor %= tactical.Count;
            }
            int budget = Mathf.Min(settings.decisionsPerStep, tactical.Count), shots = 0;
            float range = simulation.scale.MetersToUnits(settings.rangeMeters);
            float near = simulation.scale.MetersToUnits(settings.nearDecisionDistanceMeters);
            for (int i = 0; i < budget; i++)
            {
                if (cursor >= tactical.Count) cursor = 0;
                Weapon weapon = weapons[tactical[cursor++]];
                if (!weapon.ship.CanAttack || now < weapon.nextDecision) continue;
                Vector3 target = dropletSurface.AimPoint - weapon.muzzle.position;
                float distance = target.magnitude;
                double elapsed = Math.Max(0, now - weapon.lastAim); weapon.lastAim = now;
                weapon.nextDecision = now + (distance <= near ? settings.nearDecisionSeconds : settings.farDecisionSeconds);
                if (distance <= .01f || distance > range) continue;
                weapon.direction = Vector3.RotateTowards(weapon.direction, target.normalized,
                    settings.aimDegreesPerSecond * Mathf.Deg2Rad * (float)elapsed, 0).normalized;
                // This empty root socket records the actual bounded turret aim.
                weapon.muzzle.rotation = Quaternion.LookRotation(weapon.direction, weapon.ship.transform.up);
                if (now < weapon.nextShot || Vector3.Angle(weapon.direction, target) > settings.firingToleranceDegrees ||
                    shots >= settings.shotsPerStep || LastRayQueries + 3 > settings.rayQueriesPerStep) continue;
                weapon.nextShot = now + settings.fireIntervalSeconds * (.85f + .3f * StablePhase(weapon.ship.targetId));
                LastShot = FireRay(weapon.ship, weapon.muzzle.position, weapon.direction, ++nextAttackId);
                shots++; ShotsFired++;
                if (LastShot.reflected)
                {
                    ReflectionCount++; if (LastShot.damageApplied) ReflectedDamageCount++;
                    if (!weapon.ineffectiveReported) { weapon.ineffectiveReported = true; simulation.EmitEvent(CombatEventKind.AttackIneffective, weapon.ship, null, LastShot.contactPoint, DamageSource.DirectLaser); }
                    if (!weapon.reflectionReported) { weapon.reflectionReported = true; simulation.EmitEvent(CombatEventKind.LaserReflected, weapon.ship, LastShot.hitShip, LastShot.contactPoint, DamageSource.ReflectedLaser); }
                }
            }
            PeakRayQueries = Mathf.Max(PeakRayQueries, LastRayQueries);
        }

        /// <summary>One instantaneous attack. Public for deterministic scene tests;
        /// repeat calls using the same attack ID are deduplicated by ApplyDamage.</summary>
        public LaserShotResult FireRay(ShipTarget source, Vector3 origin, Vector3 direction, long attackId)
        {
            var result = new LaserShotResult { attackId = attackId };
            if (simulation == null || simulation.scale == null || settings == null || dropletSurface == null || direction.sqrMagnitude < .00001f) return result;
            direction.Normalize(); float range = simulation.scale.MetersToUnits(settings.rangeMeters);
            float offset = simulation.scale.MetersToUnits(settings.reflectionOffsetMeters);
            origin += direction * offset;
            LastRayQueries++;
            bool surfaceHit = dropletSurface.Raycast(new Ray(origin, direction), range, out var contact);
            LastRayQueries++;
            // Include the emitter: a turret aiming through its own hull is blocked.
            bool hullHit = simulation.RaycastShips(origin, direction, range, out var hull);
            float hullDistance = hullHit ? hull.distance : range;
            result.endPoint = origin + direction * range;
            if (surfaceHit && contact.distance < hullDistance)
            {
                result.reflected = true; result.contactPoint = contact.point; result.contactNormal = contact.normal;
                result.reflectedDirection = LaserGeometryMath.ReflectDirection(direction, contact.normal);
                Vector3 bounceOrigin = LaserGeometryMath.OffsetReflectionOrigin(contact.point, contact.normal, result.reflectedDirection, offset);
                float remaining = Mathf.Max(0, range - contact.distance);
                LastRayQueries++;
                // No ignored ship on the reflected query: the emitter can be hit.
                bool reflectedHit = simulation.RaycastShips(bounceOrigin, result.reflectedDirection, remaining, out var secondary);
                result.endPoint = reflectedHit ? secondary.point : bounceOrigin + result.reflectedDirection * remaining;
                if (reflectedHit)
                {
                    result.hitShip = secondary.ship;
                    if (settings.reflectedLaserIsLethal)
                        result.damageApplied = simulation.ApplyDamage(secondary.ship, new ShipHitContext(secondary.point, result.reflectedDirection, 0), DamageSource.ReflectedLaser, attackId);
                }
                if ((origin - dropletSurface.AimPoint).sqrMagnitude <= Mathf.Pow(simulation.scale.MetersToUnits(settings.visualDistanceMeters), 2))
                    beamPool?.Show(origin, contact.point, result.endPoint, true, dropletSurface.presentedMesh, contact.localPoint);
            }
            else
            {
                result.contactPoint = hullHit ? hull.point : result.endPoint; result.hitShip = hullHit ? hull.ship : null;
                result.endPoint = result.contactPoint;
                if (hullHit && settings.directLaserIsLethal)
                    result.damageApplied = simulation.ApplyDamage(hull.ship, new ShipHitContext(hull.point, direction, 0), DamageSource.DirectLaser, attackId);
                if ((origin - dropletSurface.AimPoint).sqrMagnitude <= Mathf.Pow(simulation.scale.MetersToUnits(settings.visualDistanceMeters), 2))
                    beamPool?.Show(origin, result.contactPoint, result.contactPoint, false);
            }
            return result;
        }
        void OnDisable() => beamPool?.ResetEffects();
    }
}
