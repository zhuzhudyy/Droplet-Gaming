using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class FleetAssetTests
    {
        const string FixtureFolder = "Assets/_Project/Tests/G09TemporaryFixture";
        Scene previousScene;
        Scene fixture;
        GameObject prefab;
        GameObject layout;
        Transform fleet;
        bool ownsFolder;
        bool restoreDefaultRunner;
        [Serializable] sealed class ExpectedLayout { public ExpectedMarker[] markers; }
        [Serializable] sealed class ExpectedMarker { public string name; public float[] position, forward, up, scale; }
        static Vector3 Vector(float[] components) => new Vector3(components[0], components[1], components[2]);

        [SetUp] public void Setup()
        {
            ownsFolder = false;
            restoreDefaultRunner = false;
            Assert.IsFalse(AssetDatabase.IsValidFolder(FixtureFolder), "Do not overwrite an existing test fixture folder.");
            previousScene = SceneManager.GetActiveScene();
            // Test Runner restores the user's scene after the run and supplies a
            // default untitled Camera/Light scene. Accept only that unchanged
            // default or an empty shell; preserve any additional authored content.
            bool runnerShell = IsEmptyUntitled(previousScene) || IsDefaultRunnerScene(previousScene);
            if (string.IsNullOrEmpty(previousScene.path) && !runnerShell)
                throw new InvalidOperationException("Asset tests preserve the non-default untitled scene: " + previousScene.rootCount + " roots: " + string.Join(", ", previousScene.GetRootGameObjects().Select(r => r.name + " at " + r.transform.position)));
            restoreDefaultRunner = runnerShell;
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, runnerShell ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            AssetDatabase.CreateFolder("Assets/_Project/Tests", "G09TemporaryFixture");
            ownsFolder = true;
            var source = new GameObject("TestShip");
            var target = source.AddComponent<ShipTarget>();
            target.visualRoot = new GameObject("VisualRoot");
            target.visualRoot.transform.SetParent(source.transform, false);
            var hitRoot = new GameObject("HitVolumes"); hitRoot.transform.SetParent(source.transform, false);
            var collider = hitRoot.AddComponent<BoxCollider>(); collider.isTrigger = true;
            target.hitVolumes = new Collider[] { collider };
            prefab = PrefabUtility.SaveAsPrefabAsset(source, FixtureFolder + "/TestShip.prefab");
            Object.DestroyImmediate(source);
            layout = new GameObject("ImportedLayoutRoot");
            fleet = new GameObject("GeneratedFleet").transform;
            // Only the newly created, owned fixture is saved; never the previous scene.
            Assert.IsTrue(EditorSceneManager.SaveScene(fixture, FixtureFolder + "/Fixture.unity"));
        }

        static bool IsEmptyUntitled(Scene scene) => scene.IsValid() && string.IsNullOrEmpty(scene.path) && scene.rootCount == 0;
        static bool IsDefaultRunnerScene(Scene scene)
        {
            if (!scene.IsValid() || !string.IsNullOrEmpty(scene.path) || scene.rootCount != 2) return false;
            var roots = scene.GetRootGameObjects();
            var camera = roots.SingleOrDefault(r => r.name == "Main Camera" && r.GetComponent<Camera>() != null);
            var light = roots.SingleOrDefault(r => r.name == "Directional Light" && r.GetComponent<Light>() != null);
            return camera != null && light != null && light.GetComponent<Light>().type == LightType.Directional &&
                camera.transform.childCount == 0 && light.transform.childCount == 0 &&
                camera.transform.localScale == Vector3.one && light.transform.localScale == Vector3.one &&
                Vector3.Distance(camera.transform.position, new Vector3(0, 1, -10)) < .001f &&
                Quaternion.Angle(camera.transform.rotation, Quaternion.identity) < .01f &&
                Vector3.Distance(light.transform.position, new Vector3(0, 3, 0)) < .001f &&
                Quaternion.Angle(light.transform.rotation, Quaternion.Euler(50, -30, 0)) < .01f;
        }

        [TearDown] public void Cleanup()
        {
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (ownsFolder && AssetDatabase.IsValidFolder(FixtureFolder)) AssetDatabase.DeleteAsset(FixtureFolder);
            // Closing/deleting the fixture can leave an unsaved empty shell.
            // Restore Test Runner's clean default so following existing authoring
            // tests do not see our cleanup residue as the user's unsaved work.
            if (restoreDefaultRunner && (!previousScene.IsValid() || !previousScene.isLoaded))
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        Transform Marker(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false); marker.localPosition = position; marker.localRotation = rotation;
            return marker;
        }

        ShipTarget[] Import() => FleetLayoutImporter.Import(layout, fleet, prefab, prefab, prefab);

        [Test] public void ParentTransformsAreResolvedAndRepeatedImportKeepsInstanceIdentityAndManualObjects()
        {
            layout.transform.SetPositionAndRotation(new Vector3(30, 2, -5), Quaternion.Euler(0, 40, 0));
            fleet.SetPositionAndRotation(new Vector3(-10, 8, 5), Quaternion.Euler(0, -70, 0));
            var parent = Marker("FormationParent", layout.transform, new Vector3(2, 1, 3), Quaternion.Euler(0, 20, 0));
            var first = Marker("SPAWN_Small_001", parent, new Vector3(1, 2, 4), Quaternion.Euler(10, 70, 0));
            Marker("SPAWN_Large_002", parent, new Vector3(-6, -1, 8), Quaternion.Euler(-15, -30, 0));
            var manual = new GameObject("ManualOutsideGeneratedFleet");
            manual.transform.position = new Vector3(6, 7, 8);
            var initial = Import();
            Assert.AreEqual(2, initial.Length);
            var identities = initial.ToDictionary(t => t.targetId, t => t.GetEntityId());
            var importedFirst = initial.Single(t => t.targetId == first.name);
            Assert.That(Vector3.Distance(first.position, importedFirst.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(first.rotation, importedFirst.transform.rotation), Is.LessThan(.01f));
            first.localPosition += Vector3.right * 9;
            var repeated = Import();
            Assert.AreEqual(2, repeated.Length);
            foreach (var target in repeated)
            {
                Assert.AreEqual(identities[target.targetId], target.GetEntityId(), "Reimport must update existing gameplay roots.");
                Assert.AreEqual(Vector3.one, target.transform.localScale);
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(target));
            }
            Assert.That(Vector3.Distance(first.position, repeated.Single(t => t.targetId == first.name).transform.position), Is.LessThan(.001f));
            Assert.IsNotNull(manual); Assert.AreEqual(new Vector3(6, 7, 8), manual.transform.position);
            Assert.AreEqual(2, fleet.GetComponentsInChildren<ShipTarget>(true).Length);
        }

        [TestCase("SPAWN_Small_001")]
        [TestCase("SPAWN_Command_001")]
        [TestCase("SPAWN_Unknown_003")]
        public void DuplicateOrUnknownMarkerRejectsWholeImportBeforeAnyMutation(string invalidName)
        {
            var first = Marker("SPAWN_Small_001", layout.transform, Vector3.forward * 10, Quaternion.identity);
            Marker("SPAWN_Large_002", layout.transform, Vector3.forward * 20, Quaternion.identity);
            var original = Import();
            var firstTarget = original.Single(t => t.targetId == first.name);
            var originalPose = firstTarget.transform.position;
            first.localPosition += Vector3.right * 50;
            Marker("SPAWN_Command_004", layout.transform, Vector3.forward * 40, Quaternion.identity);
            Marker(invalidName, layout.transform, Vector3.forward * 30, Quaternion.identity);
            var error = Assert.Throws<InvalidOperationException>(() => Import());
            StringAssert.Contains(invalidName, error.Message);
            Assert.AreEqual(2, fleet.GetComponentsInChildren<ShipTarget>(true).Length);
            Assert.AreEqual(originalPose, firstTarget.transform.position, "An invalid later marker must not leave earlier pose updates behind.");
            foreach (var target in original) Assert.IsNotNull(target);
        }

        [Test] public void NonUnitMarkerScaleRejectsBeforeChangesAndRemovedMarkerRemovesOnlyItsTarget()
        {
            var first = Marker("SPAWN_Small_001", layout.transform, Vector3.forward * 10, Quaternion.identity);
            var second = Marker("SPAWN_Large_002", layout.transform, Vector3.forward * 20, Quaternion.identity);
            var original = Import();
            first.localScale = new Vector3(1, 2, 1);
            Assert.Throws<InvalidOperationException>(() => Import());
            Assert.AreEqual(2, fleet.GetComponentsInChildren<ShipTarget>(true).Length);
            first.localScale = Vector3.one; Object.DestroyImmediate(second.gameObject);
            var result = Import();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(original.Single(t => t != null && t.targetId == first.name), result[0]);
        }

        [Test] public void NegativeInheritedLayoutScaleIsRejectedBeforeChangingAnExistingFleet()
        {
            Marker("SPAWN_Small_001", layout.transform, Vector3.forward * 10, Quaternion.identity);
            var original = Import(); var pose = original[0].transform.position;
            layout.transform.localScale = new Vector3(-1, 1, 1);
            Assert.Throws<InvalidOperationException>(() => Import());
            Assert.AreEqual(1, fleet.GetComponentsInChildren<ShipTarget>(true).Length);
            Assert.AreEqual(pose, original[0].transform.position);
        }

        [Test] public void SavedFleetSceneReopensWithFortyDistinctVisibleTargetsAndCompleteReferences()
        {
            // Read an exact scene-asset copy so a user's already-open FleetAssault is never closed or saved.
            string copyPath = FixtureFolder + "/FleetReadback.unity";
            Assert.IsTrue(AssetDatabase.CopyAsset(FleetAssaultBuilder.ScenePath, copyPath));
            for (int pass = 0; pass < 2; pass++)
            {
                var scene = EditorSceneManager.OpenScene(copyPath, OpenSceneMode.Additive);
                try
                {
                    var targets = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShipTarget>(true)).ToArray();
                    Assert.AreEqual(40, targets.Length);
                    Assert.AreEqual(40, targets.Select(t => t.targetId).Distinct().Count());
                    var mission = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MissionController>(true)).Single();
                    Assert.AreEqual(40, mission.targets.Length);
                    CollectionAssert.AreEquivalent(targets, mission.targets);
                    Assert.IsNotNull(mission.settings); Assert.IsNotNull(mission.motor); Assert.IsNotNull(mission.input);
                    Assert.IsNotNull(mission.chaseCamera); Assert.IsNotNull(mission.score);
                    Assert.Greater(mission.settings.missionSeconds, 120);
                    Assert.AreEqual(Vector3.one, mission.motor.transform.localScale);
                    foreach (var target in targets)
                    {
                        Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(target));
                        Assert.AreEqual(Vector3.one, target.transform.localScale);
                        Assert.IsNotNull(target.visualRoot); Assert.IsTrue(target.visualRoot.activeSelf);
                        Assert.IsNotEmpty(target.hitVolumes);
                        Assert.IsTrue(target.hitVolumes.All(c => c != null && c.enabled && c.isTrigger));
                        Assert.IsTrue(target.hitVolumes.All(c => c.GetComponentInParent<ShipTarget>() == target));
                        Assert.IsTrue(target.hitVolumes.All(c => !c.transform.IsChildOf(target.visualRoot.transform)));
                        var renderers = target.visualRoot.GetComponentsInChildren<Renderer>(true);
                        Assert.IsNotEmpty(renderers);
                        foreach (var renderer in renderers)
                        {
                            Assert.IsTrue(renderer.enabled);
                            Assert.IsTrue(renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null && m.shader != null), target.targetId + " missing material/shader");
                        }
                        Assert.IsTrue(target.visualRoot.GetComponentsInChildren<MeshFilter>(true).All(m => m.sharedMesh != null));
                    }
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Component>(true)) Assert.IsNotNull(component, "Missing script in saved FleetAssault");
                }
                finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(fixture); }
            }
        }

        [Test] public void ActualBlenderFleetImportsFortyPrefabInstancesAndSurvivesSaveReopen()
        {
            const string models = "Assets/_Project/Art/Models/Fleet/";
            const string prefabs = "Assets/_Project/Prefabs/Fleet/";
            var importedLayout = AssetDatabase.LoadAssetAtPath<GameObject>(models + "FleetLayout.fbx");
            var small = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs + "Frigate.prefab");
            var large = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs + "Cruiser.prefab");
            var command = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs + "Command.prefab");
            Assert.IsNotNull(importedLayout); Assert.IsNotNull(small); Assert.IsNotNull(large); Assert.IsNotNull(command);
            var markers = importedLayout.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("SPAWN_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(40, markers.Length, "Validate the actual FBX marker inventory.");
            string expectedPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/Blender/Exports/FleetLayout_expected.json"));
            var expected = JsonUtility.FromJson<ExpectedLayout>(File.ReadAllText(expectedPath));
            Assert.AreEqual(40, expected.markers.Length);
            foreach (var sourcePose in expected.markers)
            {
                var imported = markers.Single(m => m.name == sourcePose.name);
                Assert.That(Vector3.Distance(Vector(sourcePose.position), imported.position), Is.LessThan(.002f), sourcePose.name + " source position");
                Assert.That(Vector3.Distance(Vector(sourcePose.forward), imported.forward), Is.LessThan(.002f), sourcePose.name + " source forward");
                Assert.That(Vector3.Distance(Vector(sourcePose.up), imported.up), Is.LessThan(.002f), sourcePose.name + " source up");
                Assert.That(Vector3.Distance(Vector(sourcePose.scale), imported.lossyScale), Is.LessThan(.002f), sourcePose.name + " source scale");
            }
            var first = FleetLayoutImporter.Import(importedLayout, fleet, small, large, command);
            Assert.AreEqual(markers.Length, first.Length);
            var identities = first.ToDictionary(t => t.targetId, t => t.GetEntityId());
            var second = FleetLayoutImporter.Import(importedLayout, fleet, small, large, command);
            foreach (var target in second)
            {
                var marker = markers.Single(m => m.name == target.targetId);
                Assert.AreEqual(identities[target.targetId], target.GetEntityId());
                Assert.That(Vector3.Distance(marker.position, target.transform.position), Is.LessThan(.002f));
                Assert.That(Quaternion.Angle(marker.rotation, target.transform.rotation), Is.LessThan(.02f));
                var expectedPrefab = marker.name.Contains("_Small_") ? small : marker.name.Contains("_Large_") ? large : command;
                Assert.AreEqual(expectedPrefab, PrefabUtility.GetCorrespondingObjectFromSource(target.gameObject));
            }
            var savedPoses = second.ToDictionary(t => t.targetId, t => t.transform.position);
            var sentinel = new GameObject("SavedManualOutsideFleet"); sentinel.transform.position = new Vector3(11, 12, 13);
            string scenePath = FixtureFolder + "/ImportedFleet.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(fixture, scenePath));
            EditorSceneManager.CloseScene(fixture, true);
            fixture = EditorSceneManager.OpenScene(scenePath, IsEmptyUntitled(SceneManager.GetActiveScene()) ? OpenSceneMode.Single : OpenSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var roots = fixture.GetRootGameObjects();
            var readback = roots.SelectMany(r => r.GetComponentsInChildren<ShipTarget>(true)).ToArray();
            Assert.AreEqual(40, readback.Length);
            foreach (var target in readback)
            {
                Assert.That(Vector3.Distance(savedPoses[target.targetId], target.transform.position), Is.LessThan(.002f));
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(target));
            }
            Assert.AreEqual(new Vector3(11, 12, 13), roots.Single(r => r.name == "SavedManualOutsideFleet").transform.position);
        }

        [TestCase("Droplet", 1.2f, 1.2f, 2.4f)]
        [TestCase("Frigate", 9f, 4.455983f, 22.14f)]
        [TestCase("Cruiser", 18.7f, 8.01f, 33.275f)]
        [TestCase("Command", 22.392731f, 10.85f, 37.275f)]
        public void ActualImportedModelHasExpectedDimensionsAndUsableNormals(string name, float x, float y, float z)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Fleet/" + name + ".fbx");
            Assert.IsNotNull(asset);
            var renderers = asset.GetComponentsInChildren<Renderer>(); Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Assert.That(Vector3.Distance(new Vector3(x, y, z), bounds.size), Is.LessThan(.02f), "Compare actual imported bounds to the Blender export manifest.");
            var meshes = asset.GetComponentsInChildren<MeshFilter>(); Assert.IsNotEmpty(meshes);
            foreach (var filter in meshes)
            {
                var mesh = filter.sharedMesh; Assert.IsNotNull(mesh); Assert.Greater(mesh.vertexCount, 0);
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                Assert.IsTrue(mesh.normals.All(n => float.IsFinite(n.x) && float.IsFinite(n.y) && float.IsFinite(n.z) && n.sqrMagnitude > .9f && n.sqrMagnitude < 1.1f));
                Assert.Greater(mesh.triangles.Length, 0);
            }
        }
    }
}
