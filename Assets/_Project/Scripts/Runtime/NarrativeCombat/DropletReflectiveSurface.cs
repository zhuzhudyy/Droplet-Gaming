using UnityEngine;

namespace DropletPrototype
{
    [DisallowMultipleComponent]
    public sealed class DropletReflectiveSurface : MonoBehaviour
    {
        public DropletReflectionGeometry geometry;
        public Transform authoritativePose;
        public Transform presentedMesh;
        [SerializeField] Matrix4x4 meshToPose = Matrix4x4.identity;
        public Matrix4x4 SurfaceMatrix => authoritativePose != null ? authoritativePose.localToWorldMatrix * meshToPose : transform.localToWorldMatrix;
        public Vector3 AimPoint => SurfaceMatrix.MultiplyPoint3x4(geometry != null ? geometry.localBounds.center : Vector3.zero);
        public void Configure(Transform mesh, Transform motorRoot, DropletReflectionGeometry bakedGeometry)
        {
            presentedMesh = mesh; authoritativePose = motorRoot; geometry = bakedGeometry;
            meshToPose = motorRoot.worldToLocalMatrix * mesh.localToWorldMatrix;
        }
        public bool Raycast(Ray ray, float range, out DropletSurfaceHit hit)
        { hit = default; return geometry != null && geometry.Raycast(SurfaceMatrix, ray, range, out hit); }
    }
}
