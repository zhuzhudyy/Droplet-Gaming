using UnityEngine;

namespace DropletPrototype
{
    public static class LaserGeometryMath
    {
        public static Vector3 ReflectDirection(Vector3 incoming, Vector3 surfaceNormal)
            => Vector3.Reflect(incoming.normalized, surfaceNormal.normalized).normalized;

        public static Vector3 OffsetReflectionOrigin(Vector3 point, Vector3 normal, Vector3 reflected, float offset)
        {
            var outward = normal.normalized;
            if (Vector3.Dot(outward, reflected) < 0) outward = -outward;
            return point + outward * Mathf.Max(.00001f, offset) + reflected.normalized * Mathf.Max(.00001f, offset);
        }

        // The local direction deliberately need not have length one. A world ray
        // transformed by worldToLocal retains its WORLD-distance parameter t.
        public static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
            out float distance, out Vector3 barycentric)
        {
            distance = 0; barycentric = default;
            Vector3 ab = b - a, ac = c - a, cross = Vector3.Cross(direction, ac);
            float determinant = Vector3.Dot(ab, cross);
            if (Mathf.Abs(determinant) < 1e-10f) return false;
            float inverse = 1 / determinant;
            Vector3 relative = origin - a;
            float u = Vector3.Dot(relative, cross) * inverse;
            if (u < -.000001f || u > 1.000001f) return false;
            Vector3 q = Vector3.Cross(relative, ab);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < -.000001f || u + v > 1.000001f) return false;
            distance = Vector3.Dot(ac, q) * inverse;
            barycentric = new Vector3(1 - u - v, u, v);
            return distance >= .000001f;
        }

        public static bool RayBounds(Vector3 origin, Vector3 direction, Bounds bounds, float maxDistance)
        {
            float lower = 0, upper = maxDistance;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-12f)
                { if (origin[axis] < bounds.min[axis] || origin[axis] > bounds.max[axis]) return false; continue; }
                float a = (bounds.min[axis] - origin[axis]) / direction[axis];
                float b = (bounds.max[axis] - origin[axis]) / direction[axis];
                if (a > b) { float swap = a; a = b; b = swap; }
                lower = Mathf.Max(lower, a); upper = Mathf.Min(upper, b);
                if (lower > upper) return false;
            }
            return upper >= 0;
        }
    }
}
