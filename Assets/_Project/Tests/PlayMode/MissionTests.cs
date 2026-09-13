using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class MissionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        DropletSettings settings;
        DropletMotor motor;
        MissionController mission;
        ScoreSystem score;
        ShipTarget ship;
        GameObject New(string name) { var go = new GameObject(name); objects.Add(go); return go; }
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<DropletSettings>(); settings.targetLayers = 1 << 31;
            var player = New("Player test"); motor = player.AddComponent<DropletMotor>(); motor.settings = settings; motor.enabled = false;
            var detector = player.AddComponent<DropletHitDetector>(); detector.settings = settings; motor.hitDetector = detector;
            var target = New("Mission target"); target.layer = 31; target.transform.position = Vector3.forward * 5;
            ship = target.AddComponent<ShipTarget>(); var box = target.AddComponent<BoxCollider>(); box.isTrigger = true;
            ship.hitVolumes = new Collider[] { box };
            var systems = New("Mission test"); score = systems.AddComponent<ScoreSystem>(); score.settings = settings;
            mission = systems.AddComponent<MissionController>(); mission.enabled = false;
            mission.settings = settings; mission.motor = motor; mission.score = score; mission.targets = new[] { ship };
            mission.spawnPosition = Vector3.zero; mission.Initialize(); Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            // Destroy mission before targets/player so event cleanup has valid references.
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear(); Object.DestroyImmediate(settings); Time.timeScale = 1;
        }
        [Test] public void ReadyDoesNotAdvanceUntilStarted()
        {
            mission.Step(2, default); Assert.AreEqual(settings.missionSeconds, mission.Remaining); Assert.AreEqual(Vector3.zero, motor.transform.position);
            mission.StartMission(); mission.Step(.02f, default); Assert.Less(mission.Remaining, settings.missionSeconds);
        }
        [UnityTest] public IEnumerator PauseFreezesTimeMotionDamageAndScore()
        {
            mission.StartMission(); mission.TogglePause(); float remaining = mission.Remaining;
            motor.enabled = true; mission.enabled = true;
            mission.Step(5, new FlightCommand { boost = true }); yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(remaining, mission.Remaining); Assert.AreEqual(Vector3.zero, motor.transform.position);
            Assert.IsFalse(ship.IsDestroyed); Assert.AreEqual(0, score.Score); Assert.AreEqual(0, Time.timeScale);
            mission.TogglePause(); Assert.AreEqual(1, Time.timeScale);
        }
        [Test] public void VictoryAndTimeoutEachResolveOnlyOnce()
        {
            mission.StartMission(); mission.Step(.5f, default);
            Assert.IsTrue(mission.Won); Assert.AreEqual(1, mission.ResultTransitions);
            int finalScore = score.Score; mission.Step(200, default); Assert.AreEqual(finalScore, score.Score); Assert.AreEqual(1, mission.ResultTransitions);
            settings.missionSeconds = .01f; mission.Restart(); mission.StartMission(); mission.Step(1, default);
            Assert.IsFalse(mission.Won); Assert.AreEqual(MissionState.Results, mission.State); Assert.AreEqual(1, mission.ResultTransitions);
            mission.Step(1, default); Assert.AreEqual(1, mission.ResultTransitions);
        }
        [Test] public void FinalStepHitBeatsTimeoutAndMovementIsClamped()
        {
            settings.missionSeconds = .02f; ship.transform.position = Vector3.forward * 1.5f; mission.Restart(); Physics.SyncTransforms();
            mission.StartMission(); mission.Step(1, default);
            Assert.IsTrue(mission.Won); Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(1, mission.ResultTransitions);
            Assert.AreEqual(.02f, mission.Elapsed, .00001f); Assert.AreEqual(.36f, motor.transform.position.z, .0001f);
        }
        [Test] public void HitBeyondFinalTimeBudgetCannotWin()
        {
            settings.missionSeconds = .02f; ship.transform.position = Vector3.forward * 3; mission.Restart(); Physics.SyncTransforms();
            mission.StartMission(); mission.Step(1, default);
            Assert.IsFalse(mission.Won); Assert.IsFalse(ship.IsDestroyed);
        }
        [Test] public void TenRestartsResetTargetsScoresMotionAndSubscriptions()
        {
            for (int i = 0; i < 10; i++)
            {
                mission.Initialize(); mission.StartMission(); mission.Step(.5f, default);
                Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(1, mission.ResultTransitions);
                mission.Restart(); Assert.AreEqual(MissionState.Ready, mission.State); Assert.AreEqual(1, mission.TotalCount);
                Assert.IsFalse(ship.IsDestroyed); Assert.IsTrue(ship.hitVolumes[0].enabled);
                Assert.AreEqual(settings.missionSeconds, mission.Remaining); Assert.AreEqual(0, score.Score);
                Assert.AreEqual(0, score.Combo); Assert.AreEqual(Vector3.zero, motor.transform.position);
                Assert.AreEqual(settings.initialSpeed, motor.Speed); Assert.AreEqual(1, Time.timeScale);
            }
        }
        [Test] public void RecoveryAndRestartNeverDamageTeleportPathOrResetScore()
        {
            mission.StartMission(); score.RegisterKill(); int prior = score.Score;
            motor.ResetPose(Vector3.forward * 500, Quaternion.identity); mission.RecoverIfOutside();
            Assert.IsFalse(ship.IsDestroyed); Assert.AreEqual(prior, score.Score); Assert.AreEqual(1, mission.RecoveryCount);
            Assert.AreEqual(Vector3.zero, motor.transform.position); Assert.AreEqual(1, mission.TotalCount);
            motor.ResetPose(Vector3.forward * 500, Quaternion.identity); mission.Restart(); Assert.IsFalse(ship.IsDestroyed);
        }
    }
}
