using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    public static class CinematicAudioIntegration
    {
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity";
        public const string Evidence = "docs/verification/CinematicAudio-20260919/";
        public const string PlayerPath = "Builds/Windows-CinematicAudio-20260919/DropletGaming.exe";
        static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play and compilation before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene protected: " + SceneManager.GetSceneAt(i).path);
        }
        [MenuItem("DropletPrototype/Cinematic Audio/1 Integrate Full Safe Copy")]
        public static void Integrate()
        {
            Guard(); Directory.CreateDirectory(Evidence);
            if(!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(EnhancementIntegration.Full, ScenePath))
                throw new IOException("Could not copy the latest saved Enhanced scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate 2000-ship cinematic audio safe copy");
            var mission = Object.FindAnyObjectByType<MissionController>();
            if(mission == null || mission.targets.Length != 2000) throw new InvalidOperationException("Expected complete 2000 fleet.");
            var ids = mission.targets.Select(s => s.targetId).ToArray();
            CinematicFleetLayout.ApplyToScene(scene);
            Undo.RecordObject(mission.lasers.beamPool, "Share authoritative cinematic world scale");
            mission.lasers.beamPool.scale = mission.settings.worldScale;
            var backdrop = Object.FindAnyObjectByType<SolarSystemBackdrop>();
            if(backdrop != null)
            {
                Undo.RecordObject(backdrop, "Share authoritative cinematic world scale");
                backdrop.combatScale = mission.settings.worldScale; backdrop.ReloadLayout();
            }
            if(!ids.SequenceEqual(mission.targets.Select(s => s.targetId))) throw new InvalidOperationException("Layout changed stable identities or target ordering.");
            var owned = mission.transform.Find("CinematicPresentation");
            if(owned == null)
            {
                var go = new GameObject("CinematicPresentation"); Undo.RegisterCreatedObjectUndo(go, "Create owned presentation root");
                go.transform.SetParent(mission.transform, false); owned = go.transform;
            }
            var shots = owned.GetComponent<ShotDirector>() ?? Undo.AddComponent<ShotDirector>(owned.gameObject);
            Undo.RecordObject(shots, "Configure event shots");
            shots.Configure(mission, mission.combat, mission.chaseCamera, mission.combat.fleetRenderer);
            shots.frequency = CombatShotFrequency.Standard;
            var listener = owned.Find("CombatListener");
            if(listener == null)
            {
                var go = new GameObject("CombatListener"); Undo.RegisterCreatedObjectUndo(go, "Create stable combat listener");
                go.transform.SetParent(owned, false); listener = go.transform;
            }
            var anchor = listener.GetComponent<CombatListenerAnchor>() ?? Undo.AddComponent<CombatListenerAnchor>(listener.gameObject);
            Undo.RecordObject(anchor, "Keep audio at normal chase point"); anchor.chase = mission.chaseCamera;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var old in root.GetComponentsInChildren<AudioListener>(true))
                    if(old.transform != listener) Undo.DestroyObjectImmediate(old);
            var ear = listener.GetComponent<AudioListener>();
            if(ear == null) ear = Undo.AddComponent<AudioListener>(listener.gameObject);
            ear.enabled = true;
            mission.chaseCamera.ResetCamera();
            listener.SetPositionAndRotation(mission.chaseCamera.NormalPosition, mission.chaseCamera.NormalRotation);
            Undo.RecordObject(mission.combat.fleetRenderer, "Keep streaming around normal combat view");
            mission.combat.fleetRenderer.normalStreamingReference = listener;
            CinematicAudioAssets.Configure(mission);
            var audio = Object.FindAnyObjectByType<BattleAudioDirector>();
            if(audio != null) { Undo.RecordObject(audio, "Reference stationary combat hearing basis"); audio.listener = listener; }
            if(mission.narrative?.radio != null) mission.narrative.radio.listener = listener;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                    if(component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the new scene.");
            AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Evidence + "integration.json", JsonUtility.ToJson(new IntegrationRecord
            {
                scene=scene.path, ships=ids.Length, uniqueIds=ids.Distinct().Count(),
                activeListeners=scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<AudioListener>(true).Count(a=>a.enabled)),
                narrativeSeconds=mission.narrative.duration, voiceProvenance=mission.narrative.radio.library.voiceProvenance,
                unity=Application.unityVersion
            }, true));
        }
        [Serializable] sealed class IntegrationRecord { public string scene,unity,voiceProvenance; public int ships,uniqueIds,activeListeners; public float narrativeSeconds; }
        [MenuItem("DropletPrototype/Cinematic Audio/2 Build Windows")]
        public static void Build()
        {
            Guard(); Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath)); Directory.CreateDirectory(Evidence);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes=new[]{ScenePath}, locationPathName=PlayerPath, target=BuildTarget.StandaloneWindows64, options=BuildOptions.None });
            var summary = report.summary;
            File.WriteAllText(Evidence + "build.json", JsonUtility.ToJson(new BuildRecord
            { result=summary.result.ToString(),path=summary.outputPath,errors=summary.totalErrors,warnings=summary.totalWarnings,
                bytes=summary.totalSize,seconds=summary.totalTime.TotalSeconds }, true));
            if(summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player build failed: " + summary.result);
        }
        [Serializable] sealed class BuildRecord { public string result,path;public int errors,warnings; public ulong bytes;public double seconds; }
    }
}
