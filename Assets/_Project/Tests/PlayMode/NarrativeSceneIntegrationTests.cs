using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class NarrativeSceneIntegrationTests
    {
        MissionController mission;
        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/FleetAssault_NarrativeCombat_Small.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_NarrativeCombat_Small");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(mission); Assert.AreEqual(12, mission.TotalCount);
            mission.enabled = false; mission.input.enabled = false;
        }
        [TearDown] public void Restore() { Time.timeScale = 1; mission?.Restart(); }
        [Test]
        public void SavedSceneConnectsAllSixSystemsAndUsesRealScale()
        {
            Assert.AreEqual(12, mission.targets.Select(t => t.targetId).Distinct().Count());
            Assert.IsNotNull(mission.narrative.timeline); Assert.GreaterOrEqual(mission.narrative.radio.library.combat.Length, 30);
            Assert.GreaterOrEqual(mission.narrative.radio.library.narrative.Length, 12);
            Assert.AreSame(mission.combat, mission.motor.hitDetector.combat);
            Assert.AreSame(mission.combat, mission.lasers.simulation);
            Assert.AreEqual(100, mission.settings.MetersPerUnit); Assert.AreEqual(300, mission.settings.initialSpeed);
            Assert.AreEqual(1500, mission.settings.initialSpeed * mission.settings.boostMultiplier);
            Assert.IsTrue(mission.targets.All(t => t.transform.Find("LaserMuzzle") != null && t.hitVolumes.Length == 9));
            Assert.AreEqual(7040, mission.lasers.dropletSurface.geometry.TriangleCount);
        }
        [Test]
        public void ActualDistanceOverTimeMatchesHudAndInterTargetTravel()
        {
            mission.StartCombat(); var motor = mission.motor; var detector = motor.hitDetector; motor.hitDetector = null;
            Vector3 initial = motor.transform.position;
            for (int i = 0; i < 100; i++) motor.Simulate(.02f, default);
            float physical = mission.settings.ToMeters(Vector3.Distance(initial, motor.transform.position));
            Assert.That(physical / 2, Is.EqualTo(30000).Within(2));
            Assert.That(mission.settings.ToMeters(motor.MeasuredSpeed), Is.EqualTo(30000).Within(2));
            float distance = Vector3.Distance(mission.targets[0].transform.position, mission.targets[1].transform.position);
            Assert.That(mission.settings.ToMeters(distance), Is.EqualTo(100000).Within(1));
            float seconds = distance / motor.Speed; initial = motor.transform.position;
            motor.Simulate(seconds, default);
            Assert.That(Vector3.Distance(initial, motor.transform.position), Is.EqualTo(distance).Within(.02));
            Assert.That(seconds, Is.EqualTo(100f / 30f).Within(.001));
            motor.ResetPose(initial + Vector3.right * 10000, Quaternion.identity);
            Assert.AreEqual(0, motor.MeasuredSpeed); Assert.AreEqual(0, mission.PendingCount);
            motor.hitDetector = detector;
        }
        [Test]
        public void FatalDamageRemainsVisibleAndLastTargetsFinishAfterDeadlinesWithEffectsOff()
        {
            mission.StartCombat(); mission.lasers = null;
            var pool = Object.FindAnyObjectByType<ReactorExplosionPool>(); pool.Quality = EffectQuality.Off;
            foreach (var ship in mission.targets) Assert.IsTrue(ship.TryDestroy());
            Assert.AreEqual(12, mission.PendingCount); Assert.AreEqual(0, mission.DestroyedCount); Assert.AreEqual(0, mission.score.Score);
            Assert.IsTrue(mission.targets.All(t => !t.IsDestroyed && !t.CanAttack));
            var renderer = mission.combat.fleetRenderer; renderer.RefreshNow(); Assert.AreEqual(12, renderer.AliveCount);
            Advance(1.9f); Assert.AreEqual(0, mission.DestroyedCount); Assert.AreEqual(MissionState.Playing, mission.State);
            Advance(3.3f); Assert.AreEqual(0, mission.PendingCount); Assert.AreEqual(12, mission.DestroyedCount);
            Assert.AreEqual(MissionState.Results, mission.State); Assert.AreEqual(1, mission.ResultTransitions);
            Assert.IsTrue(mission.Won); Assert.Greater(mission.score.Score, 0);
        }
        [Test]
        public void PauseDuplicateHitAndThreeRestartsCannotLeakAnExplosion()
        {
            for (int round = 0; round < 3; round++)
            {
                mission.RestartIntoCombat(); var ship = mission.targets[0]; Assert.IsTrue(ship.TryDestroy());
                float deadline = ship.ExplosionAt; Assert.IsFalse(ship.TryDestroy()); Assert.AreEqual(deadline, ship.ExplosionAt);
                mission.TogglePause(); float clock = mission.combat.SimulatedTime;
                mission.Step(10, default); Assert.AreEqual(clock, mission.combat.SimulatedTime); Assert.AreEqual(1, mission.PendingCount);
                mission.RestartIntoCombat(); Assert.AreEqual(0, mission.PendingCount); Assert.AreEqual(0, mission.DestroyedCount);
                mission.motor.ResetPose(new Vector3(-6000, 2000, 0), Quaternion.identity);
                Advance(5.1f); Assert.AreEqual(0, mission.PendingCount); Assert.AreEqual(0, mission.DestroyedCount);
            }
        }
        [Test]
        public void NarrativeNormalSkipPauseAndReplayShareOneCleanHandoff()
        {
            var narrative = mission.narrative;
            mission.StartMission(); Assert.AreEqual(MissionState.Narrative, mission.State);
            float time = mission.Remaining; narrative.Step(12); Assert.Greater(narrative.CueCount, 0);
            Assert.AreEqual(0, mission.Elapsed); Assert.AreEqual(time, mission.Remaining); Assert.AreEqual(0, mission.lasers.ShotsFired);
            Assert.IsFalse(mission.targets[0].TryDestroy());
            mission.TogglePause(); var position = mission.motor.transform.position; narrative.Step(5);
            Assert.AreEqual(position, mission.motor.transform.position); mission.TogglePause();
            narrative.Step(48); Assert.AreEqual(MissionState.Playing, mission.State); Assert.AreEqual(1, narrative.CompletionCount);
            Assert.AreEqual(mission.spawnPosition, mission.motor.transform.position); Assert.AreEqual(0, mission.PendingCount);
            Assert.IsTrue(mission.input.GameplayEnabled); Assert.IsTrue(mission.chaseCamera.enabled);
            for (int i = 0; i < 3; i++)
            {
                mission.ReplayNarrative(); narrative.Step(1); narrative.Skip(); narrative.Skip();
                Assert.AreEqual(1, narrative.CompletionCount); Assert.AreEqual(MissionState.Playing, mission.State);
                Assert.AreEqual(0, mission.PendingCount); Assert.AreEqual(0, mission.Elapsed); Assert.AreEqual(0, mission.motor.MeasuredSpeed);
            }
        }
        [Test]
        public void EscapedAndPendingTargetsBothResolveWithoutKillInflation()
        {
            mission.StartCombat(); mission.lasers = null;
            // A real retreat advances through the valid evacuation boundary; no direct escaped-state setter.
            foreach (var ship in mission.targets.Skip(1)) ship.TryDestroy();
            var escapee = mission.targets[0]; mission.combat.RequestRetreat(escapee);
            mission.motor.ResetPose(new Vector3(-6000, 2000, 0), Quaternion.identity);
            Advance(3);
            float radius = mission.combat.scale.MetersToUnits(mission.combat.scale.escapeRadiusMeters);
            escapee.transform.position = mission.combat.evacuationCenter + Vector3.forward * (radius - .1f);
            escapee.transform.rotation = Quaternion.identity;
            mission.combat.SetShipVelocity(escapee, Vector3.forward * 60);
            mission.combat.ResetSweepHistory(); Advance(2.2f);
            Assert.AreEqual(1, mission.EscapedCount); Assert.AreEqual(11, mission.DestroyedCount); Assert.AreEqual(0, mission.PendingCount);
            Assert.AreEqual(MissionState.Results, mission.State); Assert.IsFalse(mission.Won); Assert.AreEqual(1, mission.ResultTransitions);
        }
        [Test]
        public void TimeoutAllowsPendingReactorToCommitBeforeResults()
        {
            float originalSeconds = mission.settings.missionSeconds;
            try
            {
                mission.settings.missionSeconds = .1f; mission.RestartIntoCombat(); mission.lasers = null;
                mission.targets[0].TryDestroy(); Advance(.2f);
                Assert.AreEqual(0, mission.Remaining); Assert.AreEqual(MissionState.Playing, mission.State);
                Advance(5); Assert.AreEqual(MissionState.Results, mission.State);
                Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(0, mission.PendingCount);
            }
            finally { mission.settings.missionSeconds = originalSeconds; }
        }
        void Advance(float seconds)
        { int steps = Mathf.CeilToInt(seconds / .02f); for (int i = 0; i < steps; i++) mission.Step(.02f, new FlightCommand { brake = true }); }
    }
}
