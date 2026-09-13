using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class NarrativeRadioTests
    {
        readonly List<Object> owned = new List<Object>();
        GameObject root;
        ShipTarget first, second;
        FleetCombatSimulation fleet;
        RadioController radio;
        RadioLibrary library;
        CombatScaleSettings scale;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [SetUp] public void Setup()
        {
            root = Own(new GameObject("NarrativeRadio test"));
            first = Ship("TEST-01", new Vector3(0, 0, 20)); second = Ship("TEST-02", new Vector3(100, 0, 20));
            scale = Own(ScriptableObject.CreateInstance<CombatScaleSettings>()); scale.explosionDelaySeconds = new Vector2(.4f, .4f);
            fleet = root.AddComponent<FleetCombatSimulation>(); fleet.Configure(new[] { first, second }, root.transform, scale);
            radio = root.AddComponent<RadioController>(); radio.enabled = false;
            library = Own(ScriptableObject.CreateInstance<RadioLibrary>());
            library.combat = new[] {
                new RadioLine { id = "a", eventKind = "AttackIneffective", text = "攻击没有效果", channel = "火控" },
                new RadioLine { id = "b", eventKind = "AttackIneffective", text = "没有减速", channel = "火控" },
                new RadioLine { id = "p", eventKind = "ReactorUnstable", text = "磁约束崩溃", channel = "损管", pendingOnly = true },
                new RadioLine { id = "r", eventKind = "RetreatOrdered", text = "撤离", channel = "舰队" }
            };
            library.narrative = new NarrativeLine[12];
            for (int i = 0; i < 12; i++) library.narrative[i] = new NarrativeLine { id = "n" + i, time = i * 5, duration = 5, text = "剧情" + i, channel = "测试", cameraShot = i % 4 };
            radio.library = library; radio.fleet = fleet; radio.BeginSession(11);
        }
        ShipTarget Ship(string id, Vector3 position)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform); go.transform.position = position;
            var ship = go.AddComponent<ShipTarget>(); ship.targetId = id; ship.hitVolumes = new Collider[] { go.AddComponent<BoxCollider>() }; return ship;
        }
        CombatEvent Event(CombatEventKind kind, ShipTarget speaker) => new CombatEvent(kind, speaker, speaker, speaker.transform.position, DamageSource.Penetration, fleet.SimulatedTime, fleet.Generation);
        [TearDown] public void Cleanup()
        {
            Time.timeScale = 1;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        [Test] public void RadioMatchesSpeakerStateDeduplicatesAndBoundsQueue()
        {
            radio.Receive(Event(CombatEventKind.ReactorUnstable, first)); Assert.AreEqual(0, radio.QueuedCount, "Intact ships cannot claim a reactor failure.");
            for (int i = 0; i < 100; i++) radio.Receive(Event(CombatEventKind.AttackIneffective, first));
            Assert.AreEqual(1, radio.QueuedCount);
            radio.Tick(.01f); Assert.AreEqual(1, radio.SpokenCount); Assert.AreSame(first, radio.CurrentSpeaker);
            Assert.Greater(radio.DroppedCount, 0, "Presentation budget must reject a message flood.");
        }
        [Test] public void PendingShipMaySpeakAndExplosionImmediatelyInterruptsItsAudioAndQueuedLines()
        {
            Assert.IsTrue(first.TryDestroy()); Assert.AreEqual(ShipDamageState.FatalPending, first.DamageState);
            radio.BeginSession(11); radio.Receive(Event(CombatEventKind.ReactorUnstable, first)); radio.Tick(.01f);
            Assert.AreSame(first, radio.CurrentSpeaker);
            fleet.BeginStep(.5f); fleet.EndStep(.5f);
            Assert.AreEqual(ShipDamageState.Exploded, first.DamageState); Assert.AreEqual(1, radio.InterruptedCount);
            Assert.IsNull(radio.CurrentSpeaker); StringAssert.Contains("通信中断", radio.CurrentText);
            radio.Receive(Event(CombatEventKind.AttackIneffective, first)); Assert.AreEqual(0, radio.QueuedCount);
        }
        [Test] public void ExpiredMessagesAndPreviousSessionEventsCannotSpeak()
        {
            library.eventExpiry = .1f; radio.Receive(Event(CombatEventKind.AttackIneffective, first)); radio.Tick(.2f);
            Assert.AreEqual(0, radio.QueuedCount); Assert.AreEqual(0, radio.SpokenCount);
            var stale = Event(CombatEventKind.AttackIneffective, second); fleet.ResetSimulation();
            radio.Receive(stale); radio.Tick(.01f); Assert.AreEqual(0, radio.SpokenCount); Assert.AreEqual(0, radio.History.Count);
        }
        [Test] public void QueuedIntactShipReportsAreDiscardedAfterFatalDamage()
        {
            radio.Receive(Event(CombatEventKind.AttackIneffective, first)); Assert.AreEqual(1, radio.QueuedCount);
            Assert.IsTrue(first.TryDestroy()); Assert.AreEqual(ShipDamageState.FatalPending, first.DamageState);
            radio.Tick(.01f); Assert.AreEqual(0, radio.SpokenCount); Assert.AreEqual(0, radio.QueuedCount);
            radio.Receive(Event(CombatEventKind.ReactorUnstable, first)); radio.Tick(.01f);
            Assert.AreEqual(1, radio.SpokenCount); Assert.AreSame(first, radio.CurrentSpeaker);
        }
        [UnityTest] public IEnumerator PauseFreezesRadioPlaybackAndQueueLifetime()
        {
            var clip = Own(AudioClip.Create("radio pause test", 22050, 1, 22050, false));
            foreach (var line in library.combat) line.voice = clip;
            library.eventExpiry = .1f; radio.Receive(Event(CombatEventKind.AttackIneffective, first));
            radio.SetPaused(true); radio.Tick(9); Assert.AreEqual(1, radio.QueuedCount);
            radio.SetPaused(false); radio.Tick(.01f); yield return null;
            radio.SetPaused(true); Assert.IsFalse(radio.voiceSource.isPlaying);
            int spoken = radio.SpokenCount; radio.Tick(9); Assert.AreEqual(spoken, radio.SpokenCount);
            radio.SetPaused(false); radio.Clear(); Assert.IsFalse(radio.voiceSource.isPlaying); Assert.AreEqual(0, radio.QueuedCount);
        }
        NarrativeApproachController MakeNarrative(out MissionController mission)
        {
            var settings = Own(ScriptableObject.CreateInstance<DropletSettings>()); settings.worldScale = scale; settings.ApplyWorldScale();
            var player = new GameObject("Player"); player.transform.SetParent(root.transform);
            var motor = player.AddComponent<DropletMotor>(); motor.settings = settings;
            var score = root.AddComponent<ScoreSystem>(); score.settings = settings;
            mission = root.AddComponent<MissionController>(); mission.enabled = false;
            mission.settings = settings; mission.motor = motor; mission.score = score; mission.targets = new[] { first, second };
            var narrative = root.AddComponent<NarrativeApproachController>(); narrative.mission = mission; narrative.motor = motor; narrative.radio = radio;
            narrative.director = root.AddComponent<PlayableDirector>(); narrative.director.playOnAwake = false;
            var timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>()); timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 60;
            var track = Own(timeline.CreateTrack<NarrativeBroadcastTrack>(null, "test radio"));
            for (int i = 0; i < 12; i++) { var clip = track.CreateClip<NarrativeBroadcastClip>(); clip.start = i * 5; clip.duration = 5; ((NarrativeBroadcastClip)clip.asset).lineIndex = i; Own(clip.asset); }
            narrative.timeline = timeline; mission.narrative = narrative;
            radio.mission = mission; radio.Bind(); mission.Initialize(); return narrative;
        }
        [Test] public void TimelineNaturalEndingUsesMotorAndCompletesOnceWithoutMissionTimeOrDamage()
        {
            var narrative = MakeNarrative(out var mission);
            mission.StartMission(); Assert.AreEqual(MissionState.Narrative, mission.State);
            Assert.IsTrue(narrative.AutopilotEnabled, "The first saved Timeline command must arm the motor autopilot.");
            Vector3 start = mission.motor.transform.position;
            for (int i = 0; i < 599; i++) narrative.Step(.1f);
            Assert.That((mission.motor.transform.position - start).magnitude, Is.EqualTo(17970).Within(2));
            Assert.AreEqual(0, mission.Elapsed); Assert.AreEqual(0, mission.score.Score); Assert.AreEqual(0, fleet.PendingCount);
            narrative.Step(.2f);
            Assert.AreEqual(12, narrative.CueCount); Assert.AreEqual(1, narrative.CompletionCount); Assert.AreEqual(MissionState.Playing, mission.State);
            Assert.AreEqual(mission.spawnPosition, mission.motor.transform.position); Assert.AreEqual(0, mission.Elapsed);
            narrative.Skip(); Assert.AreEqual(1, narrative.CompletionCount);
        }
        [Test] public void NarrativePauseSkipReplayAndThreeRestartsRestoreOneCombatEntry()
        {
            var narrative = MakeNarrative(out var mission);
            for (int run = 0; run < 3; run++)
            {
                mission.ReplayNarrative(); narrative.Step(5); mission.TogglePause();
                Vector3 before = mission.motor.transform.position; double timelineTime = narrative.director.time;
                narrative.Step(15); Assert.AreEqual(before, mission.motor.transform.position); Assert.AreEqual(timelineTime, narrative.director.time);
                mission.TogglePause(); narrative.Skip(); narrative.Skip();
                Assert.AreEqual(1, narrative.CompletionCount); Assert.AreEqual(MissionState.Playing, mission.State); Assert.AreEqual(1, Time.timeScale);
                mission.RestartIntoCombat(); Assert.AreEqual(MissionState.Playing, mission.State); Assert.IsFalse(narrative.IsActive);
                Assert.AreEqual(0, radio.History.Count); Assert.AreEqual(0, mission.score.Score);
            }
        }
    }
}
