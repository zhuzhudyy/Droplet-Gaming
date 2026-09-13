using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class CollisionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        DropletSettings settings;
        DropletHitDetector detector;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<DropletSettings>(); settings.targetLayers = 1 << 31; settings.hitRadius = .25f;
            var go = new GameObject("Detector test"); objects.Add(go); detector = go.AddComponent<DropletHitDetector>(); detector.settings = settings;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var go in objects) Object.DestroyImmediate(go);
            objects.Clear(); Object.DestroyImmediate(settings);
        }
        ShipTarget Ship(Vector3 position, int colliders = 1, float thickness = .01f)
        {
            var go = new GameObject("Target test"); objects.Add(go); go.transform.position = position;
            var ship = go.AddComponent<ShipTarget>(); ship.hitVolumes = new Collider[colliders];
            for (int i = 0; i < colliders; i++)
            {
                var child = new GameObject("Hit " + i); child.layer = 31; child.transform.SetParent(go.transform, false);
                var box = child.AddComponent<BoxCollider>(); box.size = new Vector3(1, 1, thickness); box.isTrigger = true; ship.hitVolumes[i] = box;
            }
            return ship;
        }
        [Test] public void ThinTargetHitAtFourTimesBoostWithEndpointsOutside()
        {
            var ship = Ship(new Vector3(0, 0, 4)); Physics.SyncTransforms();
            Assert.AreEqual(1, detector.SweepSegment(Vector3.zero, Vector3.forward * settings.maxCruiseSpeed * settings.boostMultiplier * 4 * .02f));
            Assert.IsTrue(ship.IsDestroyed);
        }
        [TestCase(8)] [TestCase(25)] public void FullSweepBufferDoesNotDropTargets(int count)
        {
            for (int i = 1; i <= count; i++) Ship(new Vector3(0, 0, i * 2));
            Physics.SyncTransforms(); Assert.AreEqual(count, detector.SweepSegment(Vector3.zero, Vector3.forward * (count * 2 + 2)));
            Assert.Greater(detector.BufferGrowthCount, 0);
        }
        [Test] public void CompoundIdentityAndRepeatedPassScoreOnlyOneEvent()
        {
            var ship = Ship(new Vector3(0, 0, 3), 5); int events = 0; ship.Destroyed += _ => events++;
            Physics.SyncTransforms(); Assert.AreEqual(1, detector.SweepSegment(Vector3.zero, Vector3.forward * 8));
            Assert.AreEqual(0, detector.SweepSegment(Vector3.forward * 8, Vector3.zero));
            Assert.IsFalse(ship.TryDestroy()); Assert.AreEqual(1, events);
        }
        [Test] public void InitialOverlapAndZeroTravelHandleSaturatedBuffer()
        {
            for (int i = 0; i < 20; i++) Ship(Vector3.zero, 2);
            Physics.SyncTransforms(); Assert.AreEqual(20, detector.SweepSegment(Vector3.zero, Vector3.zero));
            Assert.Greater(detector.BufferGrowthCount, 0);
        }
        [Test] public void SharpCornerUsesSegmentsNotDiagonalChord()
        {
            var offPath = Ship(new Vector3(5, 0, 5));
            var onPath = Ship(new Vector3(0, 0, 5)); Physics.SyncTransforms();
            detector.SweepSegment(Vector3.zero, new Vector3(0, 0, 10));
            detector.SweepSegment(new Vector3(0, 0, 10), new Vector3(10, 0, 10));
            Assert.IsFalse(offPath.IsDestroyed); Assert.IsTrue(onPath.IsDestroyed);
        }
        [Test] public void ResetTeleportDoesNotSweepAndNonTargetsNeverBlock()
        {
            var ship = Ship(new Vector3(0, 0, 5));
            var debris = new GameObject("Non target collider"); objects.Add(debris); debris.layer = 31; debris.AddComponent<BoxCollider>();
            var motor = detector.gameObject.AddComponent<DropletMotor>(); motor.enabled = false; motor.settings = settings; motor.hitDetector = detector;
            Physics.SyncTransforms(); motor.ResetPose(Vector3.forward * 10, Quaternion.identity);
            motor.Simulate(.02f, default); Assert.IsFalse(ship.IsDestroyed);
            Assert.AreEqual(0, detector.SweepSegment(Vector3.left * 2, Vector3.right * 2));
        }
    }
}
