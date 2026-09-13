using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Actual mission/sweep integration and hard VFX-budget regressions.</summary>
    public sealed class ReactorExplosionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<Object> assets = new List<Object>();
        DropletSettings flight;
        MissionController mission;
        DropletMotor motor;
        ReactorExplosionPool pool;
        ShipTarget[] targets;
        ReactorDestructionPresenter[] presenters;
        readonly Vector4[] sources = new Vector4[4], colors = new Vector4[4];

        GameObject New(string name)
        {
            var go = new GameObject("Reactor fixture " + name); objects.Add(go); return go;
        }
        void SetupRig(int count = 2, bool unsafeVisual = false)
        {
            Time.timeScale = 1;
            flight = ScriptableObject.CreateInstance<DropletSettings>(); assets.Add(flight);
            flight.targetLayers = 1 << 31; flight.hitRadius = .25f;
            flight.initialSpeed = flight.maxCruiseSpeed = 200; flight.timeBonusPerSecond = 0;
            var player = New("Player"); motor = player.AddComponent<DropletMotor>();
            motor.settings = flight; motor.enabled = false;
            var detector = player.AddComponent<DropletHitDetector>(); detector.settings = flight; motor.hitDetector = detector;
            var systems = New("Mission"); var score = systems.AddComponent<ScoreSystem>(); score.settings = flight;
            mission = systems.AddComponent<MissionController>(); mission.enabled = false;
            mission.settings = flight; mission.score = score; mission.motor = motor;
            mission.spawnPosition = Vector3.zero; mission.arenaCenter = Vector3.zero;
            pool = New("Pool").AddComponent<ReactorExplosionPool>();
            pool.mission = mission; pool.focus = player.transform;
            // Rendering assets are optional by design. These tests isolate the
            // gameplay, phase and resource contracts from shader availability.
            pool.InitializePool();
            targets = new ShipTarget[count]; presenters = new ReactorDestructionPresenter[count];
            for (int i = 0; i < count; i++)
            {
                var root = New("Ship " + i); root.transform.position = Vector3.forward * (5 + i * 20);
                ShipTarget target = root.AddComponent<ShipTarget>(); target.targetId = "reactor_test_" + i;
                var visual = new GameObject("VisualRoot"); visual.transform.SetParent(root.transform, false);
                target.visualRoot = visual;
                if (unsafeVisual) visual.AddComponent<BoxCollider>();
                var marker = new GameObject("AuthoredInternalReactor"); marker.transform.SetParent(visual.transform, false);
                marker.transform.localPosition = new Vector3(0, -.05f, -2);
                target.hitVolumes = new Collider[3];
                for (int c = 0; c < 3; c++)
                {
                    var hit = new GameObject("CompoundHull_" + c); hit.layer = 31; hit.transform.SetParent(root.transform, false);
                    hit.transform.localPosition = Vector3.right * (c - 1) * .2f;
                    var box = hit.AddComponent<BoxCollider>(); box.isTrigger = true;
                    box.size = new Vector3(.7f, 1, .01f); target.hitVolumes[c] = box;
                }
                var presenter = root.AddComponent<ReactorDestructionPresenter>();
                presenter.target = target; presenter.pool = pool; presenter.visualRoot = visual;
                presenter.reactorMarker = marker.transform; presenter.Bind();
                targets[i] = target; presenters[i] = presenter;
            }
            mission.targets = targets; mission.Initialize(); Physics.SyncTransforms();
        }

        [TearDown] public void Cleanup()
        {
            if (mission != null) Object.DestroyImmediate(mission);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear(); Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        [UnityTest] public IEnumerator SweptPenetrationCommitsScoreBeforeShortReactorInstabilityAndMainBurst()
        {
            SetupRig(); mission.StartMission(); mission.Step(.04f, default);
            Assert.AreEqual(1, mission.DestroyedCount); Assert.AreEqual(100, mission.score.Score);
            Assert.IsTrue(targets[0].IsDestroyed); Assert.IsTrue(presenters[0].IsUnstable);
            Assert.IsTrue(targets[0].visualRoot.activeSelf, "Only the collider-free intact view persists for the .16 second reactor warning.");
            foreach (Collider collider in targets[0].hitVolumes) Assert.IsFalse(collider.enabled);
            Assert.AreEqual(1, pool.ActiveEffectCount); Assert.IsFalse(targets[1].IsDestroyed);
            Assert.IsFalse(pool.TryPlay(presenters[0]), "Duplicate presentation wiring cannot allocate a second explosion for one ship.");
            Assert.AreEqual(0, motor.hitDetector.SweepSegment(Vector3.forward * 10, Vector3.zero));
            Assert.AreEqual(100, mission.score.Score, "The temporarily retained visual must never be a second scoring target.");
            Assert.AreEqual(1, pool.CopyReflectionSources(sources, colors));
            Vector3 reactor = presenters[0].reactorMarker.position;
            Assert.That(Vector3.Distance(reactor, new Vector3(sources[0].x, sources[0].y, sources[0].z)), Is.LessThan(.0001f));
            Assert.Greater(colors[0].z, colors[0].x, "Instability is initially blue-white at the authored reactor.");
            yield return new WaitForSeconds(.22f);
            Assert.IsFalse(presenters[0].IsUnstable); Assert.IsFalse(targets[0].visualRoot.activeSelf);
            Assert.AreEqual(1, pool.ActiveEffectCount);
            Assert.AreEqual(1, pool.CopyReflectionSources(sources, colors)); Assert.Greater(colors[0].x, colors[0].z);
            Assert.LessOrEqual(pool.ActiveLightCount, 2); Assert.AreEqual(100, mission.score.Score);
        }

        [TestCase(EffectQuality.High, 12, 2)]
        [TestCase(EffectQuality.Low, 6, 1)]
        [TestCase(EffectQuality.Off, 0, 0)]
        public void OneHundredTwentySimultaneousKillsRespectHardBudgetsAndNeverLoseGameplay(EffectQuality quality, int capacity, int lights)
        {
            SetupRig(120); pool.Quality = quality; int warmed = pool.PoolInstanceCount;
            mission.StartMission();
            foreach (ShipTarget target in targets)
                Assert.IsTrue(target.TryDestroy(new ShipHitContext(target.transform.position, Vector3.forward, 156)));
            mission.Step(.001f, default);
            Assert.AreEqual(120, mission.DestroyedCount); Assert.IsTrue(mission.Won); Assert.AreEqual(1, mission.ResultTransitions);
            Assert.Greater(mission.score.Score, 0);
            Assert.AreEqual(capacity, pool.Capacity); Assert.AreEqual(capacity, pool.ActiveEffectCount);
            Assert.LessOrEqual(pool.PeakActiveEffectCount, capacity); Assert.LessOrEqual(pool.ActiveLightCount, lights);
            if (capacity > 0) Assert.Greater(pool.DroppedEffectCount + pool.ReclaimedEffectCount, 0);
            Assert.AreEqual(warmed, pool.PoolInstanceCount);
            Transform subtree = pool.transform.Find("__ReactorExplosionPool"); Assert.IsNotNull(subtree);
            Assert.AreEqual(0, subtree.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, subtree.GetComponentsInChildren<Rigidbody>(true).Length);
            Assert.AreEqual(0, subtree.GetComponentsInChildren<ShipTarget>(true).Length);
            Assert.AreEqual(0, subtree.GetComponentsInChildren<ParticleSystem>(true).Length);
            Assert.AreEqual(2, subtree.GetComponentsInChildren<Light>(true).Length);
            foreach (Light light in subtree.GetComponentsInChildren<Light>(true)) Assert.AreEqual(LightShadows.None, light.shadows);
            foreach (ShipTarget target in targets) foreach (Collider collider in target.hitVolumes) Assert.IsFalse(collider.enabled);
            Assert.LessOrEqual(pool.CopyReflectionSources(sources, colors), 4);
            mission.Restart();
            Assert.AreEqual(0, pool.ActiveEffectCount); Assert.AreEqual(0, pool.ActiveLightCount);
            Assert.AreEqual(0, pool.CopyReflectionSources(sources, colors));
            Assert.AreEqual(warmed, pool.PoolInstanceCount);
            foreach (ShipTarget target in targets)
            {
                Assert.IsFalse(target.IsDestroyed); Assert.IsTrue(target.visualRoot.activeSelf);
                foreach (Collider collider in target.hitVolumes) Assert.IsTrue(collider.enabled);
            }
        }

        [UnityTest] public IEnumerator PauseFreezesInstabilityAndRestartOrDisableCannotLeaveLiveViewsOrLights()
        {
            SetupRig(); int warmed = pool.PoolInstanceCount;
            mission.StartMission(); mission.Step(.04f, default); mission.TogglePause();
            float age = pool.ElapsedSimulationTime; float remaining = mission.Remaining;
            Assert.IsTrue(presenters[0].IsUnstable);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(age, pool.ElapsedSimulationTime); Assert.AreEqual(remaining, mission.Remaining);
            Assert.IsTrue(presenters[0].IsUnstable); Assert.IsTrue(targets[0].visualRoot.activeSelf);
            mission.TogglePause(); yield return new WaitForSeconds(.04f);
            Assert.Greater(pool.ElapsedSimulationTime, age);
            mission.Restart();
            Assert.IsFalse(presenters[0].IsUnstable); Assert.IsTrue(targets[0].visualRoot.activeSelf);
            Assert.AreEqual(0, pool.ActiveEffectCount); Assert.AreEqual(0, pool.ActiveLightCount);
            Assert.AreEqual(0, pool.ElapsedSimulationTime); Assert.AreEqual(0, mission.score.Score);
            for (int run = 0; run < 2; run++)
            {
                pool.enabled = true; presenters[0].Bind(); pool.InitializePool();
                mission.StartMission(); mission.Step(.04f, default);
                Assert.AreEqual(1, pool.ActiveEffectCount);
                pool.enabled = false;
                Assert.IsFalse(presenters[0].IsUnstable); Assert.IsFalse(targets[0].visualRoot.activeSelf);
                Assert.AreEqual(0, pool.ActiveEffectCount); Assert.AreEqual(0, pool.ActiveLightCount);
                Assert.AreEqual(0, pool.CopyReflectionSources(sources, colors));
                Assert.AreEqual(100, mission.score.Score); Assert.IsTrue(targets[0].IsDestroyed);
                mission.Restart(); Assert.IsTrue(targets[0].visualRoot.activeSelf);
                Assert.AreEqual(warmed, pool.PoolInstanceCount);
            }
        }

        [Test] public void MalformedVisualContainingColliderCannotBeReactivatedForInstability()
        {
            SetupRig(2, true); mission.StartMission(); mission.Step(.04f, default);
            Assert.IsTrue(targets[0].IsDestroyed); Assert.AreEqual(100, mission.score.Score);
            Assert.IsFalse(targets[0].visualRoot.activeSelf, "Optional visual retention must reject any subtree containing physics.");
            Assert.AreEqual(1, pool.ActiveEffectCount, "An unsafe view must not suppress the independent reactor effect.");
            foreach (Collider collider in targets[0].hitVolumes) Assert.IsFalse(collider.enabled);
        }
    }
}
