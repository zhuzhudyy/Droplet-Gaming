using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class NarrativeFleetTests
    {
        GameObject root;
        CombatScaleSettings scale;
        FleetCombatSimulation simulation;

        [SetUp] public void SetUp()
        {
            root = new GameObject("NarrativeFleetTest");
            scale = ScriptableObject.CreateInstance<CombatScaleSettings>();
            scale.fleeAccelerationMetersPerSecondSquared = 0;
            scale.engagementMetersPerSecond = 0;
            scale.explosionDelaySeconds = new Vector2(2, 2);
            scale.fleetLossRetreatFraction = 1;
            simulation = root.AddComponent<FleetCombatSimulation>(); simulation.scale = scale;
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(scale); }
        ShipTarget Ship(string id, Vector3 position, Vector3 dimensions, bool compound = false)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform); go.transform.position = position;
            var ship = go.AddComponent<ShipTarget>(); ship.targetId = id;
            var box = go.AddComponent<BoxCollider>(); box.size = dimensions;
            if (compound)
            {
                var second = go.AddComponent<BoxCollider>(); second.size = dimensions * .8f;
                ship.hitVolumes = new Collider[] { box, second };
            }
            else ship.hitVolumes = new Collider[] { box };
            ship.visualRoot = new GameObject("VisualRoot"); ship.visualRoot.transform.SetParent(go.transform, false);
            return ship;
        }

        [Test] public void ScaleUsesPhysicalSpeedAndDistanceWithoutHudMultiplier()
        {
            Assert.That(scale.MetersToUnits(30000), Is.EqualTo(300).Within(.001));
            Assert.That(scale.UnitsToMeters(57.564f), Is.EqualTo(5756.4f).Within(.01));
            float meters = 100000, seconds = meters / scale.cruiseMetersPerSecond;
            Assert.That(scale.MetersToUnits(meters) / scale.MetersToUnits(scale.cruiseMetersPerSecond), Is.EqualTo(seconds).Within(.00001));
            Assert.Less(scale.escapeRadiusMeters, scale.arenaBoundaryMeters);
        }

        [Test] public void CrossingMovingShipHitsAlthoughBothEndpointHullsMissPath()
        {
            var ship = Ship("Crossing", new Vector3(-10, 0, 0), new Vector3(.5f, 2, .5f));
            simulation.Configure(new[] { ship }, null, scale); simulation.SetShipVelocity(ship, Vector3.right * 20);
            simulation.BeginStep(1);
            Assert.AreEqual(1, simulation.SweepSegment(new Vector3(0, 0, -10), new Vector3(0, 0, 10), .1f, 20));
            simulation.EndStep(1);
            Assert.AreEqual(ShipDamageState.FatalPending, ship.DamageState);
            Assert.That(ship.transform.position.x, Is.EqualTo(10).Within(.001));
            Assert.IsFalse(ship.IsDestroyed);
        }

        [Test] public void TemporalSubsegmentDoesNotUseWholeShipStepTwice()
        {
            var ship = Ship("Fractions", new Vector3(-10, 0, 0), Vector3.one);
            simulation.Configure(new[] { ship }, null, scale); simulation.SetShipVelocity(ship, Vector3.right * 20);
            simulation.BeginStep(1);
            Assert.AreEqual(0, simulation.SweepSegment(new Vector3(0, 0, -10), new Vector3(0, 0, -5), .1f, 20, 0, .25f));
            Assert.AreEqual(1, simulation.SweepSegment(new Vector3(0, 0, -5), new Vector3(0, 0, 5), .1f, 20, .25f, .75f));
            simulation.EndStep(1);
        }

        [Test] public void RotatingLongHullUsesControlledSubstepsAndRetainsCompoundIdentity()
        {
            var ship = Ship("Turning", Vector3.zero, new Vector3(1, 1, 20), true);
            ship.transform.rotation = Quaternion.Euler(0, 90, 0);
            scale.fleeTurnDegreesPerSecond = 90;
            simulation.evacuationCenter = new Vector3(0, 0, -100);
            simulation.Configure(new[] { ship }, null, scale); simulation.RequestRetreat(ship);
            simulation.BeginStep(1);
            Assert.AreEqual(1, simulation.SweepSegment(new Vector3(7, 0, 7), new Vector3(7, 0, 7), .1f, 0));
            Assert.Greater(simulation.LastSweepSubsteps, 1);
            Assert.AreEqual(1, simulation.PendingCount);
            Assert.AreEqual(0, simulation.SweepSegment(new Vector3(7, 0, 7), new Vector3(7, 0, 7), .1f, 0));
            simulation.EndStep(1);
        }

        [Test] public void DamageDeadlineIsIdempotentAndResetCannotCommitPreviousGeneration()
        {
            var ship = Ship("Delayed", Vector3.zero, Vector3.one, true);
            simulation.Configure(new[] { ship }, null, scale);
            int kills = 0; ship.Destroyed += _ => kills++;
            var hit = new ShipHitContext(Vector3.zero, Vector3.forward, 300);
            Assert.IsTrue(simulation.ApplyDamage(ship, hit, DamageSource.ReflectedLaser, 4));
            float deadline = ship.ExplosionAt;
            Assert.IsFalse(simulation.ApplyDamage(ship, hit, DamageSource.Penetration, 5));
            Assert.AreEqual(deadline, ship.ExplosionAt); Assert.AreEqual(DamageSource.ReflectedLaser, ship.LastHit.source);
            Assert.IsTrue(ship.visualRoot.activeSelf); Assert.IsFalse(ship.CanAttack);
            simulation.BeginStep(1); simulation.EndStep(1);
            Assert.AreEqual(0, kills); Assert.AreEqual(1, simulation.PendingCount);
            int generation = simulation.Generation;
            simulation.ResetSimulation();
            Assert.Greater(simulation.Generation, generation);
            simulation.BeginStep(3); simulation.EndStep(3);
            Assert.AreEqual(0, kills); Assert.AreEqual(0, simulation.PendingCount); Assert.AreEqual(1, simulation.IntactCount);
            Assert.IsTrue(simulation.ApplyDamage(ship, hit, DamageSource.Penetration, 6));
            simulation.BeginStep(2); simulation.EndStep(2);
            Assert.AreEqual(1, kills); Assert.AreEqual(1, simulation.ExplodedCount); Assert.AreEqual(0, simulation.PendingCount);
            simulation.BeginStep(2); simulation.EndStep(2); Assert.AreEqual(1, kills);
        }

        [Test] public void RayGeometryIgnoresLodColliderEnableAndFindsNearestHull()
        {
            var near = Ship("Near", new Vector3(0, 0, 10), Vector3.one * 2);
            var far = Ship("Far", new Vector3(0, 0, 20), Vector3.one * 2);
            simulation.Configure(new[] { near, far }, null, scale);
            near.hitVolumes[0].enabled = false; near.visualRoot.SetActive(false);
            Assert.IsTrue(simulation.RaycastShips(Vector3.zero, Vector3.forward, 100, out var result));
            Assert.AreSame(near, result.ship); Assert.That(result.distance, Is.EqualTo(9).Within(.001));
            Assert.That(Vector3.Dot(result.normal, Vector3.back), Is.GreaterThan(.999));
            Assert.IsTrue(simulation.RaycastShips(Vector3.zero, Vector3.forward, 100, out result, near)); Assert.AreSame(far, result.ship);
        }

        [Test] public void EscapingCrossesFiniteBoundaryAndNeverAwardsDestruction()
        {
            var ship = Ship("Escaper", new Vector3(0, 0, 9), Vector3.one);
            scale.escapeRadiusMeters = 1000; scale.arenaBoundaryMeters = 2000;
            simulation.Configure(new[] { ship }, null, scale); simulation.SetShipVelocity(ship, Vector3.forward * 2);
            simulation.RequestRetreat(ship); int kills = 0; ship.Destroyed += _ => kills++;
            for (int i = 0; i < 40 && !ship.IsEscaped; i++) { simulation.BeginStep(.1f); simulation.EndStep(.1f); }
            Assert.IsTrue(ship.IsEscaped); Assert.AreEqual(1, simulation.EscapedCount); Assert.AreEqual(0, simulation.IntactCount);
            Assert.IsFalse(ship.IsDestroyed); Assert.AreEqual(0, kills);
            Assert.GreaterOrEqual(ship.transform.position.magnitude, scale.MetersToUnits(scale.escapeRadiusMeters));
        }

        [Test] public void NearbyPanicHasDifferentReactionTimesAndExplosionWitnessIsAlive()
        {
            var ships = new[] { Ship("Victim", Vector3.zero, Vector3.one), Ship("Witness-A", Vector3.right * 20, Vector3.one), Ship("Witness-B", Vector3.left * 20, Vector3.one) };
            simulation.Configure(ships, null, scale);
            var events = new List<CombatEvent>(); simulation.EventRaised += events.Add;
            simulation.ApplyDamage(ships[0], new ShipHitContext(Vector3.zero, Vector3.forward, 300), DamageSource.Penetration, 1);
            simulation.BeginStep(2); simulation.EndStep(2);
            var exploded = events.Find(e => e.kind == CombatEventKind.ShipExploded);
            Assert.AreSame(ships[0], exploded.subject); Assert.IsNotNull(exploded.speaker); Assert.IsFalse(exploded.speaker.IsResolved);
            Assert.IsTrue(events.Exists(e => e.kind == CombatEventKind.RescueRequested));
            for (int i = 0; i < 70; i++) { simulation.BeginStep(.1f); simulation.EndStep(.1f); }
            var retreats = events.FindAll(e => e.kind == CombatEventKind.RetreatOrdered);
            Assert.AreEqual(2, retreats.Count);
            Assert.That(Mathf.Abs(retreats[0].simulationTime - retreats[1].simulationTime), Is.GreaterThan(.09f));
            simulation.ResetSimulation(); Assert.IsFalse(ships[0].IsDestroyed);
        }
    }
}
