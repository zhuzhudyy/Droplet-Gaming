using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class FleetNormalThreatTests
    {
        GameObject root;
        FleetCombatSimulation simulation;
        CombatScaleSettings scale;
        [SetUp] public void Setup()
        {
            root = new GameObject("Normal fleet threat fixture");
            simulation = root.AddComponent<FleetCombatSimulation>();
            scale = ScriptableObject.CreateInstance<CombatScaleSettings>();
            scale.engagementMetersPerSecond = 0;
            scale.fleetLossRetreatFraction = 1;
            // Exercise saved legacy settings as well as the new default.
            scale.reactionDelaySeconds = new Vector2(.7f, 5.5f);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(scale); }
        ShipTarget Ship(string id, Vector3 position)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform); go.transform.position = position;
            var ship = go.AddComponent<ShipTarget>(); ship.targetId = id;
            ship.hitVolumes = new Collider[] { go.AddComponent<BoxCollider>() };
            return ship;
        }
        void Advance(float seconds)
        { for (int i = 0; i < Mathf.RoundToInt(seconds / .02f); i++) { simulation.BeginStep(.02f); simulation.EndStep(.02f); } }
        void Penetrate(ShipTarget victim)
        {
            var p = victim.transform.position;
            Assert.AreEqual(1, simulation.SweepSegment(p - Vector3.forward * 5, p + Vector3.forward * 5, .1f, 1500));
        }

        [Test] public void NormalPenetrationHasBoundedVariedReactionAndRealDeparture()
        {
            var victim = Ship("Victim", Vector3.zero);
            var witnesses = Enumerable.Range(0, 12).Select(i => Ship("Witness-" + i,
                new Vector3(100 + i * 30, i * 5, 100))).ToArray();
            simulation.Configure(new[] { victim }.Concat(witnesses).ToArray(), null, scale);
            var positions = witnesses.Select(s => s.transform.position).ToArray();
            var notices = new List<FleetThreatNotice>(); simulation.ThreatScheduled += notices.Add;
            var retreats = new List<CombatEvent>(); simulation.EventRaised += e => { if (e.kind == CombatEventKind.RetreatOrdered) retreats.Add(e); };
            Penetrate(victim);
            Assert.AreEqual(12, notices.Count);
            Assert.IsTrue(notices.All(n => n.cause == FleetThreatCause.NearbyPenetration && n.reactionAt - n.time >= 1 && n.reactionAt - n.time <= 3));
            Advance(.98f);
            Assert.IsTrue(witnesses.All(s => s.BehaviorState == ShipBehaviorState.Engaging || s.BehaviorState == ShipBehaviorState.Formation));
            Advance(2.04f);
            Assert.AreEqual(12, retreats.Count);
            Assert.Greater(retreats.Max(e => e.simulationTime) - retreats.Min(e => e.simulationTime), .2f);
            Assert.IsTrue(witnesses.All(s => s.Velocity.magnitude > 0));
            Advance(7);
            for (int i = 0; i < witnesses.Length; i++)
            {
                Assert.AreEqual(ShipBehaviorState.Fleeing, witnesses[i].BehaviorState);
                Assert.Greater(Vector3.Distance(positions[i], witnesses[i].transform.position), 70,
                    "A behavioral label alone cannot pass; the intact hull must physically depart.");
            }
            Assert.AreEqual(13, simulation.TotalCount); Assert.AreEqual(1, simulation.ExplodedCount);
        }

        [TestCase(false)] [TestCase(true)]
        public void ExplosionThreatUsesEveryShipEndpointRegardlessOfArrayOrder(bool reverseOrder)
        {
            scale.explosionDelaySeconds = Vector2.one;
            scale.fleeAccelerationMetersPerSecondSquared = 0;
            var victim = Ship("Explosion", Vector3.zero);
            var crossing = Ship("Incoming witness", Vector3.right * 2050);
            var distant = Ship("Unthreatened", Vector3.right * 20000);
            var fleet = new[] { victim, crossing, distant };
            if (reverseOrder) System.Array.Reverse(fleet);
            simulation.Configure(fleet, null, scale);
            simulation.SetShipVelocity(crossing, Vector3.left * 1000);
            var notices = new List<FleetThreatNotice>(); simulation.ThreatScheduled += notices.Add;
            Penetrate(victim);
            Assert.AreEqual(0, notices.Count, "Witness starts outside the 1750 UU threat radius.");
            simulation.BeginStep(1); simulation.EndStep(1);
            Assert.That(crossing.transform.position.x, Is.EqualTo(1050).Within(.01));
            Assert.AreEqual(1, notices.Count);
            Assert.AreSame(crossing, notices[0].ship);
            Assert.AreEqual(FleetThreatCause.NearbyExplosion, notices[0].cause);
        }

        [Test] public void PendingHullCannotResumeRetreatAndStillExplodesOnce()
        {
            var ship = Ship("Fatal", Vector3.zero);
            simulation.Configure(new[] { ship }, null, scale);
            int retreats = 0, explosions = 0;
            simulation.ThreatScheduled += _ => retreats++;
            ship.Destroyed += _ => explosions++;
            Penetrate(ship);
            simulation.RequestRetreat(ship);
            Advance(1);
            Assert.AreEqual(ShipDamageState.FatalPending, ship.DamageState);
            Assert.IsFalse(ship.CanAttack); Assert.AreEqual(0, retreats);
            Assert.Greater(ship.transform.position.z, 0, "The disabled hull retains impact drift.");
            Advance(5);
            Assert.AreEqual(1, explosions); Assert.AreEqual(0, simulation.EscapedCount);
        }

        [Test] public void DiagnosticMovementSurvivesOriginShiftAndThreeResets()
        {
            var victim = Ship("Victim", Vector3.zero);
            var witness = Ship("Witness", Vector3.right * 200);
            var untouched = Ship("Untouched", Vector3.right * 20000);
            simulation.Configure(new[] { victim, witness, untouched }, null, scale);
            var diagnostics = root.AddComponent<FleetEscapeDiagnostics>();
            diagnostics.simulation = simulation; diagnostics.captureEnabled = true; diagnostics.Bind();
            for (int round = 0; round < 3; round++)
            {
                Penetrate(victim); Advance(3.02f);
                simulation.ShiftOrigin(new Vector3(80000, -15000, 90000));
                Advance(7);
                Assert.AreEqual(3, diagnostics.Samples.Count);
                CollectionAssert.AreEqual(new[] { 0, 3, 10 }, diagnostics.Samples.Select(s => s.checkpointSeconds));
                var final = diagnostics.Samples[2];
                Assert.Greater(final.displacementMeters.magnitude, 7000);
                Assert.Less(final.displacementMeters.magnitude, 100000, "Origin rebasing must not be counted as ship flight.");
                Assert.That(final.transformErrorMeters, Is.LessThan(.1f));
                simulation.ResetSimulation(); Assert.AreEqual(0, diagnostics.Samples.Count);
                Assert.AreEqual(0, simulation.PendingCount); Assert.AreEqual(3, simulation.IntactCount);
            }
        }

        [Test] public void NormalThreatEvacuationCountsEscapeWithoutAwardingKill()
        {
            scale.escapeRadiusMeters = 10000;
            var victim = Ship("Victim", Vector3.zero);
            var witness = Ship("Escaping witness", new Vector3(0, 0, 90));
            simulation.Configure(new[] { victim, witness }, null, scale);
            int witnessKills = 0; witness.Destroyed += _ => witnessKills++;
            Penetrate(victim); Advance(12);
            Assert.IsTrue(witness.IsEscaped);
            Assert.AreEqual(1, simulation.EscapedCount); Assert.AreEqual(0, witnessKills);
            Assert.AreEqual(1, simulation.ExplodedCount); Assert.AreEqual(0, simulation.IntactCount);
        }
    }
}
