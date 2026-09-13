using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
namespace DropletPrototype.Editor
{
    public static class Fleet2000Commands
    {
        [MenuItem("DropletPrototype/Fleet 2000 Sun/3 Start Rendered Validation")]
        public static void Validate()
        {
            Fleet2000Pipeline.Require(Application.isPlaying&&!EditorApplication.isPaused,"Enter Play first");
            var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
            var old=UnityEngine.Object.FindAnyObjectByType<Fleet2000ValidationRunner>();
            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var runner=new GameObject("__TemporaryFleet2000Validation").AddComponent<Fleet2000ValidationRunner>();runner.Begin(Fleet2000Pipeline.Evidence+"Editor","Final");
        }
        [MenuItem("DropletPrototype/Fleet 2000 Sun/4 Validation Status")]
        public static void Status()
        {var r=UnityEngine.Object.FindAnyObjectByType<Fleet2000ValidationRunner>();if(r){r.WriteProgress();Debug.Log(JsonUtility.ToJson(r.GetProgress()));}}
        [MenuItem("DropletPrototype/Fleet 2000 Sun/5 Set Panorama In Play")]
        public static void Panorama()
        {
            Fleet2000Pipeline.Require(Application.isPlaying,"Play required");var m=UnityEngine.Object.FindAnyObjectByType<MissionController>();
            m.Restart();m.StartMission();m.enabled=false;m.chaseCamera.enabled=false;
            var eye=new Vector3(-6200,1000,1800);m.chaseCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(500,65,2400)-eye));
            UnityEngine.Object.FindAnyObjectByType<FleetRenderManager>().RefreshNow();
        }
        static int startFrame;static double started;static bool oldEnabled,oldBinary;static string oldLog;
        [MenuItem("DropletPrototype/Fleet 2000 Sun/8 Capture Unity Profiler")]
        public static void Profile()
        {
            Fleet2000Pipeline.Require(Application.isPlaying&&!Profiler.enabled,"Play required; an existing Profiler recording is protected");
            Directory.CreateDirectory(Fleet2000Pipeline.Evidence);oldEnabled=Profiler.enabled;oldBinary=Profiler.enableBinaryLog;oldLog=Profiler.logFile;
            Profiler.logFile=Path.GetFullPath(Fleet2000Pipeline.Evidence+"UnityProfiler-Panorama.raw");Profiler.enableBinaryLog=true;Profiler.enabled=true;
            startFrame=Time.frameCount;started=EditorApplication.timeSinceStartup;EditorApplication.update+=EndProfile;
        }
        static void EndProfile()
        {
            if(Time.frameCount-startFrame<240&&EditorApplication.timeSinceStartup-started<30)return;
            EditorApplication.update-=EndProfile;Profiler.enabled=false;Profiler.enableBinaryLog=oldBinary;Profiler.logFile=oldLog;Profiler.enabled=oldEnabled;
            File.WriteAllText(Fleet2000Pipeline.Evidence+"profiler-capture.txt","Actual Unity Profiler binary capture; panorama Game view. Frames="+(Time.frameCount-startFrame)+"; wall seconds="+(EditorApplication.timeSinceStartup-started)+"; Unity="+Application.unityVersion+". Editor trace, not standalone GPU time.");Debug.Log("FLEET2000 Unity Profiler capture completed");
        }
        [MenuItem("DropletPrototype/Fleet 2000 Sun/9 Stop Play")]
        public static void Stop()=>EditorApplication.isPlaying=false;
    }
}
