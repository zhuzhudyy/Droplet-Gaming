using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    // Read-only asset/preview checks. No builder, SaveScene, SaveAssets or build-setting writes.
    public sealed class LightingUpgradeTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_Lighting.unity";
        const string BaselinePath = "Assets/_Project/Scenes/FleetAssault_Expanded.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/Fleet/FusionFrigate_Lighting.prefab";

        static T[] Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        static void Near(Vector3 expected, Vector3 actual, string message)
            => Assert.Less(Vector3.Distance(expected, actual), .002f, message);

        [Test]
        public void SavedLightingScenePreservesAllTargetsGameplayScaleAndMissionContract()
        {
            byte[] savedScene = File.ReadAllBytes(ScenePath);
            byte[] savedBaseline = File.ReadAllBytes(BaselinePath);
            Scene active = SceneManager.GetActiveScene();
            Scene baseline = EditorSceneManager.OpenPreviewScene(BaselinePath);
            Scene updated = default;
            try
            {
                updated = EditorSceneManager.OpenPreviewScene(ScenePath);
                var oldMission = Components<MissionController>(baseline).Single();
                var mission = Components<MissionController>(updated).Single();
                var oldTargets = Components<ShipTarget>(baseline).ToDictionary(t => t.targetId);
                var targets = Components<ShipTarget>(updated);
                Assert.AreEqual(120, targets.Length);
                CollectionAssert.AreEquivalent(oldTargets.Keys, targets.Select(t => t.targetId));
                CollectionAssert.AreEquivalent(targets, mission.targets);
                Assert.AreEqual(JsonUtility.ToJson(oldMission.settings), JsonUtility.ToJson(mission.settings),
                    "Lighting must not alter flight, camera, score, timer or boundary tuning.");
                Assert.AreEqual(540, mission.settings.missionSeconds);
                Near(oldMission.spawnPosition, mission.spawnPosition, "Spawn");
                Near(oldMission.arenaCenter, mission.arenaCenter, "Arena centre");
                Near(oldMission.motor.transform.lossyScale, mission.motor.transform.lossyScale, "Droplet scale");
                Assert.AreEqual(oldMission.chaseCamera.GetComponent<Camera>().fieldOfView,
                    mission.chaseCamera.GetComponent<Camera>().fieldOfView);
                foreach (var target in targets)
                {
                    var old = oldTargets[target.targetId];
                    Near(old.transform.position, target.transform.position, target.targetId + " position");
                    Near(old.transform.lossyScale, target.transform.lossyScale, target.targetId + " scale");
                    Assert.Less(Quaternion.Angle(old.transform.rotation, target.transform.rotation), .01f);
                    Assert.AreEqual(old.hitVolumes.Length, target.hitVolumes.Length);
                    Assert.AreEqual(PrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject));
                    Assert.IsTrue(target.visualRoot.activeSelf);
                    Assert.IsTrue(target.hitVolumes.All(c => c != null && c.enabled && !c.transform.IsChildOf(target.visualRoot.transform)));
                }
            }
            finally
            {
                if (updated.IsValid()) EditorSceneManager.ClosePreviewScene(updated);
                EditorSceneManager.ClosePreviewScene(baseline);
            }
            Assert.AreEqual(active, SceneManager.GetActiveScene());
            CollectionAssert.AreEqual(savedScene, File.ReadAllBytes(ScenePath));
            CollectionAssert.AreEqual(savedBaseline, File.ReadAllBytes(BaselinePath));
        }

        [Test]
        public void CelestialPhysicalAnglesAndDisplayMultipliersRemainSeparateAtThreeViews()
        {
            Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var backdrop = Components<SolarSystemBackdrop>(preview).Single();
                var rig = Components<SolarLightingRig>(preview).Single();
                var layout = backdrop.Layout;
                Assert.IsNotNull(rig.settings);
                Assert.That(layout.battleRadiusAu, Is.EqualTo(2.5).Within(1e-12));
                Assert.That(layout.FindBody("sun").radiusKm, Is.EqualTo(695700));
                Assert.That(layout.FindBody("earth").radiusKm, Is.EqualTo(6371));
                Assert.AreEqual(rig.settings.solarDisplayMultiplier, layout.FindBody("sun").readabilityMultiplier);
                Assert.AreEqual(rig.settings.earthDisplayMultiplier, layout.FindBody("earth").readabilityMultiplier);
                Vector3[] observers = { new Vector3(0, 10.2f, -8), new Vector3(950, 720, -1150), new Vector3(3000, 350, -2200) };
                foreach (Vector3 observer in observers)
                {
                    foreach (string id in new[] { "sun", "earth" })
                    {
                        var body = layout.FindBody(id);
                        Assert.IsTrue(SolarLayoutMath.TryProjectBody(layout, body, observer, out var projected));
                        double physical = 2 * Math.Asin(body.radiusKm / (projected.distanceAu * layout.auKm));
                        double display = 2 * Math.Asin(projected.proxyRadius / (double)projected.proxyDistance);
                        Assert.That(projected.angularDiameterRadians, Is.EqualTo(physical).Within(1e-12));
                        Assert.That(Math.Sin(display / 2) / Math.Sin(physical / 2),
                            Is.EqualTo(body.readabilityMultiplier).Within(1e-5));
                        Assert.That(physical * Mathf.Rad2Deg, id == "sun" ? Is.InRange(.213, .214) : Is.InRange(.0014, .0015));
                        Assert.That(display * Mathf.Rad2Deg, id == "sun" ? Is.InRange(.2, 1.0) : Is.InRange(.001, .01));
                        Assert.Greater(projected.proxyDistance - projected.proxyRadius, layout.boundaryRadius * 2);
                        Assert.Less(projected.proxyDistance + projected.proxyRadius, backdrop.observer.farClipPlane);
                        Assert.IsTrue(SolarLayoutMath.IsFinite(projected.worldPosition));
                    }
                }
                Assert.LessOrEqual(backdrop.observer.nearClipPlane, .1f);
                Assert.That(backdrop.observer.farClipPlane, Is.InRange(22000, 30000));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void SavedSolarKeyAndBoundedReflectionCaptureExcludePlayer()
        {
            Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var rig = Components<SolarLightingRig>(preview).Single();
                Assert.IsNotNull(rig.backdrop);
                Assert.IsNotNull(rig.sunLight);
                Assert.AreEqual(LightType.Directional, rig.sunLight.type);
                Assert.IsTrue(rig.sunLight.enabled && rig.sunLight.gameObject.activeInHierarchy);
                Assert.AreNotEqual(LightShadows.None, rig.sunLight.shadows);
                Assert.IsTrue(SolarLayoutMath.TryProjectBody(rig.backdrop.Layout, rig.backdrop.Layout.FindBody("sun"),
                    rig.backdrop.observer.transform.position, out var sun));
                Assert.Greater(Vector3.Dot(-rig.sunLight.transform.forward, sun.direction), .99999f);
                var otherKeys = Components<Light>(preview).Where(l => l != rig.sunLight && l.enabled &&
                    l.gameObject.activeInHierarchy && l.type == LightType.Directional);
                Assert.IsTrue(otherKeys.All(l => l.intensity <= rig.sunLight.intensity * .1f), "Other strong keys contradict the visible sun.");
                Assert.That(rig.settings.ambientStrength, Is.InRange(0, .15f));
                var probes = Components<ReflectionProbe>(preview);
                Assert.AreEqual(1, probes.Length, "Keep one bounded scene-wide local capture, not one per ship.");
                Assert.AreSame(probes[0], rig.localProbe);
                Assert.AreEqual(ReflectionProbeMode.Realtime, rig.localProbe.mode);
                Assert.AreEqual(ReflectionProbeRefreshMode.ViaScripting, rig.localProbe.refreshMode);
                Assert.AreNotEqual(ReflectionProbeTimeSlicingMode.NoTimeSlicing, rig.localProbe.timeSlicingMode);
                Assert.That(rig.localProbe.resolution, Is.InRange(64, 256));
                Assert.GreaterOrEqual(rig.settings.probeInterval, 1);
                var targets = Components<ShipTarget>(preview);
                var player = Components<MissionController>(preview).Single().motor;
                foreach (var renderer in player.GetComponentsInChildren<MeshRenderer>(true))
                    Assert.AreEqual(0, rig.localProbe.cullingMask & (1 << renderer.gameObject.layer), "A probe at the player must not capture the droplet's own inside surface.");
                Assert.IsEmpty(targets.SelectMany(t => t.GetComponentsInChildren<Light>(true)));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void EveryEngineCoreHasDepthAndUsesNativeLodAndSharedResources()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var target = prefab.GetComponent<ShipTarget>();
            var drives = prefab.GetComponentsInChildren<FusionDriveVisuals>(true);
            Assert.AreEqual(1, drives.Length);
            var drive = drives[0];
            Assert.IsTrue(drive.transform.IsChildOf(target.visualRoot.transform));
            Assert.AreEqual(15, drive.coreRenderers.Length);
            Assert.AreEqual(10, drive.detailRenderers.Length);
            var allEffects = drive.coreRenderers.Concat(drive.detailRenderers).ToArray();
            Assert.AreEqual(allEffects.Length, allEffects.Distinct().Count());
            var lod = prefab.GetComponentsInChildren<LODGroup>(true).Single().GetLODs();
            Assert.AreEqual(3, lod.Length);
            string[] names = { "MainExhaust_Core", "AuxiliaryExhaust_01_Core", "AuxiliaryExhaust_02_Core", "AuxiliaryExhaust_03_Core", "AuxiliaryExhaust_04_Core" };
            for (int level = 0; level < lod.Length; level++)
            {
                var cores = lod[level].renderers.Intersect(drive.coreRenderers).ToArray();
                CollectionAssert.AreEquivalent(names, cores.Select(r => r.name));
                Assert.AreEqual(level == 0 ? 10 : 0, lod[level].renderers.Intersect(drive.detailRenderers).Count());
                foreach (var renderer in cores)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.IsNotNull(mesh);
                    Assert.IsTrue(AssetDatabase.Contains(mesh));
                    Assert.Greater(mesh.bounds.size.x, .1f);
                    Assert.Greater(mesh.bounds.size.y, .1f);
                    Assert.Greater(mesh.bounds.size.z, .1f, "The energy core must be 3D geometry, not a flat coloured disc.");
                    Assert.IsTrue(renderer.transform.IsChildOf(target.visualRoot.transform));
                    Assert.AreEqual(1, lod.Sum(l => l.renderers.Count(r => r == renderer)));
                }
            }
            CollectionAssert.AreEquivalent(prefab.GetComponentsInChildren<Renderer>(true), lod.SelectMany(l => l.renderers));
            foreach (var effect in allEffects)
            {
                Assert.IsTrue(effect.sharedMaterials.All(m => m != null && AssetDatabase.Contains(m)));
                Assert.IsEmpty(effect.GetComponents<Collider>());
                Assert.AreSame(target, effect.GetComponentInParent<ShipTarget>(true));
            }
            Assert.IsEmpty(prefab.GetComponentsInChildren<Light>(true));
            Assert.LessOrEqual(allEffects.SelectMany(r => r.sharedMaterials).Distinct().Count(), 3);
        }

        [Test]
        public void FleetMaterialsAreSharedSupportedAndDropletIsSmoothMetal()
        {
            Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var targets = Components<ShipTarget>(preview);
                var first = targets[0].visualRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                var whole = targets.SelectMany(t => t.visualRoot.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                CollectionAssert.AreEquivalent(first, whole, "One fleet uses shared material assets regardless of target count.");
                Assert.That(whole.Length, Is.InRange(5, 12));
                foreach (var renderer in Components<MeshRenderer>(preview))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, renderer.name);
                        Assert.IsNotNull(material.shader, renderer.name);
                        Assert.IsTrue(material.shader.isSupported, material.name);
                        Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), material.name);
                        Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name);
                        Assert.IsTrue(AssetDatabase.Contains(material), material.name + " must be a shared saved asset.");
                    }
                var motor = Components<MissionController>(preview).Single().motor;
                var droplet = motor.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                Assert.IsNotEmpty(droplet);
                Assert.IsTrue(droplet.Any(m => m.HasProperty("_Metallic") && m.GetFloat("_Metallic") >= .95f &&
                    m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") >= .95f), "The player surface should retain a smooth metal BRDF.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
