using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SetCurrentBuildEntry {
 public static string Main() {
  const string current = "Assets/_Project/Scenes/FleetAssault_Enhanced.unity";
  if (AssetDatabase.LoadAssetAtPath<SceneAsset>(current) == null) throw new System.Exception("Current scene missing");
  EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(current, true) };
  return "Build entry: " + EditorBuildSettings.scenes[0].path + "; open scene untouched: " + SceneManager.GetActiveScene().path;
 }
}
