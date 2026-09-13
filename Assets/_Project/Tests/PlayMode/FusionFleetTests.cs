using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DropletPrototype.Tests.PlayMode
{
    // Reads the saved expanded scene and creates temporary physics fixtures only.
    // Never invokes authoring tools, writes assets, or changes build scene settings.
    public sealed class FusionFleetTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FleetAssault_Expanded.unity";
        const int ExpectedTargets = 120;
        const float FixedStep = .02f;
        readonly List<GameObject> fixtureObjects = new List<GameObject>();
        DropletSettings fixtureSettings;
        MissionController live;

        [TearDown]
        public void Cleanup()
        {
            if (live != null) live.Restart();
            live = null;
            for (int i = fixtureObjects.Count - 1; i >= 0; i--)
                if (fixtureObjects[i] != null) Object.DestroyImmediate(fixtureObjects[i]);
            fixtureObjects.Clear();
            if (fixtureSettings != null) Object.DestroyImmediate(fixtureSettings);
            fixtureSettings = null;
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator LoadExpandedScene()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_Expanded", LoadSceneMode.Single);
#endif
            yield return null;
            var missions = Object.FindObjectsByType<MissionController>(FindObjectsSortMode.None);
            Assert.AreEqual(1, missions.Length, "The expanded scene must contain one mission controller.");
            live = missions[0];
            Assert.AreEqual(MissionState.Ready, live.State);
            Assert.AreEqual(ExpectedTargets, live.TotalCount, "All saved targets must be registered before Begin.");
            Assert.AreEqual(ExpectedTargets, live.targets.Length);
            live.enabled = false;
            if (live.input != null) live.input.enabled = false;
            live.motor.enabled = false;
            Physics.SyncTransforms();
        }

        [TestCase(4096, false)]
        [TestCase(4097, false)]
        [TestCase(4097, true)]
        public void QueryCapacityLimitUsesCompleteFallbackAndDeduplicatesCompoundShips(int colliderCount, bool initialOverlap)
        {
            fixtureSettings = ScriptableObject.CreateInstance<DropletSettings>();
            fixtureSettings.targetLayers = 1 << 31;
            fixtureSettings.hitRadius = .25f;
            var queryRoot = new GameObject("Fusion query-capacity fixture");
            fixtureObjects.Add(queryRoot);
            var detector = queryRoot.AddComponent<DropletHitDetector>();
            detector.settings = fixtureSettings;
            int targetCount = Mathf.CeilToInt(colliderCount / 4f);
            var targets = new List<ShipTarget>(targetCount);
            int remaining = colliderCount, destructionEvents = 0;
            for (int i = 0; i < targetCount; i++)
            {
                var root = new GameObject("Capacity target " + i);
                fixtureObjects.Add(root);
                root.layer = 31;
                root.transform.position = initialOverlap ? Vector3.zero : Vector3.forward * 10;
                var target = root.AddComponent<ShipTarget>();
                target.targetId = "capacity_" + i;
                target.hitVolumes = new Collider[Mathf.Min(4, remaining)];
                for (int c = 0; c < target.hitVolumes.Length; c++)
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1, 1, .01f);
                    target.hitVolumes[c] = box;
                    remaining--;
                }
                target.Destroyed += _ => destructionEvents++;
                targets.Add(target);
            }
            Assert.AreEqual(0, remaining);
            Physics.SyncTransforms();
            Vector3 end = initialOverlap ? Vector3.zero : Vector3.forward * 20;
            Assert.AreEqual(targetCount, detector.SweepSegment(Vector3.zero, end, 624),
                "At 4097 collider hits a capped 4096 NonAlloc query alone cannot return every ship.");
            Assert.GreaterOrEqual(detector.BufferGrowthCount, 9, "Exercise the real 8-to-4096 growth path.");
            Assert.AreEqual(targetCount, destructionEvents);
            Assert.IsTrue(targets.All(t => t.IsDestroyed && t.hitVolumes.All(c => !c.enabled)));
            Assert.AreEqual(0, detector.SweepSegment(end, Vector3.zero, 624));
            Assert.AreEqual(targetCount, destructionEvents, "Compound shapes and repeated passes must not emit extra destruction events.");
            Debug.Log($"Fusion query boundary: {colliderCount} real colliders, {targetCount} identities, " +
                $"initialOverlap={initialOverlap}, growths={detector.BufferGrowthCount}, all identities destroyed once.");
        }

        [UnityTest]
        public IEnumerator SavedExpandedSceneRegistersAllTargetsAndSharesCompleteLodAssets()
        {
            yield return LoadExpandedScene();
            var actual = Object.FindObjectsByType<ShipTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(live.targets, actual, "No second scoring fleet, preview target, or missing saved target is allowed.");
            Assert.AreEqual(ExpectedTargets, live.targets.Select(t => t.targetId).Distinct().Count());
            Assert.IsTrue(live.targets.All(t => !string.IsNullOrWhiteSpace(t.targetId)));
            var firstMeshes = Meshes(live.targets.Take(1)).ToArray();
            var firstMaterials = Materials(live.targets.Take(1)).ToArray();
            Assert.IsNotEmpty(firstMeshes);
            Assert.That(firstMaterials.Length, Is.InRange(1, 4));
            foreach (var target in live.targets)
            {
                Assert.AreEqual(Vector3.one, target.transform.localScale, target.targetId + " gameplay root must remain unit scaled.");
                Assert.IsNotNull(target.visualRoot);
                Assert.IsTrue(target.visualRoot.activeInHierarchy);
                Assert.IsFalse(target.IsDestroyed);
                // Three tapered body sections, bridge, and five independent engine volumes.
                // Nine avoids replacing the narrow hull/engines with large empty-space boxes.
                Assert.That(target.hitVolumes.Length, Is.InRange(3, 9));
                foreach (var volume in target.hitVolumes)
                {
                    Assert.IsNotNull(volume);
                    Assert.IsTrue(volume.enabled && volume.gameObject.activeInHierarchy);
                    Assert.IsFalse(volume is MeshCollider, "Use independent simple hit shapes.");
                    Assert.AreSame(target, volume.GetComponentInParent<ShipTarget>());
                    Assert.IsFalse(volume.transform.IsChildOf(target.visualRoot.transform), "Visual LOD lifetime must not own hit volumes.");
                }
                var groups = target.GetComponentsInChildren<LODGroup>(true);
                Assert.AreEqual(1, groups.Length, "Avoid nested imported and authored LOD controllers.");
                var lods = groups[0].GetLODs();
                Assert.AreEqual(3, lods.Length);
                var assigned = new HashSet<Renderer>();
                for (int level = 0; level < lods.Length; level++)
                {
                    Assert.IsNotEmpty(lods[level].renderers);
                    foreach (var renderer in lods[level].renderers)
                    {
                        Assert.IsNotNull(renderer);
                        Assert.IsTrue(assigned.Add(renderer), "Each renderer must belong to exactly one LOD.");
                        Assert.IsTrue(renderer.transform.IsChildOf(target.visualRoot.transform));
                        Assert.IsTrue(renderer.gameObject.activeInHierarchy, "Imported hidden source states must not suppress LOD1/LOD2 permanently.");
                        Assert.IsTrue(renderer.sharedMaterials.All(m => m != null && m.shader != null));
                    }
                }
                CollectionAssert.AreEquivalent(target.visualRoot.GetComponentsInChildren<Renderer>(true), assigned);
                CollectionAssert.AreEquivalent(firstMeshes, Meshes(new[] { target }));
                CollectionAssert.AreEquivalent(firstMaterials, Materials(new[] { target }));
                Assert.Less(Vector3.Distance(target.transform.position, live.arenaCenter), live.settings.boundaryWarningRadius,
                    "An active squadron must not sit in the recovery/warning zone.");
            }
            Assert.GreaterOrEqual(live.settings.boundaryRadius, 5840, "Do not shrink the latest solar-layout flight range.");
            Assert.IsNotNull(Object.FindAnyObjectByType<HudPresenter>());
            var effects = Object.FindAnyObjectByType<MissionEffects>();
            Assert.IsNotNull(effects);
            Assert.IsTrue(effects.wreckPrefabs == null || effects.wreckPrefabs.Length == 0,
                "The old ships' shaped wrecks are not a FusionFrigate destruction model.");
        }

        [UnityTest]
        public IEnumerator SingleImportedShipSweepsAtEveryLodHideTheWholeModelAndRestoreOnRestart()
        {
            yield return LoadExpandedScene();
            var target = live.targets.OrderBy(t => (t.transform.position - live.spawnPosition).sqrMagnitude).First();
            var lod = target.GetComponentInChildren<LODGroup>(true);
            var renderers = target.visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int level = 0; level < 4; level++)
            {
                live.Restart();
                lod.ForceLOD(level < 3 ? level : -1);
                // The last pass also proves visual invisibility cannot unregister or disable a target.
                if (level == 3) foreach (var renderer in renderers) renderer.enabled = false;
                Assert.AreEqual(ExpectedTargets, live.TotalCount);
                Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
                live.StartMission();
                FlyThrough(target);
                Assert.IsTrue(target.IsDestroyed, "The same imported collider path must hit in every visual state.");
                Assert.AreEqual(1, live.DestroyedCount);
                int score = live.score.Score;
                Assert.Greater(score, 0);
                Assert.IsFalse(target.visualRoot.activeSelf);
                Assert.IsTrue(renderers.All(r => !r.gameObject.activeInHierarchy), "All three LODs must disappear on destruction.");
                Assert.IsTrue(target.hitVolumes.All(c => !c.enabled));
                Assert.IsFalse(target.TryDestroy());
                Assert.AreEqual(score, live.score.Score);
                live.Restart();
                foreach (var renderer in renderers) renderer.enabled = true;
                Assert.IsFalse(target.IsDestroyed);
                Assert.IsTrue(renderers.All(r => r.gameObject.activeInHierarchy));
                Assert.IsTrue(target.hitVolumes.All(c => c.enabled));
                Assert.AreEqual(0, live.DestroyedCount);
                Assert.AreEqual(0, live.score.Score);
                yield return null;
            }
            lod.ForceLOD(-1);
        }

        [UnityTest]
        public IEnumerator ImportedHullAndEachMajorEngineHaveCoverageWithoutDistantAirHits()
        {
            yield return LoadExpandedScene();
            var target=live.targets.Single(t=>t.targetId=="SPAWN_Small_FF001");
            // Segments end inside the particular engine before reaching the main hull.
            // Side approaches to each auxiliary cannot be rescued by a hit on the central drive.
            var ends=new[]{new Vector3(0,-.05f,-10.1f),new Vector3(-2.5f,.15f,-9.2f),new Vector3(2.5f,.15f,-9.2f),new Vector3(-1.62f,-2,-9.2f),new Vector3(1.62f,-2,-9.2f),new Vector3(0,0,7.5f),new Vector3(0,1.7f,-.5f)};
            for(int i=0;i<ends.Length;i++)
            {
                live.Restart();live.StartMission();
                Vector3 delta=i==0?Vector3.back*4:i<5?Vector3.right*Mathf.Sign(ends[i].x)*2.5f:Vector3.up*4;
                Physics.SyncTransforms();
                int hits=live.motor.hitDetector.SweepSegment(target.transform.TransformPoint(ends[i]+delta),target.transform.TransformPoint(ends[i]),156);
                Assert.AreEqual(1,hits,"Major engine/hull sample "+i+" lacks hit coverage.");
                Assert.IsTrue(target.IsDestroyed);Assert.AreEqual(1,live.DestroyedCount);
            }
            live.Restart();live.StartMission();Physics.SyncTransforms();
            Assert.AreEqual(0,live.motor.hitDetector.SweepSegment(target.transform.TransformPoint(new Vector3(5.2f,0,-15)),target.transform.TransformPoint(new Vector3(5.2f,0,15)),156),"A flight line well outside the ship must not score.");
            Assert.AreEqual(0,live.motor.hitDetector.SweepSegment(target.transform.TransformPoint(new Vector3(2.9f,0,7.5f)),target.transform.TransformPoint(new Vector3(2.9f,0,12)),156),"The narrow bow must not use the full-width hull box.");
            Assert.IsFalse(target.IsDestroyed);Assert.AreEqual(0,live.DestroyedCount);
        }

        [UnityTest]
        public IEnumerator AllExpandedTargetsWinThroughRealSweptMotorEventsAndThreeRestartsReuseResources()
        {
            yield return LoadExpandedScene();
            var effects = Object.FindAnyObjectByType<MissionEffects>();
            Assert.IsNotNull(effects);
            effects.InitializePool();
            var targetReferences = live.targets.ToArray();
            var meshes = Meshes(live.targets).ToArray();
            var materials = Materials(live.targets).ToArray();
            int poolCount = effects.PoolInstanceCount;
            int hierarchyCount = live.targets.Sum(t => t.GetComponentsInChildren<Transform>(true).Length);
            var events = live.targets.ToDictionary(t => t, _ => 0);
            foreach (var target in live.targets) target.Destroyed += t => events[t]++;
            for (int run = 0; run < 3; run++)
            {
                // Rebinding before each run checks subscription deduplication as well as Restart.
                foreach (var target in live.targets) target.GetComponent<DestructionPresenter>().Bind();
                live.Initialize();
                live.StartMission();
                foreach (var target in live.targets)
                {
                    if (target.IsDestroyed) continue;
                    FlyThrough(target);
                    Assert.IsTrue(target.IsDestroyed, target.targetId + " must be reached through its real simple hit geometry.");
                    Assert.LessOrEqual(effects.ActiveEffectCount, effects.Capacity);
                    Assert.LessOrEqual(effects.ActiveAudioCount, effects.AudioCapacity);
                }
                Assert.AreEqual(MissionState.Results, live.State);
                Assert.IsTrue(live.Won);
                Assert.AreEqual(ExpectedTargets, live.DestroyedCount);
                Assert.AreEqual(1, live.ResultTransitions);
                Assert.Greater(live.score.Score, 0);
                Assert.IsTrue(live.targets.All(t => events[t] == run + 1));
                Assert.IsTrue(live.targets.All(t => !t.visualRoot.activeSelf && t.hitVolumes.All(c => !c.enabled)));
                int resultScore = live.score.Score;
                live.Step(1, default);
                Assert.AreEqual(resultScore, live.score.Score);
                Assert.AreEqual(1, live.ResultTransitions);
                live.Restart();
                yield return null;
                Assert.AreEqual(MissionState.Ready, live.State);
                Assert.AreEqual(ExpectedTargets, live.TotalCount);
                Assert.AreEqual(0, live.DestroyedCount);
                Assert.AreEqual(0, live.score.Score);
                Assert.AreEqual(0, live.score.Combo);
                Assert.AreEqual(live.settings.missionSeconds, live.Remaining);
                Assert.AreEqual(1, Time.timeScale);
                Assert.AreEqual(live.spawnPosition, live.motor.transform.position);
                Assert.AreEqual(0, effects.ActiveEffectCount);
                Assert.AreEqual(0, effects.ActiveAudioCount);
                Assert.AreEqual(poolCount, effects.PoolInstanceCount);
                Assert.AreEqual(hierarchyCount, live.targets.Sum(t => t.GetComponentsInChildren<Transform>(true).Length));
                CollectionAssert.AreEqual(targetReferences, live.targets);
                CollectionAssert.AreEquivalent(meshes, Meshes(live.targets));
                CollectionAssert.AreEquivalent(materials, Materials(live.targets));
                Assert.AreEqual(ExpectedTargets, Object.FindObjectsByType<ShipTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                Assert.IsTrue(live.targets.All(t => !t.IsDestroyed && t.visualRoot.activeSelf && t.hitVolumes.All(c => c.enabled)));
            }
            Debug.Log($"Fusion expanded scene: {ExpectedTargets} saved targets, 3 genuine swept-motor victories/restarts, " +
                $"shared fleet meshes={meshes.Length}, materials={materials.Length}, fixed pool hierarchy={poolCount}. " +
                "Safe approach teleports are test setup, not a timed continuous-player-route claim.");
        }

        [UnityTest]
        public IEnumerator ExpandedMissionPauseTimeoutAndRestartUseTheSavedDuration()
        {
            yield return LoadExpandedScene();
            var effects = Object.FindAnyObjectByType<MissionEffects>();
            live.StartMission();
            live.TogglePause();
            float remaining = live.Remaining, effectTime = effects.ElapsedSimulationTime;
            Vector3 position = live.motor.transform.position;
            live.Step(10, new FlightCommand { boost = true, throttle = 1 });
            yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(remaining, live.Remaining);
            Assert.AreEqual(position, live.motor.transform.position);
            Assert.AreEqual(effectTime, effects.ElapsedSimulationTime);
            Assert.AreEqual(0, live.score.Score);
            Assert.AreEqual(0, live.DestroyedCount);
            Assert.AreEqual(0, Time.timeScale);
            live.TogglePause();
            Assert.AreEqual(1, Time.timeScale);
            // Advance the unchanged saved mission duration through the public simulation step.
            // No Remaining/DestroyedCount setter, private-state write, or settings shortening is used.
            int limit = Mathf.CeilToInt(live.settings.missionSeconds / .25f) + 2;
            for (int i = 0; i < limit && live.State == MissionState.Playing; i++)
                live.Step(.25f, new FlightCommand { brake = true });
            Assert.AreEqual(MissionState.Results, live.State);
            Assert.IsFalse(live.Won);
            Assert.AreEqual(0, live.DestroyedCount);
            Assert.AreEqual(0, live.Remaining, .0001f);
            Assert.AreEqual(live.settings.missionSeconds, live.Elapsed, .01f);
            Assert.AreEqual(1, live.ResultTransitions);
            live.Step(1, default);
            Assert.AreEqual(1, live.ResultTransitions);
            live.Restart();
            Assert.AreEqual(MissionState.Ready, live.State);
            Assert.AreEqual(live.settings.missionSeconds, live.Remaining);
            Assert.AreEqual(ExpectedTargets, live.TotalCount);
            live.StartMission();
            live.TogglePause();
            live.Restart();
            Assert.AreEqual(MissionState.Ready, live.State);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(0, live.score.Score);
            Assert.IsTrue(live.targets.All(t => !t.IsDestroyed && t.hitVolumes.All(c => c.enabled)));
        }

        void FlyThrough(ShipTarget target)
        {
            Assert.AreEqual(MissionState.Playing, live.State);
            var main = target.hitVolumes.OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
            Vector3 forward = target.transform.forward;
            Vector3 absForward = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
            Vector3 center = main.bounds.center;
            float extent = target.hitVolumes.Max(c => Mathf.Abs(Vector3.Dot(c.bounds.center - center, forward)) + Vector3.Dot(c.bounds.extents, absForward));
            float margin = live.settings.hitRadius + 2;
            Vector3 start = center - forward * (extent + margin);
            float length = 2 * (extent + margin);
            live.motor.ResetPose(start, target.transform.rotation);
            Physics.SyncTransforms();
            int steps = Mathf.CeilToInt(length / (live.settings.initialSpeed * FixedStep)) + 2;
            for (int i = 0; i < steps && live.State == MissionState.Playing; i++) live.Step(FixedStep, default);
            Assert.AreEqual(0, live.RecoveryCount, "The approach and complete ship crossing must stay inside the playable range.");
        }

        static IEnumerable<Mesh> Meshes(IEnumerable<ShipTarget> targets)
            => targets.SelectMany(t => t.visualRoot.GetComponentsInChildren<MeshFilter>(true))
                .Select(f => f.sharedMesh).Where(m => m != null).Distinct();

        static IEnumerable<Material> Materials(IEnumerable<ShipTarget> targets)
            => targets.SelectMany(t => t.visualRoot.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct();
    }
}
