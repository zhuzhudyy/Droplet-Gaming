using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit temporary validation hosts only; no scene, prefab, material or settings asset writes.</summary>
    public static class VisualUpgradeValidationCommands
    {
        public const string Evidence = "docs/verification/VisualUpgrade";

        [MenuItem("DropletPrototype/Visual Upgrade/7 Inspect Current Play Scene")]
        public static void Inspect() => Begin(Path.Combine(Evidence, "Editor"), "Inspection", true);

        [MenuItem("DropletPrototype/Visual Upgrade/8 Validate Current Play Scene Before")]
        public static void Before() => Begin(Path.Combine(Evidence, "Editor"), "Before");

        [MenuItem("DropletPrototype/Visual Upgrade/9 Validate Current Play Scene After")]
        public static void After() => Begin(Path.Combine(Evidence, "Editor"), "After");

        [MenuItem("DropletPrototype/Visual Upgrade/10 Validation Status")]
        public static void Status()
        {
            foreach (var runner in Resources.FindObjectsOfTypeAll<VisualUpgradeValidationRunner>())
            { runner.WriteProgress(); Debug.Log("VISUAL UPGRADE VALIDATION STATUS " + JsonUtility.ToJson(runner.GetProgress())); }
        }

        [MenuItem("DropletPrototype/Visual Upgrade/11 Clean Temporary Validation Hosts")]
        public static void Cleanup()
        {
            foreach (var runner in Resources.FindObjectsOfTypeAll<VisualUpgradeValidationRunner>())
            {
                if (runner == null || runner.gameObject.name != "__TemporaryVisualUpgradeValidation") continue;
                runner.Cancel("Explicit temporary validation cleanup requested.");
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }
        }

        public static VisualUpgradeValidationRunner Begin(string outputDirectory, string label, bool inspectionOnly = false)
        {
            if (!Application.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Enter Play mode and unpause the Editor before rendered validation.");
            var runner = Resources.FindObjectsOfTypeAll<VisualUpgradeValidationRunner>().FirstOrDefault(r => r.gameObject.scene.IsValid());
            if (runner == null) runner = new GameObject("__TemporaryVisualUpgradeValidation").AddComponent<VisualUpgradeValidationRunner>();
            runner.inspectionOnly = inspectionOnly; runner.Begin(outputDirectory, label); return runner;
        }
    }
}
