using System;
using UnityEngine;

namespace DropletPrototype
{
    [Serializable] public sealed class RadioCaption
    {
        public float time, duration;
        [TextArea] public string text, english;
        public string role;
    }
    [Serializable] public sealed class RadioLine
    {
        public string id, eventKind, channel, role;
        [TextArea] public string text;
        [TextArea] public string english;
        public float weight = 1;
        public bool pendingOnly, important;
        public AudioClip voice;
        public AudioClip lowIntensityVoice, highIntensityVoice, shortVoice;
        public bool premixed, evacuationOnly;
        [Range(0,1)] public float minimumIntensity, maximumIntensity = 1;
        public RadioCaption[] captions = Array.Empty<RadioCaption>();
    }
    [Serializable] public sealed class NarrativeLine
    {
        public string id, channel, role;
        [TextArea] public string text;
        [TextArea] public string english;
        public float time, duration = 5;
        public int cameraShot, stage;
        public AudioClip voice;
        public bool premixed;
        public RadioCaption[] captions = Array.Empty<RadioCaption>();
    }
    [Serializable] public sealed class RadioContent
    {
        public RadioLine[] combat;
        public NarrativeLine[] narrative;
        public RadioLine[] sceneExtras;
    }
    [CreateAssetMenu(menuName = "Droplet/Narrative Combat/Radio Library")]
    public sealed class RadioLibrary : ScriptableObject
    {
        public RadioLine[] combat = Array.Empty<RadioLine>();
        public NarrativeLine[] narrative = Array.Empty<NarrativeLine>();
        [Min(1)] public int queueLimit = 6;
        [Min(.1f)] public float globalCooldown = 1.1f;
        [Min(.1f)] public float eventExpiry = 7;
        [Min(1)] public int recentLineLimit = 8;
        [Min(.1f)] public float speakerEventCooldown = 12;
        [Min(1)] public int historyLimit = 40;
        [Range(1, 48)] public int eventBudgetPerFrame = 24;
        public AudioClip connectTone, interruptTone;
        public AudioClip alarm, equipmentBed;
        public bool premixedSceneAudio;
        public string voiceProvenance = "Microsoft Huihui Desktop / installed offline zh-CN synthesis";
    }
}
