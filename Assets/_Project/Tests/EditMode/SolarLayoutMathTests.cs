using System;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    // These tests never load, generate or save a project scene or asset.
    public sealed class SolarLayoutMathTests
    {
        static SolarLayoutData Layout()
        {
            return new SolarLayoutData
            {
                battlePhaseDeg = 0,
                battleInclinationDeg = 0,
                bodies = new[]
                {
                    new SolarBodyData { id = "sun", radiusKm = 695700 },
                    new SolarBodyData { id = "earth", semiMajorAu = 1, radiusKm = 6371 }
                }
            };
        }

        static Vector3 Origin(SolarLayoutData layout) => new Vector3(layout.localOrigin[0], layout.localOrigin[1], layout.localOrigin[2]);

        [Test]
        public void CircularOrbitsKeepRadiusAndRespectInclinationAndNodeConvention()
        {
            SolarVector3d p = SolarLayoutMath.OrbitPositionAu(2, 90, 30, 90);
            Assert.That(p.x, Is.EqualTo(-Math.Sqrt(3)).Within(1e-12));
            Assert.That(p.y, Is.EqualTo(1).Within(1e-12));
            Assert.That(p.z, Is.EqualTo(0).Within(1e-12));
            Assert.That(p.Magnitude, Is.EqualTo(2).Within(1e-12));
            foreach (double radius in new[] { .387, .723, 1, 1.524, 2.1, 3.3, 5.203, 9.537, 19.189, 30.070 })
                Assert.That(SolarLayoutMath.OrbitPositionAu(radius, 237, 7, 32).Magnitude, Is.EqualTo(radius).Within(1e-11));
        }

        [Test]
        public void ObserverConvertsLocalMetresAfterDoublePrecisionMacroPlacement()
        {
            SolarLayoutData layout = Layout();
            Assert.IsTrue(SolarLayoutMath.TryGetObserverAu(layout, Origin(layout), out SolarVector3d initial));
            Assert.IsTrue(SolarLayoutMath.TryGetObserverAu(layout, Origin(layout) + new Vector3(1000, 2000, -6000), out SolarVector3d moved));
            Assert.That(initial.x, Is.EqualTo(2.5).Within(1e-15));
            Assert.That(moved.x - initial.x, Is.EqualTo(1 / layout.auKm).Within(3e-16));
            Assert.That(moved.y - initial.y, Is.EqualTo(2 / layout.auKm).Within(1e-18));
            Assert.That(moved.z - initial.z, Is.EqualTo(-6 / layout.auKm).Within(1e-18));
        }

        [Test]
        public void SunAtTwoPointFiveAuHasPhysicalAngularDiameterAtBoundedProxyDistance()
        {
            SolarLayoutData layout = Layout();
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out SolarBodyProjection projection));
            Assert.That(projection.distanceAu, Is.EqualTo(2.5).Within(1e-12));
            Assert.That(projection.angularDiameterRadians * 180 / Math.PI, Is.InRange(.213, .214));
            Assert.That(projection.proxyDistance, Is.InRange(14000f, 18000f));
            double renderedAngle = 2 * Math.Asin(projection.proxyRadius / (double)projection.proxyDistance);
            Assert.That(renderedAngle, Is.EqualTo(projection.angularDiameterRadians).Within(1e-9));
            Assert.That(projection.direction, Is.EqualTo(Vector3.left));
        }

        [Test]
        public void ProxyDepthPreservesNearFarOrderAndReadabilityDoesNotMoveOrbit()
        {
            SolarLayoutData layout = Layout();
            SolarBodyData earth = layout.FindBody("earth");
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out SolarBodyProjection sun));
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, earth, Origin(layout), out SolarBodyProjection physicalEarth));
            Assert.That(physicalEarth.distanceAu, Is.EqualTo(1.5).Within(1e-12));
            Assert.Less(physicalEarth.proxyDistance, sun.proxyDistance);
            earth.readabilityMultiplier = 10;
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, earth, Origin(layout), out SolarBodyProjection readableEarth));
            Assert.AreEqual(physicalEarth.distanceAu, readableEarth.distanceAu);
            Assert.AreEqual(physicalEarth.angularDiameterRadians, readableEarth.angularDiameterRadians);
            Assert.AreEqual(physicalEarth.worldPosition, readableEarth.worldPosition);
            Assert.That(readableEarth.proxyRadius, Is.EqualTo(physicalEarth.proxyRadius * 10).Within(1e-6));
        }

        [Test]
        public void SixKilometreMovementProducesTinyNonzeroAstronomicalParallax()
        {
            SolarLayoutData layout = Layout();
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out SolarBodyProjection start));
            Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout) + Vector3.up * 6000, out SolarBodyProjection moved));
            double expected = -6 / Math.Sqrt(Math.Pow(2.5 * layout.auKm, 2) + 36);
            Assert.That(moved.direction.y, Is.EqualTo(expected).Within(1e-14));
            Assert.Greater((moved.direction - start.direction).magnitude, 0);
            Assert.Less((moved.direction - start.direction).magnitude, 1e-6);
        }

        [Test]
        public void IllegalDistancesAndNonFiniteDataFailWithoutNaNTransforms()
        {
            foreach (double distance in new[] { 0, -1, 1, .5, double.NaN, double.PositiveInfinity })
                Assert.IsFalse(SolarLayoutMath.TryAngularDiameter(1, distance, out _));
            SolarLayoutData layout = Layout();
            layout.battleRadiusAu = 0;
            Assert.IsFalse(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out _));
            layout.battleRadiusAu = 2.5;
            layout.battlePhaseDeg = double.NaN;
            Assert.IsFalse(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out _));
            layout.battlePhaseDeg = 0;
            layout.proxyNear = float.PositiveInfinity;
            Assert.IsFalse(SolarLayoutMath.TryProjectBody(layout, layout.FindBody("sun"), Origin(layout), out _));
        }

        [Test]
        public void BackdropKeepsWorldDirectionAndLocalRocksWhileRecoveringInvalidMapping()
        {
            var root = new GameObject("Temporary Solar Math Test");
            TextAsset validJson = null, invalidJson = null;
            try
            {
                var cameraObject = new GameObject("Observer"); cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                var sunObject = new GameObject("Sun proxy"); sunObject.transform.SetParent(root.transform);
                Renderer sunRenderer = sunObject.AddComponent<MeshRenderer>();
                var skyObject = new GameObject("Sky"); skyObject.transform.SetParent(root.transform);
                skyObject.transform.rotation = Quaternion.Euler(20, 40, 60);
                skyObject.transform.localScale = Vector3.one * 22000;
                var rock = new GameObject("Local rock"); rock.transform.SetParent(root.transform);
                rock.transform.position = new Vector3(17, 41, 1000);
                SolarSystemBackdrop backdrop = root.AddComponent<SolarSystemBackdrop>();
                SolarLayoutData layout = Layout();
                validJson = new TextAsset(JsonUtility.ToJson(layout));
                backdrop.layoutJson = validJson; backdrop.observer = camera;
                backdrop.sunProxy = sunObject.transform; backdrop.skyProxy = skyObject.transform;
                camera.transform.position = Origin(layout);
                backdrop.ApplyMapping(camera);
                Vector3 sunPosition = sunObject.transform.position;
                Quaternion skyRotation = skyObject.transform.rotation;
                camera.transform.rotation = Quaternion.Euler(35, 123, 0);
                backdrop.ApplyMapping(camera);
                Assert.AreEqual(sunPosition, sunObject.transform.position);
                camera.transform.position += new Vector3(1000, 200, 3000);
                backdrop.ApplyMapping(camera);
                Assert.AreEqual(new Vector3(17, 41, 1000), rock.transform.position);
                Assert.AreEqual(camera.transform.position, skyObject.transform.position);
                Assert.AreEqual(skyRotation, skyObject.transform.rotation);
                Assert.AreEqual(Vector3.one * 22000, skyObject.transform.localScale);
                Assert.IsFalse(sunRenderer.forceRenderingOff);
                layout.auKm = 0;
                invalidJson = new TextAsset(JsonUtility.ToJson(layout));
                backdrop.layoutJson = invalidJson;
                backdrop.ApplyMapping(camera);
                Assert.IsTrue(sunRenderer.forceRenderingOff);
                Assert.IsTrue(SolarLayoutMath.IsFinite(sunObject.transform.position));
                backdrop.layoutJson = validJson;
                backdrop.ApplyMapping(camera);
                Assert.IsFalse(sunRenderer.forceRenderingOff);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (validJson != null) UnityEngine.Object.DestroyImmediate(validJson);
                if (invalidJson != null) UnityEngine.Object.DestroyImmediate(invalidJson);
            }
        }
    }
}
