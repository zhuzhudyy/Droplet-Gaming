using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropletPrototype
{
    public struct DropletSurfaceHit
    {
        public Vector3 point, normal, localPoint, localNormal;
        public float distance;
        public int triangle;
    }

    /// <summary>A new baked copy of the EXISTING droplet surface and imported smooth
    /// normals. No physics collider or model import setting is changed.</summary>
    [CreateAssetMenu(menuName = "Droplet/Narrative Combat/Droplet Reflection Geometry")]
    public sealed class DropletReflectionGeometry : ScriptableObject
    {
        [SerializeField, HideInInspector] Vector3[] vertices = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] Vector3[] normals = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] int[] triangles = Array.Empty<int>();
        public Bounds localBounds;
        public string sourceMeshName;
        public int TriangleCount => triangles.Length / 3;
        public bool IsValid => vertices.Length > 2 && normals.Length == vertices.Length && triangles.Length >= 3;
        struct Node { public Bounds bounds; public int first, count, left, right; }
        Node[] nodes;
        int[] order;
        readonly int[] traversal = new int[128];

        public void BakeFromMesh(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            SetGeometry(mesh.vertices, mesh.normals, mesh.triangles);
            sourceMeshName = mesh.name;
        }

        public void SetGeometry(Vector3[] positions, Vector3[] smoothNormals, int[] indices)
        {
            if (positions == null || smoothNormals == null || indices == null || positions.Length != smoothNormals.Length)
                throw new ArgumentException("Reflection geometry requires the authored surface normals.");
            vertices = (Vector3[])positions.Clone(); normals = (Vector3[])smoothNormals.Clone(); triangles = (int[])indices.Clone();
            if (!IsValid) throw new ArgumentException("Empty reflection mesh.");
            foreach (int index in triangles) if (index < 0 || index >= vertices.Length) throw new ArgumentException("Invalid triangle index.");
            localBounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) localBounds.Encapsulate(vertex);
            BuildTree();
        }

        void OnEnable() { nodes = null; order = null; }
        void BuildTree()
        {
            if (!IsValid) return;
            order = new int[TriangleCount]; for (int i = 0; i < order.Length; i++) order[i] = i;
            var result = new List<Node>(TriangleCount / 3);
            BuildNode(result, 0, order.Length);
            nodes = result.ToArray();
        }
        int BuildNode(List<Node> result, int first, int count)
        {
            int index = result.Count; result.Add(default);
            Bounds bounds = TriangleBounds(order[first]);
            for (int i = first + 1; i < first + count; i++) bounds.Encapsulate(TriangleBounds(order[i]));
            if (count <= 8) { result[index] = new Node { bounds = bounds, first = first, count = count }; return index; }
            Vector3 size = bounds.size;
            int axis = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
            Array.Sort(order, first, count, Comparer<int>.Create((a, b) => TriangleCenter(a)[axis].CompareTo(TriangleCenter(b)[axis])));
            int left = BuildNode(result, first, count / 2), right = BuildNode(result, first + count / 2, count - count / 2);
            result[index] = new Node { bounds = bounds, left = left, right = right };
            return index;
        }
        Bounds TriangleBounds(int triangle)
        {
            int first = triangle * 3;
            var b = new Bounds(vertices[triangles[first]], Vector3.zero);
            b.Encapsulate(vertices[triangles[first + 1]]); b.Encapsulate(vertices[triangles[first + 2]]);
            b.Expand(.000002f); return b;
        }
        Vector3 TriangleCenter(int triangle)
        { int i = triangle * 3; return (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3; }

        public bool Raycast(Matrix4x4 localToWorld, Ray worldRay, float maximumDistance, out DropletSurfaceHit hit)
        {
            hit = default;
            if (!IsValid || maximumDistance <= 0 || worldRay.direction.sqrMagnitude < .000001f) return false;
            if (nodes == null) BuildTree();
            Matrix4x4 inverse = localToWorld.inverse;
            Vector3 origin = inverse.MultiplyPoint3x4(worldRay.origin);
            Vector3 worldDirection = worldRay.direction.normalized, direction = inverse.MultiplyVector(worldDirection);
            if (!LaserGeometryMath.RayBounds(origin, direction, localBounds, maximumDistance)) return false;
            int depth = 1; traversal[0] = 0; float closest = maximumDistance; bool found = false;
            while (depth > 0)
            {
                Node node = nodes[traversal[--depth]];
                if (!LaserGeometryMath.RayBounds(origin, direction, node.bounds, closest)) continue;
                if (node.count == 0) { traversal[depth++] = node.left; traversal[depth++] = node.right; continue; }
                for (int i = node.first; i < node.first + node.count; i++)
                {
                    int triangle = order[i], ti = triangle * 3;
                    int a = triangles[ti], b = triangles[ti + 1], c = triangles[ti + 2];
                    if (!LaserGeometryMath.RayTriangle(origin, direction, vertices[a], vertices[b], vertices[c], out float distance, out Vector3 barycentric)
                        || distance > closest) continue;
                    Vector3 normal = (normals[a] * barycentric.x + normals[b] * barycentric.y + normals[c] * barycentric.z).normalized;
                    Vector3 worldNormal = inverse.transpose.MultiplyVector(normal).normalized;
                    // Only the incoming outer face participates in the one-bounce reflection.
                    if (Vector3.Dot(worldDirection, worldNormal) > .00001f) continue;
                    closest = distance; found = true;
                    hit = new DropletSurfaceHit { distance = distance, triangle = triangle, localPoint = origin + direction * distance,
                        localNormal = normal, point = worldRay.origin + worldDirection * distance, normal = worldNormal };
                }
            }
            return found;
        }
    }
}
