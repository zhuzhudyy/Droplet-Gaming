using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Loads the preserved authored 2000 fleet; never saves or changes an asset.</summary>
    public sealed class FleetNormalEscapeIntegrationTests
    {
        MissionController mission;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/_Project/Scenes/FleetAssault_NarrativeCombat.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_NarrativeCombat");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(mission); Assert.AreEqual(2000, mission.TotalCount);
            mission.enabled = false; mission.input.enabled = false;
        }
        [TearDown] public void Cleanup() { Time.timeScale = 1; mission?.Restart(); }

        [Test] public void NormalFlightAfterNarrativeSkipMovesIntactFleetAndUpdatesBothRenderTiers()
        {
            var simulation = mission.combat;
            var renderer = simulation.fleetRenderer;
            var diagnostics = simulation.gameObject.AddComponent<FleetEscapeDiagnostics>();
            diagnostics.simulation = simulation; diagnostics.captureEnabled = true; diagnostics.Bind();
            var ids = mission.targets.Select(t => t.targetId).ToArray();
            var initial = mission.targets.ToDictionary(t => t, t => t.transform.position);
            mission.StartMission();
            Assert.AreEqual(MissionState.Narrative, mission.State);
            mission.narrative.Step(2); mission.narrative.Skip();
            Assert.AreEqual(MissionState.Playing, mission.State);
            // Normal motor path and collision drive the threat; never RequestRetreat,
            // ApplyDamage, ForceFlee, target teleport or a diagnostic damage setter.
            for (int i = 0; i < 750; i++) mission.Step(.02f, default);
            renderer.RefreshNow();
            Assert.Greater(mission.DestroyedCount + mission.PendingCount, 0);
            var fleeing = mission.targets.Where(t => t.DamageState == ShipDamageState.Intact &&
                (t.BehaviorState == ShipBehaviorState.Fleeing || t.BehaviorState == ShipBehaviorState.BreakingFormation)).ToArray();
            Assert.Greater(fleeing.Length, 0);
            Assert.IsTrue(fleeing.Any(t => Vector3.Distance(initial[t], t.transform.position) > 80));
            Assert.IsTrue(diagnostics.Samples.Any(s => s.cause == "NearbyPenetration" && s.checkpointSeconds == 10 &&
                s.damage == "Intact" && s.displacementMeters.magnitude > 8000));
            Assert.IsTrue(diagnostics.Samples.Where(s => s.checkpointSeconds > 0).All(s => s.renderedErrorMeters < .1f));

            var moving = fleeing.OrderByDescending(t => Vector3.Distance(initial[t], t.transform.position)).First();
            float oldNear = renderer.nearDistance, oldHysteresis = renderer.transitionHysteresis;
            try
            {
                foreach (bool near in new[] { true, false, true, false })
                {
                    renderer.nearDistance = near ? 100000 : 10; renderer.transitionHysteresis = 0;
                    renderer.RefreshNow();
                    var state = moving.BehaviorState; Vector3 before = moving.transform.position;
                    for (int i = 0; i < 5; i++) mission.Step(.02f, default);
                    renderer.RefreshNow();
                    Assert.Greater(Vector3.Distance(before, moving.transform.position), .1f);
                    Assert.AreEqual(state, moving.BehaviorState, "Changing presentation must not reset AI.");
                    Assert.IsTrue(renderer.TryGetPresentationPose(moving, out var matrix, out var instanced));
                    Assert.AreEqual(!near, instanced);
                    Assert.That(Vector3.Distance((Vector3)matrix.GetColumn(3), moving.transform.position), Is.LessThan(.001));
                    Assert.IsTrue(moving.hitVolumes.All(c => c.enabled == near));
                    // The authoritative hit model follows the moving root even when
                    // presentation streams out its native Physics colliders.
                    Assert.IsTrue(simulation.RaycastShips(moving.transform.position - moving.transform.forward * 70,
                        moving.transform.forward, 140, out var hit));
                    Assert.AreSame(moving, hit.ship);
                }
            }
            finally { renderer.nearDistance = oldNear; renderer.transitionHysteresis = oldHysteresis; }
            Assert.AreEqual(2000, simulation.TotalCount);
            CollectionAssert.AreEqual(ids, mission.targets.Select(t => t.targetId));
            Object.DestroyImmediate(diagnostics);
        }
    }
}
