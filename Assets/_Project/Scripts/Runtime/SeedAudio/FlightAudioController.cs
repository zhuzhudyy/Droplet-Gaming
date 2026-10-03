using UnityEngine;
using UnityEngine.Audio;

namespace DropletPrototype
{
    /// <summary>Plays Seed flight clips from completed motor steps; it never reads or consumes input.</summary>
    [DisallowMultipleComponent]
    public sealed class FlightAudioController : MonoBehaviour
    {
        public MissionController mission;
        public DropletMotor motor;
        public SeedAudioCatalog catalog;
        public SeedAudioMixController mix;
        public AudioMixerGroup flightGroup;
        [Range(1f, 300f)] public float turnThresholdDegreesPerSecond = 65f;
        [Min(.01f)] public float turnCooldownSeconds = .6f;
        [Min(.01f)] public float layerResponseSeconds = .22f;
        [Min(.05f)] public float loopCrossfadeSeconds = .8f;

        public float CurrentSpeedFraction { get; private set; }
        public int TransientCount { get; private set; }
        public int OwnedSourceCount => 5;
        public AudioSource CruiseSource => cruise != null ? cruise[cruiseActive] : null;
        public AudioSource BoostSource => boost != null ? boost[boostActive] : null;

        AudioSource[] cruise, boost;
        AudioSource transient;
        readonly float[] cruiseCueGain = { 1f, 1f }, boostCueGain = { 1f, 1f };
        int cruiseActive, boostActive;
        bool cruiseFading, boostFading;
        float cruiseFadeElapsed, boostFadeElapsed, cruiseFadeDuration, boostFadeDuration;
        MissionController boundMission;
        DropletMotor boundMotor;
        bool paused, hasStep, wasBoosting, wasBraking;
        long lastStepId = -1;
        float lastTurnAt = -100f;
        int cruiseVariant, boostVariant, enterVariant, releaseVariant, brakeVariant, turnVariant, recoverVariant;
        float transientBaseGain = 1f;

        void Awake()
        {
            cruise = new[] { CreateSource("CruiseLayer_A"), CreateSource("CruiseLayer_B") };
            boost = new[] { CreateSource("BoostLayer_A"), CreateSource("BoostLayer_B") };
            transient = CreateSource("FlightTransient");
        }
        AudioSource CreateSource(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = flightGroup;
            return source;
        }
        void OnEnable() { Bind(); }
        void Start() { if (mission != null) ChangeState(mission.State); }
        public void Bind()
        {
            if (boundMission != mission)
            {
                if (boundMission != null) boundMission.StateChanged -= ChangeState;
                boundMission = mission;
                if (boundMission != null) boundMission.StateChanged += ChangeState;
            }
            if (boundMotor != motor)
            {
                if (boundMotor != null)
                { boundMotor.Teleported -= OnTeleported; boundMotor.FlightStepCompleted -= AcceptStep; }
                boundMotor = motor;
                if (boundMotor != null)
                { boundMotor.Teleported += OnTeleported; boundMotor.FlightStepCompleted += AcceptStep; }
            }
        }

        void ChangeState(MissionState state)
        {
            if (state == MissionState.Paused)
            {
                if (paused) return;
                paused = true;
                foreach (var source in cruise) source.Pause();
                foreach (var source in boost) source.Pause();
                transient.Pause();
                return;
            }
            if (paused)
            {
                paused = false;
                if (state == MissionState.Playing)
                {
                    foreach (var source in cruise) source.UnPause();
                    foreach (var source in boost) source.UnPause();
                    transient.UnPause();
                    return;
                }
            }
            if (state == MissionState.Playing) StartLoops();
            else
            {
                StopAll();
                if (state == MissionState.Ready)
                {
                    cruiseVariant = boostVariant = enterVariant = releaseVariant = 0;
                    brakeVariant = turnVariant = recoverVariant = 0;
                    TransientCount = 0;
                }
            }
        }

        void StartLoops()
        {
            StopAll();
            StartLoop(cruise[cruiseActive], cruiseCueGain, cruiseActive, "Cruise", ref cruiseVariant);
            StartLoop(boost[boostActive], boostCueGain, boostActive, "BoostLoop", ref boostVariant);
        }
        bool StartLoop(AudioSource source, float[] cueGains, int index, string family, ref int cursor)
        {
            if (catalog == null || !catalog.TryNext(family, ref cursor, out var cue)) return false;
            source.Stop();
            source.clip = cue.clip;
            source.loop = false;
            source.volume = 0f;
            source.priority = Mathf.Clamp(cue.priority, 0, 256);
            cueGains[index] = Mathf.Clamp(cue.gain, 0f, 2f);
            source.Play();
            return true;
        }
        void StopAll()
        {
            foreach (var source in cruise) { source.Stop(); source.clip = null; }
            foreach (var source in boost) { source.Stop(); source.clip = null; }
            transient.Stop(); transient.clip = null;
            cruiseActive = boostActive = 0;
            cruiseFading = boostFading = false;
            cruiseFadeElapsed = boostFadeElapsed = 0f;
            cruiseFadeDuration = boostFadeDuration = 0f;
            CurrentSpeedFraction = 0f;
            ClearEdges();
        }
        void ClearEdges()
        { hasStep = wasBoosting = wasBraking = false; lastStepId = -1; lastTurnAt = -100f; }
        void OnTeleported()
        {
            // ResetPose can be a restart or boundary recovery. Neither is a boost-release or turn.
            transient.Stop();
            ClearEdges();
        }

        public static float NormalizeSpeed(float speed, float maximumSpeed)
        { return Mathf.Clamp01(speed / Mathf.Max(.0001f, maximumSpeed)); }

        public void AcceptStep(FlightPresentationSample sample)
        {
            if (mission == null || mission.State != MissionState.Playing || paused || sample.StepId <= lastStepId) return;
            lastStepId = sample.StepId;
            CurrentSpeedFraction = NormalizeSpeed(sample.Speed, sample.MaximumSpeed);
            bool effectiveBoost = sample.Boosting && !sample.Braking;
            bool brakeRise = sample.Braking && (!hasStep || !wasBraking);
            bool boostRise = effectiveBoost && (!hasStep || !wasBoosting);
            bool boostFall = !effectiveBoost && hasStep && wasBoosting;
            bool brakeFall = !sample.Braking && hasStep && wasBraking;
            // The single transient voice chooses one audible edge per motor step.
            if (brakeRise) PlayTransient("Brake", ref brakeVariant);
            else if (boostRise) PlayTransient("BoostEnter", ref enterVariant);
            else if (boostFall) PlayTransient("BoostRelease", ref releaseVariant);
            else if (brakeFall) PlayTransient("Recover", ref recoverVariant);
            else if (sample.TurnDegreesPerSecond >= turnThresholdDegreesPerSecond &&
                Time.unscaledTime - lastTurnAt >= turnCooldownSeconds)
            { PlayTransient("Turn", ref turnVariant); lastTurnAt = Time.unscaledTime; }
            wasBoosting = effectiveBoost;
            wasBraking = sample.Braking;
            hasStep = true;
        }

        void PlayTransient(string family, ref int cursor)
        {
            if (catalog == null || !catalog.TryNext(family, ref cursor, out var cue)) return;
            // One owned transient voice: rapid contradictory controls replace, never stack.
            transient.Stop();
            transient.clip = cue.clip;
            transient.loop = false;
            transientBaseGain = Mathf.Clamp(cue.gain, 0f, 2f);
            transient.volume = transientBaseGain * (mix != null ? mix.FlightGain : 1f);
            transient.priority = Mathf.Clamp(cue.priority, 0, 256);
            transient.Play();
            TransientCount++;
        }

        void Update()
        {
            if (mission == null || mission.State != MissionState.Playing || paused || motor == null) return;
            var settings = motor.settings;
            if (settings == null) return;
            float max = Mathf.Max(.0001f, settings.maxCruiseSpeed * settings.boostMultiplier);
            float speed = Mathf.Max(0f, motor.Speed);
            CurrentSpeedFraction = NormalizeSpeed(speed, max);
            float boostWeight = Mathf.Clamp01((speed - settings.maxCruiseSpeed) /
                Mathf.Max(.0001f, max - settings.maxCruiseSpeed));
            float cruiseWeight = Mathf.Clamp01(speed / Mathf.Max(.0001f, settings.maxCruiseSpeed)) *
                (1f - .68f * boostWeight);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, layerResponseSeconds));
            float gain = mix != null ? mix.FlightGain : 1f;
            TickCruise(cruiseWeight * gain, blend);
            TickBoost(boostWeight * gain, blend);
            if (transient.clip != null) transient.volume = transientBaseGain * gain;
        }

        void TickCruise(float target, float blend)
        {
            if (cruise[cruiseActive].clip == null) return;
            if (!cruiseFading && NearLoopEnd(cruise[cruiseActive]))
            {
                int next = 1 - cruiseActive;
                cruiseFadeDuration = CrossfadeDuration(cruise[cruiseActive]);
                if (StartLoop(cruise[next], cruiseCueGain, next, "Cruise", ref cruiseVariant))
                { cruiseFading = true; cruiseFadeElapsed = 0f; }
            }
            if (!cruiseFading)
            {
                var active = cruise[cruiseActive];
                active.volume = Mathf.Lerp(active.volume, target * cruiseCueGain[cruiseActive], blend);
                return;
            }
            cruiseFadeElapsed += Time.deltaTime;
            float fraction = Mathf.Clamp01(cruiseFadeElapsed / cruiseFadeDuration);
            int outgoing = cruiseActive, incoming = 1 - outgoing;
            cruise[outgoing].volume = Mathf.Lerp(cruise[outgoing].volume,
                target * cruiseCueGain[outgoing] * (1f - fraction), blend);
            cruise[incoming].volume = Mathf.Lerp(cruise[incoming].volume,
                target * cruiseCueGain[incoming] * fraction, blend);
            if (fraction >= 1f)
            {
                cruise[outgoing].Stop(); cruise[outgoing].clip = null;
                cruiseActive = incoming; cruiseFading = false;
            }
        }

        void TickBoost(float target, float blend)
        {
            if (boost[boostActive].clip == null) return;
            if (!boostFading && NearLoopEnd(boost[boostActive]))
            {
                int next = 1 - boostActive;
                boostFadeDuration = CrossfadeDuration(boost[boostActive]);
                if (StartLoop(boost[next], boostCueGain, next, "BoostLoop", ref boostVariant))
                { boostFading = true; boostFadeElapsed = 0f; }
            }
            if (!boostFading)
            {
                var active = boost[boostActive];
                active.volume = Mathf.Lerp(active.volume, target * boostCueGain[boostActive], blend);
                return;
            }
            boostFadeElapsed += Time.deltaTime;
            float fraction = Mathf.Clamp01(boostFadeElapsed / boostFadeDuration);
            int outgoing = boostActive, incoming = 1 - outgoing;
            boost[outgoing].volume = Mathf.Lerp(boost[outgoing].volume,
                target * boostCueGain[outgoing] * (1f - fraction), blend);
            boost[incoming].volume = Mathf.Lerp(boost[incoming].volume,
                target * boostCueGain[incoming] * fraction, blend);
            if (fraction >= 1f)
            {
                boost[outgoing].Stop(); boost[outgoing].clip = null;
                boostActive = incoming; boostFading = false;
            }
        }

        bool NearLoopEnd(AudioSource source)
        {
            return source.time >= source.clip.length - CrossfadeDuration(source);
        }

        float CrossfadeDuration(AudioSource source)
        { return Mathf.Max(.01f, Mathf.Min(Mathf.Max(.05f, loopCrossfadeSeconds), source.clip.length * .4f)); }

        void OnDisable()
        {
            if (boundMission != null) boundMission.StateChanged -= ChangeState;
            if (boundMotor != null)
            { boundMotor.Teleported -= OnTeleported; boundMotor.FlightStepCompleted -= AcceptStep; }
            boundMission = null; boundMotor = null;
            if (cruise != null) StopAll();
            paused = false;
        }
    }
}
