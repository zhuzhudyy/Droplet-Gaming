using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Editor
{
    public static class FleetAssetPipeline
    {
        public const string ModelFolder = "Assets/_Project/Art/Models/Fleet/";
        public static void ImportExports()
        {
            Directory.CreateDirectory(ModelFolder);
            foreach (var file in Directory.GetFiles("ArtSource/Blender/Exports", "*.fbx"))
                File.Copy(file, ModelFolder + Path.GetFileName(file), true);
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(ModelFolder, "*.fbx")) Configure(file.Replace('\\', '/'));
        }
        public static void Configure(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.addCollider = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        public static string ValidateCalibration()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "calibration.fbx");
            if (asset == null) throw new InvalidOperationException("Import calibration.fbx first.");
            var all = asset.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.Single(t => t.name == n);
            void Near(Vector3 value, Vector3 expected, string what)
            { if (Vector3.Distance(value, expected) > .002f) throw new InvalidOperationException(what + " got " + value + " expected " + expected); }
            Near(Find("Cube_1m").position, new Vector3(-8, 0, 0), "cube pivot");
            Near(Find("Cube_1m").GetComponent<Renderer>().bounds.size, Vector3.one, "cube size");
            Near(Find("FRONT_TIP").position, new Vector3(0, 0, 3), "forward tip");
            Near(Find("UP_TIP").position, new Vector3(0, 2.2f, 0), "up tip");
            Near(Find("RIGHT_TIP").position, new Vector3(1.5f, 0, 0), "right tip");
            var positions = new[] { new Vector3(0,8,100), new Vector3(-20,15,140), new Vector3(30,-5,180) };
            var rotations = new[] { Quaternion.identity, Quaternion.Euler(0,90,0), Quaternion.Euler(15,-35,0) };
            for (int i = 0; i < 3; i++)
            {
                var t = Find("SPAWN_Small_00" + (i + 1));
                Near(t.position, positions[i], t.name + " position");
                Near(t.forward, rotations[i] * Vector3.forward, t.name + " forward");
                Near(t.up, rotations[i] * Vector3.up, t.name + " up");
                Near(t.lossyScale, Vector3.one, t.name + " scale");
            }
            foreach (var mf in asset.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                if (m == null || m.vertexCount == 0 || m.normals.Length != m.vertexCount) throw new InvalidOperationException("Missing geometry/normals: " + mf.name);
                if (m.normals.Any(n => !float.IsFinite(n.x) || n.sqrMagnitude < .5f)) throw new InvalidOperationException("Invalid normals: " + mf.name);
            }
            var cube = Find("Cube_1m").GetComponent<MeshFilter>().sharedMesh;
            for (int i = 0; i < cube.vertexCount; i++)
                if (Vector3.Dot(cube.vertices[i] - cube.bounds.center, cube.normals[i]) <= 0) throw new InvalidOperationException("Cube normals face inward.");
            string result = "PASS: Unity 6000.5.10f1 imported calibration; 1m cube (0.002 tolerance); asymmetric +Z/+Y/+X; three translated/rotated markers; unit scale; nonempty geometry and valid imported normals; cube outward normals.\nImporter: scale=1, useFileScale=true, bakeAxisConversion=true, Import normals, no animation/colliders.\n";
            Directory.CreateDirectory("docs/verification/G05-G09");
            File.WriteAllText("docs/verification/G05-G09/calibration-unity.txt", result);
            return result;
        }
    }
}
