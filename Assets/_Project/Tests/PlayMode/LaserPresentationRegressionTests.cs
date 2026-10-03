using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class LaserPresentationRegressionTests
    {
        readonly List<Object> owned = new List<Object>();
        T Asset<T>() where T : ScriptableObject
        { var item = ScriptableObject.CreateInstance<T>(); owned.Add(item); return item; }
        GameObject Node(string name)
        { var item = new GameObject(name); owned.Add(item); return item; }
        LaserBeamPool Pool()
        {
            var pool = Node("Pulse pool").AddComponent<LaserBeamPool>();
            pool.settings = Asset<LaserWeaponSettings>(); pool.scale = Asset<CombatScaleSettings>();
            pool.settings.visualBeamBudget = 2; pool.InitializePool(); return pool;
        }
        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void FastMovingPresentationCannotBendOrTranslatePastReflectionPath()
        {
            var pool = Pool(); var mesh = Node("Interpolated player mesh").transform;
            Vector3 source = new Vector3(-20, 5, -30), contact = new Vector3(2, 3, 4);
            Vector3 reflectedEnd = new Vector3(-50, 40, 60);
            Assert.IsTrue(pool.Show(source, contact, reflectedEnd, true, mesh, contact, Vector3.up));
            mesh.SetPositionAndRotation(new Vector3(1800, 90, -400), Quaternion.Euler(35, 100, 12));
            pool.Step(.04f); pool.SendMessage("LateUpdate");
            var lines = pool.GetComponentsInChildren<LineRenderer>();
            var incident = lines.Single(line => line.name == "Incident");
            var reflected = lines.Single(line => line.name == "Reflected");
            Assert.AreEqual(source, incident.GetPosition(0));
            Assert.AreEqual(contact, incident.GetPosition(1));
            Assert.AreEqual(contact, reflected.GetPosition(0));
            Assert.AreEqual(reflectedEnd, reflected.GetPosition(1));
            Assert.AreEqual(contact, pool.transform.Find("LaserSnapshot_0/SurfaceContact").position);
            Assert.IsFalse(pool.transform.Find("LaserSnapshot_0/SurfaceContact").GetComponent<Renderer>().enabled,
                "A past impact must not leave a bright world-space marker after the player moves away.");
            Assert.AreEqual(0, pool.GetComponentsInChildren<ParticleSystem>().Sum(system => system.particleCount));
            Assert.AreEqual(1, pool.ActiveCount, "Only the contact flash expires; both frozen beam segments remain.");
            Assert.That(Vector3.Angle(reflected.GetPosition(1) - reflected.GetPosition(0), reflectedEnd - contact), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator ContactLastsAtMostTwoRenderFramesPauseFreezesItAndPoolReuseRearmsIt()
        {
            var pool = Pool();
            var source = Vector3.back * 20; var contact = Vector3.zero; var endpoint = Vector3.right * 30;
            Assert.IsTrue(pool.Show(source, contact, endpoint, true, contactNormal: Vector3.up));
            var marker = pool.transform.Find("LaserSnapshot_0/SurfaceContact").GetComponent<Renderer>();
            Assert.IsTrue(marker.enabled);
            // Render-frame budget also bounds the flash when no new fixed step
            // occurs (for example at 144/240 Hz). Simulation time stays at zero.
            yield return null;
            pool.SendMessage("LateUpdate"); Assert.IsTrue(marker.enabled);
            Time.timeScale = 0;
            yield return null; yield return null; yield return null;
            pool.SendMessage("LateUpdate"); Assert.IsTrue(marker.enabled, "Pause must freeze a still-visible impact.");
            Time.timeScale = 1;
            yield return null;
            pool.SendMessage("LateUpdate"); Assert.IsFalse(marker.enabled);
            Assert.AreEqual(0, pool.GetComponentsInChildren<ParticleSystem>().Sum(system => system.particleCount));
            Assert.AreEqual(1, pool.ActiveCount);
            var lines = pool.GetComponentsInChildren<LineRenderer>();
            Assert.AreEqual(contact, lines.Single(line => line.name == "Incident").GetPosition(1));
            Assert.AreEqual(endpoint, lines.Single(line => line.name == "Reflected").GetPosition(1));
            pool.Step(.17f); Assert.AreEqual(1, pool.ActiveCount);
            pool.Step(.011f); Assert.AreEqual(0, pool.ActiveCount);
            Assert.IsTrue(pool.Show(source, contact, endpoint, true, contactNormal: Vector3.up));
            Assert.IsTrue(marker.enabled, "Reusing the same slot must rearm the brief impact.");
            pool.ResetEffects(); Assert.AreEqual(0, pool.ActiveCount);
        }

        [Test]
        public void MeshLocalGlintPreservesOtherResponsesAndRespectsPauseExpiryAndReset()
        {
            var mesh = Node("Preserved mesh"); var renderer = mesh.AddComponent<MeshRenderer>();
            var response = Node("Local laser response").AddComponent<LaserContactResponse>();
            response.targetRenderer = renderer; response.presentedMesh = mesh.transform;
            var block = new MaterialPropertyBlock(); block.SetFloat("_BurstCount", 3); renderer.SetPropertyBlock(block);
            Vector3 localPoint = new Vector3(.2f, .1f, .9f);
            response.RegisterHit(new DropletSurfaceHit { localPoint = localPoint }, Vector3.back,
                Matrix4x4.identity, Color.cyan, .22f, .09f);
            mesh.transform.SetPositionAndRotation(new Vector3(100, -20, 180), Quaternion.Euler(3, 40, -10));
            response.Step(0); response.RefreshResponse(); renderer.GetPropertyBlock(block);
            Assert.AreEqual(1, response.ActiveCount);
            Assert.AreEqual(3, block.GetFloat("_BurstCount"), "The existing reactor/explosion MPB must survive.");
            Vector4 recorded = block.GetVectorArray("_LaserContactPositions")[0];
            Assert.That(Vector3.Distance((Vector3)recorded, mesh.transform.TransformPoint(localPoint)), Is.LessThan(.00001f));
            Assert.That(recorded.w, Is.EqualTo(.22f).Within(.000001f));
            response.Step(.091f); renderer.GetPropertyBlock(block);
            Assert.AreEqual(0, response.ActiveCount); Assert.AreEqual(0, block.GetFloat("_LaserContactCount"));
            response.RegisterHit(new DropletSurfaceHit { localPoint = localPoint }, Vector3.back,
                Matrix4x4.identity, Color.cyan, .22f, .09f);
            response.ResetResponse(); renderer.GetPropertyBlock(block);
            Assert.AreEqual(0, response.ActiveCount); Assert.AreEqual(Vector4.zero, block.GetVectorArray("_LaserContactPositions")[0]);
        }

        [Test]
        public void PooledPulseFadesWithoutObjectsLightsOrDamageAndClearsOnRestart()
        {
            var pool = Pool();
            int objects = pool.GetComponentsInChildren<Transform>(true).Length;
            pool.Show(Vector3.back * 30, Vector3.zero, Vector3.right * 20, true, contactNormal: Vector3.up);
            var incident = pool.GetComponentsInChildren<LineRenderer>().Single(line => line.name == "Incident");
            var block = new MaterialPropertyBlock(); incident.GetPropertyBlock(block);
            float initialAlpha = block.GetColor("_BaseColor").a;
            pool.Step(pool.settings.beamSeconds * .6f); pool.SendMessage("LateUpdate"); incident.GetPropertyBlock(block);
            Assert.Less(block.GetColor("_BaseColor").a, initialAlpha);
            Assert.Greater(block.GetColor("_BaseColor").a, 0);
            float pausedAlpha = block.GetColor("_BaseColor").a;
            pool.Step(0); pool.SendMessage("LateUpdate"); incident.GetPropertyBlock(block);
            Assert.AreEqual(pausedAlpha, block.GetColor("_BaseColor").a);
            Assert.LessOrEqual(pool.GetComponentsInChildren<ParticleSystem>().Sum(system => system.particleCount), 5);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Light>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<Collider>(true).Length);
            pool.Step(pool.settings.beamSeconds); Assert.AreEqual(0, pool.ActiveCount);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(pool.Show(Vector3.back, Vector3.zero, Vector3.forward, true));
                pool.ResetEffects(); Assert.AreEqual(0, pool.ActiveCount);
            }
            Assert.AreEqual(objects, pool.GetComponentsInChildren<Transform>(true).Length);
            Assert.AreEqual(0, pool.GetComponentsInChildren<ParticleSystem>(true).Sum(system => system.particleCount));
        }
    }
}
