using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class SeedAudioRuntimeTests
    {
        GameObject root;
        SeedAudioCatalog catalog;
        DropletSettings settings;
        AudioClip clip;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Seed audio runtime test");
            catalog = ScriptableObject.CreateInstance<SeedAudioCatalog>();
            settings = ScriptableObject.CreateInstance<DropletSettings>();
            settings.initialSpeed = settings.maxCruiseSpeed = 300f;
            settings.boostMultiplier = 5f;
            clip = AudioClip.Create("test recording placeholder", 24000, 1, 24000, false);
        }
        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(clip);
        }

        SeedAudioCue Cue(string id, string family) => new SeedAudioCue
        { id = id, family = family, clip = clip, gain = 1f, priority = 110 };

        MissionController CreateMission()
        {
            var motor = new GameObject("Motor").AddComponent<DropletMotor>();
            motor.transform.SetParent(root.transform);
            motor.settings = settings;
            var score = root.AddComponent<ScoreSystem>();
            score.settings = settings;
            var mission = root.AddComponent<MissionController>();
            mission.settings = settings;
            mission.motor = motor;
            mission.score = score;
            mission.targets = Array.Empty<ShipTarget>();
            mission.Initialize();
            return mission;
        }

        [Test]
        public void CatalogSkipsMissingRecordingsAndCyclesActualVariants()
        {
            catalog.cues = new[] { Cue("Laser_01", "Laser"),
                new SeedAudioCue { id = "Laser_02", family = "Laser", clip = null },
                Cue("Laser_03", "Laser") };
            Assert.AreEqual(1, catalog.MissingClipCount());
            Assert.IsFalse(catalog.TryGet("Laser_02", out _), "Missing Seed media must not silently fall back.");
            int cursor = 0;
            Assert.IsTrue(catalog.TryNext("Laser", ref cursor, out var first));
            Assert.IsTrue(catalog.TryNext("Laser", ref cursor, out var second));
            Assert.IsTrue(catalog.TryNext("Laser", ref cursor, out var third));
            Assert.AreEqual("Laser_01", first.id);
            Assert.AreEqual("Laser_03", second.id);
            Assert.AreEqual("Laser_01", third.id);
        }

        [Test]
        public void CruiseAndBoostHaveDifferentSpeedWeightsAndControlsUseCompletedSteps()
        {
            Assert.AreEqual(.2f, FlightAudioController.NormalizeSpeed(300f, 1500f), .0001f);
            Assert.AreEqual(1f, FlightAudioController.NormalizeSpeed(1500f, 1500f), .0001f);
            catalog.cues = new[] { Cue("Cruise_01", "Cruise"), Cue("BoostLoop_01", "BoostLoop"),
                Cue("BoostEnter_01", "BoostEnter"), Cue("BoostRelease_01", "BoostRelease"),
                Cue("Brake_01", "Brake"), Cue("Recover_01", "Recover"), Cue("Turn_01", "Turn") };
            var mission = CreateMission();
            var flight = root.AddComponent<FlightAudioController>();
            flight.mission = mission; flight.motor = mission.motor; flight.catalog = catalog; flight.Bind();
            mission.StartCombat();
            flight.AcceptStep(new FlightPresentationSample(1, 300f, 300f, 1500f, true, false, 0f));
            Assert.AreEqual(1, flight.TransientCount);
            flight.AcceptStep(new FlightPresentationSample(1, 300f, 300f, 1500f, true, false, 0f));
            Assert.AreEqual(1, flight.TransientCount, "The same completed motor step cannot replay its edge.");
            flight.AcceptStep(new FlightPresentationSample(2, 300f, 300f, 1500f, false, true, 0f));
            Assert.AreEqual(2, flight.TransientCount, "Brake wins when simultaneous edges share one transient voice.");
            mission.motor.ResetPose(Vector3.zero, Quaternion.identity);
            flight.AcceptStep(new FlightPresentationSample(3, 300f, 300f, 1500f, false, false, 0f));
            Assert.AreEqual(2, flight.TransientCount, "Teleport/restart must not sound like a brake release.");
            flight.AcceptStep(new FlightPresentationSample(4, 290f, 300f, 1500f, false, true, 0f));
            flight.AcceptStep(new FlightPresentationSample(5, 300f, 300f, 1500f, false, false, 0f));
            Assert.AreEqual(4, flight.TransientCount, "Real braking and recovery use separate cues.");
            flight.AcceptStep(new FlightPresentationSample(6, 300f, 300f, 1500f, false, false, 90f));
            flight.AcceptStep(new FlightPresentationSample(7, 300f, 300f, 1500f, false, false, 90f));
            Assert.AreEqual(5, flight.TransientCount, "A sustained turn is rate-limited.");
            Assert.AreEqual(5, flight.OwnedSourceCount);
        }

        [UnityTest]
        public IEnumerator LongFlightCrossfadesToSecondSeedLoopAndRestartClearsIt()
        {
            var second = AudioClip.Create("second Seed recording placeholder", 24000, 1, 24000, false);
            try
            {
                catalog.cues = new[]
                {
                    Cue("Cruise_01", "Cruise"),
                    new SeedAudioCue { id = "Cruise_02", family = "Cruise", clip = second, gain = 1f },
                    Cue("BoostLoop_01", "BoostLoop"),
                    new SeedAudioCue { id = "BoostLoop_02", family = "BoostLoop", clip = second, gain = 1f }
                };
                var mission = CreateMission();
                var flight = root.AddComponent<FlightAudioController>();
                flight.mission = mission;
                flight.motor = mission.motor;
                flight.catalog = catalog;
                flight.loopCrossfadeSeconds = .1f;
                flight.Bind();
                mission.StartCombat();
                // This fixture has zero ships, so MissionController.Update would
                // immediately resolve a result on the next frame and stop audio.
                // Keep its Playing state while this test advances only presentation time.
                mission.enabled = false;
                Assert.AreSame(clip, flight.CruiseSource.clip);
                Assert.AreSame(clip, flight.BoostSource.clip);

                flight.CruiseSource.time = clip.length - .08f;
                flight.BoostSource.time = clip.length - .08f;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.AreSame(second, flight.CruiseSource.clip,
                    "A long mission must reach the second cruise recording without a restart.");
                Assert.AreSame(second, flight.BoostSource.clip,
                    "A long mission must reach the second boost recording without a restart.");
                Assert.AreEqual(5, flight.OwnedSourceCount);

                mission.TogglePause();
                var selectedCruise = flight.CruiseSource;
                var selectedBoost = flight.BoostSource;
                float pausedAt = selectedCruise.time;
                yield return new WaitForSecondsRealtime(.05f);
                Assert.AreSame(selectedCruise, flight.CruiseSource);
                Assert.AreSame(selectedBoost, flight.BoostSource);
                Assert.AreEqual(pausedAt, selectedCruise.time, .03f);
                mission.TogglePause();
                mission.Restart();
                Assert.IsNull(flight.CruiseSource.clip);
                Assert.IsNull(flight.BoostSource.clip);
            }
            finally { UnityEngine.Object.DestroyImmediate(second); }
        }

        [Test]
        public void PauseKeepsSelectedMusicAndRestartClearsOldStage()
        {
            catalog.cues = new[] { Cue("Ready_01", "Ready"), Cue("BattleLow_01", "BattleLow"),
                Cue("SpaceTexture_01", "SpaceTexture"), Cue("PauseBed_01", "PauseBed") };
            var mission = CreateMission();
            var stage = root.AddComponent<StageAudioDirector>();
            stage.mission = mission; stage.catalog = catalog; stage.Bind();
            mission.StartCombat();
            var playingSource = stage.ActiveMusicSource;
            Assert.AreEqual("BattleLow", stage.CurrentMusicSelection);
            Assert.AreSame(clip, playingSource.clip);
            mission.TogglePause();
            Assert.IsTrue(stage.IsPaused);
            Assert.AreSame(playingSource, stage.ActiveMusicSource);
            mission.TogglePause();
            Assert.IsFalse(stage.IsPaused);
            Assert.AreSame(playingSource, stage.ActiveMusicSource,
                "Resume must continue the same AudioSource instead of restarting the cue.");
            mission.Restart();
            Assert.AreEqual("Ready_01", stage.CurrentMusicSelection);
            Assert.AreEqual(5, stage.OwnedSourceCount);
            Assert.AreEqual(0, root.GetComponentsInChildren<AudioListener>().Length);
        }

        [Test]
        public void MotorReportsOnlyCompletedMovementAndResetDoesNotReplayAFlightStep()
        {
            var motor = root.AddComponent<DropletMotor>();
            motor.settings = settings;
            int received = 0;
            FlightPresentationSample last = default;
            motor.FlightStepCompleted += sample => { received++; last = sample; };
            motor.Simulate(.02f, new FlightCommand { boost = true, look = new Vector2(4f, 0f) });
            Assert.AreEqual(1, received);
            Assert.Greater(last.StepId, 0);
            Assert.IsTrue(last.Boosting);
            Assert.AreEqual(1500f, last.MaximumSpeed, .0001f);
            Assert.AreEqual(motor.Speed, last.Speed, .0001f);
            Assert.Greater(last.TurnDegreesPerSecond, 0f);
            motor.ResetPose(Vector3.zero, Quaternion.identity);
            motor.HoldSimulationPose();
            Assert.AreEqual(1, received, "A teleport or held pose cannot create an input edge sound.");
        }

        [Test]
        public void VisualQualityOffRetainsTheLegacyFlightAudioBudget()
        {
            var effectSettings = ScriptableObject.CreateInstance<EffectSettings>();
            effectSettings.highAudioCapacity = 3;
            effectSettings.highEffectCapacity = 1;
            var effects = root.AddComponent<MissionEffects>();
            effects.settings = effectSettings;
            effects.InitializePool();
            effects.Quality = EffectQuality.Off;
            Assert.AreEqual(0, effects.Capacity);
            Assert.AreEqual(3, effects.AudioCapacity,
                "Turning off optional visuals must not turn off the flight audio pool.");
            UnityEngine.Object.DestroyImmediate(effectSettings);
        }
    }
}
