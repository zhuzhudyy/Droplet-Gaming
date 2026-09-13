using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class TestRangeBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestRange.unity";
        public const string ShipPath = "Assets/_Project/Prefabs/Ships/GrayboxShip.prefab";
        public const string PlayerPath = "Assets/_Project/Prefabs/Player/Droplet.prefab";
        const string Materials = "Assets/_Project/Art/Materials/";

        [MenuItem("DropletPrototype/Create or Update Test Range")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before authoring the range.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your unsaved scene changes before updating TestRange.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material hull = MaterialAsset("Hull", new Color(.64f, .72f, .79f), .25f);
            Material accent = MaterialAsset("Signal", new Color(1f, .39f, .08f), .1f);
            Material drop = MaterialAsset("Droplet", new Color(.3f, .95f, 1f), .7f);
            Material ground = MaterialAsset("Reference", new Color(.08f, .14f, .21f), 0);
            Material line = MaterialAsset("Lane", new Color(.15f, .43f, .53f), 0);
            EnsurePrefabs(hull, accent, drop);
            ConfigureShipPrefab();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update generated TestRange");
            foreach (var old in scene.GetRootGameObjects().Where(o => o.GetComponent<GeneratedRangeRoot>() != null))
                Undo.DestroyObjectImmediate(old);
            var root = new GameObject("GeneratedTestRange");
            Undo.RegisterCreatedObjectUndo(root, "Create generated range");
            root.AddComponent<GeneratedRangeRoot>();
            var fleet = Child("GeneratedFleet", root.transform);
            for (int i = 0; i < 10; i++)
            {
                var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath), fleet.transform);
                ship.name = $"Ship_{i + 1:00}";
                ship.GetComponent<ShipTarget>().targetId = ship.name;
                ship.transform.position = i < 4 ? new Vector3(0, 8, 55 + i * 35)
                    : new Vector3(i % 2 == 0 ? -30 : 30, 8, 85 + ((i - 4) / 2) * 45);
            }
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), root.transform);
            player.name = "PlayerRoot";
            player.transform.position = new Vector3(0, 8, 0);
            var environment = Child("Environment", root.transform);
            Primitive("ReferenceDeck", PrimitiveType.Cube, environment.transform, new Vector3(0, -7, 95), new Vector3(500, 1, 600), ground);
            for (int i = -2; i <= 2; i++)
                Primitive("Lane_" + i, PrimitiveType.Cube, environment.transform, new Vector3(i * 30, -6.4f, 95), new Vector3(.3f, .1f, 500), line);
            for (int i = 0; i < 7; i++)
                Primitive("Distance_" + i, PrimitiveType.Cube, environment.transform, new Vector3(0, -6.4f, i * 40), new Vector3(180, .1f, .3f), line);
            var lighting = Child("Lighting", root.transform);
            var sun = Child("Key Light", lighting.transform).AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2; sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.35f, .43f, .55f);
            var camera = Child("Chase Camera", root.transform).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 12, -14);
            camera.transform.rotation = Quaternion.Euler(8, 0, 0);
            camera.fieldOfView = 65; camera.farClipPlane = 1200;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f, .035f, .065f);
            camera.gameObject.AddComponent<AudioListener>();
            ConfigureGameplay(root, player, fleet, camera);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 5, 80), Quaternion.Euler(25, -20, 0), 130);
        }

        static void ConfigureGameplay(GameObject root, GameObject player, GameObject fleet, Camera camera)
        {
            const string settingsPath = "Assets/_Project/Data/DefaultSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<DropletSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DropletSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            settings.targetLayers = 1 << LayerMask.NameToLayer("ShipTarget");
            EditorUtility.SetDirty(settings);
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
            try { ConfigureMotor(prefab, settings); PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var motor = ConfigureMotor(player, settings);
            var input = motor.input;
            var chase = camera.gameObject.AddComponent<ChaseCamera>(); chase.target = motor; chase.settings = settings;
            var systems = Child("Systems", root.transform);
            var score = systems.AddComponent<ScoreSystem>(); score.settings = settings;
            var mission = systems.AddComponent<MissionController>();
            mission.settings = settings; mission.motor = motor; mission.input = input; mission.chaseCamera = chase; mission.score = score;
            mission.targets = fleet.GetComponentsInChildren<ShipTarget>(); motor.mission = mission;
            var hud = Child("HUD", root.transform).AddComponent<HudPresenter>(); hud.mission = mission; hud.viewCamera = camera;
        }

        static DropletMotor ConfigureMotor(GameObject player, DropletSettings settings)
        {
            var motor = player.GetComponent<DropletMotor>() ?? player.AddComponent<DropletMotor>();
            var input = player.GetComponent<DropletInput>() ?? player.AddComponent<DropletInput>();
            var detector = player.GetComponent<DropletHitDetector>() ?? player.AddComponent<DropletHitDetector>();
            motor.settings = settings; motor.input = input; motor.visualRoot = player.transform.Find("VisualRoot");
            input.motor = motor; detector.settings = settings; motor.hitDetector = detector; return motor;
        }

        [MenuItem("DropletPrototype/Use Test Range as Build Startup Scene")]
        public static void ConfigureBuildScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Create TestRange first.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
        }

        static Material MaterialAsset(string name, Color color, float metallic)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Installed URP/Lit shader unavailable.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .45f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void ConfigureShipPrefab()
        {
            int layer = LayerMask.NameToLayer("ShipTarget");
            if (layer < 0)
            {
                var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tags.FindProperty("layers");
                for (int i = 8; i < 32; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    { layers.GetArrayElementAtIndex(i).stringValue = "ShipTarget"; layer = i; break; }
                if (layer < 0) throw new InvalidOperationException("No free user layer for ShipTarget.");
                tags.ApplyModifiedProperties();
            }
            var ship = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var target = ship.GetComponent<ShipTarget>() ?? ship.AddComponent<ShipTarget>();
                target.visualRoot = ship.transform.Find("VisualRoot").gameObject;
                target.hitVolumes = ship.transform.Find("HitVolumes").GetComponentsInChildren<Collider>();
                foreach (var hit in target.hitVolumes) hit.gameObject.layer = layer;
                PrefabUtility.SaveAsPrefabAsset(ship, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(ship); }
        }

        static void EnsurePrefabs(Material hull, Material accent, Material drop)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath) == null)
            {
                var ship = new GameObject("GrayboxShip");
                try
                {
                    var visual = Child("VisualRoot", ship.transform);
                    Primitive("Hull", PrimitiveType.Cube, visual.transform, Vector3.zero, new Vector3(7, 3, 12), hull);
                    Primitive("Bridge", PrimitiveType.Cube, visual.transform, new Vector3(0, 2, -1), new Vector3(4, 2, 4), accent);
                    Primitive("Port", PrimitiveType.Cube, visual.transform, new Vector3(-5, 0, -2), new Vector3(3, 2, 6), hull);
                    Primitive("Starboard", PrimitiveType.Cube, visual.transform, new Vector3(5, 0, -2), new Vector3(3, 2, 6), hull);
                    var hit = Child("HitVolumes", ship.transform).AddComponent<BoxCollider>();
                    hit.size = new Vector3(13, 5, 12); hit.isTrigger = true;
                    PrefabUtility.SaveAsPrefabAsset(ship, ShipPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(ship); }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null)
            {
                var player = new GameObject("Droplet");
                try
                {
                    var visual = Child("VisualRoot", player.transform);
                    Primitive("Polished Core", PrimitiveType.Sphere, visual.transform, Vector3.zero, new Vector3(1.2f, 1.2f, 2.4f), drop);
                    PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(player); }
            }
        }

        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
        }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
    }
}
