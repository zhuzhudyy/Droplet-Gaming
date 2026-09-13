using System;
using System.Threading.Tasks;
using DropletPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LiveInputSmoke
{
    public static string LastResult = "Not run";
    public static void Begin() { LastResult = "Running"; RunAndRecord(); }
    static async void RunAndRecord()
    {
        try { LastResult = await Run(); }
        catch (Exception ex) { LastResult = "FAIL: " + ex; }
        UnityEngine.Debug.Log(LastResult);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../docs/verification/G04-live-input.txt"), LastResult);
    }
    public static async Task<string> Run()
    {
        var mission = UnityEngine.Object.FindAnyObjectByType<MissionController>();
        if (!Application.isPlaying || mission == null) throw new Exception("Open TestRange in Play mode first.");
        var keyboard = InputSystem.AddDevice<Keyboard>("SmokeKeyboard");
        var mouse = InputSystem.AddDevice<Mouse>("SmokeMouse");
        try
        {
            mission.Restart();
            await Press(keyboard, Key.Enter);
            if (mission.State != MissionState.Playing) throw new Exception("Enter did not start the mission.");
            var start = mission.motor.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            await Task.Delay(900);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(600);
            if (mission.motor.Speed <= mission.settings.initialSpeed || Vector3.Distance(start, mission.motor.transform.position) < 2)
                throw new Exception("Live W/Shift did not accelerate and move.");
            if (mission.DestroyedCount < 1 || mission.score.Score < 100) throw new Exception("Forward live flight did not hit and score the first ship.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../docs/verification/G04-playing.png"));
            await Task.Delay(150);
            await Press(keyboard, Key.Escape);
            if (mission.State != MissionState.Paused) throw new Exception("Esc did not pause.");
            float time = mission.Remaining; var position = mission.motor.transform.position; int score = mission.score.Score;
            await Task.Delay(250);
            if (mission.Remaining != time || mission.motor.transform.position != position || mission.score.Score != score)
                throw new Exception("Paused live state advanced.");
            if (Cursor.lockState != CursorLockMode.None) throw new Exception("Paused cursor was not released.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../docs/verification/G04-paused.png"));
            await Task.Delay(150);
            await Press(keyboard, Key.R);
            if (mission.State != MissionState.Ready || mission.DestroyedCount != 0 || mission.Remaining != 120)
                throw new Exception("R did not reset mission to Ready.");
            await Press(keyboard, Key.Enter);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Task.Delay(400);
            if (mission.motor.Speed > .01f) throw new Exception("Space did not brake to zero.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.D));
            float beforeX = mission.motor.transform.position.x; await Task.Delay(250);
            if (mission.motor.transform.position.x <= beforeX) throw new Exception("D did not strafe right.");
            float beforeYaw = mission.motor.transform.eulerAngles.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(60, 12) }); await Task.Delay(250);
            if (Mathf.Abs(Mathf.DeltaAngle(beforeYaw, mission.motor.transform.eulerAngles.y)) < 1) throw new Exception("Mouse delta did not turn the droplet.");
            if (Mathf.Abs(mission.motor.transform.right.y) > .001f) throw new Exception("Mouse steering introduced roll.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); mission.Restart();
            return "PASS: live Input System Enter -> Playing; W+Shift acceleration, real fixed-step ship penetration and score; Esc -> Paused with frozen time/position/score and released cursor; R -> Ready with 120 seconds and ten restored targets; Space stops; D strafes; mouse turns without roll. Ended in Ready. This does not establish subjective flight feel.";
        }
        finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); }
    }
    static async Task Press(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Task.Delay(150);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(150);
    }
}
