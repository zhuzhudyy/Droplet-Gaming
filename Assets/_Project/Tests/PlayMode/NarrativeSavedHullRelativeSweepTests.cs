using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>
    /// Saved-frigate geometry regression, deliberately using a 1.5-second stress
    /// step, not claiming this is the ordinary .02-second FixedUpdate cadence.
    /// At the configured 8 km/s escape speed this gives 120 UU of hull travel,
    /// enough for both endpoint hulls to miss the complete 150 km/s player path.
    /// </summary>
    public sealed class NarrativeSavedHullRelativeSweepTests
    {
        MissionController mission;
        CombatScaleSettings originalScale, isolatedScale;

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
            Assert.IsNotNull(mission); Assert.AreEqual(12, mission.TotalCount);
            mission.enabled = false; mission.input.enabled = false;
            mission.StartCombat(); mission.motor.SimulationEnabled = false;
            originalScale = mission.combat.scale;
            isolatedScale = Object.Instantiate(originalScale);
            isolatedScale.name = "TemporarySavedHullRelativeSweepScale";
            // Preserve physical scale, weapon/motor dimensions and 2-5 second delay.
            // Only suppress speed adjustment so the controlled transverse velocity
            // remains exactly the configured 8 km/s during the oversized step.
            isolatedScale.fleeAccelerationMetersPerSecondSquared = 0;
            isolatedScale.engagementMetersPerSecond = 0;
            isolatedScale.fleetLossRetreatFraction = 1;
            Assert.AreEqual(100, isolatedScale.metersPerUnityUnit);
            Assert.AreEqual(150000, isolatedScale.sprintMetersPerSecond);
            Assert.AreEqual(8000, isolatedScale.fleeMaxMetersPerSecond);
        }

        [TearDown]
        public void RestoreInMemoryConfiguration()
        {
            Time.timeScale = 1;
            if (mission != null)
            {
                mission.combat.scale = originalScale;
                mission.Restart();
            }
            if (isolatedScale != null) Object.DestroyImmediate(isolatedScale);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SavedNineVolumeFrigateCrosses150KmPerSecondSweepWithLodInteractionOff(bool turning)
        {
            var simulation = mission.combat;
            var target = mission.targets[0];
            string[] idsBefore = mission.targets.Select(t => t.targetId).ToArray();
            var originalVolumes = target.hitVolumes.ToArray();
            Assert.AreEqual(9, originalVolumes.Length);
            Assert.AreEqual(4, originalVolumes.OfType<BoxCollider>().Count(), "Retain the four authored hull boxes.");
            Assert.AreEqual(5, originalVolumes.OfType<CapsuleCollider>().Count(), "Retain the five authored engine capsules.");
            Assert.That(Vector3.Distance(target.transform.lossyScale, Vector3.one), Is.LessThan(.0001f));

            // Isolate one existing frigate far from the other eleven. This changes
            // its test pose only; no prefab, scene asset, mesh or collider is edited.
            const float stepSeconds = 1.5f;
            Vector3 crossingRoot = new Vector3(10000, 5000, -2000);
            Vector3 shipVelocity = Vector3.right * isolatedScale.MetersToUnits(isolatedScale.fleeMaxMetersPerSecond);
            Vector3 startRoot = crossingRoot - shipVelocity * (stepSeconds * .5f);
            Quaternion startRotation = Quaternion.identity;
            target.transform.SetPositionAndRotation(startRoot, startRotation);
            var hull = originalVolumes.OfType<BoxCollider>().OrderByDescending(b => b.size.x * b.size.y * b.size.z).First();
            Vector3 localHullCenter = target.transform.InverseTransformPoint(hull.transform.TransformPoint(hull.center));

            simulation.Configure(mission.targets, mission.motor.transform, isolatedScale);
            simulation.SetShipVelocity(target, shipVelocity);
            Quaternion endRotation = startRotation;
            if (turning)
            {
                simulation.RequestRetreat(target, 0);
                // No other hull is within avoidance range at this isolated pose.
                // Compute the public-config prediction independently so the path
                // crosses the center of a real saved box at the temporal midpoint.
                Vector3 outward = startRoot - simulation.evacuationCenter;
                Vector3 away = startRoot - mission.motor.transform.position;
                Vector3 escapeDirection = (outward.normalized * 1.5f + away.normalized).normalized;
                endRotation = Quaternion.RotateTowards(startRotation,
                    Quaternion.LookRotation(escapeDirection, Vector3.up),
                    isolatedScale.fleeTurnDegreesPerSecond * stepSeconds);
            }
            Quaternion midpointRotation = Quaternion.Slerp(startRotation, endRotation, .5f);
            Vector3 contactCenter = crossingRoot + midpointRotation * localHullCenter;
            float configuredPlayerSpeed = isolatedScale.MetersToUnits(isolatedScale.sprintMetersPerSecond);
            Vector3 from = contactCenter - Vector3.forward * (configuredPlayerSpeed * stepSeconds * .5f);
            Vector3 to = contactCenter + Vector3.forward * (configuredPlayerSpeed * stepSeconds * .5f);
            float pathLength = Vector3.Distance(from, to);
            float measuredPathSpeed = pathLength / stepSeconds;
            Assert.That(isolatedScale.UnitsToMeters(measuredPathSpeed), Is.EqualTo(150000).Within(2));
            Assert.That(isolatedScale.UnitsToMeters(shipVelocity.magnitude), Is.EqualTo(8000).Within(.01));

            var renderer = simulation.fleetRenderer;
            renderer.RefreshNow();
            Assert.IsTrue(renderer.IsInstanced(target), "The real saved renderer must place this isolated hull in distant LOD.");
            Assert.IsFalse(renderer.IsInteractionActive(target));
            Assert.IsTrue(target.hitVolumes.All(c => !c.enabled));
            Assert.IsFalse(simulation.RaycastShips(from, Vector3.forward, pathLength, out _),
                "The complete player path must miss every hull at the initial pose.");

            int fatalNotifications = 0;
            System.Action<ShipTarget> onFatal = ship => { if (ship == target) fatalNotifications++; };
            target.FatalDamage += onFatal;
            try
            {
                simulation.BeginStep(stepSeconds);
                Vector3 midpoint = Vector3.Lerp(from, to, .5f);
                int first = simulation.SweepSegment(from, midpoint, mission.settings.hitRadius, measuredPathSpeed, 0, .5f);
                int firstSubsteps = simulation.LastSweepSubsteps;
                int second = simulation.SweepSegment(midpoint, to, mission.settings.hitRadius, measuredPathSpeed, .5f, 1);
                Assert.AreEqual(1, first + second, "Temporal relative motion must find one stable ship across both motor-style subsegments.");
                if (turning) Assert.Greater(firstSubsteps, 1, "A 24-degree hull turn must exercise controlled angular substeps.");
                Assert.AreEqual(0, simulation.SweepSegment(from, to, mission.settings.hitRadius, measuredPathSpeed, 0, 1));
                simulation.EndStep(stepSeconds);

                Assert.That(Vector3.Distance(target.transform.position, startRoot + shipVelocity * stepSeconds), Is.LessThan(.01));
                Assert.IsFalse(simulation.RaycastShips(from, Vector3.forward, pathLength, out _),
                    "The final hull must also miss the whole player path; scanning only final ship positions cannot pass this test.");
                Assert.AreEqual(1, fatalNotifications); Assert.AreEqual(1, simulation.PendingCount);
                Assert.AreEqual(ShipDamageState.FatalPending, target.DamageState);
                Assert.AreEqual(DamageSource.Penetration, target.LastHit.source);
                Assert.IsFalse(target.IsDestroyed); Assert.AreEqual(0, mission.DestroyedCount);
                Assert.AreEqual(12, mission.TotalCount); Assert.AreEqual(12, simulation.TotalCount);
                CollectionAssert.AreEqual(idsBefore, mission.targets.Select(t => t.targetId).ToArray());
                CollectionAssert.AreEqual(originalVolumes, target.hitVolumes);
                Assert.IsTrue(target.hitVolumes.All(c => !c.enabled), "Relative detection must not silently reactivate streamed physics colliders.");
                if (turning) Assert.That(Quaternion.Angle(startRotation, target.transform.rotation),
                    Is.EqualTo(isolatedScale.fleeTurnDegreesPerSecond * stepSeconds).Within(.02));
            }
            finally { target.FatalDamage -= onFatal; }
        }
    }
}
