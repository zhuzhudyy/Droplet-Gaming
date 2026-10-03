using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DropletPrototype;

// Execute with Pipeline run_script after entering Play in the saved full scene.
// Observe real ordinary flight only; no event injection or camera control.
public static class CaptureCinematicVisual
{
    static MissionController mission;
    static ShotDirector shots;
    static string folder;
    static float oldVolume, end, shotStart;
    static long eventId;
    static int stage, count;
    static readonly List<string> records = new List<string>();
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play first.");
        mission=UnityEngine.Object.FindAnyObjectByType<MissionController>();
        shots=UnityEngine.Object.FindAnyObjectByType<ShotDirector>();
        if(mission==null || mission.TotalCount!=2000 || !mission.gameObject.scene.path.EndsWith("FleetAssault_CinematicAudio_Cubic.unity"))
            throw new InvalidOperationException("Only the full saved cinematic scene is allowed.");
        folder="docs/verification/CinematicAudio-20260919/EditorVisual";
        Directory.CreateDirectory(folder);
        oldVolume=AudioListener.volume;AudioListener.volume=0; // The independent player is verifying audible output.
        Application.runInBackground=true;mission.RestartIntoCombat();
        end=Time.realtimeSinceStartup+100;
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=State;
        return "Observing full-fleet normal 300 UU/s flight for 100 seconds; Editor audio temporarily muted to avoid overlapping the standalone opening.";
    }
    static void State(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying || mission==null || shots==null || Time.realtimeSinceStartup>=end){Finish();return;}
        if(!shots.Active)return;
        if(eventId!=shots.LastShotEventId){eventId=shots.LastShotEventId;shotStart=Time.realtimeSinceStartup;stage=0;}
        if(Time.realtimeSinceStartup-shotStart < .16f+stage*.2f || stage>=4)return;
        string file="shot-"+(count++).ToString("000")+"-"+shots.CurrentKind+"-frame"+stage+".png";
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,file));
        records.Add(file+","+mission.combat.SimulatedTime+","+eventId);
        stage++;
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;
        AudioListener.volume=oldVolume;
        File.WriteAllLines(Path.Combine(folder,"captures.csv"),records);
        File.WriteAllText(Path.Combine(folder,"completed.txt"),"Observed actual complete 2000-ship scene. Temporary Editor volume restored. No listening claim.");
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
}
