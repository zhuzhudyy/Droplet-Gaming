using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class CinematicCombatEventTests
    {
        GameObject root;
        FleetCombatSimulation fleet;
        CombatScaleSettings scale;
        ShipTarget ship;
        readonly List<CombatEvent> events = new List<CombatEvent>();
        [SetUp] public void Setup()
        {
            root = new GameObject("Cinematic event fixture");
            fleet = root.AddComponent<FleetCombatSimulation>();
            scale = ScriptableObject.CreateInstance<CombatScaleSettings>();
            scale.engagementMetersPerSecond = 0;
            var hull = new GameObject("Stable hull"); hull.transform.SetParent(root.transform);
            ship = hull.AddComponent<ShipTarget>(); ship.targetId = "Persistent-0001";
            ship.hitVolumes = new Collider[] { hull.AddComponent<BoxCollider>() };
            fleet.Configure(new[] {ship}, null, scale);
            events.Clear(); fleet.EventRaised += events.Add;
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(scale); }

        [Test] public void EventSnapshotSurvivesIdentityMutationOriginShiftAndRestart()
        {
            fleet.EmitEvent(CombatEventKind.WeaponFired, ship, null, new Vector3(10, 20, 30),
                DamageSource.DirectLaser, Vector3.forward, targetId: "Droplet");
            var snapshot = events.Single();
            Assert.Greater(snapshot.eventId, 0); Assert.AreEqual("Persistent-0001", snapshot.attackerId);
            Assert.AreEqual("Droplet", snapshot.targetId);
            ship.targetId = "Recycled-0002";
            var shift = new Vector3(1000, -200, 300);
            fleet.ShiftOrigin(shift);
            Assert.AreEqual(new Vector3(10,20,30) - shift, snapshot.PositionAtOrigin(fleet.AccumulatedOriginOffset));
            Assert.AreEqual("Persistent-0001", snapshot.attackerId, "The event must not read a reused object's current identity.");
            fleet.ResetSimulation();
            fleet.EmitEvent(CombatEventKind.WeaponFired, ship, null, Vector3.zero);
            Assert.AreNotEqual(snapshot.generation, events.Last().generation);
            Assert.AreNotEqual(snapshot.eventId, events.Last().eventId);
        }

        [Test] public void DelayedDestructionEventsAreUniqueOrderedAndDoNotAdvanceDeadline()
        {
            int destroyed = 0; ship.Destroyed += _ => destroyed++;
            Assert.IsTrue(fleet.ApplyDamage(ship, new ShipHitContext(Vector3.zero, Vector3.forward, 1500), DamageSource.Penetration, 1));
            float deadline = ship.ExplosionAt;
            Assert.That(deadline, Is.InRange(2f, 5f));
            var penetration = events.First(e => e.kind == CombatEventKind.HullPenetrated);
            Assert.AreEqual("Droplet", penetration.attackerId);
            Assert.AreEqual(ship.targetId, penetration.targetId);
            Assert.AreEqual(ShipDamageState.FatalPending, penetration.damageState);
            Assert.AreEqual(Vector3.forward, penetration.direction);
            for (int i = 0; i < 100; i++) fleet.EmitEvent(CombatEventKind.WeaponFired, ship, null, Vector3.zero);
            Assert.AreEqual(deadline, ship.ExplosionAt); Assert.AreEqual(0, destroyed);
            while(fleet.SimulatedTime + .02f < deadline) { fleet.BeginStep(.02f); fleet.EndStep(.02f); }
            Assert.AreEqual(0, destroyed);
            fleet.BeginStep(.021f); fleet.EndStep(.021f);
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(1, events.Count(e => e.kind == CombatEventKind.ShipExploded));
            Assert.AreEqual(1, events.Count(e => e.kind == CombatEventKind.CommunicationInterrupted));
            Assert.AreEqual(events.Count, events.Select(e => e.eventId).Distinct().Count());
            Assert.AreEqual(ShipDamageState.FatalPending, penetration.damageState, "Immutable state is captured at hit time.");
            Assert.Less(events.FindIndex(e => e.kind == CombatEventKind.CommunicationInterrupted),
                events.FindIndex(e => e.kind == CombatEventKind.ShipExploded));
        }
    }
}
