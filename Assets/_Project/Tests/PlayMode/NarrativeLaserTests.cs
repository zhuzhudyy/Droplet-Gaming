using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class NarrativeLaserTests
    {
        readonly List<Object> owned = new List<Object>();
        CombatScaleSettings scale;
        LaserWeaponSettings settings;
        DropletReflectionGeometry geometry;
        FleetCombatSimulation simulation;
        FleetLaserDirector director;
        DropletReflectiveSurface surface;
        GameObject Node(string name) { var node = new GameObject(name); owned.Add(node); return node; }
        T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); return asset; }
        [SetUp]
        public void Setup()
        {
            scale = Asset<CombatScaleSettings>(); settings = Asset<LaserWeaponSettings>(); geometry = Asset<DropletReflectionGeometry>();
            geometry.SetGeometry(new[] { new Vector3(-2, -2, 0), new Vector3(2, -2, 0), new Vector3(0, 2, 0) },
                new[] { Vector3.back, Vector3.back, Vector3.back }, new[] { 0, 2, 1 });
            var droplet = Node("Dedicated mirror surface"); surface = droplet.AddComponent<DropletReflectiveSurface>();
            surface.Configure(droplet.transform, droplet.transform, geometry);
            simulation = Node("Fleet simulation").AddComponent<FleetCombatSimulation>();
            director = Node("Fleet lasers").AddComponent<FleetLaserDirector>();
            director.simulation = simulation; director.settings = settings; director.dropletSurface = surface;
        }
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        ShipTarget Ship(string id, Vector3 position)
        {
            var root = Node(id); root.transform.position = position;
            var ship = root.AddComponent<ShipTarget>(); ship.targetId = id;
            var box = root.AddComponent<BoxCollider>(); box.size = Vector3.one * 2;
            ship.hitVolumes = new Collider[] { box };
            var muzzle = new GameObject("LaserMuzzle"); muzzle.transform.SetParent(root.transform, false); muzzle.transform.localPosition = Vector3.forward * 2;
            return ship;
        }

        [Test]
        public void EmitterCanBeHitByOneReflectionAndDamageCommitsOnlyOnce()
        {
            var emitter = Ship("EMITTER", Vector3.back * 12);
            simulation.Configure(new[] { emitter }, surface.transform, scale);
            var shot = director.FireRay(emitter, Vector3.back * 10, Vector3.forward, 41);
            Assert.IsTrue(shot.reflected); Assert.IsTrue(shot.damageApplied); Assert.AreSame(emitter, shot.hitShip);
            Assert.That(Vector3.Angle(shot.reflectedDirection, Vector3.back), Is.LessThan(.001f));
            Assert.AreEqual(ShipDamageState.FatalPending, emitter.DamageState);
            Assert.AreEqual(DamageSource.ReflectedLaser, emitter.LastHit.source); Assert.AreEqual(41, emitter.LastHit.attackId);
            float deadline = emitter.ExplosionAt;
            var repeated = director.FireRay(emitter, Vector3.back * 10, Vector3.forward, 41);
            Assert.IsFalse(repeated.damageApplied); Assert.AreEqual(1, simulation.PendingCount); Assert.AreEqual(deadline, emitter.ExplosionAt);
            Assert.IsFalse(emitter.IsDestroyed, "Reflection uses delayed unified destruction, not immediate model removal.");
        }

        [Test]
        public void DisabledDistantHullStillOccludesLaserAndDamageSourceIsDirect()
        {
            var emitter = Ship("EMITTER", Vector3.back * 12); var blocker = Ship("BLOCKER", Vector3.back * 5);
            simulation.Configure(new[] { emitter, blocker }, surface.transform, scale);
            foreach (var volume in blocker.hitVolumes) volume.enabled = false;
            settings.directLaserIsLethal = true;
            var shot = director.FireRay(emitter, Vector3.back * 10, Vector3.forward, 5);
            Assert.IsFalse(shot.reflected); Assert.AreSame(blocker, shot.hitShip); Assert.IsTrue(shot.damageApplied);
            Assert.AreEqual(DamageSource.DirectLaser, blocker.LastHit.source);
            Assert.AreEqual(ShipDamageState.Intact, emitter.DamageState);
        }

        [Test]
        public void AimThroughOwnHullIsBlockedBeforeDroplet()
        {
            var emitter = Ship("SELF OCCLUSION", Vector3.back * 8);
            simulation.Configure(new[] { emitter }, surface.transform, scale);
            var shot = director.FireRay(emitter, Vector3.back * 10, Vector3.forward, 7);
            Assert.IsFalse(shot.reflected); Assert.AreSame(emitter, shot.hitShip); Assert.IsFalse(shot.damageApplied);
        }

        [Test]
        public void PoolIsBoundedAndZeroSimulationTimeFreezesSnapshots()
        {
            settings.visualBeamBudget = 2;
            var pool = Node("Laser pool").AddComponent<LaserBeamPool>(); pool.settings = settings; pool.scale = scale;
            pool.InitializePool();
            Assert.IsTrue(pool.Show(Vector3.back, Vector3.zero, Vector3.right, true));
            Assert.IsTrue(pool.Show(Vector3.left, Vector3.zero, Vector3.up, true));
            Assert.IsFalse(pool.Show(Vector3.down, Vector3.zero, Vector3.forward, true));
            Assert.AreEqual(2, pool.ActiveCount); Assert.AreEqual(2, pool.Capacity); Assert.AreEqual(1, pool.DroppedVisualCount);
            pool.Step(0); Assert.AreEqual(2, pool.ActiveCount);
            pool.Step(settings.beamSeconds + .01f); Assert.AreEqual(0, pool.ActiveCount);
            pool.ResetEffects(); Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(4, pool.GetComponentsInChildren<LineRenderer>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Light>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void CentralSchedulerHonorsRayBudgetAndCannotFireFromFatalShip()
        {
            var ships = new ShipTarget[32];
            for (int i = 0; i < ships.Length; i++) ships[i] = Ship("BUDGET-" + i, new Vector3((i - 16) * 4, 0, -50));
            simulation.Configure(ships, surface.transform, scale); director.ResetWeapons();
            for (int i = 0; i < 300; i++)
            {
                simulation.BeginStep(.02f); simulation.EndStep(.02f); director.Step(.02f);
                Assert.LessOrEqual(director.LastRayQueries, settings.rayQueriesPerStep);
            }
            Assert.Greater(director.ShotsFired, 0);
            int previous = director.ShotsFired;
            foreach (var ship in ships) simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0), DamageSource.Penetration, 900);
            simulation.BeginStep(.02f); simulation.EndStep(.02f); director.Step(.02f);
            Assert.AreEqual(previous, director.ShotsFired);
            simulation.ResetSimulation(); director.ResetWeapons();
            Assert.AreEqual(0, director.ShotsFired); Assert.AreEqual(0, director.ReflectionCount);
        }
    }
}
