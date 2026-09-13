using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class PresentationRegressionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<Object> assets = new List<Object>();
        DropletSettings flight;
        EffectSettings effectSettings;
        DropletMotor motor;
        DropletHitDetector detector;
        MissionController mission;
        MissionEffects effects;
        ShipTarget[] targets;
        GameObject New(string name) { var go = new GameObject("G09 fixture " + name); objects.Add(go); return go; }

        void SetupRig(int effectCapacity = 1, int audioCapacity = 1)
        {
            flight = ScriptableObject.CreateInstance<DropletSettings>(); assets.Add(flight);
            flight.targetLayers = 1 << 31; flight.hitRadius = .25f;
            flight.initialSpeed = flight.maxCruiseSpeed = 200; flight.timeBonusPerSecond = 0;
            effectSettings = ScriptableObject.CreateInstance<EffectSettings>(); assets.Add(effectSettings);
            effectSettings.highEffectCapacity = effectSettings.lowEffectCapacity = effectCapacity;
            effectSettings.highAudioCapacity = effectSettings.lowAudioCapacity = audioCapacity;
            effectSettings.masterVolume = 0; effectSettings.wreckLifetime = 2;
            var player = New("Player"); motor = player.AddComponent<DropletMotor>(); motor.enabled = false; motor.settings = flight;
            detector = player.AddComponent<DropletHitDetector>(); detector.settings = flight; motor.hitDetector = detector;
            targets = new ShipTarget[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var root = New("Ship " + i); root.transform.position = Vector3.forward * (2 + i * 3);
                var target = root.AddComponent<ShipTarget>(); target.targetId = "fixture_" + i;
                var visual = new GameObject("VisualRoot"); visual.transform.SetParent(root.transform, false); target.visualRoot = visual;
                target.hitVolumes = new Collider[3];
                for (int j = 0; j < 3; j++)
                {
                    var hit = new GameObject("Thin compound hit " + j); hit.layer = 31; hit.transform.SetParent(root.transform, false);
                    hit.transform.localPosition = Vector3.right * ((j - 1) * .15f);
                    var collider = hit.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(.6f, 1, .01f);
                    target.hitVolumes[j] = collider;
                }
                targets[i] = target;
            }
            var systems = New("Mission"); var score = systems.AddComponent<ScoreSystem>(); score.settings = flight;
            mission = systems.AddComponent<MissionController>(); mission.enabled = false;
            mission.settings = flight; mission.motor = motor; mission.score = score; mission.targets = targets;
            mission.spawnPosition = Vector3.zero; mission.arenaCenter = Vector3.zero;
            var fxRoot = New("Effects"); effects = fxRoot.AddComponent<MissionEffects>();
            effects.settings = effectSettings; effects.mission = mission; effects.motor = motor;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); assets.Add(material);
            effects.flashMaterial = material; effects.sparkMaterial = material;
            var wreck = New("Wreck template");
            var fragment = new GameObject("Fragment"); fragment.transform.SetParent(wreck.transform, false);
            var mesh = new Mesh { name = "G09 fixture fragment", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateNormals(); assets.Add(mesh);
            fragment.AddComponent<MeshFilter>().sharedMesh = mesh; fragment.AddComponent<MeshRenderer>().sharedMaterial = material;
            wreck.SetActive(false); effects.wreckPrefabs = new[] { wreck };
            var clip = AudioClip.Create("G09 fixture sound", 24000, 1, 48000, false); assets.Add(clip);
            effects.impactClips = new[] { clip };
            effects.InitializePool();
            foreach (var target in targets)
            {
                var presenter = target.gameObject.AddComponent<DestructionPresenter>(); presenter.target = target; presenter.effects = effects; presenter.Bind();
            }
            mission.Initialize(); Physics.SyncTransforms();
        }

        [TearDown] public void Cleanup()
        {
            if (mission != null) Object.DestroyImmediate(mission);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear(); Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        [TestCase(EffectQuality.Off, 1)]
        [TestCase(EffectQuality.High, 0)]
        [TestCase(EffectQuality.High, 1)]
        public void DisabledOrExhaustedEffectsCannotChangeCompoundHighSpeedHitsOrScore(EffectQuality quality, int capacity)
        {
            SetupRig(capacity, capacity); effects.Quality = quality;
            mission.StartMission(); mission.Step(.1f, default);
            Assert.AreEqual(3, mission.DestroyedCount); Assert.IsTrue(mission.Won); Assert.AreEqual(1, mission.ResultTransitions);
            Assert.AreEqual(600, mission.score.Score, "Three identities earn 100 + 200 + 300 even when presentation is unavailable.");
            Assert.AreEqual(0, detector.SweepSegment(Vector3.forward * 20, Vector3.zero));
            Assert.AreEqual(600, mission.score.Score); Assert.IsTrue(targets.All(t => t.IsDestroyed));
            Assert.LessOrEqual(effects.ActiveEffectCount, effects.Capacity);
            Assert.LessOrEqual(effects.ActiveAudioCount, effects.AudioCapacity);
            if (quality == EffectQuality.High && capacity == 1)
            {
                Assert.AreEqual(1, effects.ActiveEffectCount); Assert.AreEqual(1, effects.ActiveAudioCount);
                Assert.AreEqual(2, effects.DroppedEffectCount, "One slot accepts one of three hits; the other two remain valid gameplay hits.");
            }
            else { Assert.AreEqual(0, effects.ActiveEffectCount); Assert.AreEqual(0, effects.ActiveAudioCount); }
            var pool = effects.transform.Find("__MissionEffectPool"); Assert.IsNotNull(pool);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Rigidbody>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<ShipTarget>(true).Length);
        }

        [Test] public void FaultyPresentationListenerCannotSuppressLaterScoringOrShipsAndHitContextUsesPath()
        {
            SetupRig();
            // Register the fault before a fresh Mission.Initialize rebinds its score listener.
            targets[0].Destroyed += _ => throw new InvalidOperationException("G09 deliberate optional presentation fault");
            mission.Initialize();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: G09 deliberate optional presentation fault"));
            mission.StartMission(); mission.Step(.1f, default);
            Assert.AreEqual(3, mission.DestroyedCount); Assert.AreEqual(600, mission.score.Score); Assert.IsTrue(mission.Won);
            foreach (var target in targets)
            {
                Assert.That(Vector3.Dot(target.LastHit.direction, Vector3.forward), Is.GreaterThan(.999f));
                Assert.AreEqual(200, target.LastHit.speed, .001f);
                Assert.That(Mathf.Abs(target.LastHit.point.z - target.transform.position.z), Is.LessThan(.02f));
                Assert.IsFalse(float.IsNaN(target.LastHit.point.x));
            }
        }

        [Test] public void SwitchingEffectsOffClearsActiveVisualsAndSoundWithoutChangingCommittedGameplay()
        {
            SetupRig(); mission.StartMission(); mission.Step(.015f, default);
            Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(1, effects.ActiveEffectCount); Assert.AreEqual(1, effects.ActiveAudioCount);
            int score = mission.score.Score; float remaining = mission.Remaining;
            effects.Quality = EffectQuality.Off;
            Assert.AreEqual(0, effects.ActiveEffectCount); Assert.AreEqual(0, effects.ActiveAudioCount);
            Assert.IsTrue(targets[0].IsDestroyed); Assert.AreEqual(1, mission.DestroyedCount);
            Assert.AreEqual(score, mission.score.Score); Assert.AreEqual(remaining, mission.Remaining);
            Assert.AreEqual(MissionState.Playing, mission.State);
            effects.Quality = EffectQuality.High;
            Assert.AreEqual(0, effects.ActiveEffectCount); Assert.IsTrue(targets[0].IsDestroyed);
            mission.Restart(); Assert.IsFalse(targets[0].IsDestroyed); Assert.AreEqual(0, mission.score.Score);
        }

        [UnityTest] public IEnumerator PauseFreezesActivePresentationAndMissionAndResumeAdvancesAgain()
        {
            SetupRig(); mission.StartMission(); mission.Step(.015f, default);
            Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(1, effects.ActiveEffectCount);
            yield return null;
            mission.TogglePause(); float elapsed = effects.ElapsedSimulationTime; float remaining = mission.Remaining;
            int score = mission.score.Score; Vector3 position = motor.transform.position;
            var transforms = effects.GetComponentsInChildren<Transform>(true);
            var poses = transforms.Select(t => t.localPosition).ToArray();
            yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(elapsed, effects.ElapsedSimulationTime); Assert.AreEqual(remaining, mission.Remaining);
            Assert.AreEqual(score, mission.score.Score); Assert.AreEqual(position, motor.transform.position);
            for (int i = 0; i < transforms.Length; i++) Assert.AreEqual(poses[i], transforms[i].localPosition);
            Assert.AreEqual(1, effects.ActiveEffectCount); Assert.AreEqual(1, effects.ActiveAudioCount);
            mission.TogglePause(); yield return new WaitForSeconds(.05f);
            Assert.Greater(effects.ElapsedSimulationTime, elapsed);
        }

        [Test] public void SafeTeleportAndRestartClearExistingTrailWithoutDamageAlongItsOldPositions()
        {
            SetupRig();
            var wake = motor.gameObject.AddComponent<DropletTrail>();
            wake.motor = motor; wake.mission = mission; wake.effects = effects; wake.settings = effectSettings;
            wake.trailMaterial = effects.flashMaterial; wake.Initialize();
            var renderer = wake.GetComponentInChildren<TrailRenderer>(); Assert.IsNotNull(renderer);
            renderer.AddPositions(new[] { Vector3.zero, Vector3.forward * 3, Vector3.forward * 6 });
            Assert.Greater(wake.PositionCount, 0);
            motor.ResetPose(Vector3.forward * 100, Quaternion.identity);
            Assert.AreEqual(0, wake.PositionCount); Assert.IsTrue(targets.All(t => !t.IsDestroyed)); Assert.AreEqual(0, mission.score.Score);
            renderer.AddPositions(new[] { Vector3.forward * 95, Vector3.forward * 100 });
            Assert.Greater(wake.PositionCount, 0);
            mission.Restart();
            Assert.AreEqual(0, wake.PositionCount); Assert.AreEqual(Vector3.zero, motor.transform.position);
            Assert.IsTrue(targets.All(t => !t.IsDestroyed)); Assert.AreEqual(0, mission.DestroyedCount);
        }

        [UnityTest] public IEnumerator ThreeRestartsAndRepeatedBindingReusePoolsAndClearAudioAndWrecks()
        {
            SetupRig(); int poolInstances = effects.PoolInstanceCount;
            int hierarchyCount = effects.GetComponentsInChildren<Transform>(true).Length;
            var identities = targets.Select(t => t.GetEntityId()).ToArray();
            for (int run = 0; run < 3; run++)
            {
                effects.InitializePool();
                foreach (var target in targets) target.GetComponent<DestructionPresenter>().Bind();
                mission.Initialize(); mission.StartMission(); mission.Step(.1f, default);
                Assert.AreEqual(3, mission.DestroyedCount); Assert.AreEqual(600, mission.score.Score);
                Assert.AreEqual(1, effects.ActiveEffectCount); Assert.AreEqual(2, effects.DroppedEffectCount);
                mission.Restart(); yield return null;
                Assert.AreEqual(MissionState.Ready, mission.State); Assert.AreEqual(0, mission.score.Score);
                Assert.AreEqual(0, mission.DestroyedCount); Assert.AreEqual(3, mission.TotalCount);
                Assert.AreEqual(0, effects.ActiveEffectCount); Assert.AreEqual(0, effects.ActiveAudioCount);
                Assert.AreEqual(poolInstances, effects.PoolInstanceCount);
                Assert.AreEqual(hierarchyCount, effects.GetComponentsInChildren<Transform>(true).Length);
                CollectionAssert.AreEqual(identities, targets.Select(t => t.GetEntityId()).ToArray());
                Assert.IsTrue(targets.All(t => !t.IsDestroyed && t.visualRoot.activeSelf && t.hitVolumes.All(c => c.enabled)));
                Assert.AreEqual(flight.missionSeconds, mission.Remaining); Assert.AreEqual(Vector3.zero, motor.transform.position);
                Assert.AreEqual(flight.initialSpeed, motor.Speed); Assert.AreEqual(1, Time.timeScale);
            }
        }

        [UnityTest] public IEnumerator ActualFleetWithImportedCompoundModelsCompletesAndRestartsThreeTimes()
        {
            yield return SceneManager.LoadSceneAsync("FleetAssault", LoadSceneMode.Single);
            yield return null;
            var live = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(live); Assert.AreEqual(40, live.TotalCount); Assert.AreEqual(MissionState.Ready, live.State);
            live.enabled = false; live.input.enabled = false;
            var liveEffects = Object.FindAnyObjectByType<MissionEffects>(); Assert.IsNotNull(liveEffects);
            Assert.IsNotEmpty(liveEffects.wreckPrefabs); Assert.IsTrue(liveEffects.wreckPrefabs.All(w => w != null));
            Assert.IsNotEmpty(liveEffects.impactClips); Assert.IsTrue(liveEffects.impactClips.All(c => c != null && c.samples > 0));
            foreach (var target in live.targets)
            {
                var presenter = target.GetComponent<DestructionPresenter>(); Assert.IsNotNull(presenter);
                Assert.AreSame(liveEffects, presenter.effects); Assert.Greater(target.hitVolumes.Length, 1);
            }
            int poolSize = liveEffects.PoolInstanceCount;
            var rootIds = live.targets.Select(t => t.GetEntityId()).ToArray();
            for (int run = 0; run < 3; run++)
            {
                live.StartMission();
                foreach (var target in live.targets)
                {
                    if (target.IsDestroyed) continue;
                    // Move to a clear approach point by the public safe teleport API, then
                    // exercise the existing real swept motor through imported compound geometry.
                    Vector3 forward = target.transform.forward;
                    float extent = target.hitVolumes.Max(c => Vector3.Dot(c.bounds.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z))));
                    live.motor.ResetPose(target.transform.position - forward * (extent + 3), target.transform.rotation);
                    Physics.SyncTransforms(); live.Step((extent + 6) / live.settings.initialSpeed, default);
                }
                Assert.IsTrue(live.Won); Assert.AreEqual(40, live.DestroyedCount); Assert.AreEqual(1, live.ResultTransitions);
                Assert.Greater(live.score.Score, 0); Assert.LessOrEqual(liveEffects.ActiveEffectCount, liveEffects.Capacity);
                live.Restart(); yield return null;
                Assert.AreEqual(MissionState.Ready, live.State); Assert.AreEqual(0, live.score.Score); Assert.AreEqual(0, live.DestroyedCount);
                Assert.AreEqual(40, Object.FindObjectsByType<ShipTarget>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(poolSize, liveEffects.PoolInstanceCount); Assert.AreEqual(0, liveEffects.ActiveEffectCount); Assert.AreEqual(0, liveEffects.ActiveAudioCount);
                CollectionAssert.AreEqual(rootIds, live.targets.Select(t => t.GetEntityId()).ToArray());
                Assert.IsTrue(live.targets.All(t => !t.IsDestroyed && t.visualRoot.activeSelf));
            }
            live.Restart();
        }
    }
}
