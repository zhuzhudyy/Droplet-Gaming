using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Temporary shared-asset fixtures; no asset writes or scene generation.</summary>
    public sealed class Fleet2000SunTests
    {
        readonly List<Object> owned = new List<Object>();
        readonly Vector3 origin = new Vector3(180000, 4500, 0);
        Mesh mesh;
        Material material;
        DropletSettings settings;
        FleetRenderManager manager;
        DropletHitDetector detector;
        Transform player;
        Camera camera;

        [SetUp]
        public void Setup()
        {
            Assert.IsTrue(SystemInfo.supportsInstancing, "Fixture requires the supported instancing target platform.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            material = new Material(shader) { enableInstancing = true };
            owned.Add(material);
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            settings = ScriptableObject.CreateInstance<DropletSettings>();
            owned.Add(settings);
            settings.targetLayers = 1 << 31; settings.hitRadius = .3f;
            var playerObject = NewObject("Fleet2000 fixture player");
            player = playerObject.transform; player.position = origin;
            detector = playerObject.AddComponent<DropletHitDetector>(); detector.settings = settings;
            var cameraObject = NewObject("Fleet2000 fixture camera");
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = origin;
            manager = NewObject("Fleet2000 fixture manager").AddComponent<FleetRenderManager>();
            manager.nearDistance = 100; manager.middleDistance = 500; manager.transitionHysteresis = 10;
            detector.fleetRenderer = manager;
        }

        [TearDown]
        public void Cleanup()
        {
            if(camera!=null)camera.targetTexture=null;
            // Unbind and restore targets before destroying their shared resources.
            if (manager != null) manager.enabled = false;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            Time.timeScale = 1;
        }

        GameObject NewObject(string name)
        {
            var go = new GameObject(name); owned.Add(go); return go;
        }

        ShipTarget CreateTarget(string id, Vector3 position)
        {
            var root = NewObject("Fixture " + id); root.transform.position = position;
            var target = root.AddComponent<ShipTarget>(); target.targetId = id;
            target.visualRoot = new GameObject("VisualRoot");
            target.visualRoot.transform.SetParent(root.transform, false);
            var group = target.visualRoot.AddComponent<LODGroup>();
            var lods = new LOD[3];
            for (int i = 0; i < 3; i++)
            {
                var visual = new GameObject("LOD" + i);
                visual.transform.SetParent(target.visualRoot.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                lods[i] = new LOD(i == 0 ? .2f : i == 1 ? .05f : .001f, new Renderer[] { renderer });
            }
            group.SetLODs(lods); group.RecalculateBounds();
            target.hitVolumes = new Collider[4];
            for (int i = 0; i < 4; i++)
            {
                var shape = new GameObject("Compound shape " + i); shape.layer = 31;
                shape.transform.SetParent(root.transform, false);
                var box = shape.AddComponent<BoxCollider>(); box.isTrigger = true;
                box.center = Vector3.forward * (i - 1.5f) * .18f;
                box.size = Vector3.one;
                target.hitVolumes[i] = box;
            }
            return target;
        }

        Renderer[] AddRepeatedDistantParts(ShipTarget target, int extraCount)
        {
            var group = target.visualRoot.GetComponent<LODGroup>();
            var lods = group.GetLODs();
            var renderers = new List<Renderer>(lods[2].renderers);
            var added = new Renderer[extraCount];
            for (int i = 0; i < extraCount; i++)
            {
                var visual = new GameObject("Shared auxiliary core " + i);
                visual.transform.SetParent(target.visualRoot.transform, false);
                visual.transform.localPosition = new Vector3((i + 1) * 2, 0, 0);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                added[i] = renderer; renderers.Add(renderer);
            }
            lods[2].renderers = renderers.ToArray(); group.SetLODs(lods); group.RecalculateBounds();
            return added;
        }

        void EnableFixtureRendering()
        {
            var texture = new RenderTexture(64, 64, 24);
            owned.Add(texture); texture.Create();
            camera.targetTexture = texture;
            camera.nearClipPlane = .1f; camera.farClipPlane = 5000;
            camera.cullingMask = 1;
            camera.enabled = true;
        }

        [Test]
        public void ArbitrarilyLongSweepActivatesEveryDistantShipAndDeduplicatesCompoundShapes()
        {
            var first = CreateTarget("first", origin + Vector3.forward * 8000);
            var middle = CreateTarget("middle", origin + Vector3.forward * 15000);
            var end = CreateTarget("end", origin + Vector3.forward * 23000);
            var missed = CreateTarget("missed", origin + new Vector3(50, 0, 15000));
            var targets = new[] { first, middle, end, missed };
            manager.Configure(targets, camera, player);
            Assert.IsTrue(manager.InstancingAvailable);
            Assert.AreEqual(4, manager.InstancedShipCount);
            Assert.IsTrue(targets.All(t => t.hitVolumes.All(c => !c.enabled)));
            int events = 0;
            foreach (var target in targets) target.Destroyed += _ => events++;
            Assert.AreEqual(3, detector.SweepSegment(origin, origin + Vector3.forward * 24000, 624));
            Assert.AreEqual(3, events);
            Assert.GreaterOrEqual(manager.LastSweepActivated, 3);
            Assert.IsTrue(new[] { first, middle, end }.All(t => t.IsDestroyed && !manager.IsInstanced(t)));
            Assert.IsFalse(missed.IsDestroyed);
            Assert.IsFalse(manager.IsInteractionActive(missed));
            Assert.AreEqual(1, manager.AliveCount);
            Assert.AreEqual(0, detector.SweepSegment(origin + Vector3.forward * 24000, origin, 624));
            Assert.AreEqual(3, events);
        }

        [Test]
        public void DistantInitialOverlapAndResetKeepIdentityAndNoGhostInstance()
        {
            var target = CreateTarget("overlap-stable-id", origin + Vector3.forward * 4000);
            manager.Configure(new[] { target }, camera, player);
            Assert.IsTrue(manager.IsInstanced(target));
            Assert.AreEqual(1, detector.SweepSegment(target.transform.position, target.transform.position));
            Assert.IsTrue(target.IsDestroyed);
            Assert.IsFalse(manager.IsInstanced(target));
            Assert.IsFalse(manager.IsInteractionActive(target));
            Assert.AreEqual(0, manager.AliveCount);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                target.ResetTarget(); manager.RefreshNow();
                Assert.AreEqual("overlap-stable-id", target.targetId);
                Assert.AreEqual(1, manager.AliveCount);
                Assert.IsTrue(manager.IsInstanced(target));
                Assert.IsFalse(target.visualRoot.activeSelf);
                Assert.IsTrue(target.hitVolumes.All(c => !c.enabled));
                Assert.AreEqual(1, detector.SweepSegment(target.transform.position, target.transform.position));
            }
        }

        [UnityTest]
        public IEnumerator CameraAndPlayerApproachSwitchExactlyOneViewAndDisablingManagerFailsOpen()
        {
            var target = CreateTarget("approach", origin + Vector3.forward * 4000);
            manager.Configure(new[] { target }, camera, player);
            Assert.IsTrue(manager.IsInstanced(target));
            Assert.IsFalse(target.visualRoot.activeSelf);
            player.position = target.transform.position;
            manager.RefreshNow();
            Assert.IsFalse(manager.IsInstanced(target));
            Assert.IsTrue(target.visualRoot.activeSelf);
            Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
            player.position = origin;
            yield return null;
            manager.RefreshNow();
            Assert.IsTrue(manager.IsInstanced(target));
            camera.transform.position = target.transform.position;
            manager.RefreshNow();
            Assert.IsFalse(manager.IsInstanced(target));
            Assert.IsTrue(target.visualRoot.activeSelf);
            camera.transform.position = origin;
            manager.RefreshNow();
            Assert.IsTrue(manager.IsInstanced(target));
            manager.enabled = false;
            Assert.IsTrue(target.visualRoot.activeSelf);
            Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
            Assert.AreEqual(1, detector.SweepSegment(target.transform.position, target.transform.position));
        }

        [Test]
        public void RepeatedCacheRebuildKeepsAllUniqueSavedTargetsAndSharedAssets()
        {
            var a = CreateTarget("stable-A", origin + Vector3.forward * 1000);
            var b = CreateTarget("stable-B", origin + Vector3.forward * 2000);
            var targets = new[] { a, b };
            manager.Configure(targets, camera, player);
            int before = a.GetComponentsInChildren<Transform>(true).Length + b.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 3; i++)
            {
                manager.RebuildCache();
                Assert.AreEqual(2, manager.TargetCount);
                Assert.AreEqual(2, manager.AliveCount);
                Assert.AreSame(targets, manager.targets);
                Assert.AreEqual(2, manager.targets.Select(t => t.targetId).Distinct().Count());
                Assert.IsTrue(manager.targets.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)).All(f => f.sharedMesh == mesh));
                Assert.IsTrue(manager.targets.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).All(r => r.sharedMaterial == material));
            }
            Assert.AreEqual(before, a.GetComponentsInChildren<Transform>(true).Length + b.GetComponentsInChildren<Transform>(true).Length);
            int destroyed = 0; a.Destroyed += _ => destroyed++;
            Assert.AreEqual(1, detector.SweepSegment(a.transform.position, a.transform.position));
            Assert.AreEqual(1, destroyed);
        }

        [UnityTest]
        public IEnumerator SharedPartsRenderInCombinedGroupsAndQualityOffHidesOnlyOptionalCores()
        {
            var target = CreateTarget("shared-quality", origin + Vector3.forward * 2000);
            var cores = AddRepeatedDistantParts(target, 3);
            var drive = target.visualRoot.AddComponent<FusionDriveVisuals>();
            drive.Configure(cores, System.Array.Empty<Renderer>());
            manager.Configure(new[] { target }, camera, player);
            // Identical mesh/material/submesh can still require separate groups when
            // the existing effects quality must hide only the optional engine parts.
            Assert.AreEqual(2, manager.Lod2DrawGroups);
            EnableFixtureRendering();
            yield return null; yield return null;
            Assert.AreEqual(1, manager.LastLod2Ships);
            Assert.AreEqual(2, manager.LastDrawCalls);
            Assert.AreEqual(4, manager.LastSubmittedPartInstances);
            drive.SetQuality(EffectQuality.Low);
            yield return null; yield return null;
            Assert.AreEqual(4, manager.LastSubmittedPartInstances, "Low keeps all five-style core attachments, without high-only detail.");
            drive.SetQuality(EffectQuality.Off);
            yield return null; yield return null;
            Assert.AreEqual(1, manager.LastDrawCalls);
            Assert.AreEqual(1, manager.LastSubmittedPartInstances, "The hull remains, and distant optional cores obey the same Off quality as the prefab.");
            Assert.IsTrue(manager.IsInstanced(target));
            Assert.AreEqual(1, detector.SweepSegment(target.transform.position, target.transform.position));
            Assert.IsTrue(target.IsDestroyed, "Optional core quality cannot affect hit identity.");
            yield return null; yield return null;
            Assert.AreEqual(0, manager.LastSubmittedPartInstances, "A destroyed grouped ship cannot leave engine instances behind.");
        }

        [UnityTest]
        public IEnumerator CombinedPartMatricesBeyond511AreSubmittedWithoutDroppingTheRemainder()
        {
            const int shipCount = 133, partsPerShip = 4;
            var targets = new ShipTarget[shipCount];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = CreateTarget("matrix-capacity-" + i, origin + new Vector3(i * 2, 0, 2000));
                AddRepeatedDistantParts(targets[i], partsPerShip - 1);
            }
            manager.chunkSize = 1400;
            manager.Configure(targets, camera, player);
            Assert.AreEqual(1, manager.ChunkCount);
            Assert.AreEqual(1, manager.Lod2DrawGroups);
            EnableFixtureRendering();
            yield return null; yield return null;
            Assert.AreEqual(shipCount, manager.LastLod2Ships);
            Assert.AreEqual(2, manager.LastDrawCalls, "532 part matrices require 511 + 21, even though only 133 ships are alive.");
            Assert.AreEqual(shipCount * partsPerShip, manager.LastSubmittedPartInstances);
        }
    }
}
