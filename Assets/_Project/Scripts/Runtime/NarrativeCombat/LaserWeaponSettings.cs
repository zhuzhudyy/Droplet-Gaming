using UnityEngine;

namespace DropletPrototype
{
    [CreateAssetMenu(menuName = "Droplet/Narrative Combat/Laser Weapon")]
    public sealed class LaserWeaponSettings : ScriptableObject
    {
        [Min(1)] public float rangeMeters = 220000;
        [Min(.1f)] public float fireIntervalSeconds = 3.5f;
        [Min(.1f)] public float aimDegreesPerSecond = 26;
        [Range(.01f, 10)] public float firingToleranceDegrees = .08f;
        [Min(.02f)] public float nearDecisionSeconds = .06f;
        [Min(.02f)] public float farDecisionSeconds = .5f;
        [Min(1)] public float nearDecisionDistanceMeters = 120000;
        [Range(1, 64)] public int decisionsPerStep = 48;
        [Range(1, 16)] public int shotsPerStep = 4;
        [Range(3, 64)] public int rayQueriesPerStep = 12;
        [Range(1, 32)] public int visualBeamBudget = 12;
        [Range(.025f, .3f)] public float beamSeconds = .18f;
        [Min(.01f)] public float beamWidthMeters = 12;
        [Min(.01f)] public float contactRadiusMeters = 14;
        [Tooltip("Visual width only; ray geometry and damage are unchanged.")]
        [Range(0, 4)] public float minimumBeamPixels = 1.8f;
        [Min(.01f)] public float maximumBeamWidthMeters = 300;
        [Range(0, 5)] public int contactParticleCount = 3;
        [Range(.025f, .2f)] public float surfaceHighlightSeconds = .09f;
        [Min(.01f)] public float surfaceHighlightRadiusMeters = 22;
        [Min(.001f)] public float reflectionOffsetMeters = .8f;
        [Min(1)] public float visualDistanceMeters = 240000;
        public bool reflectedLaserIsLethal = true;
        public bool directLaserIsLethal;
        [Range(0, 1)] public float audioVolume = .18f;
        [Min(.02f)] public float audioCooldownSeconds = .18f;
        [ColorUsage(false, true)] public Color incidentColor = new Color(.45f, 1.7f, 3.5f, 1);
        [ColorUsage(false, true)] public Color reflectedColor = new Color(3.8f, 1.25f, .3f, 1);
    }
}
