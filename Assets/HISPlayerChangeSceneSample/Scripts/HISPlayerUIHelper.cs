using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace HISPlayer
{
    public class HISPlayerUIHelper : MonoBehaviour
    {
        [Header("GameObjects")]
        public List<GameObject> UI;

        [Header("Resources")]
        public Sprite playSprite;
        public Sprite pauseSprite;
        public Sprite restartSprite;
        public Sprite muteSprite;
        public Sprite unmuteSprite;

        [Header("Playback Controller")]
        public Button[] playPauseButton;
        public Button[] muteButton;
        public Button[] restartButton;
        public Slider[] seekBar;

        [Header("Information")]
        public TextMeshProUGUI generalErrorText;
        public TextMeshProUGUI[] errorText;
        public TextMeshProUGUI[] currTimeText;
        public TextMeshProUGUI[] totalTimeText;
        public TextMeshProUGUI[] speedRateText;

        private HISPlayerController m_controller;
        private List<bool> showControls = new List<bool>();
        public void InitUI(HISPlayerController controller)
        {
            m_controller = controller;

            for (int i = 0; i < m_controller.totalScreens; i++)
            {
                playPauseButton[i].image.sprite = m_controller.isPlaying[i] ? pauseSprite : playSprite;
                muteButton[i].image.sprite = m_controller.isMuted[i] ? muteSprite : unmuteSprite;
                errorText[i].text = "";
                showControls.Add(UI[i].activeSelf);
            }
        }

        public void UpdatePlayPauseButton(int playerIndex)
        {
            if (!m_controller)
                return;

            playPauseButton[playerIndex].image.sprite = m_controller.isPlaying[playerIndex] ? pauseSprite : playSprite;
        }

        public void UpdateMuteButton(int playerIndex)
        {
            if (!m_controller)
                return;

            muteButton[playerIndex].image.sprite = m_controller.isMuted[playerIndex] ? muteSprite : unmuteSprite;
        }

        public void UpdateTotalTime(long miliseconds, int playerIndex)
        {
            if (!m_controller)
                return;

            totalTimeText[playerIndex].text = ConvertTime(miliseconds);
            seekBar[playerIndex].maxValue = miliseconds;
        }

        public void UpdateVideoPosition(long miliseconds, int playerIndex)
        {
            if (!m_controller)
                return;

            currTimeText[playerIndex].text = ConvertTime(miliseconds);
            seekBar[playerIndex].value = miliseconds;
        }

        public void UpdateErrorText(int playerIndex, string info)
        {
            if (playerIndex < 0)
                UpdateGeneralErrorText(info);
            else
            {
                generalErrorText.gameObject.SetActive(false);
                generalErrorText.text = "";
                errorText[playerIndex].text = info;
            }
        }

        public void UpdateGeneralErrorText(string info)
        {
            generalErrorText.gameObject.SetActive(true);
            generalErrorText.text = info;
            for (int i = 0; i < UI.Count; i++)
            {
                OnToggleShowUI(i);
                errorText[i].text = "";
            }
        }

        public void UpdateSpeedRateText(int playerIndex, float speedRate)
        {
            if (!m_controller)
                return;

            speedRateText[playerIndex].text = $"x{speedRate}";
        }
        public void ResetValues(int playerIndex, bool restart = false)
        {
            currTimeText[playerIndex].text = totalTimeText[playerIndex].text = "Loading";
            playPauseButton[playerIndex].image.sprite = m_controller.isPlaying[playerIndex] ? pauseSprite : playSprite;
            if (restart)
            {
                playPauseButton[playerIndex].gameObject.SetActive(false);
                restartButton[playerIndex].gameObject.SetActive(true);
            }
        }

        #region BUTTON CALLBACKS
        public void OnToggleShowUI(int playerIndex)
        {
            showControls[playerIndex] = !showControls[playerIndex];
            UI[playerIndex].SetActive(showControls[playerIndex]);
        }

        public void OnRestartTriggered(int playerIndex)
        {
            restartButton[playerIndex].gameObject.SetActive(false);
            playPauseButton[playerIndex].gameObject.SetActive(true);
            playPauseButton[playerIndex].image.sprite = pauseSprite;
        }

        #endregion

        private string ConvertTime(long miliseconds)
        {
            int hours = (int)(miliseconds / (1000 * 60 * 60));
            int minutes = (int)((miliseconds / (1000 * 60)) % 60);
            int seconds = (int)((miliseconds / 1000) % 60);

            string timeStr;

            if (minutes < 10 && seconds < 10)
            {
                timeStr = hours + ":0" + minutes + ":0" + seconds;
            }
            else if (minutes < 10)
            {
                timeStr = hours + ":0" + minutes + ":" + seconds;
            }
            else if (seconds < 10)
            {
                timeStr = hours + ":" + minutes + ":0" + seconds;
            }
            else
            {
                timeStr = hours + ":" + minutes + ":" + seconds;
            }

            return timeStr;
        }
    }
}
