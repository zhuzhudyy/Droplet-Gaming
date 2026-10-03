using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Focused opt-in checks against the saved 2000-ship Seed scene.</summary>
    [Category("SeedAudioFullScene")]
    public sealed class SeedAudioIntegratedSceneTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_SeedAudio.unity";
        MissionController mission;
        StageAudioDirector stage;
        SeedAudioCatalog catalog;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(ScenePath) == null)
                Assert.Ignore("The accepted Seed scene has not yet been authored; run this opt-in suite after integration.");
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_SeedAudio");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>();
            stage = Object.FindAnyObjectByType<StageAudioDirector>();
            Assert.IsNotNull(mission);
            Assert.IsNotNull(stage);
            catalog = stage.catalog;
            mission.enabled = false;
            if (mission.input != null) mission.input.enabled = false;
        }

        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1f;
            if (mission != null) mission.Restart();
        }

        [Test]
        public void SavedFleetAndSeedCatalogContainAllIndependentRecordedCues()
        {
            Assert.AreEqual(2000, mission.TotalCount);
            Assert.AreEqual(2000, mission.targets.Select(s => s.targetId).Distinct().Count());
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>()
                .Count(source => source.isActiveAndEnabled));
            Assert.IsNotNull(catalog);
            Assert.AreEqual(131, catalog.cues.Length);
            Assert.AreEqual(0, catalog.MissingClipCount());
            Assert.AreEqual(131, catalog.cues.Select(c => c.id).Distinct().Count());
            Assert.AreEqual(131, catalog.cues.Select(c => c.sourceHash).Distinct().Count(),
                "Every cue must have its own selected Seed output, not a pitch-only duplicate.");
        }

        [UnityTest]
        public IEnumerator NarrativePauseSkipAndThreeRestartsRetainAudioAndGameplayState()
        {
            mission.ReplayNarrative();
            yield return null;
            Assert.AreEqual("Narrative_01", stage.CurrentMusicSelection);
            Assert.AreEqual("CabinBed_01", stage.CurrentAmbienceSelection);
            var source = stage.ActiveMusicSource;
            Assert.IsNotNull(source);
            Assert.IsNotNull(source.clip);
            mission.TogglePause();
            yield return new WaitForSecondsRealtime(.1f);
            int pausedSamples = source.timeSamples;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(stage.IsPaused);
            Assert.AreEqual(pausedSamples, source.timeSamples, 4096);
            mission.TogglePause();
            yield return null;
            Assert.AreSame(source, stage.ActiveMusicSource);
            mission.narrative.Skip();
            yield return null;
            Assert.AreEqual(MissionState.Playing, mission.State);
            Assert.AreEqual("BattleLow", stage.CurrentMusicSelection);
            Assert.AreEqual("SpaceTexture", stage.CurrentAmbienceSelection);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                mission.RestartIntoCombat();
                yield return null;
                Assert.AreEqual(2000, mission.TotalCount);
                Assert.AreEqual(0, mission.DestroyedCount);
                Assert.AreEqual(0, mission.PendingCount);
                Assert.AreEqual("BattleLow", stage.CurrentMusicSelection);
                Assert.AreEqual(1f, Time.timeScale);
            }
        }

        [UnityTest]
        public IEnumerator RealFleetExplosionFallsInsideThePreBlastCaptureWindow()
        {
            mission.RestartIntoCombat();
            mission.motor.SimulationEnabled = false;
            var target = mission.targets.Where(ship => ship != null && !ship.IsResolved)
                .OrderBy(ship => (ship.transform.position - mission.spawnPosition).sqrMagnitude).First();
            int capturedExplosions = 0;
            bool inBlastWindow = false;
            System.Action<CombatEvent> onEvent = message =>
            {
                if (inBlastWindow && message.kind == CombatEventKind.ShipExploded &&
                    message.targetId == target.targetId) capturedExplosions++;
            };
            mission.combat.EventRaised += onEvent;
            try
            {
                Assert.IsTrue(mission.combat.ApplyDamage(target,
                    new ShipHitContext(target.transform.position, Vector3.forward, 1500),
                    DamageSource.Penetration, 69002));
                float deadline = target.ExplosionAt;
                Assert.Greater(deadline - mission.combat.SimulatedTime, .35f);
                float beforeBlast = deadline - .35f;
                while (mission.combat.SimulatedTime < beforeBlast - .0001f)
                {
                    mission.Step(Mathf.Min(.1f, beforeBlast - mission.combat.SimulatedTime), default);
                    yield return null;
                }
                Assert.IsFalse(target.IsDestroyed, "The first radio capture must finish before the delayed explosion.");
                inBlastWindow = true;
                while (mission.combat.SimulatedTime < deadline + .05f - .0001f)
                {
                    mission.Step(Mathf.Min(.1f, deadline + .05f - mission.combat.SimulatedTime), default);
                    yield return null;
                }
                Assert.IsTrue(target.IsDestroyed);
                Assert.AreEqual(1, capturedExplosions,
                    "The actual 2000-ship simulation must emit one explosion inside the later recording window.");
            }
            finally { mission.combat.EventRaised -= onEvent; }
        }
    }

    public sealed class SeedAudioValidationIteratorTests
    {
        static IEnumerator Outer(System.Action cleanup)
        {
            try { yield return FailingChild(); }
            finally { cleanup(); }
        }

        static IEnumerator FailingChild()
        {
            yield return null;
            throw new System.InvalidOperationException("controlled nested failure");
        }

        [Test]
        public void NestedFailureDisposesParentsBeforeValidationReportsCompletion()
        {
            bool cleaned = false;
            System.Exception observed = null;
            var walker = SeedAudioValidationRunner.WalkNested(Outer(() => cleaned = true),
                exception => observed = exception);
            while (walker.MoveNext()) { }
            Assert.IsTrue(cleaned, "Run's finally must restore the original radio library on any child failure.");
            Assert.IsInstanceOf<System.InvalidOperationException>(observed);
        }
    }
}
