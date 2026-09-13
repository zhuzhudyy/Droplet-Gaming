using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace DropletPrototype
{
    /// <summary>Explicit read-only native-input observer. Never injects events,
    /// chooses devices, changes focus, or changes input/backend settings.</summary>
    public sealed class NarrativeNativeInputProbe : MonoBehaviour
    {
        [Serializable] sealed class Entry
        {
            public string kind, utc, scene, missionState, eventType, keys, knownKeys, pressedThisFrame, releasedThisFrame;
            public string deviceName, layout, interfaceName, product, manufacturer, deviceClass, detail;
            public double realtime, eventTime;
            public int frame, deviceId = -1, currentKeyboardId = -1, keyboardCount, bufferedRecordsDropped;
            public bool focused, native, enabled;
        }
        static readonly Key[] Tracked = { Key.Enter, Key.NumpadEnter, Key.Escape, Key.Tab, Key.N, Key.R, Key.H,
            Key.LeftShift, Key.RightShift, Key.Space, Key.W, Key.S, Key.A, Key.D };
        readonly StringBuilder buffer = new StringBuilder(16384);
        readonly Dictionary<int, int> lastRawMask = new Dictionary<int, int>();
        MissionController mission;
        string outputPath, previousSnapshot;
        double nextFlush;
        int dropped;
        bool active;
        public string OutputPath => outputPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs(); string output = null; bool requested = false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-narrative-native-input")
                { requested = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal)) output = args[++i]; break; }
            if (!requested || FindAnyObjectByType<NarrativeNativeInputProbe>() != null) return;
            new GameObject("__OptInNativeInputObserver").AddComponent<NarrativeNativeInputProbe>().Begin(output);
        }
        public void Begin(string outputDirectory)
        {
            if (active) return;
            string directory = Path.GetFullPath(string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Application.persistentDataPath, "NarrativeNativeInput") : outputDirectory);
            Directory.CreateDirectory(directory);
            outputPath = Path.Combine(directory, "native-input-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
            mission = FindAnyObjectByType<MissionController>(); active = true;
            InputSystem.onEvent += ObserveEvent; InputSystem.onDeviceChange += ObserveDeviceChange;
            var start = New("observer-start");
            start.detail = "Read-only observer; no event injection, MakeCurrent, device mutation, InputSettings change, or serial-number capture. " +
                "Unity=" + Application.unityVersion + "; inputBackground=" + InputSystem.settings.backgroundBehavior + "; runInBackground=" + Application.runInBackground;
            Record(start);
            foreach (var device in InputSystem.devices) if (device is Keyboard keyboard) Record(DeviceEntry("keyboard-inventory", keyboard));
            nextFlush = Time.realtimeSinceStartupAsDouble + 1;
            Debug.Log("NATIVE INPUT OBSERVER: " + outputPath);
        }
        Entry New(string kind)
        {
            var current = Keyboard.current; int count = 0;
            foreach (var device in InputSystem.devices) if (device is Keyboard) count++;
            return new Entry { kind = kind, utc = DateTime.UtcNow.ToString("o"), realtime = Time.realtimeSinceStartupAsDouble,
                frame = Time.frameCount, scene = SceneManager.GetActiveScene().name, focused = Application.isFocused,
                missionState = mission != null ? mission.State.ToString() : "NoMission", currentKeyboardId = current != null ? current.deviceId : -1, keyboardCount = count };
        }
        Entry DeviceEntry(string kind, Keyboard keyboard)
        {
            var entry = New(kind); entry.deviceId = keyboard.deviceId; entry.deviceName = keyboard.name; entry.layout = keyboard.layout;
            entry.native = keyboard.native; entry.enabled = keyboard.enabled;
            var description = keyboard.description;
            entry.interfaceName = description.interfaceName; entry.product = description.product;
            entry.manufacturer = description.manufacturer; entry.deviceClass = description.deviceClass;
            return entry;
        }
        void ObserveDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!active || !(device is Keyboard keyboard)) return;
            var entry = DeviceEntry("keyboard-device-change", keyboard); entry.detail = change.ToString(); Record(entry);
        }
        void ObserveEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!active || !(device is Keyboard keyboard)) return;
            int pressed = 0, known = 0;
            bool stateEvent = eventPtr.IsA<StateEvent>() || eventPtr.IsA<DeltaStateEvent>();
            for (int i = 0; stateEvent && i < Tracked.Length; i++)
            {
                if (!keyboard[Tracked[i]].ReadValueFromEvent(eventPtr, out float value)) continue;
                known |= 1 << i; if (value >= .5f) pressed |= 1 << i;
            }
            // State events can contain unchanged key sets; retain transitions and
            // non-state event types (e.g. TEXT) without logging entered text.
            if (known != 0 && lastRawMask.TryGetValue(keyboard.deviceId, out int previous) && previous == pressed) return;
            if (known != 0) lastRawMask[keyboard.deviceId] = pressed;
            var entry = DeviceEntry("native-event-before-state-apply", keyboard);
            entry.eventType = eventPtr.type.ToString(); entry.eventTime = eventPtr.time;
            entry.keys = Names(pressed); entry.knownKeys = Names(known); Record(entry);
        }
        void Update()
        {
            if (!active) return;
            var keyboard = Keyboard.current;
            int held = 0, down = 0, up = 0;
            if (keyboard != null)
                for (int i = 0; i < Tracked.Length; i++)
                {
                    var key = keyboard[Tracked[i]];
                    if (key.isPressed) held |= 1 << i;
                    if (key.wasPressedThisFrame) down |= 1 << i;
                    if (key.wasReleasedThisFrame) up |= 1 << i;
                }
            string signature = (keyboard != null ? keyboard.deviceId : -1) + ":" + (keyboard != null && keyboard.enabled) + ":" +
                Application.isFocused + ":" + (mission != null ? mission.State.ToString() : "NoMission") + ":" + held;
            if (signature != previousSnapshot || down != 0 || up != 0)
            {
                previousSnapshot = signature;
                var entry = keyboard != null ? DeviceEntry("update-observation", keyboard) : New("update-no-keyboard");
                entry.keys = Names(held); entry.pressedThisFrame = Names(down); entry.releasedThisFrame = Names(up);
                entry.detail = "DropletInputEnabled=" + (mission != null && mission.input != null && mission.input.enabled) +
                    "; gameplayEnabled=" + (mission != null && mission.input != null && mission.input.GameplayEnabled);
                Record(entry);
            }
            if (Time.realtimeSinceStartupAsDouble >= nextFlush) { Flush(); nextFlush = Time.realtimeSinceStartupAsDouble + 1; }
        }
        static string Names(int mask)
        {
            if (mask == 0) return "";
            var result = new StringBuilder();
            for (int i = 0; i < Tracked.Length; i++) if ((mask & (1 << i)) != 0) { if (result.Length > 0) result.Append('|'); result.Append(Tracked[i]); }
            return result.ToString();
        }
        void Record(Entry entry)
        {
            if (buffer.Length > 262144) { dropped++; return; }
            buffer.AppendLine(JsonUtility.ToJson(entry));
        }
        void Flush()
        {
            if (string.IsNullOrEmpty(outputPath)) return;
            if (dropped > 0) { var entry = New("observer-buffer-limit"); entry.bufferedRecordsDropped = dropped; buffer.AppendLine(JsonUtility.ToJson(entry)); dropped = 0; }
            if (buffer.Length == 0) return;
            try { File.AppendAllText(outputPath, buffer.ToString(), Encoding.UTF8); buffer.Clear(); }
            catch (Exception exception) { Debug.LogWarning("Native input observer could not flush: " + exception.Message); buffer.Clear(); }
        }
        void OnDisable()
        {
            if (!active) return;
            Record(New("observer-stop")); active = false;
            InputSystem.onEvent -= ObserveEvent; InputSystem.onDeviceChange -= ObserveDeviceChange; Flush();
        }
        void OnApplicationQuit() => OnDisable();
    }
}
