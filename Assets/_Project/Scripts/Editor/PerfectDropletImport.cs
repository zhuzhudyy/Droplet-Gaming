using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit geometry import and isolated scene verification. No runtime dependency.</summary>
    public static class PerfectDropletImport
    {
        public const string Source = "ArtSource/Exports/Droplet/PerfectDroplet/PerfectDroplet_Game.fbx";
        public const string Model = "Assets/_Project/Art/Models/PerfectDroplet/PerfectDroplet_Game.fbx";
        public const string VisualPrefab = "Assets/_Project/Prefabs/Player/PerfectDropletVisual.prefab";
        public const string BaselineScene = "Assets/_Project/Scenes/FleetAssault_Lighting.unity";
        public const string TestScene = "Assets/_Project/Scenes/FleetAssault_PerfectDroplet_Test.unity";
        public const string MaterialPath = "Assets/_Project/Art/LightingUpgrade/MetalDroplet.mat";
        public const string Evidence = "docs/verification/PerfectDropletUnity/";

        [Serializable] public class AssetReport
        {
            public string unity, fbxHash, guid, meshName, calibration, hierarchy, oldVisual;
            public int meshes, vertices, triangles, badNormals, inwardTriangles, zeroArea;
            public Vector3 dimensions, center, localDimensions, localScale, rotation;
            public float positiveEndRadius, negativeEndRadius, seamNormalMaxDegrees;
            public bool normalsImported, noColliders, noAnimation, bakeAxisConversion;
        }
        [Serializable] public class SceneReport
        {
            public string path, baseHash, newHash, settings, material, modelGuid;
            public int ships, renderers, triangles, collidersBefore, collidersAfter;
            public Vector3 previousSize, newSize, visualRootScale;
            public float hitRadius;
            public bool allTargetsPreserved, gameplayReferencesPreserved;
        }
        [Serializable] public class LiveReport
        {
            public bool passed; public string error, scope; public int destroyed, score, poolBefore, poolAfter;
            public float elapsed, distance, maxSpeed; public bool pause, brake, turn, restart, newVisualPresent;
            public string[] hitIds;
        }
        static bool liveRunning;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        static void Write(string name, object report) { Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + name, JsonUtility.ToJson(report, true)); }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static void Guard()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Stop Play/compilation before authoring.");
            Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Close the prefab stage before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Unsaved scene protected: " + SceneManager.GetSceneAt(i).path);
        }
        public static T[] Components<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        [MenuItem("DropletPrototype/Perfect Droplet/1 Import and Inspect")]
        public static void Import()
        {
            Guard(); Directory.CreateDirectory(Evidence);
            Folder(Path.GetDirectoryName(Model).Replace('\\', '/'));
            if (File.Exists(Model)) Require(Hash(Model) == Hash(Source), "Existing imported FBX differs; preserve it before replacing.");
            else File.Copy(Source, Model);
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.isReadable = false; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var report = InspectAsset(); Write("import.json", report);
            Debug.Log("PERFECT DROPLET IMPORT " + JsonUtility.ToJson(report));
        }

        public static AssetReport InspectAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Require(asset != null, "Missing imported model.");
            var filters = asset.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length == 1, "Expected one mesh.");
            var mf = filters[0]; var mesh = mf.sharedMesh; var matrix = mf.transform.localToWorldMatrix;
            var points = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            var ns = mesh.normals.Select(n => matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
            var triangles = mesh.triangles; int inward = 0, zero = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i+1], c = triangles[i+2];
                var face = Vector3.Cross(points[b]-points[a], points[c]-points[a]);
                if (face.sqrMagnitude < 1e-26f) zero++;
                if (Vector3.Dot(face, ns[a]+ns[b]+ns[c]) <= 0) inward++;
            }
            float seam = 0;
            foreach (var group in points.Select((p,i) => new {p,i}).GroupBy(v => v.p))
            { var values = group.Select(v => ns[v.i]).ToArray(); foreach (var a in values) foreach (var b in values) seam = Mathf.Max(seam, Vector3.Angle(a,b)); }
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            var report = new AssetReport { unity = Application.unityVersion, fbxHash = Hash(Model), guid = AssetDatabase.AssetPathToGUID(Model),
                meshes = filters.Length, vertices = mesh.vertexCount, triangles = triangles.Length/3, meshName = mesh.name,
                dimensions = bounds.size, center = bounds.center, localDimensions = mesh.bounds.size, localScale = asset.transform.localScale,
                rotation = asset.transform.localEulerAngles, inwardTriangles = inward, zeroArea = zero, seamNormalMaxDegrees = seam,
                badNormals = ns.Count(n => !float.IsFinite(n.x) || Mathf.Abs(n.magnitude-1) > .001f),
                positiveEndRadius = points.Where(p => p.z > 1.176f).Max(p => new Vector2(p.x,p.y).magnitude),
                negativeEndRadius = points.Where(p => p.z < -1.176f).Max(p => new Vector2(p.x,p.y).magnitude),
                normalsImported = importer.importNormals == ModelImporterNormals.Import, bakeAxisConversion = importer.bakeAxisConversion,
                noColliders = asset.GetComponentsInChildren<Collider>(true).Length == 0, noAnimation = !importer.importAnimation,
                hierarchy = string.Join("\n",asset.GetComponentsInChildren<Transform>(true).Select(t => t.name+" pos="+t.localPosition+" rot="+t.localEulerAngles+" scale="+t.localScale)),
                calibration = CheckExistingCalibration() };
            var mission = Object.FindAnyObjectByType<MissionController>();
            if (mission != null) report.oldVisual = string.Join("\n", mission.motor.visualRoot.GetComponentsInChildren<Transform>(true).Select(t => t.name+" pos="+t.localPosition+" scale="+t.localScale+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().Name))));
            Require(Vector3.Distance(bounds.size, new Vector3(2.4f/3.1f,2.4f/3.1f,2.4f)) < .0001f, "Dimension/axis mismatch: " + bounds.size);
            Require(bounds.center.magnitude < .0001f && report.positiveEndRadius < .02f && report.negativeEndRadius > .04f, "Pivot or +Z tip mismatch.");
            Require(report.triangles == 5632 && inward == 0 && zero == 0 && report.badNormals == 0 && seam < .1f, "Topology/normal import failed.");
            Require(Vector3.Distance(mesh.bounds.size,bounds.size) < .0001f && matrix == Matrix4x4.identity, "Importer left an unexpected corrective transform.");
            return report;
        }

        static string CheckExistingCalibration()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FleetAssetPipeline.ModelFolder + "calibration.fbx");
            Require(asset != null,"Missing project calibration asset.");
            var nodes = asset.GetComponentsInChildren<Transform>(true);
            var cube = nodes.Single(t => t.name == "Cube_1m");
            Require(Vector3.Distance(cube.GetComponent<Renderer>().bounds.size,Vector3.one) < .002f,"1m calibration failed.");
            foreach (var item in new[] { ("FRONT_TIP", new Vector3(0,0,3)), ("UP_TIP", new Vector3(0,2.2f,0)), ("RIGHT_TIP", new Vector3(1.5f,0,0)) })
                Require(Vector3.Distance(nodes.Single(t=>t.name==item.Item1).position,item.Item2) < .002f,"Calibration axis failed.");
            return "PASS: existing 1m cube and asymmetric +Z/+Y/+X marker positions; read-only check.";
        }

        [MenuItem("DropletPrototype/Perfect Droplet/2 Create Test Scene")]
        public static void CreateTestScene()
        {
            Guard(); InspectAsset();
            Require(!File.Exists(TestScene), "Test scene already exists; open it without rebuilding.");
            Require(!File.Exists(VisualPrefab), "Visual prefab already exists; preserve it.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath); Require(material != null,"Missing existing droplet material.");
            var prefabRoot = new GameObject("PerfectDropletVisual");
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
                child.transform.SetParent(prefabRoot.transform,false);
                foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                foreach (var r in prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
                {r.sharedMaterial=material;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;}
                PrefabUtility.SaveAsPrefabAsset(prefabRoot,VisualPrefab);
            }
            finally { Object.DestroyImmediate(prefabRoot); }
            Require(AssetDatabase.CopyAsset(BaselineScene,TestScene),"Scene copy failed.");
            var scene = EditorSceneManager.OpenScene(TestScene,OpenSceneMode.Single);
            var m = Components<MissionController>(scene).Single(); var v = m.motor.visualRoot;
            var oldFilters = v.GetComponentsInChildren<MeshFilter>(true);
            Require(oldFilters.Length == 1 && oldFilters[0].transform != v,"Unexpected old visual subtree.");
            var old = oldFilters[0];
            Require(old.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c is MeshRenderer) && old.transform.childCount == 0,
                "Old mesh contains gameplay/child content; do not erase it.");
            int colliders = Components<Collider>(scene).Length;
            var targets = m.targets.ToArray(); var settings = m.settings;
            var report = new SceneReport { path=TestScene,baseHash=Hash(BaselineScene),ships=targets.Length,
                settings=AssetDatabase.GetAssetPath(settings),previousSize=old.GetComponent<Renderer>().bounds.size,
                hitRadius=settings.hitRadius,collidersBefore=colliders,visualRootScale=v.localScale,
                material=MaterialPath,modelGuid=AssetDatabase.AssetPathToGUID(Model) };
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace droplet visual in test copy");
            // Preserve the original renderer and every gameplay/FX reference.
            // Only the visual mesh changes; the reusable visual prefab is separate.
            Undo.RecordObject(old,"Assign imported droplet mesh");
            old.sharedMesh=AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentInChildren<MeshFilter>().sharedMesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(old);
            report.newSize=old.GetComponent<Renderer>().bounds.size;
            report.renderers=v.GetComponentsInChildren<Renderer>(true).Length;report.triangles=5632;
            report.collidersAfter=Components<Collider>(scene).Length;
            report.allTargetsPreserved=m.targets.SequenceEqual(targets);
            report.gameplayReferencesPreserved=m.settings==settings && m.motor.visualRoot==v && m.motor.hitDetector!=null && m.input!=null;
            Require(report.allTargetsPreserved && report.gameplayReferencesPreserved && colliders==report.collidersAfter,"Gameplay preservation failed.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(group); report.newHash=Hash(TestScene); Write("scene.json",report);
            var sv=SceneView.lastActiveSceneView;if(sv!=null)sv.LookAt(v.position,Quaternion.Euler(12,-120,0),3.5f,false,true);
            Debug.Log("PERFECT DROPLET TEST SCENE SAVED "+JsonUtility.ToJson(report));
        }

        [MenuItem("DropletPrototype/Perfect Droplet/3 Capture Model Views")]
        public static void CaptureViews()
        {
            Require(SceneManager.GetActiveScene().path==TestScene,"Open the droplet test scene.");
            Require(!Application.isPlaying,"Capture asset closeups outside Play mode.");
            Directory.CreateDirectory(Evidence);
            var m=Object.FindAnyObjectByType<MissionController>();var c=m.chaseCamera.GetComponent<Camera>();
            var r=m.motor.visualRoot.GetComponentsInChildren<MeshRenderer>(true).Single();
            var pos=c.transform.position;var rot=c.transform.rotation;float fov=c.fieldOfView;int mask=c.cullingMask;
            var clear=c.clearFlags;var bg=c.backgroundColor;var mat=r.sharedMaterial;
            var neutral=new Material(Shader.Find("Universal Render Pipeline/Lit"));neutral.SetColor("_BaseColor",new Color(.46f,.48f,.5f));neutral.SetFloat("_Metallic",0);neutral.SetFloat("_Smoothness",.4f);
            try
            {
                c.cullingMask=1<<r.gameObject.layer;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.065f,.075f,.09f);
                c.fieldOfView=34;
                var center=r.bounds.center;
                Vector3[] directions={new Vector3(3,1,4),new Vector3(4,1,-3),new Vector3(-3,1,4)};
                for(int i=0;i<directions.Length;i++)
                {c.transform.SetPositionAndRotation(center+directions[i].normalized*5,Quaternion.LookRotation(-directions[i]));r.sharedMaterial=mat;Shot(c,"Mirror-"+(i+1));}
                r.sharedMaterial=neutral;c.transform.SetPositionAndRotation(center+Vector3.right*5,Quaternion.LookRotation(Vector3.left));Shot(c,"Neutral-Side");
                c.transform.SetPositionAndRotation(center+Vector3.forward*3.5f,Quaternion.LookRotation(Vector3.back));Shot(c,"Neutral-Front");
            }
            finally{r.sharedMaterial=mat;Object.DestroyImmediate(neutral);c.transform.SetPositionAndRotation(pos,rot);c.fieldOfView=fov;c.cullingMask=mask;c.clearFlags=clear;c.backgroundColor=bg;}
            Debug.Log("PERFECT DROPLET model views saved; camera/material restored; scene not saved.");
        }
        static void Shot(Camera camera,string name) => LightingUpgradeInspection.Capture(camera,Evidence+name+".png");

        [MenuItem("DropletPrototype/Perfect Droplet/4 Live Flight Check")]
        public static async void LiveFlight()
        {
            Require(Application.isPlaying && SceneManager.GetActiveScene().path==TestScene && !liveRunning,"Enter the test scene in Play mode.");
            liveRunning=true;Directory.CreateDirectory(Evidence);
            var m=Object.FindAnyObjectByType<MissionController>();var effects=Object.FindAnyObjectByType<MissionEffects>();
            var keyboard=InputSystem.AddDevice<Keyboard>("PerfectDropletCheckKeyboard");var mouse=InputSystem.AddDevice<Mouse>("PerfectDropletCheckMouse");
            bool bg=Application.runInBackground;Action devices=()=>{keyboard.MakeCurrent();mouse.MakeCurrent();};InputSystem.onAfterUpdate+=devices;
            var report=new LiveReport{scope="New saved droplet test scene; synthetic Input System events, real FixedUpdate, no teleports along the five-ship flight. Physical keyboard feel is not evaluated."};
            try
            {
                Application.runInBackground=true;m.Restart();report.poolBefore=effects.PoolInstanceCount;
                report.newVisualPresent=m.motor.visualRoot.GetComponentsInChildren<MeshFilter>(true).Single().sharedMesh.name.Contains("PerfectDroplet");
                Require(report.newVisualPresent,"New mesh not active.");
                ScreenCapture.CaptureScreenshot(Evidence+"Live-Ready.png");await Task.Delay(150);
                await Press(keyboard,Key.Enter);Require(m.State==MissionState.Playing,"Start failed.");
                var start=m.motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
                var deadline=DateTime.UtcNow.AddSeconds(15);
                while(m.DestroyedCount<5 && DateTime.UtcNow<deadline){await Task.Delay(80);report.maxSpeed=Mathf.Max(report.maxSpeed,m.motor.Speed);}
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());report.destroyed=m.DestroyedCount;report.score=m.score.Score;
                report.elapsed=m.Elapsed;report.distance=Vector3.Distance(start,m.motor.transform.position);report.hitIds=m.targets.Where(t=>t.IsDestroyed).Select(t=>t.targetId).ToArray();
                Require(report.destroyed>=5 && report.distance>500 && report.score>0,"Continuous five-ship sweep failed.");
                ScreenCapture.CaptureScreenshot(Evidence+"Live-FiveHits.png");await Task.Delay(150);
                await Press(keyboard,Key.Escape);var p=m.motor.transform.position;float remaining=m.Remaining;await Task.Delay(250);
                report.pause=m.State==MissionState.Paused && p==m.motor.transform.position && remaining==m.Remaining;Require(report.pause,"Pause failed.");
                ScreenCapture.CaptureScreenshot(Evidence+"Live-Paused.png");await Task.Delay(150);
                await Press(keyboard,Key.R);report.restart=m.State==MissionState.Ready && m.TotalCount==120 && m.DestroyedCount==0 && m.Remaining==540 && m.score.Score==0;Require(report.restart,"Restart failed.");
                await Press(keyboard,Key.Enter);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));await Task.Delay(550);report.brake=m.motor.Speed<.01f;Require(report.brake,"Brake failed.");
                float yaw=m.motor.transform.eulerAngles.y;InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(100,15)});await Task.Delay(250);
                report.turn=Mathf.Abs(Mathf.DeltaAngle(yaw,m.motor.transform.eulerAngles.y))>1;Require(report.turn,"Steering failed.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                for(int i=0;i<3;i++){m.Restart();await Task.Delay(100);Require(m.motor.visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.name.Contains("PerfectDroplet"),"Restart lost model.");}
                report.poolAfter=effects.PoolInstanceCount;Require(report.poolAfter==report.poolBefore,"Pool grew.");
                ScreenCapture.CaptureScreenshot(Evidence+"Live-Restarted.png");await Task.Delay(150);report.passed=true;
            }
            catch(Exception ex){report.error=ex.ToString();}
            finally
            {
                InputSystem.onAfterUpdate-=devices;InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);Application.runInBackground=bg;
                m.Restart();liveRunning=false;Write("live-flight.json",report);Debug.Log("PERFECT DROPLET LIVE "+JsonUtility.ToJson(report));
            }
        }
        static async Task Press(Keyboard keyboard,Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(120);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(120);}
    }
}
