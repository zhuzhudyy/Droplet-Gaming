using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit pose-only import into the copied scene. Never creates ships or saves scenes.</summary>
    public static class CinematicFleetLayout
    {
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity";
        public const string ExportPath = "ArtSource/Exports/CinematicFleet/FleetLayout_Cubic.json";
        public const string LayoutPath = "Assets/_Project/Data/CinematicFleet/FleetLayout_Cubic.json";
        public const string DataPath = "Assets/_Project/Data/CinematicFleet/";
        public const string EvidencePath = "ArtSource/Exports/CinematicFleet/unity-layout-audit.json";

        [Serializable] public sealed class Marker
        {
            public string name, id, modelId, group;
            public int layer, column, row;
            public float[] position, forward, up, scale;
        }
        [Serializable] public sealed class Layout
        {
            public int schemaVersion, targetCount, columns, rows, layers;
            public float metersPerUnityUnit, shipLength;
            public float[] spacing, playerSpawn, playerForward, formationCenter, fixedInitialFacingTarget;
            public Marker[] markers;
        }
        [Serializable] public sealed class Audit
        {
            public string scene, unity, layout;
            public int targets, uniqueIds, colliderCount, unchangedIds, created, updated;
            public int columns, rows, layers, checkedPairs, intersectingHullAabbs;
            public bool unitRootScales, rendererMembershipMatches, simulationMembershipMatches, bowAndExhaustCorrect;
            public float minimumForwardDot, minimumHullGap, spawnHullGap, spanAspectRatio;
            public Vector3 centerMin, centerMax, centerSpan, spawn, firstHullSize;
        }

        public static Vector3 V(float[] value)
        {
            Require(value != null && value.Length == 3, "Expected a 3-component layout vector.");
            var result = new Vector3(value[0], value[1], value[2]);
            Require(SolarLayoutMath.IsFinite(result), "Nonfinite layout vector.");
            return result;
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CinematicFleet: " + message); }

        public static Layout ReadLayout(string path = ExportPath)
        {
            Require(File.Exists(path), "Generate Blender layout first: " + path);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
            ValidateLayout(layout);
            return layout;
        }
        public static void ValidateLayout(Layout layout)
        {
            Require(layout != null && layout.schemaVersion == 1 && layout.targetCount == 2000 &&
                layout.columns == 20 && layout.rows == 25 && layout.layers == 4 && layout.markers?.Length == 2000,
                "Expected schema 1, 20 x 25 x 4, exactly 2000 authored markers.");
            Require(Mathf.Abs(layout.metersPerUnityUnit - 100) < .001f && Mathf.Abs(layout.shipLength - 57.564f) < .002f,
                "Existing world scale and hull size must remain unchanged.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<Vector3Int>();
            Vector3 spawn = V(layout.playerSpawn);
            Require((spawn - V(layout.fixedInitialFacingTarget)).sqrMagnitude < .0001f, "Facing target differs from fixed combat spawn.");
            Require((V(layout.playerForward) - Vector3.forward).sqrMagnitude < .0001f, "Shared narrative/combat entry uses +Z player forward.");
            V(layout.formationCenter);
            Vector3 spacing = V(layout.spacing);
            Require(spacing.x > layout.shipLength && spacing.y > layout.shipLength && spacing.z > layout.shipLength,
                "Spacing must preserve wide full-hull channels.");
            foreach (var marker in layout.markers)
            {
                Require(marker != null && !string.IsNullOrEmpty(marker.id) && marker.name == "SPAWN_Small_" + marker.id &&
                    marker.modelId == "FusionFrigate" && ids.Add(marker.name), "Invalid or duplicate stable ship ID.");
                Require(marker.column >= 0 && marker.column < 20 && marker.row >= 0 && marker.row < 25 && marker.layer >= 0 && marker.layer < 4,
                    "Marker index is outside 20 x 25 x 4.");
                Require(slots.Add(new Vector3Int(marker.column, marker.row, marker.layer)), "Duplicate grid slot.");
                Vector3 position = V(marker.position), forward = V(marker.forward), up = V(marker.up);
                Require((V(marker.scale) - Vector3.one).sqrMagnitude < 1e-8f, "Marker scale must stay one.");
                Require(Mathf.Abs(forward.magnitude - 1) < .0001f && Mathf.Abs(up.magnitude - 1) < .0001f &&
                    Mathf.Abs(Vector3.Dot(forward, up)) < .0001f, "Marker basis is not orthonormal.");
                Require(Vector3.Dot(forward, (spawn - position).normalized) > .999999f, "A ship bow does not face the combat-start droplet.");
            }
        }

        [MenuItem("DropletPrototype/Cinematic Audio/Apply Blender Cubic Fleet (current new scene)")]
        public static void ApplyCurrentScene()
        {
            ApplyToScene(SceneManager.GetActiveScene());
            Debug.Log("CINEMATIC_FLEET_APPLIED: scene is dirty and Undo-aware; save explicitly after reviewing.");
        }

        /// <param name="acceptUnsavedOwnedScene">Only the root integration builder should opt in after opening its safe copy.</param>
        public static Audit ApplyToScene(Scene scene, bool acceptUnsavedOwnedScene = false)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Stop Play and compilation before applying layout.");
            Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Close prefab editing before applying layout.");
            Require(scene.IsValid() && scene.isLoaded && scene.path == ScenePath, "Only the new CinematicAudio_Cubic scene may be authored.");
            Require(acceptUnsavedOwnedScene || !scene.isDirty, "Unsaved scene protected; save/review it first.");
            Layout layout = ReadLayout();
            var root = scene.GetRootGameObjects().Single(g => g.GetComponent<FleetSceneRoot>() != null);
            var mission = root.GetComponentInChildren<MissionController>(true);
            var generated = root.transform.Find("GeneratedFleet");
            Require(mission != null && generated != null && mission.combat != null, "Saved mission, combat simulation and owned fleet required.");
            var targets = generated.GetComponentsInChildren<ShipTarget>(true);
            var sourceIds = new HashSet<string>(layout.markers.Select(m => m.name), StringComparer.Ordinal);
            Require(targets.Length == 2000 && targets.Select(t => t.targetId).Distinct().Count() == 2000 &&
                sourceIds.SetEquals(targets.Select(t => t.targetId)), "Saved scene IDs differ: importer never creates, deletes or renames ships.");
            Require(mission.targets.Length == 2000 && new HashSet<ShipTarget>(targets).SetEquals(mission.targets), "Mission membership must match saved owned fleet.");
            Require((root.transform.lossyScale - Vector3.one).sqrMagnitude < 1e-8f &&
                targets.All(t => (t.transform.lossyScale - Vector3.one).sqrMagnitude < 1e-8f), "Root/ship scaling is protected.");
            Require(targets.All(t => t.visualRoot != null && t.hitVolumes?.Length == 9 && t.hitVolumes.All(c => c != null)),
                "Existing visual, identity and all nine query colliders must be present.");
            var renderer = root.GetComponentInChildren<FleetRenderManager>(true);
            Require(renderer != null && mission.settings != null && mission.settings.worldScale != null, "Existing rendering and world scale required.");
            Require(Mathf.Abs(mission.settings.MetersPerUnit - layout.metersPerUnityUnit) < .001f, "Runtime and Blender world scales differ.");

            Directory.CreateDirectory(DataPath);
            File.Copy(ExportPath, LayoutPath, true);
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceSynchronousImport);
            var flight = CloneOnce(mission.settings, DataPath + "CubicFlight.asset");
            var scale = CloneOnce(mission.settings.worldScale, DataPath + "CubicScale.asset");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Blender 2000-ship cubic formation");
            try
            {
                var record = new List<Object> { mission, mission.motor, mission.motor.hitDetector, mission.score, mission.chaseCamera,
                    mission.combat, renderer, flight, scale };
                record.AddRange(targets.Select(t => (Object)t.transform));
                Undo.RecordObjects(record.ToArray(), "Apply authoritative fleet poses and references");
                var lookup = targets.ToDictionary(t => t.targetId, StringComparer.Ordinal);
                var depthGroups = new Transform[4];
                for (int layer = 0; layer < depthGroups.Length; layer++)
                {
                    string groupName = "Cubic_Layer_" + (layer + 1).ToString("00");
                    depthGroups[layer] = generated.Find(groupName);
                    if (depthGroups[layer] == null)
                    {
                        var group = new GameObject(groupName);
                        group.transform.SetParent(generated, false);
                        Undo.RegisterCreatedObjectUndo(group, "Create owned cubic depth group");
                        depthGroups[layer] = group.transform;
                    }
                    Require((depthGroups[layer].lossyScale - Vector3.one).sqrMagnitude < 1e-8f,
                        "Cubic layer roots must stay unit scale.");
                }
                foreach (var marker in layout.markers)
                {
                    var ship = lookup[marker.name];
                    if (ship.transform.parent != depthGroups[marker.layer])
                        Undo.SetTransformParent(ship.transform, depthGroups[marker.layer], "Group authored depth plane");
                    ship.transform.SetPositionAndRotation(V(marker.position), Quaternion.LookRotation(V(marker.forward), V(marker.up)));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
                }
                scale.formationSpacingMeters = V(layout.spacing) * layout.metersPerUnityUnit;
                flight.worldScale = scale;
                mission.settings = flight;
                mission.spawnPosition = V(layout.playerSpawn);
                mission.arenaCenter = V(layout.formationCenter);
                mission.motor.settings = flight;
                mission.motor.hitDetector.settings = flight;
                mission.score.settings = flight;
                mission.chaseCamera.settings = flight;
                mission.combat.scale = scale;
                mission.combat.evacuationCenter = mission.arenaCenter;
                // Do not Configure the simulation while authoring: Awake captures these saved poses on Play.
                // Thus renderer, queries, damage and escape all consume the same existing ShipTarget roots.
                renderer.Configure(mission.targets, mission.chaseCamera.GetComponent<Camera>(), mission.motor.transform);
                ConfigureOverview(root, mission);
                foreach (var item in record)
                {
                    EditorUtility.SetDirty(item);
                    if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                var audit = InspectScene(scene);
                Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
                File.WriteAllText(EvidencePath, JsonUtility.ToJson(audit, true));
                Undo.CollapseUndoOperations(undo);
                return audit;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static T CloneOnce<T>(T source, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }
        static void ConfigureOverview(GameObject root, MissionController mission)
        {
            var overview = root.GetComponentInChildren<FleetOverviewCamera>(true);
            if (overview?.overview != null)
            {
                Undo.RecordObject(overview.overview, "Frame cubic formation overview");
                Vector3 offset = new Vector3(-16000, 14000, -22000);
                overview.overview.SetPositionAndRotation(mission.arenaCenter + offset, Quaternion.LookRotation(-offset, Vector3.up));
                EditorUtility.SetDirty(overview.overview);
            }
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(mission.arenaCenter, Quaternion.Euler(20, 30, 0), 22000);
        }

        public static Audit InspectScene(Scene scene)
        {
            Require(scene.IsValid() && scene.isLoaded && scene.path == ScenePath, "Inspect the new copied scene only.");
            var layout = ReadLayout();
            var mission = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MissionController>(true)).Single();
            var targets = mission.targets;
            var allSavedTargets = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ShipTarget>(true)).ToArray();
            var poses = layout.markers.ToDictionary(m => m.name, StringComparer.Ordinal);
            Require(targets.Length == 2000 && targets.Select(t => t.targetId).Distinct().Count() == 2000, "Exactly 2000 unique targets required.");
            Require(allSavedTargets.Length == 2000 && new HashSet<ShipTarget>(targets).SetEquals(allSavedTargets),
                "The saved scene contains unexpected extra/missing target objects.");
            var audit = new Audit { scene = scene.path, unity = Application.unityVersion, layout = LayoutPath,
                targets = targets.Length, uniqueIds = targets.Select(t => t.targetId).Distinct().Count(), created = 0, updated = 2000,
                columns = 20, rows = 25, layers = 4, unitRootScales = true, bowAndExhaustCorrect = true,
                minimumForwardDot = 1, minimumHullGap = float.MaxValue, spawnHullGap = float.MaxValue, spawn = mission.spawnPosition };
            var centers = new Bounds(targets[0].transform.position, Vector3.zero);
            var hulls = new Bounds[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                var ship = targets[i];
                Require(poses.TryGetValue(ship.targetId, out var pose), "An identity changed.");
                Require(Vector3.Distance(ship.transform.position, V(pose.position)) < .01f &&
                    Vector3.Dot(ship.transform.up, V(pose.up)) > .99999f, "Saved pose differs from Blender source: " + ship.targetId);
                audit.unchangedIds++;
                centers.Encapsulate(ship.transform.position);
                audit.minimumForwardDot = Mathf.Min(audit.minimumForwardDot,
                    Vector3.Dot(ship.transform.forward, (mission.spawnPosition - ship.transform.position).normalized));
                audit.unitRootScales &= (ship.transform.lossyScale - Vector3.one).sqrMagnitude < .000001f;
                audit.colliderCount += ship.hitVolumes.Length;
                var lod = ship.visualRoot.GetComponentInChildren<LODGroup>(true);
                Require(lod != null, "Preserved hull LOD required.");
                var renderers = lod.GetLODs()[0].renderers.Where(r => r != null && r.name.StartsWith("FusionFrigate_LOD0", StringComparison.Ordinal)).ToArray();
                hulls[i] = FusionFleetPipeline.BoundsOf(renderers);
                if (i == 0) audit.firstHullSize = hulls[i].size;
                audit.spawnHullGap = Mathf.Min(audit.spawnHullGap, Vector3.Distance(hulls[i].ClosestPoint(mission.spawnPosition), mission.spawnPosition));
                var bow = ship.hitVolumes.OfType<BoxCollider>().Single(c => c.name == "Bow");
                var engine = ship.hitVolumes.OfType<CapsuleCollider>().Single(c => c.name == "Main engine");
                Vector3 bowCenter = bow.transform.TransformPoint(bow.center), engineCenter = engine.transform.TransformPoint(engine.center);
                audit.bowAndExhaustCorrect &= Vector3.Dot((bowCenter-engineCenter).normalized,
                    (mission.spawnPosition-ship.transform.position).normalized) > .999f;
            }
            for (int i = 0; i < hulls.Length; i++) for (int j = i+1; j < hulls.Length; j++)
            {
                audit.checkedPairs++;
                if (hulls[i].Intersects(hulls[j])) audit.intersectingHullAabbs++;
                Vector3 gap = Vector3.Max(Vector3.zero, Vector3.Max(hulls[i].min-hulls[j].max, hulls[j].min-hulls[i].max));
                audit.minimumHullGap = Mathf.Min(audit.minimumHullGap, gap.magnitude);
            }
            audit.centerMin = centers.min; audit.centerMax = centers.max; audit.centerSpan = centers.size;
            audit.spanAspectRatio = Mathf.Max(centers.size.x, centers.size.y, centers.size.z) / Mathf.Min(centers.size.x, centers.size.y, centers.size.z);
            audit.rendererMembershipMatches = new HashSet<ShipTarget>(mission.combat.fleetRenderer.targets).SetEquals(targets);
            audit.simulationMembershipMatches = new HashSet<ShipTarget>(mission.combat.targets).SetEquals(targets);
            Require(audit.minimumForwardDot > .99999f && audit.bowAndExhaustCorrect && audit.unitRootScales,
                "Bow orientation/engine direction/unit-root verification failed.");
            Require(audit.intersectingHullAabbs == 0 && audit.minimumHullGap > 100 && audit.spawnHullGap > 100,
                "Rotated real mesh hulls intersect or do not leave wide passages/spawn clearance.");
            Require(audit.spanAspectRatio < 1.05f && audit.rendererMembershipMatches && audit.simulationMembershipMatches,
                "Near-cubic volume or authoritative membership check failed.");
            return audit;
        }
    }
}
