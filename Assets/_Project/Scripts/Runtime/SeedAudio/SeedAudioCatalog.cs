using System;
using UnityEngine;

namespace DropletPrototype
{
    [Serializable]
    public sealed class SeedAudioCue
    {
        public string id;
        public string family;
        public string category;
        public AudioClip clip;
        public bool loop;
        [Range(0f, 2f)] public float gain = 1f;
        [Range(0, 256)] public int priority = 128;
        [Min(0f)] public float cooldownSeconds;
        public string sourceRequestId;
        public string sourceHash;
    }

    /// <summary>Imported Seed recordings. A missing recording never falls back to a synthetic clip.</summary>
    [CreateAssetMenu(menuName = "DropletPrototype/Seed Audio Catalog")]
    public sealed class SeedAudioCatalog : ScriptableObject
    {
        public SeedAudioCue[] cues = Array.Empty<SeedAudioCue>();

        public bool TryGet(string id, out SeedAudioCue cue)
        {
            if (cues != null && !string.IsNullOrEmpty(id))
            {
                foreach (var candidate in cues)
                    if (candidate != null && candidate.clip != null &&
                        string.Equals(candidate.id, id, StringComparison.Ordinal))
                    { cue = candidate; return true; }
            }
            cue = null;
            return false;
        }

        public bool TryNext(string family, ref int cursor, out SeedAudioCue cue)
        {
            int count = 0;
            if (cues != null)
                foreach (var candidate in cues)
                    if (candidate != null && candidate.clip != null &&
                        string.Equals(candidate.family, family, StringComparison.Ordinal)) count++;
            if (count == 0) { cue = null; return false; }
            int selected = (cursor & int.MaxValue) % count;
            cursor = (cursor + 1) & int.MaxValue;
            foreach (var candidate in cues)
                if (candidate != null && candidate.clip != null &&
                    string.Equals(candidate.family, family, StringComparison.Ordinal) && selected-- == 0)
                { cue = candidate; return true; }
            cue = null;
            return false;
        }

        public int MissingClipCount()
        {
            if (cues == null) return 0;
            int missing = 0;
            foreach (var cue in cues) if (cue == null || cue.clip == null) missing++;
            return missing;
        }
    }
}
