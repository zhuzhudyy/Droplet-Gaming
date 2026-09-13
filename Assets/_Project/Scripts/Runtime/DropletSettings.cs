using UnityEngine;

namespace DropletPrototype
{
    [CreateAssetMenu(menuName = "DropletPrototype/Flight and Mission Settings")]
    public sealed class DropletSettings : ScriptableObject
    {
        [Header("Optional narrative combat scale (legacy scenes use one metre per unit)")]
        public CombatScaleSettings worldScale;
        public float MetersPerUnit => worldScale != null ? worldScale.metersPerUnityUnit : 1f;
        public float ToMeters(float units) => units * MetersPerUnit;
        public void ApplyWorldScale()
        {
            if (worldScale == null) return;
            initialSpeed = worldScale.MetersToUnits(worldScale.cruiseMetersPerSecond);
            maxCruiseSpeed = initialSpeed;
            boostMultiplier = worldScale.sprintMetersPerSecond / worldScale.cruiseMetersPerSecond;
            acceleration = worldScale.MetersToUnits(worldScale.accelerationMetersPerSecondSquared);
            brakeStrength = worldScale.MetersToUnits(worldScale.brakeMetersPerSecondSquared);
            speedAdjustment = worldScale.MetersToUnits(worldScale.speedAdjustmentMetersPerSecondSquared);
            strafeSpeed = worldScale.MetersToUnits(worldScale.strafeMetersPerSecond);
            boundaryRadius = worldScale.MetersToUnits(worldScale.arenaBoundaryMeters);
            boundaryWarningRadius = boundaryRadius * .91f;
        }
        [Header("Flight")]
        [Min(0)] public float initialSpeed = 18;
        [Min(1)] public float maxCruiseSpeed = 40;
        [Min(1)] public float speedAdjustment = 18;
        [Min(1)] public float acceleration = 45;
        [Min(1)] public float boostMultiplier = 3;
        [Min(1)] public float brakeStrength = 90;
        [Min(0)] public float strafeSpeed = 12;
        [Min(.01f)] public float mouseSensitivity = .14f;
        [Min(1)] public float turnDegreesPerSecond = 150;
        [Range(10, 85)] public float pitchLimit = 75;
        public bool invertY;
        [Header("Camera")]
        public float cameraDistance = 13;
        public float cameraHeight = 4;
        public float cameraSmoothing = 12;
        public float fieldOfView = 65;
        [Header("Hit queries")]
        [Min(.01f)] public float hitRadius = .7f;
        public LayerMask targetLayers = 1 << 8;
        [Header("Mission")]
        [Min(.01f)] public float missionSeconds = 120;
        public float boundaryWarningRadius = 230;
        public float boundaryRadius = 280;
        public int baseScore = 100;
        public float comboWindow = 4;
        public int maxMultiplier = 5;
        public int timeBonusPerSecond = 10;
    }
}
