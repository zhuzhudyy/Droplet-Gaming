using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class CinematicFullSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity";
        MissionController mission;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_CinematicAudio_Cubic");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(mission); Assert.AreEqual(2000, mission.TotalCount);
            mission.enabled = false; mission.input.enabled = false;
        }
        [TearDown] public void Cleanup() { Time.timeScale = 1; mission?.Restart(); }

        [Test] public void FullFleetHasUniqueIdsCubicDepthAndBowsTowardFixedCombatStart()
        {
            Assert.AreEqual(2000, mission.targets.Select(s => s.targetId).Distinct().Count());
            var bounds = new Bounds(mission.targets[0].transform.position, Vector3.zero);
            foreach (var ship in mission.targets)
            {
                bounds.Encapsulate(ship.transform.position);
                Assert.That(Vector3.Dot(ship.transform.forward, (mission.spawnPosition - ship.transform.position).normalized), Is.GreaterThan(.9999f), ship.targetId);
                Assert.That(Vector3.Distance(ship.transform.position, mission.spawnPosition), Is.GreaterThan(100));
                Assert.That(Vector3.Distance(ship.transform.lossyScale, Vector3.one), Is.LessThan(.001));
            }
            float min = Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z), max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            Assert.That(max / min, Is.LessThan(1.15f));
            Assert.AreEqual(20, mission.targets.Select(s => Mathf.RoundToInt(s.transform.position.x)).Distinct().Count());
            Assert.AreEqual(25, mission.targets.Select(s => Mathf.RoundToInt(s.transform.position.y)).Distinct().Count());
            Assert.AreEqual(4, mission.targets.Select(s => Mathf.RoundToInt(s.transform.position.z)).Distinct().Count());
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled));
        }

        [Test] public void SkipNaturalCompletionAndThreeRestartsShareSavedFormation()
        {
            var saved = mission.targets.Select(s => s.transform.position).ToArray();
            var rotation = mission.targets.Select(s => s.transform.rotation).ToArray();
            for(int i = 0; i < 3; i++)
            {
                mission.ReplayNarrative();
                if(i == 1) mission.narrative.Step(mission.narrative.duration + .1f);
                else { mission.narrative.Step(1); mission.narrative.Skip(); mission.narrative.Skip(); }
                Assert.AreEqual(MissionState.Playing, mission.State);
                Assert.AreEqual(1, mission.narrative.CompletionCount);
                Assert.AreEqual(mission.spawnPosition, mission.motor.transform.position);
                CollectionAssert.AreEqual(saved, mission.targets.Select(s => s.transform.position));
                CollectionAssert.AreEqual(rotation, mission.targets.Select(s => s.transform.rotation));
                mission.Step(.1f, default); mission.TogglePause();
                float time = mission.combat.SimulatedTime;
                mission.Step(1, default); Assert.AreEqual(time, mission.combat.SimulatedTime);
                mission.RestartIntoCombat();
                Assert.AreEqual(0, mission.DestroyedCount); Assert.AreEqual(0, mission.PendingCount);
                Assert.AreEqual(0, mission.EscapedCount); Assert.AreEqual(1, Time.timeScale);
                Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled));
            }
        }

        [Test] public void OrdinaryMotorPenetrationStartsRealEscapeAndBothRenderPosesAgree()
        {
            var initial = mission.targets.ToDictionary(s => s, s => s.transform.position);
            var events = new List<CombatEvent>(); mission.combat.EventRaised += events.Add;
            mission.RestartIntoCombat();
            for(int i = 0; i < 750; i++) mission.Step(.02f, default);
            Assert.Greater(mission.DestroyedCount + mission.PendingCount, 0);
            Assert.IsTrue(events.Any(e => e.kind == CombatEventKind.HullPenetrated && e.source == DamageSource.Penetration));
            var fleeing = mission.targets.Where(s => s.DamageState == ShipDamageState.Intact &&
                (s.BehaviorState == ShipBehaviorState.Fleeing || s.BehaviorState == ShipBehaviorState.BreakingFormation) &&
                Vector3.Distance(initial[s], s.transform.position) > 40).ToArray();
            Assert.Greater(fleeing.Length, 0, "Normal collision must provoke physical movement, not only a state label.");
            var renderer = mission.combat.fleetRenderer; renderer.RefreshNow();
            foreach(var ship in fleeing)
            {
                Assert.IsTrue(renderer.TryGetPresentationPose(ship, out var matrix, out _));
                Assert.That(Vector3.Distance((Vector3)matrix.GetColumn(3), ship.transform.position), Is.LessThan(.001));
                Assert.That(Vector3.Dot(((Vector3)matrix.GetColumn(2)).normalized, ship.transform.forward), Is.GreaterThan(.9999f));
            }
            Assert.AreEqual(2000, mission.TotalCount);
        }

        [Test] public void PenetrationUnstableExplosionInterruptionKeepOriginalDeadlineAndScore()
        {
            mission.RestartIntoCombat(); mission.motor.SimulationEnabled = false;
            var lasers = mission.lasers; mission.lasers = null;
            try
            {
                var ship = mission.targets.OrderBy(s => (s.transform.position - mission.spawnPosition).sqrMagnitude).First();
                var events = new List<CombatEvent>(); mission.combat.EventRaised += events.Add;
                Assert.IsTrue(mission.combat.ApplyDamage(ship, new ShipHitContext(ship.transform.position, Vector3.forward, 1500), DamageSource.Penetration, 971));
                float deadline = ship.ExplosionAt;
                while(mission.combat.SimulatedTime + .02f < deadline) mission.Step(.02f, default);
                Assert.AreEqual(0, mission.DestroyedCount); Assert.AreEqual(1, mission.PendingCount);
                mission.Step(.021f, default);
                Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(0, mission.PendingCount);
                Assert.AreEqual(mission.settings.baseScore, mission.score.Score);
                Assert.AreEqual(deadline, ship.ExplosionAt);
                var kinds = events.Where(e => e.subject == ship).Select(e => e.kind).ToList();
                Assert.Less(kinds.IndexOf(CombatEventKind.HullPenetrated), kinds.IndexOf(CombatEventKind.ReactorUnstable));
                Assert.Less(kinds.IndexOf(CombatEventKind.ReactorUnstable), kinds.IndexOf(CombatEventKind.CommunicationInterrupted));
                Assert.Less(kinds.IndexOf(CombatEventKind.CommunicationInterrupted), kinds.IndexOf(CombatEventKind.ShipExploded));
                for(int i = 0; i < 100; i++) mission.Step(.02f, default);
                Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(1, events.Count(e => e.kind == CombatEventKind.ShipExploded));
                Assert.AreEqual(mission.settings.baseScore, mission.score.Score);
            }
            finally { mission.lasers = lasers; }
        }

        [Test] public void FullFleetOriginShiftRetainsAuthoritativeIdentityOrientationAndReset()
        {
            mission.RestartIntoCombat();
            var positions = mission.targets.Select(s=>s.transform.position).ToArray();
            var rotations = mission.targets.Select(s=>s.transform.rotation).ToArray();
            var originalSpawn = mission.spawnPosition; var originalCenter = mission.arenaCenter;
            var offset = new Vector3(12000, -3500, 16000);
            try
            {
                mission.combat.ShiftOrigin(offset); mission.spawnPosition -= offset; mission.arenaCenter -= offset;
                mission.motor.ResetPose(mission.motor.transform.position-offset,mission.motor.transform.rotation);
                mission.combat.fleetRenderer.RefreshNow();
                for(int i=0;i<mission.targets.Length;i++)
                {
                    var ship=mission.targets[i];
                    Assert.That(Vector3.Distance(positions[i]-offset,ship.transform.position),Is.LessThan(.002));
                    Assert.That(Quaternion.Angle(rotations[i],ship.transform.rotation),Is.LessThan(.05));
                    Assert.IsTrue(mission.combat.fleetRenderer.TryGetPresentationPose(ship,out var matrix,out _));
                    Assert.That(Vector3.Distance((Vector3)matrix.GetColumn(3),ship.transform.position),Is.LessThan(.002));
                }
                mission.RestartIntoCombat();
                Assert.AreEqual(2000,mission.TotalCount);
                Assert.AreEqual(mission.spawnPosition,mission.motor.transform.position);
                for(int i=0;i<mission.targets.Length;i++)
                    Assert.That(Vector3.Distance(positions[i]-offset,mission.targets[i].transform.position),Is.LessThan(.002));
            }
            finally
            {
                mission.combat.ShiftOrigin(-offset);mission.spawnPosition=originalSpawn;mission.arenaCenter=originalCenter;mission.Restart();
            }
        }
    }
}
