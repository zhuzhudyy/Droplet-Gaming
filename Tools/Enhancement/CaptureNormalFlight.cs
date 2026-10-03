// Run via Unity eval_file while the saved Enhanced scene is in Play.
// Captures normal FixedUpdate flight; never drives target state or damage.
var mission = UnityEngine.Object.FindAnyObjectByType<DropletPrototype.MissionController>();
var oldKeyboard = UnityEngine.InputSystem.Keyboard.current;
var oldMouse = UnityEngine.InputSystem.Mouse.current;
if(oldKeyboard!=null) UnityEngine.InputSystem.InputSystem.DisableDevice(oldKeyboard);
if(oldMouse!=null) UnityEngine.InputSystem.InputSystem.DisableDevice(oldMouse);
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();keyboard.MakeCurrent();
var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();mouse.MakeCurrent();
mission.RestartIntoCombat();UnityEngine.Application.runInBackground=true;
var directory=System.IO.Path.GetFullPath("docs/verification/Enhancement-20260919/NormalFlightFinal");
System.IO.Directory.CreateDirectory(directory);
double begin=UnityEditor.EditorApplication.timeSinceStartup,lastShot=begin;int frame=0;
var records=new System.Collections.Generic.List<string>{"frame,wallSeconds,simulationSeconds,speedUU,destroyed,pending"};
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
    double now=UnityEditor.EditorApplication.timeSinceStartup;
    if(!UnityEditor.EditorApplication.isPlaying||now-begin>18){
        UnityEditor.EditorApplication.update-=tick;
        System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,"frames.csv"),records);
        UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        if(oldMouse!=null){UnityEngine.InputSystem.InputSystem.EnableDevice(oldMouse);oldMouse.MakeCurrent();}
        if(oldKeyboard!=null){UnityEngine.InputSystem.InputSystem.EnableDevice(oldKeyboard);oldKeyboard.MakeCurrent();}
        return;
    }
    if(now-lastShot>.15){lastShot=now;
        UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory,"frame-"+frame.ToString("000")+".png"));
        records.Add(frame+","+(now-begin)+","+mission.combat.SimulatedTime+","+mission.motor.Speed+","+mission.DestroyedCount+","+mission.PendingCount);frame++;
    }
};
UnityEditor.EditorApplication.update+=tick;
UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
return "18 second normal flight capture scheduled; no scripted FireRay, damage, flee, camera or motor pose changes.";
