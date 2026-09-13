using System;
using UnityEngine;

namespace DropletPrototype
{
    // The exported JSON is the one authority shared with the Blender authoring tool.
    // Orbit radii are AU, physical radii are km, and local positions are game units.
    [Serializable]
    public sealed class SolarLayoutData
    {
        public double auKm = 149597870.7;
        public SolarBodyData[] bodies = Array.Empty<SolarBodyData>();
        public double battleRadiusAu = 2.5;
        public double battlePhaseDeg = 230;
        public double battleInclinationDeg = 2;
        public double battleAscendingNodeDeg;
        public double metersPerUnit = 1;
        public float[] localOrigin = { 0, 20, 260 };
        public float proxyNear = 14000;
        public float proxySpan = 4000;
        public float skyRadius = 22000;
        public float nearClip = .1f;
        public float farClip = 25000;
        public float fieldOfView = 65;
        public float oldBoundaryRadius = 730;
        public float boundaryRadius = 5840;
        public float warningRadius = 4880;

        public SolarBodyData FindBody(string id)
        {
            if (bodies == null) return null;
            foreach (SolarBodyData body in bodies)
                if (body != null && string.Equals(body.id, id, StringComparison.OrdinalIgnoreCase)) return body;
            return null;
        }
    }

    [Serializable]
    public sealed class SolarBodyData
    {
        public string id;
        public double semiMajorAu;
        public double phaseDeg;
        public double inclinationDeg;
        public double ascendingNodeDeg;
        public double radiusKm;
        public string assetId;
        public double readabilityMultiplier = 1;
    }

    public readonly struct SolarVector3d
    {
        public readonly double x, y, z;
        public SolarVector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public double Magnitude => Math.Sqrt(x * x + y * y + z * z);
        public bool IsFinite => SolarLayoutMath.IsFinite(x) && SolarLayoutMath.IsFinite(y) && SolarLayoutMath.IsFinite(z);
        public static SolarVector3d operator +(SolarVector3d a, SolarVector3d b) => new SolarVector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static SolarVector3d operator -(SolarVector3d a, SolarVector3d b) => new SolarVector3d(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public readonly struct SolarBodyProjection
    {
        public readonly Vector3 direction;
        public readonly Vector3 worldPosition;
        public readonly double distanceAu;
        public readonly double angularDiameterRadians;
        public readonly float proxyDistance;
        public readonly float proxyRadius;
        public SolarBodyProjection(Vector3 direction, Vector3 worldPosition, double distanceAu,
            double angularDiameterRadians, float proxyDistance, float proxyRadius)
        {
            this.direction = direction;
            this.worldPosition = worldPosition;
            this.distanceAu = distanceAu;
            this.angularDiameterRadians = angularDiameterRadians;
            this.proxyDistance = proxyDistance;
            this.proxyRadius = proxyRadius;
        }
    }

    // Pure calculations: these do not read Transform state or create scene objects.
    public static class SolarLayoutMath
    {
        const double DegToRad = Math.PI / 180;
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        // A circular orbit in XZ: phase and node run from +X toward +Z.
        // Positive inclination lifts the +Z half of the orbit toward +Y.
        // These fixed art-directed phases are not an ephemeris for a real date.
        public static SolarVector3d OrbitPositionAu(double radiusAu, double phaseDeg,
            double inclinationDeg = 0, double ascendingNodeDeg = 0)
        {
            double phase = phaseDeg * DegToRad;
            double inclination = inclinationDeg * DegToRad;
            double node = ascendingNodeDeg * DegToRad;
            double x = radiusAu * Math.Cos(phase);
            double z = radiusAu * Math.Sin(phase);
            double tiltedZ = z * Math.Cos(inclination);
            return new SolarVector3d(x * Math.Cos(node) - tiltedZ * Math.Sin(node),
                z * Math.Sin(inclination), x * Math.Sin(node) + tiltedZ * Math.Cos(node));
        }

        public static SolarVector3d BodyPositionAu(SolarBodyData body) => body == null
            ? new SolarVector3d(double.NaN, double.NaN, double.NaN)
            : OrbitPositionAu(body.semiMajorAu, body.phaseDeg, body.inclinationDeg, body.ascendingNodeDeg);

        public static SolarVector3d BattleAnchorAu(SolarLayoutData layout) => layout == null
            ? new SolarVector3d(double.NaN, double.NaN, double.NaN)
            : OrbitPositionAu(layout.battleRadiusAu, layout.battlePhaseDeg,
                layout.battleInclinationDeg, layout.battleAscendingNodeDeg);

        public static bool TryGetObserverAu(SolarLayoutData layout, Vector3 localPosition, out SolarVector3d observerAu)
        {
            observerAu = default;
            if (layout == null || layout.localOrigin == null || layout.localOrigin.Length != 3 ||
                !IsFinite(localPosition) || !IsFinite(layout.auKm) || layout.auKm <= 0 ||
                !IsFinite(layout.metersPerUnit) || layout.metersPerUnit <= 0 ||
                !IsFinite(layout.battleRadiusAu) || layout.battleRadiusAu < 0) return false;
            double unitsToAu = layout.metersPerUnit / 1000 / layout.auKm;
            // Convert to double before subtracting, not after a float Vector3 subtraction.
            SolarVector3d localOffset = new SolarVector3d(
                ((double)localPosition.x - layout.localOrigin[0]) * unitsToAu,
                ((double)localPosition.y - layout.localOrigin[1]) * unitsToAu,
                ((double)localPosition.z - layout.localOrigin[2]) * unitsToAu);
            observerAu = BattleAnchorAu(layout) + localOffset;
            return observerAu.IsFinite;
        }

        public static bool TryAngularDiameter(double radius, double distance, out double radians)
        {
            radians = 0;
            if (!IsFinite(radius) || !IsFinite(distance) || radius <= 0 || distance <= radius) return false;
            radians = 2 * Math.Asin(radius / distance);
            return IsFinite(radians) && radians > 0;
        }

        public static bool TryProjectBody(SolarLayoutData layout, SolarBodyData body,
            Vector3 observerPosition, out SolarBodyProjection projection)
        {
            projection = default;
            if (body == null || !TryGetObserverAu(layout, observerPosition, out SolarVector3d observerAu) ||
                !IsFinite(body.semiMajorAu) || body.semiMajorAu < 0 ||
                !IsFinite(body.readabilityMultiplier) || body.readabilityMultiplier <= 0 ||
                !IsFinite(layout.proxyNear) || layout.proxyNear <= 0 ||
                !IsFinite(layout.proxySpan) || layout.proxySpan < 0) return false;

            // Subtraction and normalization stay double precision until the final render mapping.
            SolarVector3d relative = BodyPositionAu(body) - observerAu;
            double distanceAu = relative.Magnitude;
            double distanceKm = distanceAu * layout.auKm;
            if (!relative.IsFinite || !TryAngularDiameter(body.radiusKm, distanceKm, out double physicalAngle)) return false;
            double displayRatio = body.radiusKm / distanceKm * body.readabilityMultiplier;
            if (!IsFinite(displayRatio) || displayRatio <= 0 || displayRatio >= 1) return false;

            double proxyDistance = layout.proxyNear + layout.proxySpan * (distanceAu / (distanceAu + 1));
            double proxyRadius = proxyDistance * displayRatio;
            if (!IsFinite(proxyRadius) || proxyRadius <= 0 || proxyRadius > float.MaxValue ||
                !IsFinite(proxyDistance) || proxyDistance > float.MaxValue) return false;
            Vector3 direction = new Vector3((float)(relative.x / distanceAu),
                (float)(relative.y / distanceAu), (float)(relative.z / distanceAu));
            Vector3 worldPosition = observerPosition + direction * (float)proxyDistance;
            float renderRadius = (float)proxyRadius;
            if (!IsFinite(worldPosition) || renderRadius <= 0) return false;
            projection = new SolarBodyProjection(direction, worldPosition, distanceAu,
                physicalAngle, (float)proxyDistance, renderRadius);
            return true;
        }
    }
}
