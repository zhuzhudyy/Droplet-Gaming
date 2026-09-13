using UnityEngine;

namespace DropletPrototype
{
    [CreateAssetMenu(menuName="DropletPrototype/Lighting Quality Profile")]
    public sealed class LightingQualityProfile : ScriptableObject
    {
        [Min(.1f)] public float sunIntensity=2.15f;
        public Color sunColor=new Color(1f,.945f,.84f);
        [Range(0,1)] public float ambientStrength=.075f;
        [Range(1,4)] public float solarDisplayMultiplier=2;
        [Range(1,16)] public float earthDisplayMultiplier=1;
        [Range(0,1)] public float bloomIntensity=.16f;
        [Min(1)] public float probeInterval=3;
        [Min(1)] public float probeMoveDistance=25;
    }
}
