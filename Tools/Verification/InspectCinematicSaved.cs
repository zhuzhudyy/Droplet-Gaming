using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using DropletPrototype;

public static class InspectCinematicSaved
{
    [Serializable] public sealed class Record
    {
        public string scene, mixer, timeline;
        public string[] buildScenes;
        public int targets, identities, listeners;
        public float listenerVolume;
        public bool dirty, stopped;
    }
    public static string Main()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || !scene.path.EndsWith("FleetAssault_CinematicAudio_Cubic.unity"))
            throw new InvalidOperationException("Stop in the saved cinematic scene first.");
        var mission=UnityEngine.Object.FindAnyObjectByType<MissionController>();
        var record=new Record { scene=scene.path,dirty=scene.isDirty,stopped=true,targets=mission.targets.Length,
            identities=mission.targets.Select(t=>t.targetId).Distinct().Count(),
            listeners=scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<AudioListener>(true).Count(a=>a.enabled)),
            listenerVolume=AudioListener.volume,buildScenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            mixer=AssetDatabase.GetAssetPath(mission.narrative.radio.voiceSource.outputAudioMixerGroup.audioMixer),
            timeline=AssetDatabase.GetAssetPath(mission.narrative.GetComponent<UnityEngine.Playables.PlayableDirector>().playableAsset)};
        if(record.dirty || record.targets!=2000 || record.identities!=2000 || record.listeners!=1 || record.listenerVolume<=0 ||
            record.buildScenes.Length!=1 || record.buildScenes[0]!=scene.path)throw new InvalidOperationException(JsonUtility.ToJson(record));
        string json=JsonUtility.ToJson(record,true);
        File.WriteAllText("docs/verification/CinematicAudio-20260919/saved-scene-final.json",json);
        return json;
    }
}
