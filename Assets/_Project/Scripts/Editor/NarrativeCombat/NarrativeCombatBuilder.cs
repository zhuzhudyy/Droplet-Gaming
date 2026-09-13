using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class NarrativeCombatBuilder
    {
        public const string Baseline = "Assets/_Project/Scenes/FleetAssault_2000_Sun.unity";
        public const string SmallScene = "Assets/_Project/Scenes/FleetAssault_NarrativeCombat_Small.unity";
        public const string FullScene = "Assets/_Project/Scenes/FleetAssault_NarrativeCombat.unity";
        public const string Art = "Assets/_Project/Art/NarrativeCombat/";
        public const string Evidence = "docs/verification/NarrativeCombat/";
        [MenuItem("DropletPrototype/Narrative Combat/1 Build Small Integration Scene")]
        public static void BuildSmall() => Build(true);
        [MenuItem("DropletPrototype/Narrative Combat/2 Build Full 2000 Scene")]
        public static void BuildFull() => Build(false);
        [MenuItem("DropletPrototype/Narrative Combat/0 Resume Owned Small Authoring")]
        public static void ResumeSmall()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != SmallScene || scene.GetRootGameObjects().All(g => g.GetComponentInChildren<FleetCombatSimulation>(true) == null))
                throw new InvalidOperationException("Only the in-progress generated small scene can be resumed.");
            EditorSceneManager.SaveScene(scene); BuildSmall();
        }
        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Stop Play before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene protected: " + SceneManager.GetSceneAt(i).path);
        }
        static T Asset<T>(string name, Func<T> create) where T : Object
        {
            string path = Art + name;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = create(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        public static void Build(bool small)
        {
            Guard(); Directory.CreateDirectory(Art); Directory.CreateDirectory(Evidence);
            string path = small ? SmallScene : FullScene;
            if (!File.Exists(path) && !AssetDatabase.CopyAsset(Baseline, path)) throw new IOException("Could not copy stable scene.");
            var scene = EditorSceneManager.OpenScene(path);
            var root = scene.GetRootGameObjects().Single(g => g.GetComponent<FleetSceneRoot>());
            var mission = root.GetComponentInChildren<MissionController>();
            var all = mission.targets.OrderBy(t => t.targetId, StringComparer.Ordinal).ToArray();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Author Narrative Combat version");
            var owned = root.transform.Find("GeneratedNarrativeCombat");
            if (owned == null) { var go = new GameObject("GeneratedNarrativeCombat"); Undo.RegisterCreatedObjectUndo(go, "Create combat owner"); go.transform.SetParent(root.transform, false); owned = go.transform; }
            var scale = Asset((small ? "Small" : "Fleet2000") + "Scale.asset", ScriptableObject.CreateInstance<CombatScaleSettings>);
            Undo.RecordObject(scale, "Set physical scale");
            scale.metersPerUnityUnit = 100; scale.escapeRadiusMeters = small ? 500000 : 2600000; scale.arenaBoundaryMeters = small ? 800000 : 3200000;
            scale.formationSpacingMeters = new Vector3(75000, 50000, 100000);
            EditorUtility.SetDirty(scale);
            var targets = small ? all.Take(12).ToArray() : all;
            if (!small && targets.Length != 2000) throw new InvalidOperationException("Formal fleet must retain exactly 2000 stable identities.");
            if (small) foreach (var removed in all.Skip(12)) Undo.DestroyObjectImmediate(removed.gameObject);
            var bounds = new Bounds();
            for (int i = 0; i < targets.Length; i++)
            {
                var ship = targets[i]; int layer, column, row;
                if (small) { layer = i / 6; column = (i % 6) / 2; row = i % 2; }
                else { layer = i / 1000; column = (i % 1000) / 20; row = i % 20; }
                var spacing = scale.MetersToUnits(scale.formationSpacingMeters);
                Vector3 position = new Vector3((column - (small ? 1 : 24)) * spacing.x + layer * 60, 8 + layer * spacing.y, 1000 + row * spacing.z + layer * 125);
                Undo.RecordObject(ship.transform, "Move retained ship to interstellar formation");
                ship.transform.SetPositionAndRotation(position, Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
                if (i == 0) bounds = new Bounds(position, Vector3.zero); else bounds.Encapsulate(position);
                if (ship.transform.Find("LaserMuzzle") == null)
                {
                    var turret = ship.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "FusionFrigate_LOD0_Turret_03");
                    if (turret == null) throw new InvalidOperationException("Authored turret 03 missing on " + ship.targetId);
                    var local = turret.sharedMesh.bounds;
                    // Top-front of the existing turret mesh, just outside its actual surface.
                    Vector3 muzzleWorld = turret.transform.TransformPoint(new Vector3(local.center.x, local.max.y + .015f, local.max.z + .02f));
                    var muzzle = new GameObject("LaserMuzzle"); Undo.RegisterCreatedObjectUndo(muzzle, "Add muzzle socket on existing turret");
                    muzzle.transform.SetParent(ship.transform, false);
                    muzzle.transform.position = muzzleWorld; muzzle.transform.rotation = ship.transform.rotation;
                }
            }
            var settings = Asset((small ? "Small" : "Fleet2000") + "Flight.asset", () => Object.Instantiate(mission.settings));
            settings.worldScale = scale; settings.ApplyWorldScale();
            settings.acceleration = scale.MetersToUnits(scale.accelerationMetersPerSecondSquared);
            settings.speedAdjustment = scale.MetersToUnits(15000); settings.brakeStrength = scale.MetersToUnits(150000); settings.strafeSpeed = scale.MetersToUnits(6000);
            settings.boundaryRadius = scale.MetersToUnits(scale.arenaBoundaryMeters); settings.boundaryWarningRadius = settings.boundaryRadius * .91f;
            settings.missionSeconds = 5400; settings.comboWindow = 8; EditorUtility.SetDirty(settings);
            mission.settings = settings; mission.targets = targets; mission.arenaCenter = bounds.center; mission.spawnPosition = new Vector3(0, 8, 0);
            mission.motor.settings = settings; mission.motor.hitDetector.settings = settings; mission.score.settings = settings; mission.chaseCamera.settings = settings;
            mission.motor.ResetPose(mission.spawnPosition, Quaternion.identity);
            var view = mission.chaseCamera.GetComponent<Camera>(); view.farClipPlane = 250000;
            // The preserved HDR cubemap is reflection-only. At the larger far range
            // Skybox's depth epsilon can paint it over the actual distant star mesh.
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.black;
            view.nearClipPlane = .5f;
            var manager = root.GetComponentInChildren<FleetRenderManager>();
            // Larger render batches reduce CPU submission overhead; per-ship frustum
            // tests, authoritative movement, collision cells and LOD distances stay unchanged.
            manager.nearDistance = 500; manager.middleDistance = 4500; manager.chunkSize = 20000;
            manager.Configure(targets, view, mission.motor.transform);
            var combat = Get<FleetCombatSimulation>(owned.gameObject); combat.mission = mission; combat.scale = scale; combat.targets = targets; combat.threat = mission.motor.transform; combat.fleetRenderer = manager; combat.evacuationCenter = bounds.center;
            mission.combat = combat; mission.motor.hitDetector.combat = combat;
            var mesh = mission.motor.visualRoot.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh != null && f.sharedMesh.name.Contains("Droplet"));
            var geometry = Asset("DropletReflectionGeometry.asset", ScriptableObject.CreateInstance<DropletReflectionGeometry>);
            geometry.BakeFromMesh(mesh.sharedMesh); EditorUtility.SetDirty(geometry);
            var surface = Get<DropletReflectiveSurface>(mission.motor.gameObject); surface.Configure(mesh.transform, mission.motor.transform, geometry);
            var weapon = Asset("LaserWeapon.asset", ScriptableObject.CreateInstance<LaserWeaponSettings>);
            var material = Asset("LaserEmission.mat", () => new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            material.SetColor("_BaseColor", Color.white * 3); material.enableInstancing = true; EditorUtility.SetDirty(material);
            var beams = Get<LaserBeamPool>(owned.gameObject); beams.material = material; beams.scale = scale; beams.settings = weapon;
            var lasers = Get<FleetLaserDirector>(owned.gameObject); lasers.simulation = combat; lasers.settings = weapon; lasers.dropletSurface = surface; lasers.beamPool = beams; mission.lasers = lasers;
            ConfigurePresentation(root, owned, mission, combat, targets, scale, view);
            NarrativeRadioAssets.Configure(owned, mission, combat);
            mission.chaseCamera.ResetCamera();
            var overview = root.GetComponentInChildren<FleetOverviewCamera>();
            if (overview != null && overview.overview != null)
                overview.overview.SetPositionAndRotation(bounds.center + new Vector3(-bounds.extents.x * 1.4f - 1200, bounds.extents.y + 1500, -bounds.extents.z * .45f), Quaternion.LookRotation(new Vector3(bounds.extents.x * 1.4f + 1200, -bounds.extents.y - 1500, bounds.extents.z * .45f)));
            var hud = root.GetComponentInChildren<HudPresenter>(); hud.locationLabel = small ? "INTEGRATION RANGE / 12 SHIPS" : "NARRATIVE COMBAT / 2000 SHIPS";
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            { if (component == null) continue; EditorUtility.SetDirty(component); if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            File.WriteAllText(Evidence + (small ? "small" : "full") + "-authoring.json", JsonUtility.ToJson(new AuthoringAudit { scene = path, count = targets.Length, distinctIds = targets.Select(t => t.targetId).Distinct().Count(), scale = scale.metersPerUnityUnit, shipLengthUnits = 57.564f, dropletLengthUnits = geometry.localBounds.size.z, spacingMeters = scale.formationSpacingMeters, min = bounds.min, max = bounds.max, escapeRadiusUnits = scale.MetersToUnits(scale.escapeRadiusMeters), boundaryUnits = settings.boundaryRadius }, true));
            Debug.Log("NARRATIVE_COMBAT_SAVED " + path + " targets=" + targets.Length);
        }
        static T Get<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var value) ? value : Undo.AddComponent<T>(go);
        static void ConfigurePresentation(GameObject root, Transform owned, MissionController mission, FleetCombatSimulation combat, ShipTarget[] targets, CombatScaleSettings scale, Camera view)
        {
            var response = root.GetComponentInChildren<DropletReflectionResponse>();
            response.reactorSources = targets.Select(t => t.visualRoot.transform.Find("FusionDriveEffects/LOD0/MainExhaust/MainExhaust_Core")).ToArray();
            var rig = root.GetComponentInChildren<SolarLightingRig>(); rig.drives = targets.Select(t => t.GetComponentInChildren<FusionDriveVisuals>(true)).ToArray();
            var backdrop = Object.FindAnyObjectByType<SolarSystemBackdrop>();
            if (backdrop != null)
            {
                var data = JsonUtility.FromJson<SolarLayoutData>(backdrop.layoutJson.text);
                data.metersPerUnit = scale.metersPerUnityUnit; data.proxyNear = 140000; data.proxySpan = 40000; data.skyRadius = 220000; data.farClip = 250000;
                string path = Art + "SolarLocalProjection.json";
                File.WriteAllText(path, JsonUtility.ToJson(data, true)); AssetDatabase.ImportAsset(path);
                backdrop.layoutJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path); backdrop.combatScale = scale;
                backdrop.skyProxy.localScale = Vector3.one * data.skyRadius; backdrop.ReloadLayout(); backdrop.ApplyMapping(view);
            }
            var sun = root.GetComponentInChildren<SunDisplayRig>();
            if (sun != null && sun.lensFlare != null)
            { sun.lensFlare.maxAttenuationDistance = 250000; sun.lensFlare.maxAttenuationScale = 250000; }
            var pool = root.GetComponentInChildren<ReactorExplosionPool>(); pool.instabilityDuration = .08f;
            var pending = Get<PendingDamageVisualPool>(owned.gameObject);
            pending.simulation = combat; pending.reactorPool = pool; pending.focus = mission.motor.transform; pending.viewCamera = view;
            // The existing reactor and reflection pools remain bound to all retained targets.
        }
        [Serializable] sealed class AuthoringAudit
        { public string scene; public int count, distinctIds; public float scale, shipLengthUnits, dropletLengthUnits, escapeRadiusUnits, boundaryUnits; public Vector3 spacingMeters, min, max; }
    }
}
