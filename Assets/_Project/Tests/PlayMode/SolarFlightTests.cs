using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    // Temporary fixtures only. This suite never opens, generates or saves a scene or asset.
    // The scene audit separately checks that these preserved Fleet settings match the saved variant.
    public sealed class SolarFlightTests
    {
        const float OldRadius = 730;
        const float FixedStep = .02f;
        readonly List<GameObject> objects = new List<GameObject>();
        DropletSettings settings;
        DropletMotor motor;
        MissionController mission;
        ChaseCamera chase;
        ShipTarget scoredTarget, returnPathTarget;
        float previousFixedDeltaTime;

        GameObject New(string name)
        {
            var go = new GameObject("Solar flight fixture " + name);
            objects.Add(go);
            return go;
        }

        ShipTarget Target(string name, Vector3 position)
        {
            var go = New(name);
            go.transform.position = position;
            var ship = go.AddComponent<ShipTarget>();
            ship.targetId = name;
            ship.hitVolumes = new Collider[3];
            for (int i = 0; i < ship.hitVolumes.Length; i++)
            {
                var hit = new GameObject("Compound hit " + i);
                hit.layer = 31;
                hit.transform.SetParent(go.transform, false);
                hit.transform.localPosition = Vector3.right * ((i - 1) * .1f);
                var box = hit.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1, 1, .01f);
                ship.hitVolumes[i] = box;
            }
            return ship;
        }

        [SetUp]
        public void Setup()
        {
            previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = FixedStep;
            Time.timeScale = 1;
            settings = ScriptableObject.CreateInstance<DropletSettings>();
            settings.initialSpeed = 24;
            settings.maxCruiseSpeed = 52;
            settings.boostMultiplier = 3;
            settings.acceleration = 45;
            settings.speedAdjustment = 18;
            settings.missionSeconds = 240;
            settings.comboWindow = 6;
            settings.boundaryWarningRadius = 610 * 8;
            settings.boundaryRadius = OldRadius * 8;
            settings.cameraDistance = 8;
            settings.cameraHeight = 2.2f;
            settings.fieldOfView = 65;
            settings.targetLayers = 1 << 31;
            var player = New("Player");
            motor = player.AddComponent<DropletMotor>();
            motor.enabled = false;
            motor.settings = settings;
            var visual = new GameObject("VisualRoot");
            visual.transform.SetParent(player.transform, false);
            motor.visualRoot = visual.transform;
            motor.hitDetector = player.AddComponent<DropletHitDetector>();
            motor.hitDetector.settings = settings;
            scoredTarget = Target("Scored ship", new Vector3(12000, 8, 100));
            returnPathTarget = Target("Return path guard", new Vector3(12000, 8, 3000));
            var camera = New("Camera");
            camera.AddComponent<Camera>().enabled = false;
            chase = camera.AddComponent<ChaseCamera>();
            chase.enabled = false;
            chase.target = motor;
            chase.settings = settings;
            var systems = New("Mission");
            var score = systems.AddComponent<ScoreSystem>();
            score.settings = settings;
            mission = systems.AddComponent<MissionController>();
            mission.enabled = false;
            mission.settings = settings;
            mission.motor = motor;
            mission.score = score;
            mission.chaseCamera = chase;
            mission.targets = new[] { scoredTarget, returnPathTarget };
            mission.spawnPosition = new Vector3(0, 8, 0);
            mission.arenaCenter = new Vector3(0, 20, 260);
            mission.Initialize();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (mission != null) Object.DestroyImmediate(mission);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            if (settings != null) Object.DestroyImmediate(settings);
            Time.timeScale = 1;
            Time.fixedDeltaTime = previousFixedDeltaTime;
        }

        [Test]
        public void FixedStepBoostFlightCrossesOldLimitThenWarnsAndRecoversAtEightTimesRadius()
        {
            mission.StartMission();
            scoredTarget.TryDestroy();
            int scoreBefore = mission.score.Score;
            int teleports = 0;
            motor.Teleported += () => teleports++;
            float oldCrossingSeconds = -1, warningSeconds = -1, lastRadius = 0;
            int steps = 0;
            while (mission.RecoveryCount == 0 && steps < 4000)
            {
                lastRadius = Vector3.Distance(motor.transform.position, mission.arenaCenter);
                mission.Step(FixedStep, new FlightCommand { throttle = 1, boost = true });
                steps++;
                float radius = Vector3.Distance(motor.transform.position, mission.arenaCenter);
                if (radius > OldRadius && oldCrossingSeconds < 0)
                {
                    oldCrossingSeconds = steps * FixedStep;
                    Assert.AreEqual(0, mission.RecoveryCount);
                    Assert.IsFalse(mission.NearBoundary, "The old radius must not show the new boundary warning.");
                }
                if (mission.NearBoundary && warningSeconds < 0)
                {
                    warningSeconds = steps * FixedStep;
                    Assert.That(radius, Is.GreaterThan(4880).And.LessThan(4880 + 156 * FixedStep + .02f));
                    // This ship is now behind the player, along the return segment, but was never flown through.
                    returnPathTarget.transform.position = new Vector3(0, 8, 3000);
                    Physics.SyncTransforms();
                }
            }
            Assert.That(oldCrossingSeconds, Is.GreaterThan(0));
            Assert.That(warningSeconds, Is.GreaterThan(oldCrossingSeconds));
            Assert.AreEqual(1, mission.RecoveryCount);
            Assert.AreEqual(1, teleports);
            Assert.That(lastRadius, Is.InRange(settings.boundaryRadius - 156 * FixedStep - .02f, settings.boundaryRadius));
            Assert.That(lastRadius / OldRadius, Is.InRange(7.99f, 8f));
            Assert.AreEqual(mission.spawnPosition, motor.transform.position);
            Assert.AreEqual(mission.spawnPosition, motor.PresentedPosition);
            Assert.AreEqual(mission.spawnPosition, motor.visualRoot.position);
            Assert.AreEqual(settings.initialSpeed, motor.Speed);
            Assert.That(Vector3.Distance(chase.transform.position,
                mission.spawnPosition + motor.transform.rotation * new Vector3(0, 2.2f, -8)), Is.LessThan(.001f));
            Assert.IsFalse(returnPathTarget.IsDestroyed, "Recovery must not sweep the teleport segment.");
            Assert.AreEqual(scoreBefore, mission.score.Score);
            Assert.AreEqual(1, mission.DestroyedCount);
            Assert.AreEqual(3, mission.RecoveryNotice);
            Assert.AreEqual(MissionState.Playing, mission.State);
            Debug.Log($"SolarFlight fixed-step runtime fixture: dt={FixedStep:F2}s, oldCrossing={oldCrossingSeconds:F2}s, " +
                $"warning={warningSeconds:F2}s, recovery={steps * FixedStep:F2}s, lastRadius={lastRadius:F3}m, " +
                $"measuredRadiusRatio={lastRadius / OldRadius:F5}, recoveredRadius={settings.boundaryRadius:F0}m. " +
                "Driven through MissionController.Step; this is not a saved-scene or rendered-input test.");
        }

        [UnityTest]
        public IEnumerator ActualFixedUpdateContinuesBeyondOldBoundaryAndPauseRestartRemainCorrect()
        {
            mission.StartMission();
            // This check exercises Unity's actual FixedUpdate scheduler; the complete path is covered above.
            motor.ResetPose(mission.arenaCenter + Vector3.forward * (OldRadius + 20), Quaternion.identity);
            motor.enabled = true;
            mission.enabled = true;
            Vector3 before = motor.transform.position;
            float timeBefore = mission.Remaining;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(motor.transform.position.z, Is.GreaterThan(before.z));
            Assert.That(mission.Remaining, Is.LessThan(timeBefore));
            Assert.AreEqual(0, mission.RecoveryCount);
            Assert.IsFalse(mission.NearBoundary);
            mission.TogglePause();
            before = motor.transform.position;
            timeBefore = mission.Remaining;
            yield return new WaitForSecondsRealtime(.06f);
            Assert.AreEqual(before, motor.transform.position);
            Assert.AreEqual(timeBefore, mission.Remaining);
            Assert.AreEqual(0, Time.timeScale);
            mission.Restart();
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(MissionState.Ready, mission.State);
            Assert.AreEqual(settings.missionSeconds, mission.Remaining);
            Assert.AreEqual(mission.spawnPosition, motor.transform.position);
            Assert.AreEqual(0, mission.score.Score);
            Assert.AreEqual(0, mission.DestroyedCount);
            Assert.AreEqual(0, mission.RecoveryCount);
            Assert.IsFalse(scoredTarget.IsDestroyed);
            Assert.IsFalse(returnPathTarget.IsDestroyed);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(mission.spawnPosition, motor.transform.position);
        }

        [Test]
        public void HighSpeedCompoundHitsAndTurnsRemainStableFarOutsideOriginalArena()
        {
            var start = mission.arenaCenter + Vector3.forward * 4200;
            scoredTarget.transform.position = start + Vector3.forward * 2;
            returnPathTarget.transform.position = start + Vector3.forward * 4;
            mission.StartMission();
            motor.ResetPose(start - Vector3.forward * 500, Quaternion.identity);
            // Reach the unchanged 156 m/s boost speed before the attack; no speed override is used.
            for (int i = 0; i < 200; i++) motor.Simulate(FixedStep, new FlightCommand { throttle = 1, boost = true });
            motor.transform.position = start;
            Physics.SyncTransforms();
            mission.Step(FixedStep, new FlightCommand { boost = true });
            mission.Step(FixedStep, new FlightCommand { boost = true });
            Assert.AreEqual(2, mission.DestroyedCount);
            Assert.AreEqual(300 + Mathf.FloorToInt(mission.Remaining) * settings.timeBonusPerSecond, mission.score.Score);
            Assert.AreEqual(1, mission.ResultTransitions);
            Assert.AreEqual(0, mission.RecoveryCount);
            int scoreAtVictory = mission.score.Score;
            mission.Step(FixedStep, new FlightCommand { boost = true });
            Assert.AreEqual(scoreAtVictory, mission.score.Score);
            mission.Restart();
            mission.StartMission();
            motor.ResetPose(mission.arenaCenter + Vector3.right * 4200, Quaternion.identity);
            for (int i = 0; i < 120; i++)
                mission.Step(FixedStep, new FlightCommand { throttle = 1, boost = true, look = new Vector2(20, 1) });
            Assert.IsFalse(float.IsNaN(motor.transform.position.x));
            Assert.That(Mathf.Abs(Vector3.Dot(motor.transform.right, Vector3.up)), Is.LessThan(.0001f));
            Assert.That(motor.Speed, Is.LessThanOrEqualTo(156));
            Assert.AreEqual(0, mission.RecoveryCount);
            Assert.AreEqual(0, mission.DestroyedCount);
        }
    }
}
