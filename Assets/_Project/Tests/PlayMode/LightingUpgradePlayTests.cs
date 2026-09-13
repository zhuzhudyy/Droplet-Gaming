using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DropletPrototype.Tests.PlayMode
{
    // Only loads the saved new scene. Does not invoke authoring or write source assets.
    public sealed class LightingUpgradePlayTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_Lighting.unity";
        MissionController mission;
        MissionEffects effects;
        SolarLightingRig rig;

        [TearDown]
        public void Cleanup()
        {
            if (effects != null) effects.Quality = EffectQuality.High;
            if (rig != null) { rig.engineEffects = true; rig.enhancedReflections = true; rig.bloomEnabled = true; rig.ApplyLighting(); }
            if (mission != null) mission.Restart();
            mission = null; effects = null; rig = null;
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_Lighting", LoadSceneMode.Single);
#endif
            yield return null;
            mission = Object.FindObjectsByType<MissionController>(FindObjectsSortMode.None).Single();
            effects = Object.FindObjectsByType<MissionEffects>(FindObjectsSortMode.None).Single();
            rig = Object.FindObjectsByType<SolarLightingRig>(FindObjectsSortMode.None).Single();
            Assert.AreEqual(MissionState.Ready, mission.State);
            Assert.AreEqual(120, mission.TotalCount);
            Assert.AreEqual(540, mission.Remaining);
            mission.enabled = false;
            mission.motor.enabled = false;
            mission.input.enabled = false;
            effects.Quality = EffectQuality.High;
            rig.engineEffects = true;
            rig.ApplyLighting();
            Physics.SyncTransforms();
        }

        static Renderer[] AllEffects(FusionDriveVisuals drive)
            => drive.coreRenderers.Concat(drive.detailRenderers).ToArray();

        [UnityTest]
        public IEnumerator SolarLightingStaysInWorldDirectionAcrossViewpointsAndPause()
        {
            yield return LoadScene();
            var camera = rig.backdrop.observer;
            mission.chaseCamera.enabled = false;
            Vector3[] positions = { mission.spawnPosition, new Vector3(950, 720, -1150), new Vector3(3000, 350, -2200) };
            Vector3[] oldTargets = mission.targets.Select(t => t.transform.position).ToArray();
            foreach (Vector3 position in positions)
            {
                camera.transform.SetPositionAndRotation(position, Quaternion.Euler(25, 130, 0));
                rig.backdrop.ApplyMapping(camera);
                rig.ApplyLighting();
                var toSun = (rig.backdrop.sunProxy.position - position).normalized;
                Assert.Greater(Vector3.Dot(-rig.sunLight.transform.forward, toSun), .99999f);
                Quaternion lightRotation = rig.sunLight.transform.rotation;
                camera.transform.rotation = Quaternion.Euler(-15, -60, 0);
                rig.backdrop.ApplyMapping(camera);
                rig.ApplyLighting();
                Assert.Less(Quaternion.Angle(lightRotation, rig.sunLight.transform.rotation), .001f,
                    "Turning the camera must not rotate the sun or the illumination.");
                CollectionAssert.AreEqual(oldTargets, mission.targets.Select(t => t.transform.position));
            }
            mission.StartMission();
            mission.TogglePause();
            float remaining = mission.Remaining;
            Vector3 player = mission.motor.transform.position;
            mission.Step(2, new FlightCommand { throttle = 1, boost = true });
            yield return new WaitForSecondsRealtime(.05f);
            Assert.AreEqual(remaining, mission.Remaining);
            Assert.AreEqual(player, mission.motor.transform.position);
            Assert.AreEqual(0, Time.timeScale);
            mission.Restart();
            mission.chaseCamera.enabled = true;
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(MissionState.Ready, mission.State);
        }

        [UnityTest]
        public IEnumerator FiveDrivesRespectEveryLodQualityDestructionAndRestore()
        {
            yield return LoadScene();
            var target = mission.targets.OrderBy(t => (t.transform.position - mission.spawnPosition).sqrMagnitude).First();
            var drive = target.GetComponentInChildren<FusionDriveVisuals>(true);
            var lod = target.GetComponentInChildren<LODGroup>(true);
            Assert.IsNotNull(drive);
            Assert.AreEqual(15, drive.coreRenderers.Length);
            Assert.AreEqual(10, drive.detailRenderers.Length);
            var renderers = AllEffects(drive);
            var materials = renderers.SelectMany(r => r.sharedMaterials).ToArray();
            int nodes = target.GetComponentsInChildren<Transform>(true).Length;
            foreach (var quality in new[] { EffectQuality.High, EffectQuality.Low, EffectQuality.Off })
            {
                effects.Quality = quality;
                rig.ApplyLighting();
                Assert.AreEqual(quality, drive.Quality);
                Assert.IsTrue(drive.coreRenderers.All(r => r.forceRenderingOff == (quality == EffectQuality.Off)));
                Assert.IsTrue(drive.detailRenderers.All(r => r.forceRenderingOff == (quality != EffectQuality.High)));
                for (int level = 0; level < 3; level++)
                {
                    mission.Restart();
                    lod.ForceLOD(level);
                    mission.StartMission();
                    Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
                    FlyThrough(target);
                    Assert.IsTrue(target.IsDestroyed, "Quality or LOD must not change swept gameplay.");
                    Assert.AreEqual(1, mission.DestroyedCount);
                    Assert.Greater(mission.score.Score, 0);
                    Assert.IsTrue(renderers.All(r => !r.gameObject.activeInHierarchy), "No floating core may survive the intact visual.");
                    Assert.IsFalse(target.TryDestroy());
                    mission.Restart();
                    Assert.IsTrue(renderers.All(r => r.gameObject.activeInHierarchy));
                    Assert.AreEqual(quality, drive.Quality);
                    Assert.AreEqual(nodes, target.GetComponentsInChildren<Transform>(true).Length);
                    CollectionAssert.AreEqual(materials, renderers.SelectMany(r => r.sharedMaterials));
                    Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
                }
                yield return null;
            }
            lod.ForceLOD(-1);
            effects.Quality = EffectQuality.High;
            rig.engineEffects = false;
            rig.ApplyLighting();
            Assert.IsTrue(renderers.All(r => r.forceRenderingOff));
            rig.engineEffects = true;
            rig.ApplyLighting();
            Assert.IsTrue(renderers.All(r => !r.forceRenderingOff));
            Assert.IsEmpty(target.GetComponentsInChildren<Light>(true));
        }

        [UnityTest]
        public IEnumerator DestroyAndRestartInvalidateLocalReflectionsAndQualityDisablesCapture()
        {
            yield return LoadScene();
            Assert.AreEqual(1, Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.IsTrue(QualitySettings.realtimeReflectionProbes, "The scene must opt in to Unity's realtime-probe quality gate.");
            // This is a rendered Play-mode check. Wait for an actual sliced capture,
            // so the invalidation assertions cannot pass merely on startup defaults.
            float deadline = Time.realtimeSinceStartup + 15;
            while (!rig.ProbeHasFreshCapture && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(rig.ProbeHasFreshCapture, "The configured realtime probe did not finish a real rendered capture.");
            Assert.Greater(rig.localProbe.size.x, 0);
            Assert.IsNotNull(rig.localProbe.realtimeTexture);
            var target = mission.targets[0];
            mission.StartMission();
            target.TryDestroy();
            Assert.IsFalse(rig.ProbeHasFreshCapture);
            Assert.IsNull(rig.localProbe.realtimeTexture, "Immediately withdraw stale fleet reflections after destruction.");
            Assert.AreEqual(Vector3.zero,rig.localProbe.size, "Unity retains a native texture fallback; zero influence must exclude the destroyed fleet from the probe atlas.");
            mission.Restart();
            Assert.IsFalse(rig.ProbeHasFreshCapture);
            Assert.IsNull(rig.localProbe.realtimeTexture);
            effects.Quality = EffectQuality.Low;
            rig.ApplyLighting();
            Assert.IsFalse(rig.localProbe.enabled);
            int captures = rig.CaptureCount;
            yield return null;
            yield return null;
            Assert.AreEqual(captures, rig.CaptureCount, "Low quality must not schedule local cubemap work.");
            effects.Quality = EffectQuality.High;
            rig.enhancedReflections = false;
            rig.ApplyLighting();
            Assert.IsFalse(rig.localProbe.enabled);
            Assert.IsTrue(mission.targets.All(t => !t.IsDestroyed && t.hitVolumes.All(c => c.enabled)));
        }

        [UnityTest]
        public IEnumerator LightingFleetCompletesThroughSweptMovementAndThreeRestartsReuseVisuals()
        {
            yield return LoadScene();
            effects.InitializePool();
            var targetReferences = mission.targets.ToArray();
            var materialReferences = Materials(mission.targets).ToArray();
            var meshReferences = Meshes(mission.targets).ToArray();
            var drives = mission.targets.Select(t => t.GetComponentInChildren<FusionDriveVisuals>(true)).ToArray();
            int nodes = mission.targets.Sum(t => t.GetComponentsInChildren<Transform>(true).Length);
            int pool = effects.PoolInstanceCount;
            var events = mission.targets.ToDictionary(t => t, _ => 0);
            foreach (var target in mission.targets) target.Destroyed += t => events[t]++;
            for (int run = 0; run < 3; run++)
            {
                mission.Restart();
                mission.StartMission();
                foreach (var target in mission.targets)
                    if (!target.IsDestroyed) FlyThrough(target);
                Assert.AreEqual(120, mission.DestroyedCount);
                Assert.AreEqual(MissionState.Results, mission.State);
                Assert.IsTrue(mission.Won);
                Assert.AreEqual(1, mission.ResultTransitions);
                Assert.IsTrue(events.Values.All(v => v == run + 1));
                Assert.IsTrue(drives.SelectMany(AllEffects).All(r => !r.gameObject.activeInHierarchy));
                int score = mission.score.Score;
                mission.Step(1, default);
                Assert.AreEqual(score, mission.score.Score);
                mission.Restart();
                yield return null;
                Assert.AreEqual(MissionState.Ready, mission.State);
                Assert.AreEqual(0, mission.DestroyedCount);
                Assert.AreEqual(0, mission.score.Score);
                Assert.AreEqual(540, mission.Remaining);
                Assert.AreEqual(1, Time.timeScale);
                Assert.AreEqual(mission.spawnPosition, mission.motor.transform.position);
                Assert.AreEqual(0, effects.ActiveEffectCount);
                Assert.AreEqual(0, effects.ActiveAudioCount);
                Assert.AreEqual(pool, effects.PoolInstanceCount);
                Assert.AreEqual(nodes, mission.targets.Sum(t => t.GetComponentsInChildren<Transform>(true).Length));
                CollectionAssert.AreEqual(targetReferences, mission.targets);
                CollectionAssert.AreEquivalent(materialReferences, Materials(mission.targets));
                CollectionAssert.AreEquivalent(meshReferences, Meshes(mission.targets));
                Assert.IsTrue(mission.targets.All(t => !t.IsDestroyed && t.visualRoot.activeSelf && t.hitVolumes.All(c => c.enabled)));
                Assert.IsTrue(drives.SelectMany(AllEffects).All(r => r.gameObject.activeInHierarchy && !r.forceRenderingOff));
            }
            Debug.Log("Lighting scene: 120 saved targets completed by actual motor sweeps, three victories/restarts; engine geometry, shared meshes/materials and impact pool retained. Teleports are isolated approach setup, not a measured human flight route.");
        }

        void FlyThrough(ShipTarget target)
        {
            const float step = .02f;
            var main = target.hitVolumes.OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
            Vector3 forward = target.transform.forward;
            Vector3 absForward = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
            Vector3 center = main.bounds.center;
            float extent = target.hitVolumes.Max(c => Mathf.Abs(Vector3.Dot(c.bounds.center - center, forward)) + Vector3.Dot(c.bounds.extents, absForward));
            float margin = mission.settings.hitRadius + 2;
            mission.motor.ResetPose(center - forward * (extent + margin), target.transform.rotation);
            Physics.SyncTransforms();
            int steps = Mathf.CeilToInt(2 * (extent + margin) / (mission.settings.initialSpeed * step)) + 2;
            for (int i = 0; i < steps && mission.State == MissionState.Playing; i++) mission.Step(step, default);
            Assert.IsTrue(target.IsDestroyed, target.targetId + " was not crossed by the motor sweep.");
            Assert.AreEqual(0, mission.RecoveryCount);
        }

        static IEnumerable<Material> Materials(IEnumerable<ShipTarget> targets)
            => targets.SelectMany(t => t.visualRoot.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials).Distinct();

        static IEnumerable<Mesh> Meshes(IEnumerable<ShipTarget> targets)
            => targets.SelectMany(t => t.visualRoot.GetComponentsInChildren<MeshFilter>(true))
                .Select(f => f.sharedMesh).Distinct();
    }
}
