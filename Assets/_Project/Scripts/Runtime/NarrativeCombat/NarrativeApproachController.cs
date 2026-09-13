using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DropletPrototype
{
    /// <summary>Timeline commands presentation; the existing motor remains the only PlayerRoot writer.</summary>
    public sealed class NarrativeApproachController : MonoBehaviour
    {
        public MissionController mission;
        public DropletMotor motor;
        public ChaseCamera chaseCamera;
        public RadioController radio;
        public PlayableDirector director;
        public TimelineAsset timeline;
        public float duration = 60;
        [Tooltip("Uses initialSpeed in Unity units; no separate cinematic world scale.")]
        public bool positionFromFlightDuration = true;
        public event Action Completed;
        public bool IsActive { get; private set; }
        public bool IsPaused { get; private set; }
        public float Elapsed { get; private set; }
        public int CompletionCount { get; private set; }
        public int CueCount => playedCues.Count;
        public int CurrentShot { get; private set; }
        public bool AutopilotEnabled { get; private set; }
        readonly HashSet<int> playedCues = new HashSet<int>();
        bool cameraWasEnabled;
        float previousFov;
        Camera cinematicCamera;
        Vector3 cameraOffset;

        public void BeginApproach()
        {
            Cancel();
            if (motor == null || director == null || timeline == null) { Debug.LogError("Narrative approach requires motor, saved Timeline and director.", this); IsActive = true; Complete(); return; }
            CompletionCount = 0; Elapsed = 0; CurrentShot = 0; playedCues.Clear(); IsActive = true; IsPaused = false; AutopilotEnabled = false;
            motor.SimulationEnabled = true;
            Vector3 end = mission != null ? mission.spawnPosition : motor.transform.position;
            float approachLength = positionFromFlightDuration && motor.settings != null ? motor.settings.initialSpeed * duration : 18000;
            motor.ResetPose(end - Vector3.forward * approachLength, Quaternion.identity);
            if (chaseCamera != null)
            {
                cameraWasEnabled = chaseCamera.enabled; cinematicCamera = chaseCamera.GetComponent<Camera>();
                if (cinematicCamera != null) previousFov = cinematicCamera.fieldOfView;
                chaseCamera.enabled = false;
            }
            radio?.SetNarrativeMode(true); radio?.SetPaused(false);
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.playableAsset = timeline;
            foreach (var output in timeline.outputs) director.SetGenericBinding(output.sourceObject, this);
            director.time = 0; director.Play(); director.Evaluate();
            UpdateCamera(1);
        }
        public void Step(float dt)
        {
            if (!IsActive || IsPaused || dt <= 0) return;
            // Small controlled substeps also preserve cue crossings in deterministic fast-forward tests.
            float remaining = Mathf.Min(dt, duration - Elapsed);
            while (remaining > .000001f && IsActive)
            {
                float step = Mathf.Min(.1f, remaining);
                if (AutopilotEnabled) motor.Simulate(step, default);
                Elapsed = Mathf.Min(duration, Elapsed + step); remaining -= step;
                director.time = Elapsed; director.Evaluate();
            }
            if (Elapsed >= duration - .001f) Complete();
        }
        public void ApplyCue(int index, bool enableAutopilot = true)
        {
            if (!IsActive || IsPaused || radio == null || radio.library == null || index < 0 || index >= radio.library.narrative.Length || !playedCues.Add(index)) return;
            var line = radio.library.narrative[index]; CurrentShot = line.cameraShot; AutopilotEnabled = enableAutopilot; radio.PlayNarrative(line);
        }
        public void SetPaused(bool value) { IsPaused = value; radio?.SetPaused(value); }
        public void Skip() { if (IsActive) Complete(); }
        void Complete()
        {
            if (!IsActive) return;
            IsActive = false; IsPaused = false; AutopilotEnabled = false; CompletionCount++;
            director?.Stop(); radio?.SetNarrativeMode(false); RestoreCamera();
            Completed?.Invoke();
        }
        public void Cancel()
        {
            bool wasActive = IsActive;
            IsActive = false; IsPaused = false; AutopilotEnabled = false; director?.Stop(); radio?.SetPaused(false); radio?.SetNarrativeMode(false);
            if (wasActive) RestoreCamera();
        }
        void RestoreCamera()
        {
            if (chaseCamera == null) return;
            chaseCamera.enabled = cameraWasEnabled; chaseCamera.ResetCamera();
            if (cinematicCamera != null) cinematicCamera.fieldOfView = previousFov;
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (IsActive && keyboard.tabKey.wasPressedThisFrame) Skip();
            if (keyboard.nKey.wasPressedThisFrame && mission != null) mission.ReplayNarrative();
        }
        void LateUpdate() { if (IsActive && !IsPaused) UpdateCamera(1 - Mathf.Exp(-3 * Time.deltaTime)); }
        void UpdateCamera(float blend)
        {
            if (chaseCamera == null || motor == null || motor.settings == null) return;
            float distance = motor.settings.cameraDistance;
            Vector3 offset;
            switch (CurrentShot)
            {
                case 1: offset = new Vector3(-distance * .65f, distance * .14f, -distance * .75f); break;
                case 2: offset = new Vector3(distance * .55f, distance * .33f, -distance); break;
                case 3: offset = new Vector3(0, distance * .08f, -distance * .75f); break;
                default: offset = new Vector3(0, motor.settings.cameraHeight, -distance); break;
            }
            Vector3 playerPosition = motor.PresentedPosition;
            Quaternion heading = motor.PresentedRotation;
            cameraOffset = Vector3.Lerp(cameraOffset, offset, blend);
            chaseCamera.transform.position = playerPosition + heading * cameraOffset;
            Quaternion rotation = Quaternion.LookRotation(playerPosition + heading * Vector3.forward * 4 - chaseCamera.transform.position, Vector3.up);
            chaseCamera.transform.rotation = Quaternion.Slerp(chaseCamera.transform.rotation, rotation, blend);
            if (cinematicCamera != null) cinematicCamera.fieldOfView = Mathf.Lerp(cinematicCamera.fieldOfView, CurrentShot == 3 ? 53 : 65, blend);
        }
        void OnDisable() { Cancel(); }
    }
}
