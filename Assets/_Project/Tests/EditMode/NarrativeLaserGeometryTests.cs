using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class NarrativeLaserGeometryTests
    {
        [Test]
        public void ReflectionUsesNormalizedWorldNormalAndObeysEqualAngles()
        {
            Vector3 incident = new Vector3(1, -2, 3).normalized, normal = Vector3.up;
            Vector3 reflected = LaserGeometryMath.ReflectDirection(incident * 17, normal * 8);
            Assert.That(reflected.magnitude, Is.EqualTo(1).Within(.000001f));
            Assert.That(Vector3.Dot(incident, normal), Is.EqualTo(-Vector3.Dot(reflected, normal)).Within(.000001f));
            Assert.That(reflected.x, Is.EqualTo(incident.x).Within(.000001f));
            Assert.That(reflected.z, Is.EqualTo(incident.z).Within(.000001f));
            Vector3 offset = LaserGeometryMath.OffsetReflectionOrigin(Vector3.zero, normal, reflected, .008f);
            Assert.Greater(Vector3.Dot(offset, normal), 0);
        }

        [Test]
        public void BarycentricNormalAndWorldDistanceSurviveNonuniformTransform()
        {
            var geometry = ScriptableObject.CreateInstance<DropletReflectionGeometry>();
            try
            {
                var vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(0, 1, 0) };
                var normals = new[] { new Vector3(-.2f, 0, -1).normalized, new Vector3(.2f, 0, -1).normalized, new Vector3(0, .3f, -1).normalized };
                geometry.SetGeometry(vertices, normals, new[] { 0, 2, 1 });
                Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(11, 8, 23), Quaternion.Euler(16, 30, -4), new Vector3(2, 3, .5f));
                Vector3 point = matrix.MultiplyPoint3x4(Vector3.zero), forward = matrix.MultiplyVector(Vector3.forward).normalized;
                Assert.IsTrue(geometry.Raycast(matrix, new Ray(point - forward * 9, forward), 10, out var hit));
                Assert.That(hit.distance, Is.EqualTo(9).Within(.00002f));
                Assert.That(Vector3.Distance(hit.point, point), Is.LessThan(.00002f));
                Vector3 localNormal = (normals[0] * .25f + normals[1] * .25f + normals[2] * .5f).normalized;
                Vector3 expected = matrix.inverse.transpose.MultiplyVector(localNormal).normalized;
                Assert.That(Vector3.Angle(expected, hit.normal), Is.LessThan(.03f));
                Assert.Greater(Vector3.Angle(hit.normal, matrix.inverse.transpose.MultiplyVector(Vector3.back)), 1);
                Assert.IsFalse(geometry.Raycast(matrix, new Ray(point - forward * 9, forward), 8, out _));
            }
            finally { Object.DestroyImmediate(geometry); }
        }

        [Test]
        public void ActualRebuiltDropletSurfaceHasRoundFrontAndRejectsOversizedCombatSphereMiss()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Droplet_Rebuilt/Droplet_Rebuilt.fbx");
            Assert.NotNull(model);
            Mesh mesh = model.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            var geometry = ScriptableObject.CreateInstance<DropletReflectionGeometry>();
            try
            {
                geometry.BakeFromMesh(mesh);
                Assert.AreEqual(7040, geometry.TriangleCount);
                Assert.That(geometry.localBounds.size.z, Is.EqualTo(2.4f).Within(.0001f));
                Assert.IsTrue(geometry.Raycast(Matrix4x4.identity, new Ray(new Vector3(0, 0, 10), Vector3.back), 20, out var head));
                Assert.That(head.point.z, Is.EqualTo(1.2f).Within(.0001f));
                Assert.Greater(head.normal.z, .995f);
                // Old penetration sphere is .7 UU. x=.55 is inside that sphere,
                // yet outside the preserved .4 UU maximum visible radius.
                Assert.IsFalse(geometry.Raycast(Matrix4x4.identity, new Ray(new Vector3(.55f, 0, 10), Vector3.back), 20, out _));
                Assert.IsTrue(geometry.Raycast(Matrix4x4.identity, new Ray(new Vector3(.2f, 0, 10), Vector3.back), 20, out var offAxis));
                Assert.Greater(offAxis.normal.x, .1f);
                Vector3 reflected = LaserGeometryMath.ReflectDirection(Vector3.back, offAxis.normal);
                Assert.Greater(Mathf.Abs(reflected.x), .1f, "Surface reflection must not be artificially aimed back at the emitter.");
            }
            finally { Object.DestroyImmediate(geometry); }
        }

        [Test]
        public void SurfaceAuthorityIgnoresInterpolatedVisualPose()
        {
            var root = new GameObject("Authoritative root"); var visual = new GameObject("Interpolated visual");
            var geometry = ScriptableObject.CreateInstance<DropletReflectionGeometry>();
            try
            {
                var surface = root.AddComponent<DropletReflectiveSurface>();
                surface.Configure(visual.transform, root.transform, geometry);
                root.transform.position = new Vector3(25, 0, 10); visual.transform.position = new Vector3(-3, 0, -4);
                Assert.That(Vector3.Distance(surface.AimPoint, root.transform.position), Is.LessThan(.00001f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(visual); Object.DestroyImmediate(geometry); }
        }
    }
}
