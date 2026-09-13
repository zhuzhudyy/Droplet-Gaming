using UnityEngine;

namespace DropletPrototype
{
    [CreateAssetMenu(menuName = "Droplet/Narrative Combat/Scale and Fleet")]
    public sealed class CombatScaleSettings : ScriptableObject
    {
        [Min(.001f)] public float metersPerUnityUnit = 100;
        [Header("Game scale: unchanged 57.564 UU hull = 5.7564 km; 2.4 UU droplet = 240 m")]
        public float cruiseMetersPerSecond = 30000;
        public float sprintMetersPerSecond = 150000;
        public float accelerationMetersPerSecondSquared = 60000;
        public float brakeMetersPerSecondSquared = 150000;
        public float speedAdjustmentMetersPerSecondSquared = 15000;
        public float strafeMetersPerSecond = 6000;
        public Vector3 formationSpacingMeters = new Vector3(75000, 50000, 100000);
        public float fleeMinMetersPerSecond = 2000;
        public float fleeMaxMetersPerSecond = 8000;
        public float fleeAccelerationMetersPerSecondSquared = 1400;
        public float engagementMetersPerSecond = 250;
        public float engagementRangeMeters = 250000;
        public float nearDecisionRangeMeters = 200000;
        public float nearDecisionInterval = .12f;
        public float farDecisionInterval = .6f;
        public float fleeTurnDegreesPerSecond = 16;
        public float avoidanceDistanceMeters = 16000;
        public float nearbyPanicRadiusMeters = 175000;
        [Range(0, 1)] public float fleetLossRetreatFraction = .08f;
        public Vector2 reactionDelaySeconds = new Vector2(.7f, 5.5f);
        public Vector2 explosionDelaySeconds = new Vector2(2, 5);
        public float escapeRadiusMeters = 2600000;
        public float arenaBoundaryMeters = 3200000;
        public int randomSeed = 73191;
        public float spatialCellMeters = 100000;
        public float MetersToUnits(float meters) => meters / Mathf.Max(.001f, metersPerUnityUnit);
        public float UnitsToMeters(float units) => units * Mathf.Max(.001f, metersPerUnityUnit);
        public Vector3 MetersToUnits(Vector3 meters) => meters / Mathf.Max(.001f, metersPerUnityUnit);
        public Vector3 UnitsToMeters(Vector3 units) => units * Mathf.Max(.001f, metersPerUnityUnit);
    }
}
