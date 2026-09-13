using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit temporary diagnostics; no scene or asset save operations.</summary>
    public static class LightingValidationCommands
    {
        public const string Evidence = "docs/verification/LightingUpgrade";

        [MenuItem("DropletPrototype/Lighting Materials/8 Validate Current Play Scene (Before)")]
        public static void Before() => Begin(Path.Combine(Evidence, "Editor"), "Before");

        [MenuItem("DropletPrototype/Lighting Materials/9 Validate Current Play Scene (After)")]
        public static void After() => Begin(Path.Combine(Evidence, "Editor"), "After");

        [MenuItem("DropletPrototype/Lighting Materials/10 Validation Status")]
        public static void Status()
        {
            var runners = Resources.FindObjectsOfTypeAll<LightingValidationRunner>();
            Debug.Log("LIGHTING VALIDATION EDITOR STATUS: playing=" + EditorApplication.isPlaying + ", paused=" + EditorApplication.isPaused +
                ", focused=" + Application.isFocused + ", background=" + Application.runInBackground + ", frame=" + Time.frameCount + ", runners=" + runners.Length);
            foreach (var runner in runners)
            {
                runner.WriteProgress();
                Debug.Log("LIGHTING VALIDATION RUNNER STATUS " + JsonUtility.ToJson(runner.GetProgress()) +
                    ", flags=" + runner.gameObject.hideFlags + ", scene=" + runner.gameObject.scene.path);
            }
        }

        [MenuItem("DropletPrototype/Lighting Materials/11 Clean Stale Validation Hosts")]
        public static void Cleanup()
        {
            int removed = CleanupOwnedHosts(!Application.isPlaying);
            Debug.Log("LIGHTING VALIDATION CLEANUP: removed=" + removed + ", playing=" + Application.isPlaying +
                ". Only exact owned __TemporaryLightingValidation objects were eligible.");
        }

        static int CleanupOwnedHosts(bool includeSceneHosts)
        {
            int removed = 0;
            foreach (var runner in Resources.FindObjectsOfTypeAll<LightingValidationRunner>())
            {
                if (runner == null || runner.gameObject.name != "__TemporaryLightingValidation") continue;
                GameObject host = runner.gameObject;
                bool stale = !host.scene.IsValid() || string.IsNullOrEmpty(host.scene.path) || (host.hideFlags & HideFlags.DontSave) != 0;
                if (!includeSceneHosts && !stale) continue;
                runner.Cancel("Owned stale validation host removed before a fresh diagnostic run.");
                // This is an explicitly generated diagnostic host, never authored gameplay.
                // Immediate removal prevents it from blocking Begin during the same editor update.
                UnityEngine.Object.DestroyImmediate(host);
                removed++;
            }
            return removed;
        }

        public static LightingValidationRunner Begin(string outputDirectory, string label)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode before rendered lighting validation.");
            if (EditorApplication.isPaused) throw new InvalidOperationException("Unpause the Editor before rendered lighting validation.");
            CleanupOwnedHosts(false);
            var runner = Resources.FindObjectsOfTypeAll<LightingValidationRunner>().FirstOrDefault(r => r.gameObject.scene.IsValid());
            if (runner == null)
            {
                // Play-mode scene edits are discarded automatically on exit. DontSave
                // excludes objects from normal discovery and is unnecessary here.
                var host = new GameObject("__TemporaryLightingValidation");
                runner = host.AddComponent<LightingValidationRunner>();
            }
            runner.Begin(outputDirectory, label);
            return runner;
        }
    }
}
