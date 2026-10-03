using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class EnhancementRadioPriorityTests
    {
        GameObject root;
        RadioController radio;
        RadioLibrary library;
        AudioClip clip;
        ShipTarget first, second;
        [SetUp] public void Setup()
        {
            root = new GameObject("Enhancement radio priority test");
            first = Ship("A"); second = Ship("B");
            radio = root.AddComponent<RadioController>(); radio.enabled = false;
            clip = AudioClip.Create("ten-second local voice", 240000, 1, 24000, false);
            library = ScriptableObject.CreateInstance<RadioLibrary>();
            library.combat = new[] {
                new RadioLine { id="routine", eventKind="AttackIneffective", text="Routine", voice=clip },
                new RadioLine { id="warning", eventKind="LaserReflected", text="Clear the beam", important=true, voice=clip },
                new RadioLine { id="retreat", eventKind="RetreatOrdered", text="Break formation", important=true, voice=clip }
            };
            radio.library = library; radio.BeginSession(1);
        }
        ShipTarget Ship(string id)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform);
            var ship = go.AddComponent<ShipTarget>(); ship.targetId = id;
            ship.hitVolumes = new Collider[] { go.AddComponent<BoxCollider>() }; return ship;
        }
        CombatEvent Event(CombatEventKind kind, ShipTarget speaker) => new CombatEvent(kind,speaker,speaker,Vector3.zero,DamageSource.DirectLaser,0,0);
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(library); Object.DestroyImmediate(clip); Time.timeScale = 1;
        }
        [Test] public void ImportantWarningPreemptsRoutineButEquivalentWarningsDoNotStarveIt()
        {
            radio.Receive(Event(CombatEventKind.AttackIneffective,first)); radio.Tick(.01f);
            Assert.AreEqual("routine",radio.CurrentLineId); Assert.IsTrue(radio.IsDucking);
            radio.Receive(Event(CombatEventKind.LaserReflected,second)); radio.Tick(.01f);
            Assert.AreEqual("warning",radio.CurrentLineId); Assert.AreEqual(1,radio.InterruptedCount);
            radio.Receive(Event(CombatEventKind.RetreatOrdered,first)); radio.Tick(.01f);
            Assert.AreEqual("warning",radio.CurrentLineId); Assert.AreEqual(1,radio.InterruptedCount);
            radio.Clear(); Assert.IsFalse(radio.IsDucking); Assert.AreEqual(0,radio.QueuedCount);
        }
        [Test] public void PauseAndThreeRestartsClearPriorityAndMixState()
        {
            for(int i=0;i<3;i++)
            {
                radio.BeginSession(i);radio.Receive(Event(CombatEventKind.AttackIneffective,first));radio.Tick(.01f);
                radio.SetPaused(true);radio.Tick(20);Assert.AreEqual("routine",radio.CurrentLineId);Assert.IsTrue(radio.IsBroadcasting);
                radio.SetPaused(false);radio.Receive(Event(CombatEventKind.LaserReflected,second));radio.Tick(.01f);
                Assert.AreEqual("warning",radio.CurrentLineId);radio.BeginSession(i+10);
                Assert.IsFalse(radio.IsDucking);Assert.IsFalse(radio.IsBroadcasting);Assert.IsNull(radio.CurrentLineId);
            }
        }
    }
}
