using UnityEngine;

namespace DropletPrototype
{
    public enum EffectQuality { Off, Low, High }

    [CreateAssetMenu(menuName = "Droplet/Effect settings")]
    public sealed class EffectSettings : ScriptableObject
    {
        public EffectQuality initialQuality = EffectQuality.High;
        [Header("Hard concurrency budgets (prewarmed)")]
        [Range(0, 32)] public int highEffectCapacity = 16;
        [Range(0, 32)] public int lowEffectCapacity = 8;
        [Range(0, 12)] public int highAudioCapacity = 8;
        [Range(0, 12)] public int lowAudioCapacity = 4;
        [Header("Impact")]
        [Min(.1f)] public float wreckLifetime = 4.2f;
        [Min(.01f)] public float flashLifetime = .14f;
        [Min(.01f)] public float sparkLifetime = .32f;
        [Min(0)] public float outwardSpeed = 3.5f;
        [Min(0)] public float directionalSpeed = 5f;
        [Min(0)] public float tumbleDegreesPerSecond = 12f;
        [Min(0)] public float flashSize = 2.8f;
        [Range(0, .5f)] public float cameraImpulse = .1f;
        public bool cameraFeedback = true;
        [Header("Sound")]
        [Range(0, 1)] public float masterVolume = .55f;
        [Range(0, 1)] public float impactVolume = .75f;
        [Range(0, .25f)] public float flightVolume = .025f;
        [Header("Trail")]
        public bool enableTrails = true;
        [Range(.02f, 1f)] public float trailLifetime = .28f;
        [Range(.01f, .5f)] public float trailWidth = .085f;
        [Min(0)] public float trailMinimumSpeed = 24f;
    }
}
