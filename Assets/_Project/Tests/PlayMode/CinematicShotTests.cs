using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class CinematicShotTests
    {
        readonly List<Object> owned = new List<Object>();
        DropletMotor motor;
        ChaseCamera chase;
        FleetCombatSimulation simulation;
        CombatScaleSettings scale;
        ShotDirector director;
        ShipTarget ship;
        Light previousSun;
        GameObject Node(string name) { var node = new GameObject(name); owned.Add(node); return node; }
        T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); return asset; }
        ShipTarget Ship(string id, Vector3 point, Vector3 size)
        {
            var node = Node(id); node.transform.position = point;
            var target = node.AddComponent<ShipTarget>(); target.targetId = id;
            var collider = node.AddComponent<BoxCollider>(); collider.size = size;
            target.hitVolumes = new Collider[] { collider }; return target;
        }
        [SetUp]
        public void Setup()
        {
            Time.timeScale = 1;
            previousSun = RenderSettings.sun; RenderSettings.sun = null;
            scale = Asset<CombatScaleSettings>();
            var settings = Asset<DropletSettings>(); settings.initialSpeed = 0;
            motor = Node("Cinematic test motor").AddComponent<DropletMotor>();
            motor.settings = settings; motor.ResetPose(Vector3.zero, Quaternion.identity); motor.SimulationEnabled = false;
            var cameraNode = Node("Cinematic test camera"); cameraNode.AddComponent<Camera>();
            chase = cameraNode.AddComponent<ChaseCamera>(); chase.target = motor; chase.settings = settings; chase.ResetCamera();
            ship = Ship("SHOT-SUBJECT", new Vector3(0, 0, 100), Vector3.one * 2);
            simulation = Node("Cinematic test simulation").AddComponent<FleetCombatSimulation>();
            simulation.Configure(new[] { ship }, motor.transform, scale);
            director = Node("Cinematic test director").AddComponent<ShotDirector>();
            director.showControls = false; director.randomSeed = 14 ^ simulation.Generation;
            director.Configure(null, simulation, chase, null);
            Step(1);
        }
        void Step(float dt) { simulation.BeginStep(dt); simulation.EndStep(dt); }
        void Attempt(CombatEventKind kind, int tries = 300)
        {
            for (int i = 0; i < tries && !director.Active; i++)
            {
                bool droplet = kind == CombatEventKind.DropletContact;
                simulation.EmitEvent(kind, ship, droplet || kind == CombatEventKind.WeaponFired ? null : ship,
                    droplet ? motor.transform.position : ship.transform.position,
                    DamageSource.Penetration, Vector3.forward, Vector3.back, ship.targetId, droplet ? "Droplet" : ship.targetId);
                director.TickPresentation(.001f);
            }
        }
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            RenderSettings.sun = previousSun;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void RealNotificationPolicyStartsShotWithoutChangingMotorOrDamageAndEnforcesCooldown()
        {
            Vector3 position = motor.transform.position; Quaternion rotation = motor.transform.rotation;
            float speed = motor.Speed; float clock = simulation.SimulatedTime;
            Attempt(CombatEventKind.WeaponFired);
            Assert.IsTrue(director.Active); Assert.AreEqual(CombatShotKind.Attacker, director.CurrentKind);
            Assert.AreEqual(position, motor.transform.position); Assert.AreEqual(rotation, motor.transform.rotation);
            Assert.AreEqual(speed, motor.Speed); Assert.AreEqual(clock, simulation.SimulatedTime);
            Assert.AreEqual(ShipDamageState.Intact, ship.DamageState); Assert.AreEqual(1, Time.timeScale);
            Assert.That(director.NextEligibleTime - clock, Is.InRange(12f, 20f));
            director.ReturnImmediately(); Attempt(CombatEventKind.WeaponFired);
            Assert.IsFalse(director.Active); Assert.AreEqual(1, director.StartedCount);
        }

        [Test]
        public void PenetrationCloseupCannotChangeExplosionDeadlineOrResolveDamage()
        {
            simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 100), DamageSource.Penetration, 812);
            float deadline = ship.ExplosionAt;
            Attempt(CombatEventKind.HullPenetrated);
            Assert.IsTrue(director.Active); Assert.AreEqual(CombatShotKind.Penetration, director.CurrentKind);
            director.TickPresentation(1.2f);
            Assert.IsFalse(director.Active); Assert.AreEqual(deadline, ship.ExplosionAt);
            Assert.AreEqual(ShipDamageState.FatalPending, ship.DamageState); Assert.IsFalse(ship.IsDestroyed);
            Assert.AreEqual(1, simulation.PendingCount);
        }

        [Test]
        public void ReflectedLaserHullDamageCannotMasqueradeAsDropletPenetration()
        {
            simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0), DamageSource.ReflectedLaser, 901);
            director.TickPresentation(.01f);
            Assert.IsFalse(director.Active); Assert.AreEqual(0, director.PendingCount);
            Assert.AreEqual(ShipDamageState.FatalPending, ship.DamageState);
        }

        [Test]
        public void ExpiredAndPreviousGenerationEventsAreDroppedAndNeverQueued()
        {
            for (int i = 1; i < 100; i++) director.ConsiderEvent(new CombatEvent(CombatEventKind.WeaponFired,
                ship, null, ship.transform.position, DamageSource.DirectLaser, simulation.SimulatedTime - 2,
                simulation.Generation, i, ship.targetId, "Droplet", Vector3.forward, Vector3.zero, Vector3.zero));
            Assert.AreEqual(99, director.ExpiredCount); Assert.AreEqual(0, director.PendingCount);
            director.ConsiderEvent(new CombatEvent(CombatEventKind.WeaponFired, ship, null, ship.transform.position,
                DamageSource.DirectLaser, simulation.SimulatedTime, simulation.Generation - 1, 200,
                ship.targetId, "Droplet", Vector3.forward, Vector3.zero, Vector3.zero));
            director.TickPresentation(.02f); Assert.IsFalse(director.Active);
        }

        [Test]
        public void AuthoritativeHullOcclusionRejectsShotEvenWhenColliderIsStreamedOut()
        {
            var blocker = Ship("OCCLUDER", new Vector3(0, 10, 114), new Vector3(95, 45, 45));
            simulation.Configure(new[] { ship, blocker }, motor.transform, scale);
            director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector(); Step(1);
            blocker.hitVolumes[0].enabled = false;
            Attempt(CombatEventKind.WeaponFired);
            Assert.IsFalse(director.Active); Assert.AreEqual(0, director.StartedCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AttackerChoosesLitSideWithOcclusionFallback(bool blockLitSide)
        {
            var sun = Node("Existing key light").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(Vector3.right); RenderSettings.sun = sun;
            if (blockLitSide)
            {
                var blocker = Ship("LIT-SIDE OCCLUDER", new Vector3(-27, 10, 114), Vector3.one * 45);
                simulation.Configure(new[] { ship, blocker }, motor.transform, scale);
                director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector(); Step(1);
            }
            float intensity = sun.intensity; Quaternion lightRotation = sun.transform.rotation;
            Attempt(CombatEventKind.WeaponFired); Assert.IsTrue(director.Active);
            chase.SendMessage("LateUpdate");
            float side = Vector3.Dot(chase.transform.position - ship.transform.position, ship.transform.right);
            Assert.That(side * (blockLitSide ? 1 : -1), Is.GreaterThan(0));
            Assert.AreEqual(intensity, sun.intensity); Assert.AreEqual(lightRotation, sun.transform.rotation);
            Assert.AreEqual(1, director.StartedCount);
        }

        [Test]
        public void NearbyPendingExplosionReducesCommonEventOpportunityWithoutChangingDeadline()
        {
            director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector(); Step(1);
            simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0), DamageSource.ReflectedLaser, 902);
            float deadline = ship.ExplosionAt; float clock = simulation.SimulatedTime;
            Attempt(CombatEventKind.DropletContact);
            Assert.IsFalse(director.Active); Assert.AreEqual(0, director.PendingCount);
            Assert.AreEqual(deadline, ship.ExplosionAt); Assert.AreEqual(clock, simulation.SimulatedTime);
            simulation.ResetSimulation(); director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector(); Step(1);
            Attempt(CombatEventKind.DropletContact);
            Assert.IsTrue(director.Active, "The same probability draw is accepted when no nearby pending reactor needs the opportunity.");
        }

        [Test]
        public void DistantPendingExplosionDoesNotSuppressNearbyDropletContact()
        {
            ship.transform.position = Vector3.forward * 5000;
            simulation.Configure(new[] { ship }, motor.transform, scale);
            director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector(); Step(1);
            simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0), DamageSource.ReflectedLaser, 903);
            Attempt(CombatEventKind.DropletContact);
            Assert.IsTrue(director.Active); Assert.AreEqual(CombatShotKind.DropletContact, director.CurrentKind);
        }

        [Test]
        public void ActualDelayedExplosionSelectsARebasedShotWithoutReplayingDamage()
        {
            CombatEvent observed = default;
            director.ShotStarted += (value, kind) => observed = value;
            // Use the actual simulation's delayed destruction. No synthetic
            // ShipExploded notification and no selection-policy bypass.
            Assert.IsTrue(simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0),
                DamageSource.ReflectedLaser, 904));
            float deadline = ship.ExplosionAt;
            Step(deadline - simulation.SimulatedTime + .01f);
            director.TickPresentation(.02f); chase.SendMessage("LateUpdate");
            Assert.IsTrue(ship.IsDestroyed); Assert.AreEqual(1, simulation.ExplodedCount);
            Assert.IsTrue(director.Active); Assert.AreEqual(CombatShotKind.Explosion, director.CurrentKind);
            Assert.AreEqual(CombatEventKind.ShipExploded, observed.kind); Assert.Greater(observed.eventId, 0);
            Assert.AreEqual(ship.targetId, observed.targetId); Assert.AreEqual(deadline, ship.ExplosionAt);
            Vector3 before = chase.transform.position;
            Vector3 offset = new Vector3(10000, 2000, -6000);
            simulation.ShiftOrigin(offset); motor.transform.position -= offset; motor.HoldSimulationPose();
            director.TickPresentation(.02f); chase.SendMessage("LateUpdate");
            Assert.IsTrue(director.Active); Assert.Less(Vector3.Distance(before - offset, chase.transform.position), .01f);
            Assert.AreEqual(1, simulation.ExplodedCount); Assert.AreEqual(0, simulation.PendingCount);
        }

        [Test]
        public void SharedOrdinaryOpportunityDoesNotBlockTheActualMajorEvent()
        {
            // Seed 11 yields .4729 then .0449: the first ordinary opportunity
            // declines; the next draw belongs to the actual explosion.
            director.randomSeed = 11 ^ simulation.Generation; director.ResetDirector(); Step(1);
            Assert.IsTrue(simulation.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 0),
                DamageSource.ReflectedLaser, 906));
            Step(ship.ExplosionAt - simulation.SimulatedTime - .01f);
            Attempt(CombatEventKind.WeaponFired); Assert.IsFalse(director.Active);
            Attempt(CombatEventKind.DropletContact);
            Assert.IsFalse(director.Active, "A contact notification cannot buy a second lottery ticket in the same sampling window.");
            Step(.02f); director.TickPresentation(.01f);
            Assert.IsTrue(ship.IsDestroyed); Assert.IsTrue(director.Active);
            Assert.AreEqual(CombatShotKind.Explosion, director.CurrentKind);
        }

        [Test]
        public void SameFrameContactCanCompeteWithItsWeaponFiredNotification()
        {
            simulation.EmitEvent(CombatEventKind.WeaponFired, ship, null, ship.transform.position,
                DamageSource.DirectLaser, Vector3.forward, attackerId: ship.targetId, targetId: "Droplet");
            simulation.EmitEvent(CombatEventKind.DropletContact, ship, null, motor.transform.position,
                DamageSource.ReflectedLaser, Vector3.back, Vector3.back, ship.targetId, "Droplet");
            director.TickPresentation(.01f);
            Assert.IsTrue(director.Active); Assert.AreEqual(CombatShotKind.DropletContact, director.CurrentKind);
            Assert.AreEqual(1, director.StartedCount);
        }

        [Test]
        public void SubjectRestoreAndGenerationResetCancelWithoutFollowingReusedObject()
        {
            Attempt(CombatEventKind.WeaponFired); Assert.IsTrue(director.Active);
            ship.ResetTarget(); Assert.IsFalse(director.Active); Assert.IsFalse(chase.CinematicActive);
            simulation.ResetSimulation(); director.randomSeed = 14 ^ simulation.Generation; director.ResetDirector();
            Step(1); Attempt(CombatEventKind.WeaponFired);
            Assert.IsTrue(director.Active);
            simulation.ResetSimulation();
            Assert.IsFalse(director.Active); Assert.AreEqual(0, director.StartedCount); Assert.AreEqual(0, director.PendingCount);
        }

        [Test]
        public void OriginShiftKeepsTheSameSubjectAndRebasesTheCamera()
        {
            Attempt(CombatEventKind.WeaponFired); Assert.IsTrue(director.Active);
            chase.SendMessage("LateUpdate"); Vector3 before = chase.transform.position;
            Vector3 offset = new Vector3(10000, -2000, 3000);
            simulation.ShiftOrigin(offset); motor.transform.position -= offset; motor.HoldSimulationPose();
            director.TickPresentation(.01f); chase.SendMessage("LateUpdate");
            Assert.IsTrue(director.Active);
            Assert.Less(Vector3.Distance(before - offset, chase.transform.position), .01f);
        }

        [Test]
        public void ListenerStaysAtNormalChasePoseAndPauseDropsTheShot()
        {
            var anchor = Node("Sole listener").AddComponent<CombatListenerAnchor>(); anchor.chase = chase;
            anchor.gameObject.AddComponent<AudioListener>();
            Attempt(CombatEventKind.WeaponFired); chase.SendMessage("LateUpdate"); anchor.SendMessage("LateUpdate");
            Assert.AreEqual(chase.NormalPosition, anchor.transform.position);
            Assert.Greater(Vector3.Distance(anchor.transform.position, chase.transform.position), 50);
            Assert.IsNull(chase.GetComponent<AudioListener>());
            Time.timeScale = 0; director.TickPresentation(0);
            Assert.IsFalse(director.Active); Assert.IsFalse(chase.CinematicActive);
            Assert.AreEqual(chase.NormalPosition, chase.transform.position);
        }

        [Test]
        public void NarrativeListenerFollowsDropletWhenChaseCameraIsDisabled()
        {
            var anchor = Node("Narrative listener").AddComponent<CombatListenerAnchor>(); anchor.chase = chase;
            chase.enabled = false;
            motor.ResetPose(new Vector3(0, 8, -18000), Quaternion.identity);
            anchor.SendMessage("LateUpdate");
            Vector3 expected = motor.PresentedPosition + new Vector3(0, chase.settings.cameraHeight, -chase.settings.cameraDistance);
            Assert.AreEqual(expected, anchor.transform.position);
            Assert.Greater(Vector3.Distance(chase.NormalPosition, anchor.transform.position), 17000);
        }

        [Test]
        public void StrongPlayerTurnAndOffModeDoNotStealControl()
        {
            Attempt(CombatEventKind.DropletContact); Assert.IsTrue(director.Active);
            motor.transform.rotation = Quaternion.Euler(0, 90, 0);
            director.TickPresentation(.01f);
            Assert.IsFalse(director.Active); Assert.Less(Quaternion.Angle(motor.transform.rotation, Quaternion.Euler(0, 90, 0)), .001f);
            director.SetFrequency(CombatShotFrequency.Off); Step(30); Attempt(CombatEventKind.WeaponFired);
            Assert.IsFalse(director.Active);
            director.RebindReturn("<Keyboard>/backquote");
            Assert.AreEqual("<Keyboard>/backquote", director.returnBinding);
        }

        [Test]
        public void HighRenderRateDoesNotMisclassifyModerateFixedStepSteering()
        {
            Attempt(CombatEventKind.DropletContact); Assert.IsTrue(director.Active);
            motor.SimulationEnabled = true;
            // 0.7 degrees over the actual 20 ms physics step is 35 degrees/sec.
            // The next 4 ms render frame must not report it as 175 degrees/sec.
            motor.Simulate(.02f, new FlightCommand { look = new Vector2(.7f / motor.settings.mouseSensitivity, 0) });
            director.TickPresentation(.004f);
            Assert.IsTrue(director.Active, "Moderate steering must preserve the optional close-up at high FPS.");
            // A genuine 100 degrees/sec turn still interrupts on that same render rate.
            motor.Simulate(.02f, new FlightCommand { look = new Vector2(2f / motor.settings.mouseSensitivity, 0) });
            director.TickPresentation(.004f);
            Assert.IsFalse(director.Active);
            Assert.That(Quaternion.Angle(Quaternion.identity, motor.transform.rotation), Is.EqualTo(2.7f).Within(.002f));
        }
    }
}
