using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using HISPlayerAPI;
using TMPro;

public class HISPlayerVRController : HISPlayerManager
{
    #region Variables and References
    // ------------------------------
    // Video Samples
    // ------------------------------
    [SerializeField] private string[] videoSamples;     // List of videos used for switching between previous and next playback


    // ------------------------------
    // UI Panels
    // ------------------------------
    [Header("Misc")]
    public GameObject settingsPanel;            // Panel for settings menu
    public GameObject playbackControlsPanel;    // Panel containing playback buttons and controls


    // ------------------------------
    // Playback Controls
    // ------------------------------
    [Header("Playback Controller")]
    public Button playPauseToggle;              // Toggle for play/pause
    public Image playPauseImage;                // Image that changes when toggled
    public Image muteImage;                     // Mute/unmute icon
    public Image subtitlesIcon;                 // Subtitles on/off icon
    public Image settingsIcon;                  // Settings on/off icon
    public Button restartToggle;                // Restart button toggle
    public Slider volumeSlider;                 // Volume control
    public TMP_Dropdown resolutionDropDown;     // Dropdown for resolution selection
    public TMP_Dropdown bitrateDropDown;        // Dropdown for min bitrate selection
    public TMP_Dropdown captionsDropdown;       // Dropdown for subtitle track selection
    public TMP_Dropdown audioDropdown;          // Dropdown for audio track selection
    public TextMeshProUGUI currentBitrate;
    public TextMeshProUGUI framerate;      
    public TextMeshProUGUI networkBandwith;      
    public Toggle toggleABR;
    public Slider seekBar;                      // Video progress bar
    public bool mute;                           // Current mute state


    // ------------------------------
    // Information Display
    // ------------------------------
    [Header("Information")]
    public TextMeshProUGUI currTimeText;        // Displays current playback time
    public TextMeshProUGUI totalTimeText;       // Displays total video duration
    public TextMeshProUGUI subtitlesText;       // Displays active subtitles
    public TextMeshProUGUI errorText;           // Displays error messages
    public TextMeshProUGUI speedRateText;       // Displays playback speed


    // ------------------------------
    // UI Resources (Sprites)
    // ------------------------------
    [Header("Resources")]
    public Sprite playSprite;                   // Icon for play
    public Sprite pauseSprite;                  // Icon for pause
    public Sprite restartSprite;                // Icon for restart
    public Sprite muteSprite;                   // Icon for mute
    public Sprite unmuteSprite;                 // Icon for unmute


    // ------------------------------
    // Video Data and Tracks
    // ------------------------------
    private HISPlayerTrack[] videoTracks = null;        // Available video tracks
    private HISPlayerCaptionTrack[] captions = null;    // Available subtitle tracks
    private HISPlayerAudioTrack[] audios = null;        // Available audio tracks


    // ------------------------------
    // State Flags
    // ------------------------------
    private bool showSubtitles = false;         // Whether subtitles are visible
    private bool showSettings = false;          // Whether settings panel is open
    private bool showControls = true;           // Whether playback controls are visible
    private bool isPlaying;                     // Whether video is currently playing
    private bool isPlaybackReady = false;       // Whether video is ready to play
    private bool updateSettings = true;         // Whether UI settings need refreshing
    private bool isSeeking = false;             // Whether user is scrubbing the timeline
    private bool isFirstTime = true;            // Temp variable to be used when videoSizeChanged event occurs for the first time


    // ------------------------------
    // Playback and Indexing
    // ------------------------------
    private int streamIndex = 0;                // Current stream index
    private int currentVideoIndex = 0;          // Index of current video in playlist


    // ------------------------------
    // Timing Variables
    // ------------------------------
    private long msWhenNetworkLost;             // Timestamp when connection was lost
    private long milliseconds = 0;              // Current playback time in milliseconds


    // ------------------------------
    // Dropdown Index Mappings
    // ------------------------------
    private Dictionary<int, int> videoDropdownIndex = new Dictionary<int, int>();   // Video dropdown mapping
    private Dictionary<int, int> bitrateDropdownIndex = new Dictionary<int, int>();   // Video dropdown mapping
    private Dictionary<int, int> audioDropdownIndex = new Dictionary<int, int>();   // Audio dropdown mapping
    private Dictionary<int, int> captionDropdownIndex = new Dictionary<int, int>(); // Subtitles dropdown mapping
    #endregion

    #region Unity Functions

    enum STREAM_PROPERTIES_ITEM
    {
        MATERIAL,
        URL,
        KEY_SERVER_URI,
        DRM_TOKEN_KEY,
        DRM_TOKEN_VALUE
    }
    protected override void Awake()
    {
        base.Awake();

        SetUpPlayer();

        isPlaying = multiStreamProperties[0].autoPlay;
        playPauseImage.sprite = isPlaying ? pauseSprite : playSprite;

        settingsPanel.SetActive(false);
        muteImage.sprite = mute ? muteSprite : unmuteSprite;
        subtitlesText.text = "";
        errorText.text = "";

        StartCoroutine(StartSomeValues());
    }

    private void Update()
    {
        UpdateVideoPosition();

        if (!showSettings) return;

        audioDropdown.RefreshShownValue();
        captionsDropdown.RefreshShownValue();
        resolutionDropDown.RefreshShownValue();
        bitrateDropDown.RefreshShownValue();
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

    #region Playback Controller
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
        playPauseImage.sprite = playSprite;
        isPlaying = false;
        milliseconds = 0;
        Stop(streamIndex);
    }

    public void OnToggleMute()
    {
        mute = !mute;
        SetVolume(streamIndex, mute ? 0.0f : volumeSlider.value);
        muteImage.sprite = mute ? muteSprite : unmuteSprite;
    }

    public void OnChangeVolume()
    {
        if (mute)
        {
            mute = false;
            muteImage.sprite = unmuteSprite;
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
        playPauseImage.sprite = pauseSprite;

        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        //ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex], (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.KEY_SERVER_URI), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_KEY), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_VALUE));
    }

    private object GetStreamProperties(STREAM_PROPERTIES_ITEM item)
    {
        if (multiStreamProperties == null || multiStreamProperties.Count == 0)
        {
            Debug.LogError("MultiStreamProperties not found");
            return null;
        }

        switch (item)
        {
            case STREAM_PROPERTIES_ITEM.MATERIAL:
                return multiStreamProperties[streamIndex].material;

            case STREAM_PROPERTIES_ITEM.KEY_SERVER_URI:
                if (multiStreamProperties[0].keyServerURI.Count > 0)
                {
                    return multiStreamProperties[streamIndex].keyServerURI[0];
                }
                else
                {
                    Debug.LogError("keyServerURI not found");
                }
                break;

            case STREAM_PROPERTIES_ITEM.DRM_TOKEN_KEY:
                if (multiStreamProperties[0].DRMTokens.Count > 0)
                {
                    return multiStreamProperties[streamIndex].DRMTokens[0].tokenKey;
                }
                else
                {
                    Debug.LogError("DRM Token not found");
                }
                break;

            case STREAM_PROPERTIES_ITEM.DRM_TOKEN_VALUE:
                if (multiStreamProperties[0].DRMTokens.Count > 0)
                {
                    return multiStreamProperties[streamIndex].DRMTokens[0].tokenValue;
                }
                else
                {
                    Debug.LogError("DRM Token not found");
                }
                break;
        }

        return null;
    }

    public void OnForward(int ms)
    {
        milliseconds = GetVideoPosition(streamIndex) + ms;
        Seek(streamIndex, milliseconds);
    }
   
    public void OnTogglePlayPause()
    {
        Debug.Log("Toggle Play/Pause. Currently Playing: " + isPlaying);
		if (isPlaying)
        {
            Pause(streamIndex);
        }
        else
        {
            Play(streamIndex);
        }

        isPlaying = !isPlaying;
        playPauseImage.sprite = isPlaying ? pauseSprite : playSprite;
    }

    public void OnRestart()
    {
        isPlaying = true;
        Seek(streamIndex, 0);
        Play(streamIndex);
        restartToggle.gameObject.SetActive(false);
        playPauseToggle.gameObject.SetActive(true);
        playPauseImage.sprite = pauseSprite;
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
        playPauseImage.sprite = pauseSprite;

        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        //ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex], (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.KEY_SERVER_URI), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_KEY), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_VALUE));
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
        subtitlesIcon.color = showSubtitles ? Color.green : Color.white;
    }

    #endregion

    #region Track Controller
    public void OnChangeResolution()
    {
        SelectTrack(streamIndex, videoDropdownIndex[resolutionDropDown.value]); //This action will disable ABR
        settingsPanel.SetActive(false);
        playbackControlsPanel.SetActive(true);
        OnShowSettings(false);
        toggleABR.isOn = false;
    }

    public void OnChangeMinBitrate()
    {
        SetMinBitrate(streamIndex, bitrateDropdownIndex[bitrateDropDown.value] * 1000);  //This action will not disable ABR; Convert the value from kbps to bits per second
        settingsPanel.SetActive(false);
        playbackControlsPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeSubtitles()
    {
        SelectCaptionTrack(streamIndex, captionDropdownIndex[captionsDropdown.value]);
        settingsPanel.SetActive(false);
        playbackControlsPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeAudioTrack()
    {
        SelectAudioTrack(streamIndex, audioDropdownIndex[audioDropdown.value]);
        settingsPanel.SetActive(false);
        playbackControlsPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnEnableABR()
    {
        EnableABR(streamIndex);
        toggleABR.isOn = true;
    }
    #endregion

    #region Unity UI
    IEnumerator StartSomeValues()
    {
        yield return new WaitUntil(() => isPlaybackReady);

        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);
        muteImage.sprite = mute ? muteSprite : unmuteSprite;
        SetVolume(streamIndex, mute ? 0.0f : volumeSlider.value);

        if (isPlaying)
            Play(streamIndex);
    }

    public void OnToggleShowUI()
    {
        showControls = !showControls;
        playbackControlsPanel.gameObject.SetActive(showControls);
        settingsPanel.gameObject.SetActive(false);
    }

    public void OnToggleSettings()
    {
        OnShowSettings(!showSettings);
    }

    public void OnShowSettings(bool show)
    {
        Debug.Log("Toggle Settings. Currently showing: " + showSettings);
		showSettings = show;
        settingsPanel.SetActive(show);
        settingsIcon.color = show ? Color.green : Color.white;

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

        playPauseImage.sprite = isPlaying ? pauseSprite : playSprite;
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
        bitrateDropDown.ClearOptions();
        bitrateDropdownIndex.Clear();
        int[] minBitrates = new int[] { 5, 10, 20, 30 };
        if (minBitrates != null)
        {
            foreach (var bitrate in minBitrates)
            {
                bitrateDropDown.options.Add(new TMP_Dropdown.OptionData()
                {
                    text = bitrate.ToString()
                });

                bitrateDropdownIndex.Add(dropdownIndex, trackIndex);
                dropdownIndex++;

                trackIndex++;
            }

        }

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

    #region Misc
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

    #region HISPlayer Events Override
    protected override void EventPlaybackPlay(HISPlayerEventInfo eventInfo)
    {
        base.EventPlaybackPlay(eventInfo);
        errorText.text = "";
    }

    protected override void EventVideoSizeChange(HISPlayerEventInfo eventInfo)
    {
        base.EventVideoSizeChange(eventInfo);             

        if (isFirstTime)
        {
            isFirstTime = false;
            updateSettings = true;
            videoTracks = GetTracks(streamIndex);
            captions = GetCaptionTrackList(streamIndex);
            audios = GetAudioTrackList(streamIndex);
            UpdateSettingsPanel();
        }

        if (videoTracks != null)
        {
            var trackIndex = videoDropdownIndex[0];
            var stop = false;
            while (trackIndex < videoDropdownIndex.Count && !stop)
            {
                if ((int)eventInfo.param1 == videoTracks[trackIndex].width &&
                    (int)eventInfo.param2 == videoTracks[trackIndex].height)
                {
                    resolutionDropDown.SetValueWithoutNotify(trackIndex);
                    currentBitrate.text = (GetTrackBitrate(streamIndex, trackIndex) / (1024.0f * 1024.0f)).ToString("0.00") + " Mbps";
                    framerate.text = GetTracks(streamIndex)[trackIndex].framerate.ToString("0.0");
                    networkBandwith.text = GetNetworkBandwidth().ToString();
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
        restartToggle.gameObject.SetActive(true);
        playPauseToggle.gameObject.SetActive(false);
        StartCoroutine(StartSomeValues());

        // ChangeVideoContent using a string URL parameter is available from HISPlayer SDK v3.3.0
        //ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex]);
        ChangeVideoContent(streamIndex, videoSamples[currentVideoIndex], (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.KEY_SERVER_URI), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_KEY), (string)GetStreamProperties(STREAM_PROPERTIES_ITEM.DRM_TOKEN_VALUE));
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
        errorText.gameObject.SetActive(true);
        errorText.text += Environment.NewLine + errorInfo.stringInfo;

        if (errorInfo.errorType == HISPlayerError.HISPLAYER_ERROR_PLAYBACK_DURATION_LIMIT_REACHED)
        {
            isPlaying = false;
            playPauseImage.sprite = playSprite;
        }
    }

    protected override void ErrorNetworkFailed(HISPlayerErrorInfo errorInfo)
    {
        base.ErrorNetworkFailed(errorInfo);
        msWhenNetworkLost = GetVideoPosition(streamIndex);
        Debug.Log("MS WHEN NETWORK LOST: " + msWhenNetworkLost);
    }
    #endregion
}
