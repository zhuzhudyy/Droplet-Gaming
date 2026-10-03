namespace DropletPrototype
{
    /// <summary>Read-only result of one completed motor step; never reads player input again.</summary>
    public readonly struct FlightPresentationSample
    {
        public readonly long StepId;
        public readonly float Speed;
        public readonly float CruiseSpeed;
        public readonly float MaximumSpeed;
        public readonly bool Boosting;
        public readonly bool Braking;
        public readonly float TurnDegreesPerSecond;

        public FlightPresentationSample(long stepId, float speed, float cruiseSpeed,
            float maximumSpeed, bool boosting, bool braking, float turnDegreesPerSecond)
        {
            StepId = stepId;
            Speed = speed;
            CruiseSpeed = cruiseSpeed;
            MaximumSpeed = maximumSpeed;
            Boosting = boosting;
            Braking = braking;
            TurnDegreesPerSecond = turnDegreesPerSecond;
        }
    }
}
