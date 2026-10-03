using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    /// <summary>Real native LOD visibility, including the inactive distant prefab lifecycle.</summary>
    public sealed class CinematicLodPinTests
    {
        readonly List<Object> owned = new List<Object>();
        readonly List<string> lodWarnings = new List<string>();
        FleetRenderManager manager;
        Camera view;
        RenderTexture texture;
        GameObject Node(string name) { var node = new GameObject(name); owned.Add(node); return node; }
        void OnLog(string message, string trace, LogType type)
        { if (message.Contains("LOD on a disabled LODGroup")) lodWarnings.Add(message); }

        [SetUp] public void Setup()
        { Time.timeScale = 1; Application.logMessageReceived += OnLog; }
        [TearDown] public void Cleanup()
        {
            Application.logMessageReceived -= OnLog;
            if (manager != null) manager.enabled = false;
            if (view != null) view.targetTexture = null;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); lodWarnings.Clear(); Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator DistantPinDestructionAndRestartsRestoreNativeAutomaticLod()
        {
            Assert.IsTrue(SystemInfo.supportsInstancing);
            Vector3 origin = new Vector3(180000, 4500, 0);
            var player = Node("Stable hearing position").transform; player.position = origin;
            view = Node("LOD proof camera").AddComponent<Camera>(); view.transform.position = origin;
            view.nearClipPlane = .1f; view.farClipPlane = 10000; view.cullingMask = 1; view.fieldOfView = 60;
            texture = new RenderTexture(128, 128, 24); owned.Add(texture); texture.Create(); view.targetTexture = texture;
            var ship = Node("Distant cinematic subject").AddComponent<ShipTarget>(); ship.targetId = "LOD-LIFECYCLE";
            ship.transform.position = origin + Vector3.forward * 4000;
            var collider = ship.gameObject.AddComponent<BoxCollider>(); collider.size = Vector3.one * 10;
            ship.hitVolumes = new Collider[] { collider };
            ship.visualRoot = new GameObject("VisualRoot"); ship.visualRoot.transform.SetParent(ship.transform, false);
            var group = ship.visualRoot.AddComponent<LODGroup>();
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(primitive);
            var shader = Shader.Find("Universal Render Pipeline/Lit"); Assert.IsNotNull(shader);
            var material = new Material(shader) { enableInstancing = true }; owned.Add(material);
            var levels = new LOD[3]; var renderers = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var part = new GameObject("LOD" + i); part.transform.SetParent(ship.visualRoot.transform, false); part.transform.localScale = Vector3.one * 10;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderers[i] = part.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = material;
                levels[i] = new LOD(i == 0 ? .2f : i == 1 ? .05f : .001f, new[] { renderers[i] });
            }
            group.SetLODs(levels); group.RecalculateBounds();
            manager = Node("Cinematic LOD streaming manager").AddComponent<FleetRenderManager>();
            manager.nearDistance = 100; manager.normalStreamingReference = player;
            manager.Configure(new[] { ship }, view, player);
            Assert.IsTrue(manager.InstancingAvailable); Assert.IsTrue(manager.IsInstanced(ship));
            Assert.IsFalse(ship.visualRoot.activeInHierarchy);

            // Baseline without any shot: the unchanged native distance policy selects LOD2.
            manager.enabled = false;
            yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
            Assert.IsFalse(renderers[0].isVisible); Assert.IsTrue(renderers[2].isVisible);
            for (int restart = 0; restart < 2; restart++)
            {
                manager.enabled = true; manager.RefreshNow();
                Assert.IsFalse(ship.visualRoot.activeInHierarchy);
                manager.PinCinematicSubject(ship, player);
                Assert.IsTrue(group.enabled && group.gameObject.activeInHierarchy, "Pin must activate exactly the selected root before ForceLOD.");
                yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
                Assert.IsTrue(renderers[0].isVisible, "The native renderer must actually display forced LOD0, even at 4000 units.");
                Assert.IsFalse(renderers[2].isVisible);
                Assert.IsTrue(ship.TryDestroy()); manager.ClearCinematicSubject();
                Assert.IsFalse(ship.visualRoot.activeInHierarchy, "Releasing a pin must never resurrect the destroyed hull.");
                // Second cycle also covers renderer disable before the ship is restored.
                if (restart == 1) manager.enabled = false;
                ship.ResetTarget(); manager.enabled = false;
                yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
                Assert.IsFalse(renderers[0].isVisible, "Restart must release the native forced LOD, not only clear a tracking field.");
                Assert.IsTrue(renderers[2].isVisible, "Automatic LOD2 must resume after restart, even if the manager was disabled first.");
                Assert.IsEmpty(lodWarnings);
            }
        }
    }
}
