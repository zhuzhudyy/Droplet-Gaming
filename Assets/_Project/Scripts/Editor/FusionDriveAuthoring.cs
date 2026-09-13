using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype.Editor
{
    /// <summary>Explicit authoring helper for the versioned lighting prefab only.</summary>
    public static class FusionDriveAuthoring
    {
        public const string OwnedRootName = "FusionDriveEffects";

        public static FusionDriveVisuals Attach(Transform visualRoot, Transform socketsRoot,
            Mesh sphereMesh, Material coreMaterial, Material shellMaterial, Material innerWallMaterial)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Fusion drive authoring requires stopped Edit mode.");
            if (visualRoot == null || socketsRoot == null || sphereMesh == null ||
                coreMaterial == null || shellMaterial == null || innerWallMaterial == null)
                throw new ArgumentException("Fusion drives need a visual root, five sockets, a shared imported sphere and three shared materials.");
            var group = visualRoot.GetComponent<LODGroup>();
            if (group == null || group.lodCount != 3)
                throw new InvalidOperationException("Expected the existing three-level FusionFrigate LODGroup.");
            var names = new[] { "MainExhaust", "AuxiliaryExhaust_01", "AuxiliaryExhaust_02", "AuxiliaryExhaust_03", "AuxiliaryExhaust_04" };
            var sockets = names.Select(n => socketsRoot.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == n)).ToArray();
            if (sockets.Any(t => t == null))
                throw new InvalidOperationException("The corrected main and four auxiliary exhaust sockets are required.");
            if (sphereMesh.bounds.size.x < .001f || sphereMesh.bounds.size.y < .001f || sphereMesh.bounds.size.z < .001f)
                throw new InvalidOperationException("The shared imported source must be a volumetric sphere.");

            var lods = group.GetLODs();
            var old = visualRoot.Find(OwnedRootName);
            if (old != null)
            {
                if (old.GetComponent<FusionDriveVisuals>() == null)
                    throw new InvalidOperationException("Unowned FusionDriveEffects child is protected.");
                var oldRenderers = new HashSet<Renderer>(old.GetComponentsInChildren<Renderer>(true));
                for (int i = 0; i < lods.Length; i++)
                    lods[i].renderers = lods[i].renderers.Where(r => r != null && !oldRenderers.Contains(r)).ToArray();
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            Undo.RecordObject(group, "Assign fusion drive LOD renderers");
            var effects = Child(visualRoot, OwnedRootName);
            var component = Undo.AddComponent<FusionDriveVisuals>(effects.gameObject);
            var cores = new List<Renderer>(15);
            var details = new List<Renderer>(10);

            for (int level = 0; level < 3; level++)
            {
                var bank = Child(effects, "LOD" + level);
                var added = new List<Renderer>();
                for (int engine = 0; engine < sockets.Length; engine++)
                {
                    bool main = engine == 0;
                    float radial = main ? 1 : .445f;
                    float axial = main ? 1 : .7f;
                    var mount = Child(bank, names[engine]);
                    mount.SetPositionAndRotation(sockets[engine].position, sockets[engine].rotation);
                    mount.localScale = Vector3.one;
                    var core = Sphere(mount, names[engine] + "_Core", sphereMesh, coreMaterial,
                        -.24f * axial, new Vector3(.61f * radial, .61f * radial, .77f * axial));
                    cores.Add(core); added.Add(core);
                    if (level != 0) continue;
                    var shell = Sphere(mount, names[engine] + "_Shell", sphereMesh, shellMaterial,
                        -.24f * axial, new Vector3(.92f * radial, .92f * radial, .93f * axial));
                    // Thin curved emissive liner touches the nozzle interior. This
                    // suggests confined plasma heating without a per-ship light.
                    var liner = Sphere(mount, names[engine] + "_NozzleLiner", sphereMesh, innerWallMaterial,
                        -.28f * axial, new Vector3(1.18f * radial, 1.18f * radial, .13f * axial));
                    details.Add(shell); details.Add(liner); added.Add(shell); added.Add(liner);
                }
                lods[level].renderers = lods[level].renderers.Concat(added).ToArray();
            }
            component.Configure(cores.ToArray(), details.ToArray());
            group.SetLODs(lods);
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(component);
            return component;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Author fusion drive presentation");
            return go.transform;
        }

        static Renderer Sphere(Transform parent, string name, Mesh sphere, Material material,
            float localZ, Vector3 radii)
        {
            var t = Child(parent, name);
            var size = sphere.bounds.size;
            t.localScale = new Vector3(radii.x * 2 / size.x, radii.y * 2 / size.y, radii.z * 2 / size.z);
            t.localPosition = new Vector3(0, 0, localZ) - Vector3.Scale(sphere.bounds.center, t.localScale);
            var filter = Undo.AddComponent<MeshFilter>(t.gameObject);
            filter.sharedMesh = sphere;
            var renderer = Undo.AddComponent<MeshRenderer>(t.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }
    }
}
