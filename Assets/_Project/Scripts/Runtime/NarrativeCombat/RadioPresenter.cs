using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DropletPrototype
{
    public sealed class RadioPresenter : MonoBehaviour
    {
        public RadioController radio;
        public TMP_Text sourceLabel, subtitleLabel, signalLabel, historyLabel, controlsLabel;
        public RectTransform radioPanel;
        public GameObject historyPanel;
        public bool HistoryOpen { get; private set; }
        readonly StringBuilder historyText = new StringBuilder(2048);
        int historyCount = -1, historyRevision = -1;
        bool layoutInitialized;
        MissionState displayedState;
        public void ToggleHistory() { HistoryOpen = !HistoryOpen; if (historyPanel != null) historyPanel.SetActive(HistoryOpen); RefreshHistory(); }
        public void SetVolume(float value) { if (radio != null) radio.Volume = value; }
        void Update()
        {
            if (radio == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame) ToggleHistory();
                if (keyboard.minusKey.wasPressedThisFrame) radio.Volume -= .1f;
                if (keyboard.equalsKey.wasPressedThisFrame) radio.Volume += .1f;
            }
            RefreshView();
        }
        public void RefreshView()
        {
            if (radio == null) return;
            RefreshPanelPlacement();
            if (sourceLabel != null) sourceLabel.text = "截获无线电  /  " + radio.CurrentChannel;
            if (subtitleLabel != null) subtitleLabel.text = radio.CurrentText;
            if (signalLabel != null) signalLabel.text = radio.SignalStatus + "   音量 " + Mathf.RoundToInt(radio.Volume * 100) + "%";
            if (controlsLabel != null) controlsLabel.text = "H 历史   - / + 广播音量   TAB 跳过剧情   N 重播剧情";
            if (HistoryOpen && historyRevision != radio.HistoryRevision) RefreshHistory();
        }
        void RefreshPanelPlacement()
        {
            if (radioPanel == null && sourceLabel != null) radioPanel = sourceLabel.rectTransform.parent as RectTransform;
            var state = radio.mission != null ? radio.mission.State : MissionState.Playing;
            if (layoutInitialized && state == displayedState) return;
            displayedState = state; layoutInitialized = true;
            bool visible = state != MissionState.Ready && state != MissionState.Results;
            if (radioPanel != null) radioPanel.gameObject.SetActive(visible);
            // Keep this Canvas/presenter alive for volume/history keys even while the main caption panel is hidden.
            bool right = state == MissionState.Paused;
            PlacePanel(radioPanel, right, 68);
            if (historyPanel != null) PlacePanel(historyPanel.transform as RectTransform, right, 288);
        }
        static void PlacePanel(RectTransform panel, bool right, float bottom)
        {
            if (panel == null) return;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(right ? 1 : 0, 0);
            panel.anchoredPosition = new Vector2(right ? -28 : 28, bottom);
        }
        void RefreshHistory()
        {
            if (radio == null || historyLabel == null) return;
            historyCount = radio.History.Count; historyRevision = radio.HistoryRevision; historyText.Clear(); historyText.AppendLine("截获记录  /  最近十条\n");
            for (int i = Mathf.Max(0, historyCount - 10); i < historyCount; i++) historyText.AppendLine(radio.History[i]).AppendLine();
            historyLabel.text = historyText.ToString();
        }
    }
}
