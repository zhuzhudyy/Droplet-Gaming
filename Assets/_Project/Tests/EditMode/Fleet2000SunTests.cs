using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    // Isolated preview objects only. No source scene, prefab, asset or pipeline
    // writes, and no additional URP/Core reference is required by this assembly.
    public sealed class Fleet2000SunTests
    {
        Scene preview;
        GameObject root;
        TextAsset source;
        Camera camera;
        SolarSystemBackdrop backdrop;
        SunDisplayRig display;
        Light mainLight, previousSun;
        Behaviour flare;
        Renderer chrome;

        Transform Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child.transform;
        }

        [SetUp]
        public void SetUp()
        {
            previousSun = RenderSettings.sun;
            preview = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Temporary independent solar display test");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            camera = Child("Observer").gameObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.farClipPlane = 25000f;
            var data = new SolarLayoutData
            {
                bodies = new[]
                {
                    new SolarBodyData { id = "sun", radiusKm = 695700, readabilityMultiplier = 2 },
                    new SolarBodyData { id = "earth", radiusKm = 6371, semiMajorAu = 1, phaseDeg = 44 }
                }
            };
            source = new TextAsset(JsonUtility.ToJson(data));
            backdrop = Child("Preserved backdrop").gameObject.AddComponent<SolarSystemBackdrop>();
            backdrop.layoutJson = source;
            backdrop.observer = camera;
            backdrop.sunProxy = Child("Existing Sun proxy");
            backdrop.earthProxy = Child("Existing Earth proxy");
            backdrop.skyProxy = Child("Existing sky proxy");
            var lightRig = Child("Preserved lighting").gameObject.AddComponent<SolarLightingRig>();
            lightRig.backdrop = backdrop;
            mainLight = Child("Existing main light").gameObject.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            lightRig.sunLight = mainLight;
            chrome = Child("Unique player renderer").gameObject.AddComponent<MeshRenderer>();
            display = Child("Display controller").gameObject.AddComponent<SunDisplayRig>();
            display.backdrop = backdrop;
            display.lightingRig = lightRig;
            display.observer = camera;
            display.sunProxy = backdrop.sunProxy;
            display.flareAnchor = Child("Front surface optical sample");
            // Reflection keeps the test assembly independent of package types.
            var flareField = typeof(SunDisplayRig).GetField("lensFlare");
            flare = (Behaviour)display.flareAnchor.gameObject.AddComponent(flareField.FieldType);
            flareField.SetValue(display, flare);
            display.dropletRenderer = chrome;
            display.displayAngularDiameter = 34f;
            display.disabledReflectionAngularRadius = .006f;
            root.SetActive(true);
            backdrop.ReloadLayout();
            display.ApplyForCamera(camera);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            RenderSettings.sun = previousSun;
        }

        [Test]
        public void ThreeViewsKeepMacroLayoutEarthAndSolarDirectionWhileUsingIndependentAngularSize()
        {
            string before = JsonUtility.ToJson(backdrop.Layout);
            foreach (var position in new[] { Vector3.zero, new Vector3(-6500, 1800, -700), new Vector3(6000, -500, 4700) })
            {
                camera.transform.position = position;
                display.ApplyForCamera(camera);
                Assert.IsTrue(SolarLayoutMath.TryProjectBody(backdrop.Layout, backdrop.Layout.FindBody("sun"),
                    position, out var sun));
                Assert.IsTrue(SolarLayoutMath.TryProjectBody(backdrop.Layout, backdrop.Layout.FindBody("earth"),
                    position, out var earth));
                float distance = Vector3.Distance(camera.transform.position, backdrop.sunProxy.position);
                double angle = 2 * Math.Asin(backdrop.sunProxy.localScale.x / distance) * Mathf.Rad2Deg;
                Assert.That(angle, Is.EqualTo(34).Within(.0001));
                Assert.That(Vector3.Distance(sun.worldPosition, backdrop.sunProxy.position), Is.LessThan(.003f));
                Assert.That(Vector3.Dot(sun.direction, -mainLight.transform.forward), Is.GreaterThan(.999999f));
                Assert.That(Vector3.Distance(earth.worldPosition, backdrop.earthProxy.position), Is.LessThan(.003f));
                Assert.That(backdrop.earthProxy.localScale.x, Is.EqualTo(earth.proxyRadius).Within(.0001));
                Assert.That(display.PhysicalAngularDiameter, Is.InRange(.213f, .214f));
                Assert.Greater(display.DisplayMultiplier, 150f);
            }
            Assert.AreEqual(before, JsonUtility.ToJson(backdrop.Layout), "Art display cannot mutate the macro layout.");
            Assert.AreEqual(before, source.text);
        }

        [Test]
        public void OpticalSampleStaysInFrontOfOpaqueSunAndOcclusionRadiusTracksItsWorldDistance()
        {
            foreach (float angularDiameter in new[] { 28f, 34f, 40f })
            {
                display.displayAngularDiameter = angularDiameter;
                display.ApplyForCamera(camera);
                float centerDistance = Vector3.Distance(camera.transform.position, backdrop.sunProxy.position);
                float sampleDistance = Vector3.Distance(camera.transform.position, display.flareAnchor.position);
                Assert.Less(sampleDistance, centerDistance - display.DisplayRadiusUnits,
                    "The sun itself must not cover the SRP flare's depth sample.");
                Assert.Greater(sampleDistance, 10000f, "The sample remains beyond the playable fleet.");
                Assert.That(Vector3.Dot((display.flareAnchor.position - camera.transform.position).normalized,
                    display.SunDirection), Is.GreaterThan(.999999f));
                Assert.IsNull(display.flareAnchor.GetComponent<Light>(), "A directional component would move the sample to the far plane.");
                float radius = (float)flare.GetType().GetField("occlusionRadius").GetValue(flare);
                Assert.That(radius, Is.EqualTo(sampleDistance * Mathf.Tan(display.flareOcclusionAngularRadius * Mathf.Deg2Rad)).Within(.001f));
            }
        }

        [Test]
        public void DisableAndReenableRestoreBothSolarScaleAndChromeWithoutErasingExplosionResponse()
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat("_BurstCount", 3f);
            chrome.SetPropertyBlock(block);
            display.ApplyForCamera(camera);
            chrome.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_SunAngularRadius"), Is.EqualTo(17f * Mathf.Deg2Rad).Within(.000001f));
            Assert.AreEqual(3f, block.GetFloat("_BurstCount"));
            int objects = root.GetComponentsInChildren<Transform>(true).Length;
            for (int restart = 0; restart < 3; restart++)
            {
                display.enabled = false;
                Assert.IsFalse(flare.enabled);
                Assert.IsTrue(SolarLayoutMath.TryProjectBody(backdrop.Layout, backdrop.Layout.FindBody("sun"),
                    camera.transform.position, out var physical));
                Assert.That(backdrop.sunProxy.localScale.x, Is.EqualTo(physical.proxyRadius).Within(.0001f));
                chrome.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_SunAngularRadius"), Is.EqualTo(.006f).Within(.000001f));
                Assert.AreEqual(3f, block.GetFloat("_BurstCount"));
                display.enabled = true;
                display.ApplyForCamera(camera);
                chrome.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_SunAngularRadius"), Is.EqualTo(17f * Mathf.Deg2Rad).Within(.000001f));
                Assert.AreEqual(3f, block.GetFloat("_BurstCount"));
                Assert.AreEqual(objects, root.GetComponentsInChildren<Transform>(true).Length);
            }
        }
    }
}
