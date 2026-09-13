using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class ScoreTests
    {
        [Test] public void ComboWindowCapAndResetAreDeterministic()
        {
            var go = new GameObject("Score test"); var settings = ScriptableObject.CreateInstance<DropletSettings>();
            try
            {
                var score = go.AddComponent<ScoreSystem>(); score.settings = settings;
                for (int i = 0; i < 7; i++) score.RegisterKill();
                Assert.AreEqual(2500, score.Score); Assert.AreEqual(5, score.Multiplier);
                score.Advance(settings.comboWindow + .1f); score.RegisterKill();
                Assert.AreEqual(2600, score.Score); Assert.AreEqual(1, score.Multiplier);
                score.ResetScore(); Assert.AreEqual(0, score.Score); Assert.AreEqual(0, score.ComboRemaining);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(settings); }
        }
    }
}
