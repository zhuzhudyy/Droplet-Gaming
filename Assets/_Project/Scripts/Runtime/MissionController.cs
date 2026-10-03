using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropletPrototype
{
    public enum MissionState { Ready, Playing, Paused, Results, Narrative }

    public sealed class MissionController : MonoBehaviour
    {
        public DropletSettings settings;
        public DropletMotor motor;
        public DropletInput input;
        public ChaseCamera chaseCamera;
        public ScoreSystem score;
        public ShipTarget[] targets;
        public FleetCombatSimulation combat;
        public FleetLaserDirector lasers;
        public NarrativeApproachController narrative;
        public bool replayNarrativeOnStart = true;
        MissionState pausedFrom = MissionState.Playing;
        public int PendingCount => combat != null ? combat.PendingCount : 0;
        public int EscapedCount => combat != null ? combat.EscapedCount : 0;
        public int IntactCount => combat != null ? combat.IntactCount : TotalCount - DestroyedCount;
        public string Rating => EscapedCount == 0 && Won ? "S" : DestroyedCount >= TotalCount * .8f ? "A" : DestroyedCount >= TotalCount * .5f ? "B" : "C";
        public Vector3 spawnPosition = new Vector3(0, 8, 0);
        public Vector3 arenaCenter = new Vector3(0, 8, 100);
        public MissionState State { get; private set; }
        public float Remaining { get; private set; }
        public float Elapsed { get; private set; }
        public bool Won { get; private set; }
        public int DestroyedCount { get; private set; }
        public int TotalCount => membership.Count;
        public int ResultTransitions { get; private set; }
        public int RecoveryCount { get; private set; }
        public float RecoveryNotice { get; private set; }
        public bool NearBoundary => motor != null && Vector3.Distance(motor.transform.position, arenaCenter) > settings.boundaryWarningRadius;
        public event Action ResultsShown;
        public event Action Restarted;
        public event Action<MissionState> StateChanged;
        readonly HashSet<ShipTarget> membership = new HashSet<ShipTarget>();
        readonly HashSet<ShipTarget> accepted = new HashSet<ShipTarget>();

        void Awake() { if (settings != null && motor != null && score != null) Initialize(); }
        public void Initialize()
        {
            Unbind(); membership.Clear();
            settings.ApplyWorldScale();
            if (targets != null) foreach (var target in targets) if (target != null) membership.Add(target);
            foreach (var target in membership) target.Destroyed += OnDestroyed;
            if (input != null)
            {
                input.PauseRequested = TogglePause; input.StartRequested = StartMission;
                input.RestartRequested = combat != null ? RestartIntoCombat : Restart;
            }
            motor.mission = this;
            if (combat != null)
            {
                combat.mission = this;
                combat.evacuationCenter = arenaCenter;
                combat.Configure(targets, motor.transform, settings.worldScale);
                motor.hitDetector.combat = combat;
            }
            if (narrative != null) narrative.Completed += StartCombat;
            Restart();
        }
        void FixedUpdate()
        {
            if (State == MissionState.Narrative)
            { if (narrative != null) narrative.Step(Time.fixedDeltaTime); return; }
            if (State != MissionState.Playing) return;
            float dt = Remaining > 0 ? Mathf.Min(Time.fixedDeltaTime, Remaining) : Time.fixedDeltaTime;
            Step(dt, input != null ? input.ReadStep(dt, settings) : default);
        }
        public void Step(float proposedDt, FlightCommand command)
        {
            if (State != MissionState.Playing || proposedDt <= 0) return;
            bool hadCombatTime = Remaining > 0;
            float dt = Remaining > 0 ? Mathf.Min(proposedDt, Remaining) : proposedDt;
            score.Advance(dt);
            Elapsed += dt; Remaining = Mathf.Max(0, Remaining - dt);
            RecoveryNotice = Mathf.Max(0, RecoveryNotice - dt);
            // Final-step rule: clamp movement to remaining time, resolve ALL hits,
            // then evaluate victory before timeout. A tie is a victory, once only.
            combat?.BeginStep(dt);
            if (hadCombatTime || combat == null) motor.Simulate(dt, command);
            else motor.HoldSimulationPose();
            combat?.EndStep(dt);
            if (hadCombatTime) lasers?.Step(dt);
            else if (lasers != null)
            {
                lasers.beamPool?.Step(dt);
                lasers.dropletSurface?.contactResponse?.Step(dt);
            }
            if (DestroyedCount + EscapedCount == TotalCount && PendingCount == 0) Finish(EscapedCount == 0);
            else if (Remaining <= .00001f && PendingCount == 0) Finish(false);
            else RecoverIfOutside();
        }
        void OnDestroyed(ShipTarget target)
        {
            if (State != MissionState.Playing || !membership.Contains(target) || !accepted.Add(target)) return;
            DestroyedCount++; score.RegisterKill();
        }
        public void StartMission()
        {
            if (State != MissionState.Ready) return;
            if (narrative != null && replayNarrativeOnStart) BeginNarrative(); else StartCombat();
        }
        public void BeginNarrative()
        {
            if (narrative == null) { StartCombat(); return; }
            SetState(MissionState.Narrative);
            narrative.BeginApproach();
        }
        public void StartCombat()
        {
            if (State == MissionState.Playing || State == MissionState.Results) return;
            narrative?.Cancel();
            motor.ResetPose(spawnPosition, Quaternion.identity);
            if (chaseCamera != null) { chaseCamera.enabled = true; chaseCamera.ResetCamera(); }
            SetState(MissionState.Playing);
        }
        public void RestartIntoCombat() { Restart(); StartCombat(); }
        public void ReplayNarrative() { Restart(); BeginNarrative(); }
        public void TogglePause()
        {
            if (State == MissionState.Playing || State == MissionState.Narrative)
            { pausedFrom = State; narrative?.SetPaused(true); SetState(MissionState.Paused); }
            else if (State == MissionState.Paused)
            { SetState(pausedFrom); narrative?.SetPaused(false); }
        }
        void Finish(bool won)
        {
            if (State != MissionState.Playing) return;
            Won = won; ResultTransitions++;
            if (won) score.AwardTimeBonus(Remaining);
            SetState(MissionState.Results); ResultsShown?.Invoke();
        }
        void SetState(MissionState state)
        {
            State = state; Time.timeScale = state == MissionState.Paused ? 0 : 1;
            if (state == MissionState.Ready || state == MissionState.Results || state == MissionState.Narrative)
            {
                lasers?.beamPool?.ResetEffects();
                lasers?.dropletSurface?.contactResponse?.ResetResponse();
            }
            motor.SimulationEnabled = state == MissionState.Playing || state == MissionState.Narrative;
            if (input != null) input.SetGameplay(state == MissionState.Playing);
            StateChanged?.Invoke(state);
        }
        public void Restart()
        {
            narrative?.Cancel();
            combat?.ResetSimulation();
            lasers?.ResetWeapons();
            Time.timeScale = 1; accepted.Clear();
            foreach (var target in membership) target.ResetTarget();
            DestroyedCount = 0; Remaining = settings.missionSeconds; Elapsed = 0; Won = false;
            ResultTransitions = 0; RecoveryCount = 0; RecoveryNotice = 0; score.ResetScore();
            motor.ResetPose(spawnPosition, Quaternion.identity);
            Physics.SyncTransforms();
            if (chaseCamera != null) chaseCamera.ResetCamera();
            SetState(MissionState.Ready);
            Restarted?.Invoke();
        }
        public void RecoverIfOutside()
        {
            if (State != MissionState.Playing || Vector3.Distance(motor.transform.position, arenaCenter) <= settings.boundaryRadius) return;
            Vector3 direction = arenaCenter - spawnPosition;
            motor.ResetPose(spawnPosition, Quaternion.LookRotation(direction.normalized, Vector3.up));
            if (chaseCamera != null) chaseCamera.ResetCamera();
            RecoveryCount++; RecoveryNotice = 3;
        }
        void Unbind()
        {
            if (narrative != null) narrative.Completed -= StartCombat;
            foreach (var target in membership) if (target != null) target.Destroyed -= OnDestroyed;
            if (input != null) { input.PauseRequested = null; input.StartRequested = null; input.RestartRequested = null; }
        }
        void OnDestroy() { Unbind(); Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
