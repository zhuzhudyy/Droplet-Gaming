using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class SceneLoopTests
    {
        [UnityTest] public IEnumerator SavedTenShipSceneCanStartPauseWinAndRestartThreeTimes()
        {
            yield return SceneManager.LoadSceneAsync("TestRange", LoadSceneMode.Single);
            yield return null;
            var mission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(mission); Assert.AreEqual(10, mission.TotalCount); Assert.AreEqual(MissionState.Ready, mission.State);
            mission.enabled = false; // Drive the identical mission step deterministically after checking scene Awake.
            mission.input.enabled = false;
            for (int run = 0; run < 3; run++)
            {
                mission.StartMission(); mission.TogglePause();
                float remaining = mission.Remaining; Vector3 before = mission.motor.transform.position;
                yield return new WaitForSecondsRealtime(.03f);
                Assert.AreEqual(remaining, mission.Remaining); Assert.AreEqual(before, mission.motor.transform.position);
                mission.TogglePause();
                foreach (var target in mission.targets)
                {
                    // Teleport to a clear approach point, then perform a real swept flight through the authored ship.
                    mission.motor.ResetPose(target.transform.position - Vector3.forward * 12, Quaternion.identity);
                    Physics.SyncTransforms(); mission.Step(1, default);
                }
                Assert.IsTrue(mission.Won); Assert.AreEqual(10, mission.DestroyedCount); Assert.AreEqual(1, mission.ResultTransitions);
                mission.Restart(); Assert.AreEqual(MissionState.Ready, mission.State); Assert.AreEqual(0, mission.score.Score);
                Assert.AreEqual(10, Object.FindObjectsByType<ShipTarget>(FindObjectsSortMode.None).Length);
                foreach (var target in mission.targets) Assert.IsFalse(target.IsDestroyed);
            }
            Assert.IsNotNull(Object.FindAnyObjectByType<HudPresenter>());
        }
    }
}
