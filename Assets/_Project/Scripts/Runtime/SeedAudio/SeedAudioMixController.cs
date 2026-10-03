using UnityEngine;

namespace DropletPrototype
{
    public enum SeedAudioBus { Music, Ambience, Flight, Combat, Communications, Ui, Mission }

    /// <summary>One source of category gain and radio ducking for the new Seed beds.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class SeedAudioMixController : MonoBehaviour
    {
        const string PreferencePrefix = "Droplet.SeedAudio.";
        public MissionController mission;
        public RadioController radio;
        [Range(0f, 1f)] public float musicVolume = .75f;
        [Range(0f, 1f)] public float ambienceVolume = .55f;
        [Range(0f, 1f)] public float flightVolume = .72f;
        [Range(0f, 1f)] public float combatVolume = .8f;
        [Range(0f, 1f)] public float communicationsVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = .8f;
        [Range(0f, 1f)] public float missionVolume = .9f;
        [Range(0f, 1f)] public float musicWhileBroadcasting = .42f;
        [Range(0f, 1f)] public float ambienceWhileBroadcasting = .5f;
        [Range(0f, 1f)] public float flightWhileBroadcasting = .68f;
        [Range(0f, 1f)] public float combatWhileBroadcasting = .72f;
        [Min(.01f)] public float duckAttackSeconds = .12f;
        [Min(.01f)] public float duckReleaseSeconds = .45f;

        float broadcastBlend;
        MissionController boundMission;
        public float MusicGain => Mathf.Clamp01(musicVolume) * Mathf.Lerp(1f, musicWhileBroadcasting, broadcastBlend);
        public float AmbienceGain => Mathf.Clamp01(ambienceVolume) * Mathf.Lerp(1f, ambienceWhileBroadcasting, broadcastBlend);
        public float FlightGain => Mathf.Clamp01(flightVolume) * Mathf.Lerp(1f, flightWhileBroadcasting, broadcastBlend);
        public float CombatGain => Mathf.Clamp01(combatVolume) * Mathf.Lerp(1f, combatWhileBroadcasting, broadcastBlend);
        public float CommunicationsGain => Mathf.Clamp01(communicationsVolume);
        public float UiGain => Mathf.Clamp01(uiVolume);
        public float MissionGain => Mathf.Clamp01(missionVolume);
        public bool IsDucking => broadcastBlend > .01f;

        void Awake()
        {
            foreach (SeedAudioBus bus in System.Enum.GetValues(typeof(SeedAudioBus)))
            {
                float saved = PlayerPrefs.GetFloat(PreferencePrefix + bus, GetUserVolume(bus));
                SetUserVolume(bus, saved, false);
            }
        }

        public float GetUserVolume(SeedAudioBus bus)
        {
            switch (bus)
            {
                case SeedAudioBus.Music: return musicVolume;
                case SeedAudioBus.Ambience: return ambienceVolume;
                case SeedAudioBus.Flight: return flightVolume;
                case SeedAudioBus.Combat: return combatVolume;
                case SeedAudioBus.Communications: return communicationsVolume;
                case SeedAudioBus.Ui: return uiVolume;
                case SeedAudioBus.Mission: return missionVolume;
                default: return 1f;
            }
        }

        public void SetUserVolume(SeedAudioBus bus, float value, bool save = true)
        {
            value = Mathf.Clamp01(value);
            switch (bus)
            {
                case SeedAudioBus.Music: musicVolume = value; break;
                case SeedAudioBus.Ambience: ambienceVolume = value; break;
                case SeedAudioBus.Flight: flightVolume = value; break;
                case SeedAudioBus.Combat: combatVolume = value; break;
                case SeedAudioBus.Communications: communicationsVolume = value; break;
                case SeedAudioBus.Ui: uiVolume = value; break;
                case SeedAudioBus.Mission: missionVolume = value; break;
                default: return;
            }
            if (save) PlayerPrefs.SetFloat(PreferencePrefix + bus, value);
        }

        public void ResetUserVolumes()
        {
            SetUserVolume(SeedAudioBus.Music, .75f);
            SetUserVolume(SeedAudioBus.Ambience, .55f);
            SetUserVolume(SeedAudioBus.Flight, .72f);
            SetUserVolume(SeedAudioBus.Combat, .8f);
            SetUserVolume(SeedAudioBus.Communications, 1f);
            SetUserVolume(SeedAudioBus.Ui, .8f);
            SetUserVolume(SeedAudioBus.Mission, .9f);
        }

        void OnEnable()
        {
            broadcastBlend = radio != null && radio.IsBroadcasting ? 1f : 0f;
            if (boundMission != null) boundMission.Restarted -= ResetMix;
            boundMission = mission;
            if (boundMission != null) boundMission.Restarted += ResetMix;
        }

        void OnDisable()
        {
            if (boundMission != null) boundMission.Restarted -= ResetMix;
            boundMission = null;
            ResetMix();
        }

        void Update()
        {
            bool broadcasting = radio != null && radio.IsBroadcasting;
            float target = broadcasting ? 1f : 0f;
            float response = broadcasting ? duckAttackSeconds : duckReleaseSeconds;
            broadcastBlend = Mathf.MoveTowards(broadcastBlend, target,
                Time.unscaledDeltaTime / Mathf.Max(.01f, response));
        }

        public void ResetMix()
        { broadcastBlend = 0f; }
    }
}
