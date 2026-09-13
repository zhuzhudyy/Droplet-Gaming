using UnityEngine;

namespace DropletPrototype
{
    public sealed class ScoreSystem : MonoBehaviour
    {
        public DropletSettings settings;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int Multiplier => Mathf.Clamp(Combo, 1, settings != null ? settings.maxMultiplier : 1);
        public float ComboRemaining { get; private set; }
        public void ResetScore() { Score = Combo = 0; ComboRemaining = 0; }
        public void Advance(float dt)
        {
            ComboRemaining = Mathf.Max(0, ComboRemaining - dt);
            if (ComboRemaining == 0) Combo = 0;
        }
        public void RegisterKill()
        {
            Combo = ComboRemaining > 0 ? Combo + 1 : 1;
            ComboRemaining = settings.comboWindow;
            Score += settings.baseScore * Multiplier;
        }
        public void AwardTimeBonus(float remaining) { Score += Mathf.FloorToInt(Mathf.Max(0, remaining)) * settings.timeBonusPerSecond; }
    }
}
