using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Controlled geometry arrangement in the saved scene, not a claim that
    /// a human pilot produced this rare secondary hit in the performance run.</summary>
    public sealed class NarrativeSavedHullReflectionTests
    {
        MissionController mission;
        CombatScaleSettings originalScale, stationaryScale;

        [UnitySetUp]
        public IEnumerator LoadSavedSmallScene()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/_Project/Scenes/FleetAssault_NarrativeCombat_Small.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_NarrativeCombat_Small");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(mission);
            Assert.AreEqual(12, mission.TotalCount);
            mission.enabled = false;
            mission.input.enabled = false;
        }

        [TearDown]
        public void RestoreSavedScene()
        {
            Time.timeScale = 1;
            if (mission != null)
            {
                if (originalScale != null) mission.combat.scale = originalScale;
                mission.Restart();
            }
            if (stationaryScale != null) Object.DestroyImmediate(stationaryScale);
        }

        [Test]
        public void Saved7040TriangleDropletReflectsNormalScheduledShotIntoOriginalCompoundHullExactlyOnce()
        {
            mission.RestartIntoCombat();
            mission.motor.SimulationEnabled = false;
            var combat = mission.combat;
            var lasers = mission.lasers;
            var surface = lasers.dropletSurface;
            var savedGeometry = surface.geometry;
            string[] savedIds = mission.targets.Select(ship => ship.targetId).ToArray();
            Assert.AreEqual(7040, savedGeometry.TriangleCount);
            Assert.IsTrue(savedGeometry.IsValid);
            Assert.IsTrue(lasers.settings.reflectedLaserIsLethal);

            // Only suppress formation drift while arranging an optical bench.
            // Original geometry, turret aiming/fire intervals, laser settings,
            // damage entry point, seeded 2-5 s deadlines and scoring stay intact.
            originalScale = combat.scale;
            stationaryScale = Object.Instantiate(originalScale);
            stationaryScale.engagementMetersPerSecond = 0;
            combat.scale = stationaryScale;
            for (int i = 0; i < mission.targets.Length; i++)
            {
                mission.targets[i].transform.SetPositionAndRotation(
                    new Vector3(0, 6000 + i * 150, 1000), Quaternion.identity);
                combat.SetShipVelocity(mission.targets[i], Vector3.zero);
            }
            ShipTarget emitter = mission.targets[0], receiver = mission.targets[1];
            Assert.AreEqual(9, emitter.hitVolumes.Length);
            Assert.AreEqual(9, receiver.hitVolumes.Length);
            Collider[] savedReceiverHull = receiver.hitVolumes.ToArray();
            emitter.transform.SetPositionAndRotation(new Vector3(0, 8, 1000), Quaternion.identity);
            Transform muzzle = emitter.transform.Find("LaserMuzzle");
            Assert.IsNotNull(muzzle, "Use the saved model's authored turret socket.");
            BoxCollider receiverHull = receiver.hitVolumes.OfType<BoxCollider>()
                .OrderByDescending(box => box.size.sqrMagnitude).First();

            Assert.IsTrue(ArrangeAlongActualReflectedRay(emitter, receiver, receiverHull, muzzle,
                out var expectedContact, out var expectedReflection, out var chosenRotation),
                "No unobstructed actual-mesh reflection / compound-hull arrangement was found.");
            lasers.ResetWeapons();
            combat.ResetSweepHistory();
            Assert.AreSame(savedGeometry, surface.geometry, "Do not replace the saved droplet with a fixture.");
            CollectionAssert.AreEqual(savedReceiverHull, receiver.hitVolumes);
            CollectionAssert.AreEqual(savedIds, mission.targets.Select(ship => ship.targetId).ToArray());

            int fatalEvents = 0, explosionEvents = 0;
            receiver.FatalDamage += _ => fatalEvents++;
            receiver.Destroyed += _ => explosionEvents++;
            for (int i = 0; i < 200 && lasers.ShotsFired == 0; i++) mission.Step(.02f, default);

            Assert.AreEqual(1, lasers.ShotsFired, "The away-facing receiver cannot finish its real turret turn before the emitter fires.");
            LaserShotResult shot = lasers.LastShot;
            Assert.IsTrue(shot.reflected, "The scheduled incident beam must touch the actual 7040-triangle surface.");
            Assert.IsTrue(shot.damageApplied, "The normal secondary query must reach a real compound ship hull.");
            Assert.AreSame(receiver, shot.hitShip);
            Assert.That(Vector3.Distance(expectedContact.point, shot.contactPoint), Is.LessThan(.005f));
            Assert.That(Vector3.Dot(expectedReflection, shot.reflectedDirection), Is.GreaterThan(.9999f));
            Assert.That(Vector3.Dot(expectedContact.normal, shot.contactNormal), Is.GreaterThan(.9999f));
            Assert.AreEqual(DamageSource.ReflectedLaser, receiver.LastHit.source);
            Assert.AreEqual(shot.attackId, receiver.LastHit.attackId);
            Assert.Greater(shot.attackId, 0);
            Assert.AreEqual(ShipDamageState.FatalPending, receiver.DamageState);
            Assert.AreEqual(ShipDamageState.Intact, emitter.DamageState);
            Assert.AreEqual(1, fatalEvents);
            Assert.AreEqual(1, lasers.ReflectionCount);
            Assert.AreEqual(1, lasers.ReflectedDamageCount);
            Assert.AreEqual(1, mission.PendingCount);
            Assert.AreEqual(0, mission.DestroyedCount);
            Assert.AreEqual(0, mission.score.Score);
            float explosionAt = receiver.ExplosionAt;
            Assert.That(explosionAt - combat.SimulatedTime,
                Is.InRange(originalScale.explosionDelaySeconds.x, originalScale.explosionDelaySeconds.y));

            // Fifteen more ordinary simulation/render-lifetime steps are still
            // the same attack, not fifteen damage applications or reset timers.
            for (int i = 0; i < 15; i++) mission.Step(.02f, default);
            Assert.AreEqual(1, lasers.ShotsFired);
            Assert.AreEqual(1, lasers.ReflectedDamageCount);
            Assert.AreEqual(1, fatalEvents);
            Assert.AreEqual(explosionAt, receiver.ExplosionAt);
            Assert.AreEqual(shot.attackId, receiver.LastHit.attackId);
            Assert.AreEqual(0, mission.DestroyedCount);
            Assert.AreEqual(0, mission.score.Score);

            int remainingSteps = Mathf.CeilToInt((explosionAt + .3f - combat.SimulatedTime) / .02f);
            for (int i = 0; i < remainingSteps; i++) mission.Step(.02f, default);
            Assert.AreEqual(ShipDamageState.Exploded, receiver.DamageState);
            Assert.AreEqual(DamageSource.ReflectedLaser, receiver.LastHit.source);
            Assert.AreEqual(shot.attackId, receiver.LastHit.attackId);
            Assert.AreEqual(1, fatalEvents);
            Assert.AreEqual(1, explosionEvents);
            Assert.AreEqual(1, lasers.ReflectedDamageCount);
            Assert.AreEqual(0, mission.PendingCount);
            Assert.AreEqual(1, mission.DestroyedCount);
            Assert.AreEqual(mission.settings.baseScore, mission.score.Score);
            Assert.AreEqual(12, mission.TotalCount, "No fleet reduction is part of this controlled test.");
            Debug.Log($"SAVED_HULL_REFLECTION controlledPlacement=true triangles={savedGeometry.TriangleCount} " +
                $"source={emitter.targetId} receiver={receiver.targetId} compoundVolumes={receiver.hitVolumes.Length} " +
                $"dropletEuler={chosenRotation} contact={shot.contactPoint} normal={shot.contactNormal} " +
                $"reflected={shot.reflectedDirection} attackId={shot.attackId} damageSource={receiver.LastHit.source} " +
                $"fatalEvents={fatalEvents} explosionEvents={explosionEvents} score={mission.score.Score}");
        }

        bool ArrangeAlongActualReflectedRay(ShipTarget emitter, ShipTarget receiver, BoxCollider receiverHull,
            Transform muzzle, out DropletSurfaceHit contact, out Vector3 reflected, out Vector3 chosenRotation)
        {
            contact = default; reflected = chosenRotation = default;
            var lasers = mission.lasers;
            var surface = lasers.dropletSurface;
            float range = mission.combat.scale.MetersToUnits(lasers.settings.rangeMeters);
            float offset = mission.combat.scale.MetersToUnits(lasers.settings.reflectionOffsetMeters);
            // Search only rigid poses of the real surface. Its vertices and
            // interpolated imported normals are never altered or aimed at a ship.
            foreach (float pitch in new[] { 0f, 25f, -25f })
            for (int yaw = 30; yaw < 360; yaw += 15)
            {
                Vector3 euler = new Vector3(pitch, yaw, 0);
                mission.motor.ResetPose(muzzle.position + Vector3.forward * 120, Quaternion.Euler(euler));
                Vector3 incoming = (surface.AimPoint - muzzle.position).normalized;
                Vector3 origin = muzzle.position + incoming * offset;
                if (!surface.Raycast(new Ray(origin, incoming), range, out var candidate)) continue;
                Vector3 direction = LaserGeometryMath.ReflectDirection(incoming, candidate.normal);
                Vector3 centerOnRay = candidate.point + direction * 160;
                // Keep the receiving hull well outside the incident path. Its
                // orientation faces away, preserving a meaningful turret turn.
                if (Vector3.Cross(incoming, centerOnRay - muzzle.position).magnitude < 70) continue;
                receiver.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                Vector3 oldCenter = receiverHull.transform.TransformPoint(receiverHull.center);
                receiver.transform.position += centerOnRay - oldCenter;
                mission.combat.ResetSweepHistory();
                bool incidentBlocked = mission.combat.RaycastShips(origin, incoming, range, out var first)
                    && first.distance <= candidate.distance;
                if (incidentBlocked) continue;
                Vector3 bounceOrigin = LaserGeometryMath.OffsetReflectionOrigin(candidate.point,
                    candidate.normal, direction, offset);
                if (!mission.combat.RaycastShips(bounceOrigin, direction, range - candidate.distance, out var secondary)
                    || secondary.ship != receiver) continue;
                contact = candidate; reflected = direction; chosenRotation = euler;
                return true;
            }
            return false;
        }
    }
}
