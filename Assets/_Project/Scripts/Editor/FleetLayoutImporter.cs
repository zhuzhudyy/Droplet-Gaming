using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Editor
{
    public static class FleetLayoutImporter
    {
        struct Pose { public string id; public GameObject prefab; public Vector3 position; public Quaternion rotation; }
        public static ShipTarget[] Import(GameObject layoutAsset, Transform generatedFleet, GameObject smallPrefab, GameObject largePrefab, GameObject commandPrefab)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing a fleet.");
            if (layoutAsset == null || generatedFleet == null) throw new InvalidOperationException("Assign the layout and GeneratedFleet destination.");
            CheckScale(generatedFleet);
            var ids = new HashSet<string>();
            var poses = new List<Pose>();
            foreach (Transform marker in layoutAsset.GetComponentsInChildren<Transform>(true))
            {
                if (!marker.name.StartsWith("SPAWN_", StringComparison.Ordinal)) continue;
                string[] parts = marker.name.Split('_');
                if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[2])) throw new InvalidOperationException("Invalid layout marker: " + marker.name);
                if (!ids.Add(parts[2])) throw new InvalidOperationException("Duplicate layout ID: " + parts[2] + " at " + marker.name);
                GameObject prefab = parts[1] == "Small" ? smallPrefab : parts[1] == "Large" ? largePrefab : parts[1] == "Command" ? commandPrefab : null;
                if (prefab == null || prefab.GetComponent<ShipTarget>() == null) throw new InvalidOperationException("Unknown ship type or missing ShipTarget prefab: " + marker.name);
                CheckScale(marker); CheckScale(prefab.transform);
                Vector3 p = marker.position;
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new InvalidOperationException("Non-finite marker pose: " + marker.name);
                poses.Add(new Pose { id = marker.name, prefab = prefab, position = p, rotation = marker.rotation });
            }
            if (poses.Count == 0) throw new InvalidOperationException("No SPAWN_Type_ID markers found in layout.");
            // Preflight everything (including existing identity) before touching the scene.
            var existing = new Dictionary<string, ShipTarget>();
            foreach (var target in generatedFleet.GetComponentsInChildren<ShipTarget>(true))
            {
                if (string.IsNullOrEmpty(target.targetId) || !target.targetId.StartsWith("SPAWN_", StringComparison.Ordinal)) continue;
                if (!existing.TryAdd(target.targetId, target)) throw new InvalidOperationException("Duplicate existing target identity: " + target.targetId);
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import authored fleet");
            var result = new List<ShipTarget>(poses.Count);
            foreach (var pose in poses)
            {
                if (!existing.TryGetValue(pose.id, out var target))
                {
                    var go = PrefabUtility.IsPartOfPrefabAsset(pose.prefab)
                        ? (GameObject)PrefabUtility.InstantiatePrefab(pose.prefab, generatedFleet)
                        : UnityEngine.Object.Instantiate(pose.prefab, generatedFleet);
                    Undo.RegisterCreatedObjectUndo(go, "Create fleet target");
                    target = go.GetComponent<ShipTarget>();
                }
                Undo.RecordObject(target, "Update target identity"); Undo.RecordObject(target.transform, "Update fleet pose");
                target.name = pose.id; target.targetId = pose.id;
                target.transform.SetPositionAndRotation(pose.position, pose.rotation); target.transform.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);
                result.Add(target);
            }
            var retained = new HashSet<ShipTarget>(result);
            foreach (var target in existing.Values) if (!retained.Contains(target)) Undo.DestroyObjectImmediate(target.gameObject);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Fleet import: {result.Count} markers; {result.Count(t => !existing.ContainsKey(t.targetId))} created, {result.Count(t => existing.ContainsKey(t.targetId))} updated, {existing.Values.Count(t => !retained.Contains(t))} removed.");
            return result.ToArray();
        }
        static void CheckScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            if ((s - Vector3.one).sqrMagnitude > .00001f || t.localToWorldMatrix.determinant <= 0)
                throw new InvalidOperationException("Expected positive unit scale on " + t.name + "; apply source scale before export.");
        }
    }
}
