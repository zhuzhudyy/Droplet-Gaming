using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class NarrativeCombatCommands
    {
        [MenuItem("DropletPrototype/Narrative Combat/3 Start Rendered Validation")]
        public static void Validate()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            var game = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus();
            if (UnityEngine.Object.FindAnyObjectByType<NarrativeCombatValidationRunner>() != null) throw new InvalidOperationException("Validation already exists.");
            var runner = new GameObject("__OptInNarrativeValidation").AddComponent<NarrativeCombatValidationRunner>();
            runner.Begin(NarrativeCombatBuilder.Evidence + "Editor-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        }
        [MenuItem("DropletPrototype/Narrative Combat/4 Build Windows 1080p")]
        public static void BuildWindows()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before building.");
            const string output = "Builds/Windows-NarrativeCombat/DropletPrototype.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            bool timing = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { NarrativeCombatBuilder.FullScene, NarrativeCombatBuilder.SmallScene },
                    locationPathName = output, target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                File.WriteAllText(NarrativeCombatBuilder.Evidence + "windows-build.json", JsonUtility.ToJson(new BuildAudit
                { result = report.summary.result.ToString(), output = Path.GetFullPath(output), errors = (int)report.summary.totalErrors,
                    warnings = (int)report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize,
                    unity = Application.unityVersion, frameTimingStats = true }, true));
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Narrative Windows build failed.");
            }
            finally { PlayerSettings.enableFrameTimingStats = timing; AssetDatabase.SaveAssets(); }
        }
        [Serializable] sealed class BuildAudit { public string result, output, unity; public int errors, warnings; public double seconds; public ulong bytes; public bool frameTimingStats; }
        [MenuItem("DropletPrototype/Narrative Combat/5 Open Formal Entry")]
        public static void OpenFormal()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Stop Play and preserve current unsaved scene first.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(NarrativeCombatBuilder.FullScene);
        }
        [MenuItem("DropletPrototype/Narrative Combat/6 Open Small Test Entry")]
        public static void OpenSmall()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Stop Play and preserve current unsaved scene first.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(NarrativeCombatBuilder.SmallScene);
        }
        [MenuItem("DropletPrototype/Narrative Combat/7 Validate Actual Input Only")]
        public static void ValidateInput()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            var game = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus();
            var runner = new GameObject("__OptInNarrativeInputValidation").AddComponent<NarrativeCombatValidationRunner>();
            runner.BeginInputOnly(NarrativeCombatBuilder.Evidence + "Input-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        }
        [MenuItem("DropletPrototype/Narrative Combat/8 Validate Retreat Only")]
        public static void ValidateRetreat()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            var game = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus();
            var runner = new GameObject("__OptInNarrativeRetreatValidation").AddComponent<NarrativeCombatValidationRunner>();
            runner.BeginRetreatOnly(NarrativeCombatBuilder.Evidence + "Retreat-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        }
    }
}
