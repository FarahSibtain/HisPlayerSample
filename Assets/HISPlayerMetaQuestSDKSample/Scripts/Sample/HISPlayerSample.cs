using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HISPlayerAPI;
using System.Collections.Generic;
using System;

public class HISPlayerSample : HISPlayerManager
{
    [Header("Misc")]
    public GameObject UISettings;
    public GameObject UIPanel;

    [Header("Playback Controller")]
    public Button playPauseButton;
    public Button muteButton;
    public Button subtitlesButton;
    public Button restartButton;
    public Slider volumeSlider;
    public TMP_Dropdown resolutionDropDown;
    public TMP_Dropdown captionsDropdown;
    public TMP_Dropdown audioDropdown;
    public Slider seekBar;
    public bool mute;

    [Header("Information")]
    public TextMeshProUGUI currTimeText;
    public TextMeshProUGUI totalTimeText;
    public TextMeshProUGUI subtitlesText;
    public TextMeshProUGUI speedRateText;
    public TextMeshProUGUI errorText;

    [Header("Resources")]
    public Sprite playSprite;
    public Sprite pauseSprite;
    public Sprite restartSprite;
    public Sprite muteSprite;
    public Sprite unmuteSprite;

    private HISPlayerTrack[] videoTracks = null;
    private HISPlayerCaptionTrack[] captions = null;
    private HISPlayerAudioTrack[] audios = null;

    private bool showSubtitles = false;
    private bool showSettings = false;
    private bool showControls = true;
    private bool isPlaying;
    private bool isPlaybackReady = false;
    private bool updateSettings = true;
    private bool isSeeking = false;

    private int streamIndex = 0;
    private int currentVideoIndex = 0;

    private long msWhenNetworkLost;
    private long milliseconds;

    private Dictionary<int, int> videoDropdownIndex = new Dictionary<int, int>();
    private Dictionary<int, int> audioDropdownIndex = new Dictionary<int, int>();
    private Dictionary<int, int> captionDropdownIndex = new Dictionary<int, int>();

    private string[] videoSamples =
    {
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=s7PwvPwJ&protocol=hls",
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=GdCDsEmW&protocol=hls",
    };

    #region UNITY_FUNCTIONS

    protected override void Awake()
    {
        base.Awake();

        if (Application.platform == RuntimePlatform.Android)
        {
           SetUpMetaQuest();
        }
        else
        {
            SetUpPlayer();
        }

        isPlaying = multiStreamProperties[0].autoPlay;
        milliseconds = 0;

        playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;
        subtitlesText.text = "";
        errorText.text = "";
        StartCoroutine(StartSomeValues());
    }

    private void SetUpMetaQuest()
    {
        // Find GameObject where OVROverlay Script is attached
        OVROverlay overlay = overlay = GameObject.Find("RenderScreen").GetComponent<OVROverlay>();

        if (overlay == null)
            overlay = GameObject.Find("RenderScreen").AddComponent<OVROverlay>();

        overlay.enabled = true;

        if (overlay.isExternalSurface)
        {
            OVROverlay.ExternalSurfaceObjectCreated surfaceCreatedCallback = () =>
            {
                // Set the external surface to HISPlayer multistream properties
                multiStreamProperties[streamIndex].externalSurface = overlay.externalSurfaceObject;

                // Set-up HISPlayer after setting the external surface. 
                SetUpPlayer();

                SetStereoscopicRendering(streamIndex, HISPlayerStereoMode.TopBottom, ref overlay.overrideTextureRectMatrix, 
                    ref overlay.srcRectLeft, ref overlay.srcRectRight, ref overlay.destRectLeft, ref overlay.destRectRight);
            };

            if (overlay.externalSurfaceObject == IntPtr.Zero)
            {
                overlay.externalSurfaceObjectCreated = surfaceCreatedCallback;
            }
            else
            {
                surfaceCreatedCallback.Invoke();
            }
        }
    }

    private void Update()
    {
        UpdateVideoPosition();

        if (!showSettings)
            return;

        audioDropdown.RefreshShownValue();
        captionsDropdown.RefreshShownValue();
        resolutionDropDown.RefreshShownValue();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!isPlaybackReady)
            return;

        if (!hasFocus)
        {
            Pause(streamIndex);
            return;
        }

        if (isPlaying)
            Play(streamIndex);
    }

    void OnApplicationQuit()
    {
        Release();
    }

    #endregion

    #region PLAYBACK_CONTROLLER

    public void OnSeekBegin()
    {
        isSeeking = true;
    }

    public void OnSeekEnd()
    {
        milliseconds = (long)seekBar.value;
        Seek(streamIndex, milliseconds);
    }

    public void OnStop()
    {
        playPauseButton.image.sprite = playSprite;
        isPlaying = false;
        milliseconds = 0;
        Stop(streamIndex);
    }

    public void OnToggleMute()
    {
        mute = !mute;
        SetVolume(streamIndex, mute ? 0.0f : volumeSlider.value);
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;
    }

    public void OnChangeVolume()
    {
        if (mute)
        {
            mute = false;
            muteButton.image.sprite = unmuteSprite;
        }

        SetVolume(streamIndex, volumeSlider.value);
    }

    public void OnPreviousPlayback()
    {
        currentVideoIndex--;
        if (currentVideoIndex < 0)
            currentVideoIndex = videoSamples.Length - 1;

        isPlaybackReady = false;
        isPlaying = true;
        currTimeText.text = totalTimeText.text = "Loading";
        playPauseButton.image.sprite = pauseSprite;

        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
    }

    public void OnForward(int ms)
    {
        milliseconds = GetVideoPosition(streamIndex) + ms;
        Seek(streamIndex, milliseconds);
    }

    public void OnTogglePlayPause()
    {
        if (isPlaying)
        {
            Pause(streamIndex);
        }
        else
        {
            Play(streamIndex);
        }

        isPlaying = !isPlaying;
        playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
    }

    public void OnRestart()
    {
        isPlaying = true;
        Seek(streamIndex, 0);
        Play(streamIndex);
        restartButton.gameObject.SetActive(false);
        playPauseButton.gameObject.SetActive(true);
        playPauseButton.image.sprite = pauseSprite;
        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);
    }

    public void OnNextPlayback()
    {
        currentVideoIndex++;
        if (currentVideoIndex >= videoSamples.Length)
            currentVideoIndex = 0;

        isPlaybackReady = false;
        currTimeText.text = totalTimeText.text = "Loading";
        isPlaying = true;
        playPauseButton.image.sprite = pauseSprite;

        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
    }

    public void OnChangeSpeedRate()
    {
        float currentSpeed = GetPlaybackSpeedRate(streamIndex);
        float newSpeed = 1.0f;
        switch (currentSpeed)
        {
            case 1.0f:
                newSpeed = 1.25f;
                speedRateText.text = "x1.25";
                break;
            case 1.25f:
                newSpeed = 1.5f;
                speedRateText.text = "x1.5";
                break;
            case 1.5f:
                newSpeed = 2.0f;
                speedRateText.text = "x2.0";
                break;
            case 2.0f:
                newSpeed = 8.0f;
                speedRateText.text = "x8.0";
                break;
            case 8.0f:
                newSpeed = 1.0f;
                speedRateText.text = "x1.0";
                break;
            default:
                break;
        }

        SetPlaybackSpeedRate(streamIndex, newSpeed);
    }

    public void OnToggleSubtitles()
    {
        showSubtitles = !showSubtitles;
        EnableCaptions(0, showSubtitles);
        subtitlesText.gameObject.SetActive(showSubtitles);
        subtitlesButton.image.color = showSubtitles ? Color.green : Color.white;
    }

    #endregion

    #region TRACK_CONTROLLER

    public void OnChangeResolution()
    {
        SelectTrack(streamIndex, videoDropdownIndex[resolutionDropDown.value]); //This action will disable ABR
        UISettings.SetActive(false);
        UIPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeSubtitles()
    {
        SelectCaptionTrack(streamIndex, captionDropdownIndex[captionsDropdown.value]);
        UISettings.SetActive(false);
        UIPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeAudioTrack()
    {
        SelectAudioTrack(streamIndex, audioDropdownIndex[audioDropdown.value]);
        UISettings.SetActive(false);
        UIPanel.SetActive(true);
        OnShowSettings(false);
    }

    #endregion

    #region UNITY_UI
    IEnumerator StartSomeValues()
    {
        yield return new WaitUntil(() => isPlaybackReady);

        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;
        SetVolume(streamIndex, mute ? 0.0f : volumeSlider.value);

        if (isPlaying)
            Play(streamIndex);
    }

    public void OnToggleShowUI()
    {
        showControls = !showControls;
        UIPanel.gameObject.SetActive(showControls);
        UISettings.gameObject.SetActive(false);
    }

    public void OnShowSettings(bool show)
    {
        showSettings = show;
        if (showSettings)
        {
            isPlaying = false;
            Pause(streamIndex);
        }
        else
        {
            isPlaying = true;
            Play(streamIndex);
        }

        playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
    }

    public void UpdateSettingsPanel()
    {
        if (!updateSettings)
            return;

        int trackIndex = 0;
        int dropdownIndex = 0;
        resolutionDropDown.ClearOptions();
        videoDropdownIndex.Clear();
        if (videoTracks != null)
        {
            foreach (var video in videoTracks)
            {
                if (video.width > 0 &&
                    video.height > 0)
                {
                    resolutionDropDown.options.Add(new TMP_Dropdown.OptionData()
                    {
                        text = /*"Resolution: " + */video.width.ToString() +
                        " x " + video.height.ToString()
                    });

                    videoDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }
        }

        if (resolutionDropDown.options.Count == 0)
            resolutionDropDown.options.Add(new TMP_Dropdown.OptionData()
            {
                text = "Resolution not available"
            });

        trackIndex = 0;
        dropdownIndex = 0;
        captionsDropdown.ClearOptions();
        captionDropdownIndex.Clear();
        if (captions != null)
        {
            foreach (var cap in captions)
            {
                if (cap.language != null && 
                    cap.language != "")
                {
                    captionsDropdown.options.Add(new TMP_Dropdown.OptionData()
                    {
                        text = cap.language
                    });

                    captionDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }

        }

        if (captionsDropdown.options.Count == 0)
            captionsDropdown.options.Add(new TMP_Dropdown.OptionData()
            {
                text = "Subtitle not available"
            });

        trackIndex = 0;
        dropdownIndex = 0;
        audioDropdown.ClearOptions();
        audioDropdownIndex.Clear();
        if (audios != null)
        {
            foreach (var aud in audios)
            {
                if (aud.language != null && 
                    aud.language != "")
                {
                    audioDropdown.options.Add(new TMP_Dropdown.OptionData()
                    {
                        text = aud.language
                    });

                    audioDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }
        }

        if (audioDropdown.options.Count == 0)
            audioDropdown.options.Add(new TMP_Dropdown.OptionData()
            {
                text = "Audio not available"
            });

        updateSettings = false;
    }

    private void UpdateVideoPosition()
    {
        if (!isPlaybackReady)
            return;

        if (!isSeeking)
        {
            long ms = GetVideoPosition(streamIndex);
            currTimeText.text = ConvertTime(ms);
            seekBar.value = ms;
        }
        else
        {
            float ms = seekBar.value;
            currTimeText.text = ConvertTime((long)ms);
        }
    }

    #endregion

    #region MISC

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

    #endregion


    // HISPLAYER EVENTS OVERRIDE
    protected override void EventPlaybackPlay(HISPlayerEventInfo eventInfo)
    {
        base.EventPlaybackPlay(eventInfo);
        errorText.text = "";
    }

    protected override void EventVideoSizeChange(HISPlayerEventInfo eventInfo)
    {
        base.EventVideoSizeChange(eventInfo);

        Debug.Log("EventVideoSizeChanged + !isPlaybackReady" + isPlaybackReady.ToString());

        Debug.Log("Current Resolution is : " + (int)eventInfo.param1 + "x" + (int)eventInfo.param2);

        if (!isPlaybackReady)
        {
            updateSettings = true;
            videoTracks = GetTracks(streamIndex);
            captions = GetCaptionTrackList(streamIndex);
            audios = GetAudioTrackList(streamIndex);
            UpdateSettingsPanel();
        }

        if (videoTracks != null)
        {
            int trackIndex = videoDropdownIndex[0];
            bool stop = false;
            while (trackIndex < videoDropdownIndex.Count && !stop)
            {
                if ((int)eventInfo.param1 == videoTracks[trackIndex].width &&
                    (int)eventInfo.param2 == videoTracks[trackIndex].height)
                {
                    resolutionDropDown.SetValueWithoutNotify(trackIndex);
                    stop = true;
                }

                trackIndex++;
            }
        }
    }

    protected override void EventPlaybackReady(HISPlayerEventInfo eventInfo)
    {
        base.EventPlaybackReady(eventInfo);
        isPlaybackReady = true;

        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);
    }

    protected override void EventTextRender(HISPlayerCaptionElement subtitlesInfo)
    {
        base.EventTextRender(subtitlesInfo);
        subtitlesText.text = subtitlesInfo.caption;
    }

    protected override void EventEndOfPlaylist(HISPlayerEventInfo eventInfo)
    {
        base.EventEndOfPlaylist(eventInfo);
        currentVideoIndex = 0;

        currTimeText.text = totalTimeText.text = "Loading";
        isPlaybackReady = false;
        isPlaying = false;
        restartButton.gameObject.SetActive(true);
        playPauseButton.gameObject.SetActive(false);
        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
    }

    protected override void EventAutoTransition(HISPlayerEventInfo eventInfo)
    {
        base.EventAutoTransition(eventInfo);

        currTimeText.text = totalTimeText.text = "Loading";
        isPlaybackReady = false;
        currentVideoIndex++;
        StartCoroutine(StartSomeValues());
    }

    protected override void EventPlaybackSeek(HISPlayerEventInfo eventInfo)
    {
        base.EventPlaybackSeek(eventInfo);
        isSeeking = false;
    }

    protected override void EventNetworkConnected(HISPlayerEventInfo eventInfo)
    {
        base.EventNetworkConnected(eventInfo);

        Seek(streamIndex, msWhenNetworkLost);
        Play(streamIndex);
    }

    protected override void ErrorInfo(HISPlayerErrorInfo errorInfo)
    {
        base.ErrorInfo(errorInfo);
        errorText.text += Environment.NewLine + errorInfo.stringInfo;

        if (errorInfo.errorType == HISPlayerError.HISPLAYER_ERROR_PLAYBACK_DURATION_LIMIT_REACHED)
        {
            isPlaying = false;
            playPauseButton.image.sprite = playSprite;
        }
    }

    protected override void ErrorNetworkFailed(HISPlayerErrorInfo errorInfo)
    {
        base.ErrorNetworkFailed(errorInfo);
        msWhenNetworkLost = GetVideoPosition(streamIndex);
        Debug.Log("MS WHEN NETWORK LOST: " + msWhenNetworkLost);
    }
}
