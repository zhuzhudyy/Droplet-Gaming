using UnityEngine;

namespace DropletPrototype
{
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerOptions : MonoBehaviour
    {
        public MissionController mission;
        public float Sensitivity { get; private set; } = 1;
        public bool InvertY { get; private set; }
        public float FieldOfView { get; private set; } = 65;
        public float Volume { get; private set; } = .65f;
        public bool ReducedMotion { get; private set; } = true;
        DropletSettings runtimeSettings;
        float baseSensitivity;
        void Awake()
        {
            if (mission == null || mission.settings == null) return;
            runtimeSettings = Instantiate(mission.settings); baseSensitivity = runtimeSettings.mouseSensitivity;
            mission.settings = runtimeSettings; mission.motor.settings = runtimeSettings;
            mission.motor.hitDetector.settings = runtimeSettings; mission.score.settings = runtimeSettings; mission.chaseCamera.settings = runtimeSettings;
            Apply(PlayerPrefs.GetFloat("Droplet.Sensitivity", 1), PlayerPrefs.GetInt("Droplet.InvertY", 0) != 0,
                PlayerPrefs.GetFloat("Droplet.FOV", runtimeSettings.fieldOfView), PlayerPrefs.GetFloat("Droplet.Volume", .65f),
                PlayerPrefs.GetInt("Droplet.ReducedMotion", 1) != 0, false);
        }
        public void Apply(float sensitivity, bool invertY, float fov, float volume, bool reducedMotion, bool save = true)
        {
            Sensitivity = Mathf.Clamp(sensitivity, .35f, 2.5f); InvertY = invertY; FieldOfView = Mathf.Clamp(fov, 50, 90);
            Volume = Mathf.Clamp01(volume); ReducedMotion = reducedMotion;
            if (runtimeSettings != null) { runtimeSettings.mouseSensitivity = baseSensitivity * Sensitivity; runtimeSettings.invertY = InvertY; runtimeSettings.fieldOfView = FieldOfView; }
            if (mission != null && mission.chaseCamera != null) mission.chaseCamera.ReducedMotion = ReducedMotion;
            AudioListener.volume = Volume;
            if (!save) return;
            PlayerPrefs.SetFloat("Droplet.Sensitivity", Sensitivity); PlayerPrefs.SetInt("Droplet.InvertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("Droplet.FOV", FieldOfView); PlayerPrefs.SetFloat("Droplet.Volume", Volume);
            PlayerPrefs.SetInt("Droplet.ReducedMotion", ReducedMotion ? 1 : 0);
        }
        void OnDestroy() { if (runtimeSettings != null) Destroy(runtimeSettings); }
        void OnApplicationQuit() { PlayerPrefs.Save(); }
    }
}
