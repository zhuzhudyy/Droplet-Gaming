using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class NarrativeRadioLayoutTests
    {
        MissionController mission;
        RadioPresenter presenter;
        [UnitySetUp] public IEnumerator LoadSavedLayout()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/FleetAssault_NarrativeCombat_Small.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FleetAssault_NarrativeCombat_Small");
#endif
            yield return null;
            mission = Object.FindAnyObjectByType<MissionController>(); Assert.IsNotNull(mission);
            mission.enabled = false; mission.input.enabled = false;
            presenter = mission.narrative.radio.presenter; Assert.IsNotNull(presenter);
        }
        [TearDown] public void Restore() { Time.timeScale = 1; mission?.Restart(); }
        [Test] public void SignalStatusAndVolumeGenerateEveryVisibleGlyphInTheSavedRectangle()
        {
            mission.StartCombat(); presenter.RefreshView(); Canvas.ForceUpdateCanvases();
            TMP_Text signal = presenter.signalLabel;
            const string content = "信号中断 / 短促杂音   音量 100%";
            signal.text = content; signal.ForceMeshUpdate(true, true);
            Assert.That(signal.rectTransform.rect.height, Is.GreaterThanOrEqualTo(30));
            Assert.AreEqual(1, signal.textInfo.lineCount, "The actual signal line must fit on one line.");
            int visible = 0;
            for (int i = 0; i < signal.textInfo.characterCount; i++) if (signal.textInfo.characterInfo[i].isVisible) visible++;
            Assert.AreEqual(content.Count(c => !char.IsWhiteSpace(c)), visible, "Ellipsis must not clear the signal or volume glyphs.");
            Assert.IsFalse(signal.isTextTruncated);
            Assert.That(signal.rectTransform.rect.height, Is.GreaterThanOrEqualTo(signal.preferredHeight));
            var controls = presenter.controlsLabel;
            controls.ForceMeshUpdate(true, true); Assert.Greater(controls.textInfo.characterCount, 15);
            Assert.IsFalse(controls.isTextTruncated);
        }
        [Test] public void RadioPanelAvoidsReadyPausedAndResultsMenusWithoutDisablingControls()
        {
            presenter.RefreshView(); Assert.IsFalse(presenter.radioPanel.gameObject.activeSelf);
            Assert.IsTrue(presenter.enabled); Assert.IsTrue(presenter.gameObject.activeInHierarchy);
            Assert.IsTrue(presenter.radio.enabled);
            mission.StartCombat(); presenter.RefreshView();
            Assert.IsTrue(presenter.radioPanel.gameObject.activeSelf); Assert.AreEqual(0, presenter.radioPanel.anchorMin.x);
            mission.TogglePause(); presenter.RefreshView();
            Assert.IsTrue(presenter.radioPanel.gameObject.activeSelf); Assert.AreEqual(1, presenter.radioPanel.anchorMin.x);
            Assert.AreEqual(1, presenter.radioPanel.pivot.x); Assert.Less(presenter.radioPanel.anchoredPosition.x, 0);
            mission.TogglePause(); presenter.RefreshView(); Assert.AreEqual(0, presenter.radioPanel.anchorMin.x);
            mission.settings.missionSeconds = .01f; mission.RestartIntoCombat(); mission.lasers = null;
            mission.Step(.01f, new FlightCommand { brake = true }); Assert.AreEqual(MissionState.Results, mission.State);
            presenter.RefreshView(); Assert.IsFalse(presenter.radioPanel.gameObject.activeSelf);
            Assert.IsTrue(presenter.enabled); Assert.IsTrue(presenter.gameObject.activeInHierarchy);
            presenter.ToggleHistory(); Assert.IsTrue(presenter.HistoryOpen, "History controls remain usable with the main radio panel hidden.");
            presenter.ToggleHistory();
        }
    }
}
