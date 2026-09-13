using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class FlightTests
    {
        GameObject go;
        DropletSettings settings;
        DropletMotor motor;
        [SetUp] public void Setup()
        {
            go = new GameObject("Flight test"); settings = ScriptableObject.CreateInstance<DropletSettings>();
            motor = go.AddComponent<DropletMotor>(); motor.settings = settings; motor.enabled = false;
            motor.ResetPose(Vector3.zero, Quaternion.identity);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(go); Object.DestroyImmediate(settings); Time.timeScale = 1; }
        [Test] public void BoostBrakeAndThrottleAreBounded()
        {
            for (int i = 0; i < 200; i++) motor.Simulate(.02f, new FlightCommand { throttle = 1, boost = true });
            Assert.AreEqual(settings.maxCruiseSpeed, motor.CruiseSpeed, .01f);
            Assert.AreEqual(settings.maxCruiseSpeed * settings.boostMultiplier, motor.Speed, .01f);
            for (int i = 0; i < 100; i++) motor.Simulate(.02f, new FlightCommand { brake = true });
            Assert.AreEqual(0, motor.Speed, .01f);
            for (int i = 0; i < 200; i++) motor.Simulate(.02f, new FlightCommand { throttle = -1 });
            Assert.GreaterOrEqual(motor.Speed, 0); Assert.AreEqual(0, motor.CruiseSpeed);
        }
        [Test] public void FixedInputPathIndependentOfRenderBatching()
        {
            for (int i = 0; i < 120; i++) motor.Simulate(.02f, new FlightCommand { look = new Vector2(2, 1) });
            Vector3 expected = motor.transform.position;
            motor.ResetPose(Vector3.zero, Quaternion.identity);
            for (int renderFrame = 0; renderFrame < 40; renderFrame++)
                for (int j = 0; j < 3; j++) motor.Simulate(.02f, new FlightCommand { look = new Vector2(2, 1) });
            Assert.That(Vector3.Distance(expected, motor.transform.position), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(Vector3.Dot(motor.transform.right, Vector3.up)), Is.LessThan(.0001f));
        }
        [UnityTest] public IEnumerator DisabledSimulationDoesNotMoveInRealFixedSteps()
        {
            motor.Simulate(.02f, default);
            motor.enabled = true; motor.SimulationEnabled = false;
            var start = motor.transform.position;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(start, motor.transform.position);
            Assert.AreEqual(start, motor.PresentedPosition);
        }
    }
}
