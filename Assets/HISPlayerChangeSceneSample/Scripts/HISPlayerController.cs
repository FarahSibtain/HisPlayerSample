using System.Collections;
using System.Collections.Generic;
using HISPlayerAPI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HISPlayer
{
    public class HISPlayerController : HISPlayerManager
    {
        [Header("Misc")]
        public HISPlayerUIHelper HISPlayerUIHelper;

        /// <summary>
        /// Determines if a stream is muted or not
        /// </summary>
        public List<bool> isMuted;

        /// <summary>
        /// Represents the number of the streams in the scene.
        /// It is initialized in function of multiStreamProperties.Count
        /// </summary>
        [HideInInspector]
        public int totalScreens = 0;

        /// <summary>
        /// Determines if a stream is pllaying or not
        /// </summary>
        [HideInInspector]
        public List<bool> isPlaying = new List<bool>();

        /// <summary>
        /// Determines if a stream is seeking.
        /// </summary>
        [HideInInspector]
        private List<bool> isSeeking = new List<bool>();

        /// <summary>
        /// Determines the current index of the video for each stream in the scene
        /// </summary>
        private List<int> videoIndex = new List<int>();

        /// <summary>
        /// Determines if a stream is ready to play or not
        /// </summary>
        private List<bool> isPlaybackReady = new List<bool>();

        /// <summary>
        /// Determines the current runtime platform
        /// </summary>
        private RuntimePlatform runtimePlatform = Application.platform;

        /// <summary>
        /// Determines if the quality has been set to 720 for the multistream performance.
        /// In the case of Windows this action is not needed becausue Windows doesn't support multi stream
        /// </summary>
        private List<bool> isQuality720 = new List<bool>();

        private string errorText = "";
        private string[] videoSamples = {
            "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=s7PwvPwJ&protocol=hls",
            "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=GdCDsEmW&protocol=hls",
        };

        #region UNITY FUNCTIONS

        protected override void Awake()
        {
            if (runtimePlatform == RuntimePlatform.Android || runtimePlatform == RuntimePlatform.IPhonePlayer)
                Screen.orientation = ScreenOrientation.LandscapeLeft;

            base.Awake();
            SetUpPlayer();
            totalScreens = multiStreamProperties.Count;

            for (int i = 0; i < totalScreens; i++)
            {
                StreamProperties stream = multiStreamProperties[i];
                isPlaying.Add(stream.autoPlay);
                videoIndex.Add(i);
                isSeeking.Add(false);
                isPlaybackReady.Add(false);
                isQuality720.Add(false);

                SetVolume(i, isMuted[i] ? 0.0f : 1.0f);
                StartCoroutine(StartSomeValues(i));
                StartCoroutine(UpdateVideoPosition(i));
            }

            HISPlayerUIHelper.InitUI(this);
        }

        private void OnApplicationFocus(bool focus)
        {
            for (int i = 0; i < totalScreens; i++)
            {
                if (!isPlaybackReady[i])
                    continue;

                if (focus)
                {
                    Play(i);
                }
                else
                {
                    Pause(i);
                }

                isPlaying[i] = focus;
                HISPlayerUIHelper.UpdatePlayPauseButton(i);
            }
        }

        private void OnApplicationQuit()
        {
            Release();
        }

        #endregion

        #region PLAYBACK CONTROLLER

        public void OnSeekBegin(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            isSeeking[playerIndex] = true;
        }

        public void OnSeekEnd(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            long milliseconds = (long)HISPlayerUIHelper.seekBar[playerIndex].value;
            Seek(playerIndex, milliseconds);
        }

        public void OnTogglePlayPause(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            if (isPlaying[playerIndex])
            {
                Pause(playerIndex);
            }
            else
            {
                Play(playerIndex);
            }

            isPlaying[playerIndex] = !isPlaying[playerIndex];
            HISPlayerUIHelper.UpdatePlayPauseButton(playerIndex);
        }

        public void OnStop(int playerIndex)
        {
            isPlaying[playerIndex] = false;
            Stop(playerIndex);
            HISPlayerUIHelper.UpdatePlayPauseButton(playerIndex);
        }

        public void OnRestart(int playerIndex)
        {
            isPlaying[playerIndex] = true;
            Seek(playerIndex, 0);
            Play(playerIndex);

            HISPlayerUIHelper.OnRestartTriggered(playerIndex);
            HISPlayerUIHelper.UpdateTotalTime(GetVideoDuration(playerIndex), playerIndex);
        }

        public void OnToggleMute(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            isMuted[playerIndex] = !isMuted[playerIndex];
            SetVolume(playerIndex, isMuted[playerIndex] ? 0.0f : 1.0f);
            HISPlayerUIHelper.UpdateMuteButton(playerIndex);
        }

        public void OnForward(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            var currTime = GetVideoPosition(playerIndex);
            Seek(playerIndex, currTime + 10000);
        }

        public void OnBackward(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            var currTime = GetVideoPosition(playerIndex);
            Seek(playerIndex, currTime - 10000);
        }

        public void OnPreviousPlayback(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            videoIndex[playerIndex]--;
            if (videoIndex[playerIndex] < 0)
                videoIndex[playerIndex] = videoSamples.Length - 1;

            isPlaybackReady[playerIndex] = false;
            isPlaying[playerIndex] = true;
            HISPlayerUIHelper.ResetValues(playerIndex);

            ResetUpdateVideoPosition(playerIndex);
            StartCoroutine(StartSomeValues(playerIndex));

            // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
            // For lower versions than 3.3.0, please, refer to the ChangeVideoContent(int playerIndex, int urlIndex) API
            // https://hisplayer.github.io/UnityAndroid-SDK/#/hisplayer-api?id=protected-void-changevideocontentint-playerindex-int-urlindex
            ChangeVideoContent(playerIndex, videoSamples[videoIndex[playerIndex]]);
        }

        public void OnNextPlayback(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex > totalScreens)
                return;

            videoIndex[playerIndex]++;
            if (videoIndex[playerIndex] >= videoSamples.Length)
                videoIndex[playerIndex] = 0;

            isPlaybackReady[playerIndex] = false;
            isPlaying[playerIndex] = true;
            HISPlayerUIHelper.ResetValues(playerIndex);

            ResetUpdateVideoPosition(playerIndex);
            StartCoroutine(StartSomeValues(playerIndex));

            // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
            // For lower versions than 3.3.0, please, refer to the ChangeVideoContent(int playerIndex, int urlIndex) API
            // https://hisplayer.github.io/UnityAndroid-SDK/#/hisplayer-api?id=protected-void-changevideocontentint-playerindex-int-urlindex
            ChangeVideoContent(playerIndex, videoSamples[videoIndex[playerIndex]]);
        }

        public void OnChangeSpeedRate(int playerIndex)
        {
            float currentSpeed = GetPlaybackSpeedRate(playerIndex);
            float newSpeed = 1.0f;
            switch (currentSpeed)
            {
                case 1.0f:
                    newSpeed = 1.25f;
                    break;
                case 1.25f:
                    newSpeed = 1.5f;
                    break;
                case 1.5f:
                    newSpeed = 2.0f;
                    break;
                case 2.0f:
                    newSpeed = 8.0f;
                    break;
                case 8.0f:
                    newSpeed = 1.0f;
                    break;
                default:
                    break;
            }

            SetPlaybackSpeedRate(playerIndex, newSpeed);
            HISPlayerUIHelper.UpdateSpeedRateText(playerIndex, newSpeed);
        }

        #endregion

        #region MISC

        IEnumerator StartSomeValues(int playerIndex)
        {
            yield return new WaitUntil(() => isPlaybackReady[playerIndex]);

            HISPlayerUIHelper.UpdateTotalTime(GetVideoDuration(playerIndex), playerIndex);
            HISPlayerUIHelper.UpdateMuteButton(playerIndex);
            SetVolume(playerIndex, isMuted[playerIndex] ? 0.0f : 1.0f);

            SetPlaybackSpeedRate(playerIndex, 1.0f);
            HISPlayerUIHelper.UpdateSpeedRateText(playerIndex, 1.0f);
        }

        IEnumerator UpdateVideoPosition(int playerIndex)
        {
            yield return new WaitUntil(() => isPlaybackReady[playerIndex]);

            while (isPlaybackReady[playerIndex])
            {
                float ms = 0;
                if (!isSeeking[playerIndex])
                {
                    ms = GetVideoPosition(playerIndex);
                }
                else
                {
                    ms = HISPlayerUIHelper.seekBar[playerIndex].value;
                }

                HISPlayerUIHelper.UpdateVideoPosition((long)ms, playerIndex);

                yield return null;
            }
        }

        private void ResetUpdateVideoPosition(int playerIndex)
        {
            StopCoroutine(UpdateVideoPosition(playerIndex));
            StartCoroutine(UpdateVideoPosition(playerIndex));
        }

        public void ReleasePlayer()
        {
            for (int i = 0; i < totalScreens; i++)
            {
                StopCoroutine(UpdateVideoPosition(i));
            }

            Release();
        }

        public void SetAllPlaybacksAt720(int playerIndex)
        {
            if (isQuality720[playerIndex])
                return;

            var tracks = GetTracks(playerIndex);
            if (tracks == null)
            {
                return;
            }

            int i = 0;
            while (i < tracks.Length && !isQuality720[playerIndex])
            {
                var track = tracks[i];
                if (track.width == 1280 && track.height == 720)
                {
                    SelectTrack(playerIndex, i);
                    isQuality720[playerIndex] = true;
                }

                i++;
            }
        }

        public void OnChangeScene()
        {
            ReleasePlayer();

            SceneManager.LoadScene("HomeScene");
        }

        #endregion

        #region HISPLAYER EVENTS

        protected override void EventPlaybackPlay(HISPlayerEventInfo eventInfo)
        {
            base.EventPlaybackPlay(eventInfo);
            int playerIndex = eventInfo.playerIndex;
            isPlaying[playerIndex] = true;

            HISPlayerUIHelper.UpdateErrorText(playerIndex, "");
            HISPlayerUIHelper.UpdatePlayPauseButton(playerIndex);
        }

        protected override void EventPlaybackReady(HISPlayerEventInfo eventInfo)
        {
            base.EventPlaybackReady(eventInfo);
            int playerIndex = eventInfo.playerIndex;

            isPlaybackReady[playerIndex] = true;
            HISPlayerUIHelper.UpdateTotalTime(GetVideoDuration(playerIndex), playerIndex);

            if (runtimePlatform != RuntimePlatform.Android)
                SetAllPlaybacksAt720(playerIndex);
        }

        protected override void EventEndOfPlaylist(HISPlayerEventInfo eventInfo)
        {
            base.EventEndOfPlaylist(eventInfo);

            int playerIndex = eventInfo.playerIndex;

            if (multiStreamProperties[playerIndex].LoopPlayback)
                return;

            isPlaybackReady[playerIndex] = false;
            isPlaying[playerIndex] = false;

            HISPlayerUIHelper.ResetValues(playerIndex, restart: true);

            StartCoroutine(StartSomeValues(playerIndex));

            // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
            // For lower versions than 3.3.0, please, refer to the ChangeVideoContent(int playerIndex, int urlIndex) API
            // https://hisplayer.github.io/UnityAndroid-SDK/#/hisplayer-api?id=protected-void-changevideocontentint-playerindex-int-urlindex
            ChangeVideoContent(playerIndex, videoSamples[videoIndex[playerIndex]]);
        }

        protected override void EventPlaybackSeek(HISPlayerEventInfo eventInfo)
        {
            base.EventPlaybackSeek(eventInfo);
            isSeeking[eventInfo.playerIndex] = false;
        }

        protected override void EventVideoSizeChange(HISPlayerEventInfo eventInfo)
        {
            base.EventVideoSizeChange(eventInfo);
            if (runtimePlatform == RuntimePlatform.Android)
                SetAllPlaybacksAt720(eventInfo.playerIndex);
        }

        protected override void EventOnTrackChange(HISPlayerEventInfo eventInfo)
        {
            base.EventOnTrackChange(eventInfo);
        }

        protected override void ErrorInfo(HISPlayerErrorInfo errorInfo)
        {
            base.ErrorInfo(errorInfo);
            errorText = errorInfo.stringInfo;
            if (errorInfo.errorType == HISPlayerError.HISPLAYER_ERROR_PLAYBACK_DURATION_LIMIT_REACHED)
            {
                HISPlayerUIHelper.UpdateErrorText(errorInfo.playerIndex, errorText);
                return;
            }

            HISPlayerUIHelper.UpdateGeneralErrorText(errorText);
        }

        protected override void EventPlaybackStop(HISPlayerEventInfo eventInfo)
        {
            base.EventPlaybackStop(eventInfo);
            int playerIndex = eventInfo.playerIndex;
            isPlaying[playerIndex] = false;
            HISPlayerUIHelper.UpdatePlayPauseButton(playerIndex);
        }

        #endregion

    }
}
