using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    // Read-only release contracts. Never run an authoring tool or save a scene/asset.
    // Preview scenes keep the user's active scene and any unsaved work intact.
    public sealed class FusionFleetAssetTests
    {
        const string ModelPath = "Assets/_Project/Art/Models/Ships/FusionFrigate/FusionFrigate.fbx";
        const string PrefabPath = "Assets/_Project/Prefabs/Fleet/FusionFrigate.prefab";
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_Expanded.unity";
        const string OriginalScenePath = "Assets/_Project/Scenes/FleetAssault_SolarLayout.unity";
        const string ConfigPath = "Tools/Blender/FleetExpansion/layout_config.json";

        [Serializable] sealed class LayoutContract
        {
            public int originalCount, targetCount, seed;
            public float shipLength;
            public MarkerContract[] markers;
        }
        [Serializable] sealed class MarkerContract
        {
            public string name, id, group;
            public float[] position, forward, up, scale;
        }

        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Generate and save the FusionFrigate gameplay prefab before this asset check.");
            return prefab;
        }

        static LayoutContract Layout()
        {
            Assert.IsTrue(File.Exists(ConfigPath), "The versioned authority for this fleet must exist.");
            var layout = JsonUtility.FromJson<LayoutContract>(File.ReadAllText(ConfigPath));
            Assert.IsNotNull(layout);
            Assert.IsNotNull(layout.markers);
            Assert.Greater(layout.originalCount, 0);
            Assert.Greater(layout.targetCount, layout.originalCount, "An expanded fleet must not shrink the baseline.");
            Assert.GreaterOrEqual(layout.targetCount, 120);
            Assert.AreEqual(layout.targetCount, layout.markers.Length);
            Assert.AreEqual(layout.targetCount, layout.markers.Select(m => m.name).Distinct().Count());
            Assert.AreEqual(layout.targetCount, layout.markers.Select(m => m.id).Distinct().Count());
            return layout;
        }

        static Vector3 Vector(float[] values)
        {
            Assert.IsNotNull(values);
            Assert.AreEqual(3, values.Length);
            Assert.IsTrue(values.All(float.IsFinite));
            return new Vector3(values[0], values[1], values[2]);
        }

        static void Near(Vector3 actual, Vector3 expected, float tolerance, string label)
            => Assert.That(Vector3.Distance(actual, expected), Is.LessThanOrEqualTo(tolerance), label + ": " + actual + " expected " + expected);

        static T[] Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        static long Triangles(Renderer[] renderers)
        {
            long count = 0;
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer);
                var filter = renderer.GetComponent<MeshFilter>();
                Assert.IsNotNull(filter);
                Assert.IsNotNull(filter.sharedMesh);
                // GetIndexCount and bounds work with Read/Write disabled; no mesh copy.
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    count += (long)filter.sharedMesh.GetIndexCount(i) / 3;
            }
            return count;
        }

        static Bounds MeshBounds(Renderer[] renderers, Transform relativeTo = null)
        {
            bool initialized = false;
            Bounds result = default;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                Assert.IsNotNull(filter);
                Assert.IsNotNull(filter.sharedMesh);
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                if (relativeTo != null) matrix = relativeTo.worldToLocalMatrix * matrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                    if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                    else result.Encapsulate(point);
                }
            }
            Assert.IsTrue(initialized);
            return result;
        }

        [Test]
        public void ImportedModelUsesSafeSettingsAndSharedNativeResources()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.IsNotNull(importer, "Import the verified FBX before this asset check.");
            Assert.IsFalse(importer.isReadable, "No gameplay code reads this imported mesh's vertices or indices.");
            Assert.IsFalse(importer.importAnimation);
            Assert.IsFalse(importer.importCameras);
            Assert.IsFalse(importer.importLights);
            Assert.IsFalse(importer.addCollider);
            Assert.IsTrue(importer.useFileScale);
            Assert.IsTrue(importer.bakeAxisConversion);
            Assert.That(importer.globalScale, Is.EqualTo(1).Within(.00001f));
            Assert.AreEqual(ModelImporterNormals.Import, importer.importNormals);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.IsNotNull(model);
            var modelMeshes = model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Distinct().ToArray();
            Assert.AreEqual(19, modelMeshes.Length, "The actual verified export has 19 shared mesh resources across all LODs.");
            Assert.IsTrue(modelMeshes.All(m => m != null && !m.isReadable));
            var prefab = Prefab();
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            CollectionAssert.AreEquivalent(modelMeshes, filters.Select(f => f.sharedMesh).Distinct());
            Assert.IsTrue(filters.All(f => AssetDatabase.GetAssetPath(f.sharedMesh) == ModelPath), "Gameplay prefabs must reference the shared FBX, not per-ship copied meshes.");
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Assert.AreEqual(4, materials.Length);
            foreach (var material in materials)
            {
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                Assert.IsTrue(AssetDatabase.Contains(material), "Materials must be shared persisted assets.");
                StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                foreach (string property in material.GetTexturePropertyNames())
                    Assert.IsNull(material.GetTexture(property), "This verified model has no authored texture dependency: " + material.name + "/" + property);
            }
            Assert.IsEmpty(model.GetComponentsInChildren<Camera>(true));
            Assert.IsEmpty(model.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(model.GetComponentsInChildren<Animator>(true));
            Assert.IsEmpty(model.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }

        [Test]
        public void GameplayPrefabHasOneCompleteAlignedLodGroupWithoutUncontrolledGeometry()
        {
            var prefab = Prefab();
            Near(prefab.transform.localScale, Vector3.one, .00001f, "gameplay root scale");
            var target = prefab.GetComponent<ShipTarget>();
            Assert.IsNotNull(target);
            Assert.IsNotNull(target.visualRoot);
            Assert.IsTrue(target.visualRoot.activeSelf);
            Assert.IsTrue(target.visualRoot.transform.IsChildOf(prefab.transform));
            var groups = prefab.GetComponentsInChildren<LODGroup>(true);
            Assert.AreEqual(1, groups.Length, "An imported and a gameplay LODGroup must not both control the ship.");
            Assert.IsTrue(groups[0].enabled);
            var lods = groups[0].GetLODs();
            Assert.AreEqual(3, lods.Length);
            int[] expectedRenderers = { 13, 13, 10 };
            long[] expectedTriangles = { 17224, 5400, 1396 };
            var assigned = new HashSet<Renderer>();
            Bounds reference = default;
            for (int i = 0; i < lods.Length; i++)
            {
                Assert.AreEqual(expectedRenderers[i], lods[i].renderers.Length, "LOD" + i);
                Assert.AreEqual(expectedTriangles[i], Triangles(lods[i].renderers), "LOD" + i + " full-ship triangles include repeated engines and turrets.");
                Assert.Greater(lods[i].screenRelativeTransitionHeight, 0);
                Assert.Less(lods[i].screenRelativeTransitionHeight, i == 0 ? 1 : lods[i - 1].screenRelativeTransitionHeight);
                foreach (var renderer in lods[i].renderers)
                {
                    Assert.IsTrue(assigned.Add(renderer), "One Renderer may belong to only one LOD.");
                    Assert.IsTrue(renderer.transform.IsChildOf(target.visualRoot.transform), "Destroying VisualRoot must hide every LOD.");
                    Assert.IsTrue(renderer.enabled);
                    Assert.IsFalse(renderer.forceRenderingOff);
                    for (Transform node = renderer.transform; node != prefab.transform; node = node.parent)
                        Assert.IsTrue(node.gameObject.activeSelf, "An inactive imported parent must not permanently hide a lower LOD: " + node.name);
                }
                Bounds bounds = MeshBounds(lods[i].renderers, prefab.transform);
                if (i == 0) reference = bounds;
                else { Near(bounds.center, reference.center, .003f, "LOD centers"); Near(bounds.size, reference.size, .003f, "LOD dimensions"); }
            }
            CollectionAssert.AreEquivalent(prefab.GetComponentsInChildren<Renderer>(true), assigned, "No visual mesh should draw outside the LODGroup.");
            Near(reference.size, new Vector3(7.38f, 6.211535f, 22.14f), .005f, "calibrated gameplay dimensions");
            Near(reference.center, new Vector3(0, .3742325f, 0), .005f, "calibrated gameplay pivot");
        }

        [Test]
        public void GameplayPrefabKeepsSimpleIndependentHitVolumesAndFiveCorrectSockets()
        {
            var prefab = Prefab();
            var target = prefab.GetComponent<ShipTarget>();
            Assert.IsNotNull(target);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<ShipTarget>(true).Length);
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.InRange(3, 9), "Use a small compound shape, not per-armour or mesh collision.");
            CollectionAssert.AreEquivalent(colliders, target.hitVolumes, "Every hit volume must be disabled and restored with this one target.");
            Assert.AreEqual(colliders.Length, target.hitVolumes.Distinct().Count());
            foreach (var collider in colliders)
            {
                Assert.IsTrue(collider is BoxCollider || collider is CapsuleCollider, collider.name);
                Assert.IsTrue(collider.enabled);
                Assert.IsTrue(collider.isTrigger);
                Assert.AreSame(target, collider.GetComponentInParent<ShipTarget>(true));
                Assert.IsFalse(collider.transform.IsChildOf(target.visualRoot.transform), "LOD or intact-visual changes must not control collision.");
                Assert.AreEqual(LayerMask.NameToLayer("ShipTarget"), collider.gameObject.layer);
            }
            foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), node.name);
                Assert.AreEqual((StaticEditorFlags)0, GameObjectUtility.GetStaticEditorFlags(node.gameObject), "Independent destruction must not use static batching: " + node.name);
            }
            Assert.IsEmpty(prefab.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Camera>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true));
            string[] names = { "MainExhaust", "AuxiliaryExhaust_01", "AuxiliaryExhaust_02", "AuxiliaryExhaust_03", "AuxiliaryExhaust_04" };
            Vector3[] positions = { new Vector3(0, -.05f, -11.07f), new Vector3(-2.5f, .15f, -10.549f), new Vector3(2.5f, .15f, -10.549f), new Vector3(-1.62f, -2, -10.549f), new Vector3(1.62f, -2, -10.549f) };
            var nodes = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                var matches = nodes.Where(t => t.name == names[i]).ToArray();
                Assert.AreEqual(1, matches.Length, names[i]);
                Near(prefab.transform.InverseTransformPoint(matches[0].position), positions[i], .003f, names[i] + " position");
                Assert.Greater(Vector3.Dot(prefab.transform.InverseTransformDirection(matches[0].forward), Vector3.back), .999f, names[i] + " must face out of the stern");
            }
            var presenter = prefab.GetComponent<DestructionPresenter>();
            Assert.IsNotNull(presenter);
            Assert.AreSame(target, presenter.target);
        }

        [Test]
        public void SavedExpandedFleetMatchesItsVersionedAuthorityAndReopensWithoutDuplicates()
        {
            LayoutContract config = Layout();
            var expected = config.markers.ToDictionary(m => m.name);
            var activeBefore = SceneManager.GetActiveScene();
            byte[] savedBytes = File.ReadAllBytes(ScenePath);
            string[] firstReadback = null;
            for (int pass = 0; pass < 2; pass++)
            {
                Scene preview = EditorSceneManager.OpenPreviewScene(ScenePath);
                try
                {
                    var targets = Components<ShipTarget>(preview);
                    Assert.AreEqual(config.targetCount, targets.Length, "LOD children or a second scoring fleet must not inflate the target count.");
                    CollectionAssert.AreEquivalent(expected.Keys, targets.Select(t => t.targetId));
                    Assert.AreEqual(targets.Length, targets.Select(t => t.targetId).Distinct().Count());
                    var mission = Components<MissionController>(preview).Single();
                    CollectionAssert.AreEquivalent(targets, mission.targets, "The complete membership is serialized before Play.");
                    var allNodes = Components<Transform>(preview);
                    var fleetRoots = allNodes.Where(t => t.name == "GeneratedFleet").ToArray();
                    Assert.AreEqual(1, fleetRoots.Length);
                    Near(fleetRoots[0].lossyScale, Vector3.one, .00001f, "fleet scale must not enlarge ships");
                    var bounds = new List<Bounds>();
                    foreach (var target in targets)
                    {
                        MarkerContract marker = expected[target.targetId];
                        Assert.IsTrue(target.transform.IsChildOf(fleetRoots[0]));
                        Assert.AreEqual(marker.group, target.transform.parent.name);
                        Assert.AreEqual(PrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject));
                        Near(target.transform.position, Vector(marker.position), .003f, target.targetId + " position");
                        Near(target.transform.forward, Vector(marker.forward), .0001f, target.targetId + " forward");
                        Near(target.transform.up, Vector(marker.up), .0001f, target.targetId + " up");
                        Near(target.transform.lossyScale, Vector(marker.scale), .0001f, target.targetId + " scale");
                        Assert.IsTrue(target.gameObject.activeInHierarchy);
                        Assert.IsNotNull(target.visualRoot);
                        Assert.IsTrue(target.visualRoot.activeSelf);
                        Assert.IsTrue(target.hitVolumes.All(c => c != null && c.enabled && c.GetComponentInParent<ShipTarget>() == target));
                        var presenter = target.GetComponent<DestructionPresenter>();
                        Assert.IsNotNull(presenter);
                        Assert.IsNotNull(presenter.effects, "Connect the existing shared feedback pool.");
                        Assert.AreSame(mission, presenter.effects.mission);
                        var wrecks = presenter.effects.wreckPrefabs;
                        Assert.IsTrue(presenter.wreckKind < 0 || wrecks == null || presenter.wreckKind >= wrecks.Length || wrecks[presenter.wreckKind] == null,
                            "Generic feedback must not display an incompatible old ship wreck.");
                        var group = target.GetComponentsInChildren<LODGroup>(true).Single();
                        Bounds shape = MeshBounds(group.GetLODs()[0].renderers);
                        foreach (Bounds prior in bounds) Assert.IsFalse(prior.Intersects(shape), "Saved ship geometry bounds overlap at " + target.targetId);
                        bounds.Add(shape);
                        Assert.Less(Vector3.Distance(target.transform.position, mission.arenaCenter) + config.shipLength * 2,
                            mission.settings.boundaryWarningRadius, "Targets and turning space must lie inside the warning boundary.");
                    }
                    var meshes = targets.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh).Distinct().ToArray();
                    var materials = targets.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                    Assert.AreEqual(19, meshes.Length, "More ships must share the same imported resources.");
                    Assert.AreEqual(4, materials.Length);
                    string[] readback = targets.OrderBy(t => t.targetId).Select(t => t.targetId + "|" + JsonUtility.ToJson(t.transform.position) + "|" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).ToArray();
                    if (pass == 0) firstReadback = readback;
                    else CollectionAssert.AreEqual(firstReadback, readback);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            Assert.AreEqual(activeBefore, SceneManager.GetActiveScene(), "Preview inspection must not replace the active scene.");
            CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(ScenePath), "This test never saves the scene.");
        }

        [Test]
        public void ExpandedMissionUsesIndependentSettingsAndPreservesSolarFlightContract()
        {
            Scene original = EditorSceneManager.OpenPreviewScene(OriginalScenePath);
            Scene expanded = default;
            try
            {
                expanded = EditorSceneManager.OpenPreviewScene(ScenePath);
                var before = Components<MissionController>(original).Single();
                var after = Components<MissionController>(expanded).Single();
                LayoutContract config = Layout();
                Assert.AreEqual(config.originalCount, Components<ShipTarget>(original).Length);
                Assert.IsNotNull(after.settings);
                Assert.AreNotSame(before.settings, after.settings, "Keep the original solar mission configuration.");
                Assert.IsTrue(AssetDatabase.Contains(after.settings));
                Assert.Greater(after.settings.missionSeconds, before.settings.missionSeconds);
                Assert.GreaterOrEqual(after.settings.boundaryRadius, before.settings.boundaryRadius);
                Assert.GreaterOrEqual(after.settings.boundaryWarningRadius, before.settings.boundaryWarningRadius);
                Assert.AreEqual(before.settings.maxCruiseSpeed, after.settings.maxCruiseSpeed);
                Assert.AreEqual(before.settings.boostMultiplier, after.settings.boostMultiplier);
                Assert.AreEqual(before.settings.acceleration, after.settings.acceleration);
                Assert.AreEqual(before.settings.turnDegreesPerSecond, after.settings.turnDegreesPerSecond);
                Assert.AreEqual(before.settings.hitRadius, after.settings.hitRadius);
                Assert.AreEqual(before.settings.targetLayers, after.settings.targetLayers);
                Assert.AreEqual(before.settings.fieldOfView, after.settings.fieldOfView);
                Assert.AreEqual(before.settings.cameraDistance, after.settings.cameraDistance);
                Assert.IsNotNull(after.motor);
                Assert.IsNotNull(after.input);
                Assert.IsNotNull(after.chaseCamera);
                Assert.IsNotNull(after.score);
                Assert.IsTrue(Components<HudPresenter>(expanded).All(h => h.mission == after));
                var oldBackdrop = Components<SolarSystemBackdrop>(original).Single();
                var newBackdrop = Components<SolarSystemBackdrop>(expanded).Single();
                Assert.AreSame(oldBackdrop.layoutJson, newBackdrop.layoutJson, "Fleet expansion must retain the current solar-system macro layout authority.");
            }
            finally
            {
                if (expanded.IsValid()) EditorSceneManager.ClosePreviewScene(expanded);
                EditorSceneManager.ClosePreviewScene(original);
            }
        }
    }
}
