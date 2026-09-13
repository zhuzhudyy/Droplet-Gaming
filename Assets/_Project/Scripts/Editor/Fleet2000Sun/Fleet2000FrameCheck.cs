using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype.Editor
{
    // Small actual Frame Debugger path check; does not reinterpret batch index counts as GPU triangles.
    public static class Fleet2000FrameCheck
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static Type utility;static EditorWindow game,window;static int ticks;static double start;static bool owns,paused;
        [Serializable] public class Kind{public string name;public int count;}
        [Serializable] public class Report{public bool captured,srpBatcherConfigured;public int eventCount;public string scope,error;public Kind[] eventKinds;public string[] eventNames;}
        static object Call(string name,params object[] args)=>utility.GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(null,args);
        [MenuItem("DropletPrototype/Fleet 2000 Sun/7 Capture Frame Debugger")]
        public static void Begin()
        {
            Fleet2000Pipeline.Require(!owns,"An owned capture is already running.");
            utility=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility",true);
            var debugger=typeof(Camera).Assembly.GetType("UnityEngine.FrameDebugger",true);
            Fleet2000Pipeline.Require(!(bool)debugger.GetProperty("enabled",Flags).GetValue(null),"Existing Frame Debugger capture is protected.");
            game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
            window=(EditorWindow)ScriptableObject.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetTypes().First(t=>t.Name=="FrameDebuggerWindow"));window.ShowUtility();paused=EditorApplication.isPaused;
            if(Application.isPlaying)EditorApplication.isPaused=true;
            window.GetType().GetMethod("EnableFrameDebugger",Flags).Invoke(window,null);
            Call("SetEnabled",true,UnityEditorInternal.ProfilerDriver.connectedProfiler);owns=true;ticks=0;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Update;
        }
        static void Update()
        {
            try
            {
                game.Repaint();game.SendEvent(new Event{type=EventType.Repaint});window.Repaint();window.SendEvent(new Event{type=EventType.Repaint});EditorApplication.QueuePlayerLoopUpdate();
                if(++ticks<12)return;var events=(Array)Call("GetFrameEvents");
                if(events.Length==0&&EditorApplication.timeSinceStartup-start<30)return;
                var types=events.Cast<object>().Select(e=>e.GetType().GetField("m_Type",Flags).GetValue(e).ToString()).ToArray();
                var report=new Report{captured=events.Length>0,srpBatcherConfigured=GraphicsSettings.useScriptableRenderPipelineBatching,eventCount=events.Length,scope="Actual Unity Frame Debugger event list for the current GameView, all passes. Event categories establish render path only. No GPU time or submitted-triangle total inferred from these events.",eventKinds=types.GroupBy(x=>x).Select(g=>new Kind{name=g.Key,count=g.Count()}).ToArray(),eventNames=Enumerable.Range(0,events.Length).Select(i=>Call("GetFrameEventInfoName",i) as string).ToArray()};
                File.WriteAllText(Fleet2000Pipeline.Evidence+"frame-render-path.json",JsonUtility.ToJson(report,true));Debug.Log("FLEET2000 actual frame path captured: "+report.eventCount);Finish();
            }
            catch(Exception ex){File.WriteAllText(Fleet2000Pipeline.Evidence+"frame-render-path.json",JsonUtility.ToJson(new Report{error=ex.ToString()},true));Finish();}
        }
        static void Finish(){EditorApplication.update-=Update;if(owns){Call("SetEnabled",false,UnityEditorInternal.ProfilerDriver.connectedProfiler);owns=false;}if(window!=null)window.Close();EditorApplication.isPaused=paused;}
    }
}
