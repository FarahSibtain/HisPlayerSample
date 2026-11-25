using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HISPlayerAPI;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Stream settings read from StreamingAssets at runtime (see HISPlayerStreamConfig.json).
/// Enum values are written as strings so the file stays human-editable.
/// </summary>
[Serializable]
public class HISPlayerStreamContent
{
    /// <summary>Remote URL or file name inside StreamingAssets.</summary>
    public string url = "";

    /// <summary>DRM license server URL. Leave empty for clear (non-encrypted) content or to use the global keyServerURI.</summary>
    public string keyServerURI = "";

    /// <summary>Optional license request header name (Android / Widevine only). Empty = use the global tokenKey.</summary>
    public string tokenKey = "";

    /// <summary>Optional license request header value (Android / Widevine only). Empty = use the global tokenValue.</summary>
    public string tokenValue = "";
}

[Serializable]
public class HISPlayerStreamConfig
{
    /// <summary>Simple playlist. Remote URLs or file names inside StreamingAssets (e.g. "Ambisonic_AmbiX_16Ch.mkv").
    /// When enableDRM is true, these use the global keyServerURI / tokenKey / tokenValue.</summary>
    public string[] urls = new string[0];

    /// <summary>Playlist entries with their own DRM settings. Played before the entries of "urls".</summary>
    public HISPlayerStreamContent[] contents = new HISPlayerStreamContent[0];

    /// <summary>True when (any of) the content is DRM encrypted. Must be true to use keyServerURI.</summary>
    public bool enableDRM = false;

    /// <summary>Default DRM license server URL for all entries that don't define their own.</summary>
    public string keyServerURI = "";

    /// <summary>Default license request header name/value (Android / Widevine only).</summary>
    public string tokenKey = "";
    public string tokenValue = "";

    /// <summary>HISPlayerMimeTypes name: URL_EXTENSION, HLS or DASH.</summary>
    public string mimeType = "URL_EXTENSION";

    /// <summary>
    /// HISPlayerAmbisonicAudio value as a number:
    /// 0 NONE, 1 AMBIX_4Channels, 2 AMBIX_4Channels_2HeadLockedChannels, 3 AMBIX_9Channels,
    /// 4 AMBIX_9Channels_2HeadLockedChannels, 5 AMBIX_16Channels, 6 AMBIX_16Channels_2HeadLockedChannels,
    /// 7 TBE_4Channels, 8 TBE_4Channels_2HeadLockedChannels, 9 TBE_6Channels, 10 TBE_6Channels_2HeadLockedChannels,
    /// 11 TBE_8Channels, 12 TBE_8Channels_2HeadLockedChannels
    /// </summary>
    public int ambisonicAudio = 0;

    public bool autoPlay = true;
    public bool loopPlayback = true;
    public bool autoTransition = false;
    public bool unityAudio = false;

    public bool startMuted = false;
    [Range(0f, 1f)] public float volume = 1.0f;
}

/// <summary>
/// 360 / ambisonic sample that creates its player from code with AddStream()
/// (instead of the Inspector's Multi Stream Properties list) as soon as the scene starts.
/// The video is rendered with HISPlayerRenderMode.Material into the skybox material.
/// </summary>
public class HISPlayerAddStreamSample : HISPlayerManager
{
    [Header("Add Stream")]
    [Tooltip("JSON file inside Assets/StreamingAssets that describes the stream.")]
    public string configFileName = "HISPlayerStreamConfig.json";

    [Tooltip("360 material the video is rendered into. Leave empty to use the scene's skybox material (Lighting settings).")]
    public Material skyboxMaterial;

    [Header("XR UI")]
    [Tooltip("Distance of the Screen Space - Camera canvas plane. Must be shorter than the controller ray " +
             "cast distance (10 m on the XRI Starter Assets Near-Far Interactor) or the UI can't be clicked.")]
    public float uiPlaneDistance = 3.0f;

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

    private HISPlayerStreamConfig config;

    private bool streamAdded = false;
    private bool isAddingStream = false;

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

    /// <summary>Resolved playlist (contents + urls, with DRM defaults applied), in AddVideoContent order.</summary>
    private readonly List<HISPlayerStreamContent> playlist = new List<HISPlayerStreamContent>();

    private int PlaylistLength => playlist.Count;

    #region UNITY_FUNCTIONS

    protected override void Awake()
    {
        // No stream is configured in the Inspector: it is created later by OnAddStream().
        multiStreamProperties.Clear();

        // 1. HISPlayerManager Awake call
        base.Awake();

        // 2. HISPlayer SetUpPlayer (with an empty stream list, same as the AddStream sample)
        SetUpPlayer();

        if (skyboxMaterial == null)
            skyboxMaterial = RenderSettings.skybox;

        milliseconds = 0;
        subtitlesText.text = "";
        errorText.text = "";
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;

        SetUpXRUI();

        // The control panel appears once the stream has been added.
        UIPanel.SetActive(false);
        UISettings.SetActive(false);
    }

    private void Start()
    {
        // Add the stream straight away (reads the config, then AddStream + AddVideoContent).
        OnAddStream();
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
        if (!streamAdded || !isPlaybackReady)
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

    /// <summary>
    /// Keeps the UI reachable by the XR controller rays,
    /// independently of what is serialized in the scene.
    /// </summary>
    private void SetUpXRUI()
    {
        Canvas canvas = UIPanel != null ? UIPanel.GetComponentInParent<Canvas>(true) : null;

        if (canvas != null)
        {
            canvas = canvas.rootCanvas;
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiPlaneDistance > 0f)
                canvas.planeDistance = uiPlaneDistance;
        }
    }

    #region ADD_STREAM

    /// <summary>
    /// Called on Start(). Reads the JSON config and adds the player with AddStream().
    /// </summary>
    public void OnAddStream()
    {
        if (streamAdded || isAddingStream)
            return;

        StartCoroutine(AddStreamRoutine());
    }

    private IEnumerator AddStreamRoutine()
    {
        isAddingStream = true;
        errorText.text = "Loading configuration...";

        // 1. Read the stream configuration from StreamingAssets
        yield return LoadConfig();

        BuildPlaylist();

        if (config == null || PlaylistLength == 0)
        {
            errorText.text = "Invalid or missing '" + configFileName + "' in StreamingAssets (no urls / contents).";
            isAddingStream = false;
            yield break;
        }

        if (skyboxMaterial == null)
        {
            errorText.text = "No 360 material assigned and no skybox material set in Lighting settings.";
            isAddingStream = false;
            yield break;
        }

        // 2. Prepare the StreamProperties to be added
        //    loopPlayback / autoTransition / unityAudio are read-only properties in this SDK version,
        //    so they are passed through the alternative constructor.
        StreamProperties stream = new StreamProperties(config.loopPlayback, config.autoTransition, config.unityAudio);
        stream.renderMode = HISPlayerRenderMode.Material;
        stream.material = skyboxMaterial;
        stream.autoPlay = config.autoPlay;

        //    DRM must be enabled on the stream before AddStream() to be able to add encrypted content.
        stream.enableDRM = config.enableDRM;

        //    ambisonicAudio has no public setter or constructor parameter. It is a serialized field
        //    (the one shown in the Inspector), so set it through Unity serialization (IL2CPP safe).
        HISPlayerAmbisonicAudio ambisonic = HISPlayerAmbisonicAudio.NONE;
        if (Enum.IsDefined(typeof(HISPlayerAmbisonicAudio), config.ambisonicAudio))
            ambisonic = (HISPlayerAmbisonicAudio)config.ambisonicAudio;
        else
            Debug.LogWarning("[HISPlayerAddStreamSample] Unknown ambisonicAudio value " + config.ambisonicAudio + ". Using NONE (0).");
        JsonUtility.FromJsonOverwrite("{\"ambisonicAudio\":" + (int)ambisonic + "}", stream);
        if (stream.AmbisonicAudio != ambisonic)
            Debug.LogWarning("[HISPlayerAddStreamSample] Could not apply ambisonicAudio = " + ambisonic + " to the stream.");

        // 3. Add the prepared stream. It is appended to multiStreamProperties.
        AddStream(stream);
        streamIndex = multiStreamProperties.Count - 1;

        // 4. Add the video content(s) to the new player (this builds its playlist)
        HISPlayerMimeTypes mimeType = ParseEnum(config.mimeType, HISPlayerMimeTypes.URL_EXTENSION);
        foreach (HISPlayerStreamContent content in playlist)
        {
            if (config.enableDRM && !string.IsNullOrEmpty(content.keyServerURI))
            {
                // Encrypted content: url + license server (+ optional Widevine header token, Android only)
                AddVideoContent(streamIndex, content.url, content.keyServerURI,
                                content.tokenKey, content.tokenValue, mimeType);
            }
            else
            {
                // Clear content (or DRM stream without license URL: HISPlayer adds it with an empty license)
                AddVideoContent(streamIndex, content.url, mimeType);
            }
        }

        // 5. Initialize the UI for the stream
        streamAdded = true;
        isAddingStream = false;
        currentVideoIndex = 0;
        isPlaying = config.autoPlay;
        mute = config.startMuted;
        volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(config.volume));

        errorText.text = "";
        currTimeText.text = totalTimeText.text = "Loading";
        playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;

        showControls = true;
        UIPanel.SetActive(true);

        StartCoroutine(StartSomeValues());
    }

    /// <summary>
    /// Merges "contents" and "urls" into one playlist and fills empty DRM fields with the global defaults.
    /// </summary>
    private void BuildPlaylist()
    {
        playlist.Clear();
        if (config == null)
            return;

        if (config.contents != null)
        {
            foreach (HISPlayerStreamContent c in config.contents)
            {
                if (c == null || string.IsNullOrEmpty(c.url))
                    continue;

                playlist.Add(new HISPlayerStreamContent
                {
                    url = c.url,
                    keyServerURI = Fallback(c.keyServerURI, config.keyServerURI),
                    tokenKey = Fallback(c.tokenKey, config.tokenKey),
                    tokenValue = Fallback(c.tokenValue, config.tokenValue),
                });
            }
        }

        if (config.urls != null)
        {
            foreach (string url in config.urls)
            {
                if (string.IsNullOrEmpty(url))
                    continue;

                playlist.Add(new HISPlayerStreamContent
                {
                    url = url,
                    keyServerURI = config.keyServerURI ?? "",
                    tokenKey = config.tokenKey ?? "",
                    tokenValue = config.tokenValue ?? "",
                });
            }
        }

        bool anyLicense = playlist.Exists(p => !string.IsNullOrEmpty(p.keyServerURI));
        if (anyLicense && !config.enableDRM)
            Debug.LogWarning("[HISPlayerAddStreamSample] keyServerURI is set but enableDRM is false: content will be added without DRM.");
        if (config.enableDRM && !anyLicense)
            Debug.LogWarning("[HISPlayerAddStreamSample] enableDRM is true but no keyServerURI is set: content will be added with an empty license.");
    }

    private static string Fallback(string value, string defaultValue)
    {
        return !string.IsNullOrEmpty(value) ? value : (defaultValue ?? "");
    }

    private IEnumerator LoadConfig()
    {
        config = null;
        string json = null;

        // 1. Override: a config pushed to the app's storage (no rebuild needed), e.g. on Quest:
        //    adb push HISPlayerStreamConfig.json /sdcard/Android/data/<package>/files/HISPlayerStreamConfig.json
        string overridePath = Path.Combine(Application.persistentDataPath, configFileName);
        if (File.Exists(overridePath))
        {
            try
            {
                json = File.ReadAllText(overridePath);
                Debug.Log("[HISPlayerAddStreamSample] Using config override: " + overridePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HISPlayerAddStreamSample] Could not read " + overridePath + ": " + e.Message + ". Using the built-in config.");
                json = null;
            }
        }

        // 2. Built-in config from StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, configFileName);

        if (json != null)
        {
            // Override found, nothing else to read
        }
        else if (path.Contains("://"))
        {
            // Android (jar:file://) and WebGL: StreamingAssets must be read with UnityWebRequest
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    json = request.downloadHandler.text;
                else
                    Debug.LogError("[HISPlayerAddStreamSample] Failed to read " + path + ": " + request.error);
            }
        }
        else if (File.Exists(path))
        {
            json = File.ReadAllText(path);
        }
        else
        {
            Debug.LogError("[HISPlayerAddStreamSample] Config file not found: " + path);
        }

        if (string.IsNullOrEmpty(json))
            yield break;

        try
        {
            config = JsonUtility.FromJson<HISPlayerStreamConfig>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[HISPlayerAddStreamSample] Invalid JSON in " + configFileName + ": " + e.Message);
            config = null;
        }
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out T result))
            return result;

        if (!string.IsNullOrEmpty(value))
            Debug.LogWarning("[HISPlayerAddStreamSample] Unknown " + typeof(T).Name + " '" + value + "'. Using " + fallback + ".");

        return fallback;
    }

    #endregion

    #region PLAYBACK_CONTROLLER

    public void OnSeekBegin()
    {
        if (!streamAdded) return;
        isSeeking = true;
    }

    public void OnSeekEnd()
    {
        if (!streamAdded) return;
        milliseconds = (long)seekBar.value;
        Seek(streamIndex, milliseconds);
    }

    public void OnStop()
    {
        if (!streamAdded) return;
        playPauseButton.image.sprite = playSprite;
        isPlaying = false;
        milliseconds = 0;
        Stop(streamIndex);
    }

    public void OnToggleMute()
    {
        if (!streamAdded) return;
        mute = !mute;
        SetVolume(streamIndex, mute ? 0.0f : volumeSlider.value);
        muteButton.image.sprite = mute ? muteSprite : unmuteSprite;
    }

    public void OnChangeVolume()
    {
        if (!streamAdded) return;
        if (mute)
        {
            mute = false;
            muteButton.image.sprite = unmuteSprite;
        }

        SetVolume(streamIndex, volumeSlider.value);
    }

    public void OnPreviousPlayback()
    {
        if (!streamAdded || PlaylistLength == 0) return;

        currentVideoIndex--;
        if (currentVideoIndex < 0)
            currentVideoIndex = PlaylistLength - 1;

        ChangeToCurrentVideo();
    }

    public void OnNextPlayback()
    {
        if (!streamAdded || PlaylistLength == 0) return;

        currentVideoIndex++;
        if (currentVideoIndex >= PlaylistLength)
            currentVideoIndex = 0;

        ChangeToCurrentVideo();
    }

    private void ChangeToCurrentVideo()
    {
        isPlaybackReady = false;
        isPlaying = true;
        currTimeText.text = totalTimeText.text = "Loading";
        playPauseButton.image.sprite = pauseSprite;

        StartCoroutine(StartSomeValues());

        // Switch to another entry of the playlist created with AddVideoContent
        ChangeVideoContent(streamIndex, currentVideoIndex);
    }

    public void OnForward(int ms)
    {
        if (!streamAdded) return;
        milliseconds = GetVideoPosition(streamIndex) + ms;
        Seek(streamIndex, milliseconds);
    }

    public void OnTogglePlayPause()
    {
        if (!streamAdded) return;

        if (isPlaying)
            Pause(streamIndex);
        else
            Play(streamIndex);

        isPlaying = !isPlaying;
        playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
    }

    public void OnRestart()
    {
        if (!streamAdded) return;
        isPlaying = true;
        Seek(streamIndex, 0);
        Play(streamIndex);
        restartButton.gameObject.SetActive(false);
        playPauseButton.gameObject.SetActive(true);
        playPauseButton.image.sprite = pauseSprite;
        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);
    }

    public void OnChangeSpeedRate()
    {
        if (!streamAdded) return;

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
        if (!streamAdded) return;
        showSubtitles = !showSubtitles;
        EnableCaptions(streamIndex, showSubtitles);
        subtitlesText.gameObject.SetActive(showSubtitles);
        subtitlesButton.image.color = showSubtitles ? Color.green : Color.white;
    }

    #endregion

    #region TRACK_CONTROLLER

    public void OnChangeResolution()
    {
        if (!streamAdded || !videoDropdownIndex.ContainsKey(resolutionDropDown.value)) return;
        SelectTrack(streamIndex, videoDropdownIndex[resolutionDropDown.value]); //This action will disable ABR
        UISettings.SetActive(false);
        UIPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeSubtitles()
    {
        if (!streamAdded || !captionDropdownIndex.ContainsKey(captionsDropdown.value)) return;
        SelectCaptionTrack(streamIndex, captionDropdownIndex[captionsDropdown.value]);
        UISettings.SetActive(false);
        UIPanel.SetActive(true);
        OnShowSettings(false);
    }

    public void OnChangeAudioTrack()
    {
        if (!streamAdded || !audioDropdownIndex.ContainsKey(audioDropdown.value)) return;
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
        if (!streamAdded) return;
        showControls = !showControls;
        UIPanel.gameObject.SetActive(showControls);
        UISettings.gameObject.SetActive(false);
    }

    public void OnShowSettings(bool show)
    {
        if (!streamAdded) return;
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
                if (video.width > 0 && video.height > 0)
                {
                    resolutionDropDown.options.Add(new TMP_Dropdown.OptionData()
                    {
                        text = video.width.ToString() + " x " + video.height.ToString()
                    });

                    videoDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }
        }

        if (resolutionDropDown.options.Count == 0)
            resolutionDropDown.options.Add(new TMP_Dropdown.OptionData() { text = "Resolution not available" });

        trackIndex = 0;
        dropdownIndex = 0;
        captionsDropdown.ClearOptions();
        captionDropdownIndex.Clear();
        if (captions != null)
        {
            foreach (var cap in captions)
            {
                if (!string.IsNullOrEmpty(cap.language))
                {
                    captionsDropdown.options.Add(new TMP_Dropdown.OptionData() { text = cap.language });
                    captionDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }
        }

        if (captionsDropdown.options.Count == 0)
            captionsDropdown.options.Add(new TMP_Dropdown.OptionData() { text = "Subtitle not available" });

        trackIndex = 0;
        dropdownIndex = 0;
        audioDropdown.ClearOptions();
        audioDropdownIndex.Clear();
        if (audios != null)
        {
            foreach (var aud in audios)
            {
                if (!string.IsNullOrEmpty(aud.language))
                {
                    audioDropdown.options.Add(new TMP_Dropdown.OptionData() { text = aud.language });
                    audioDropdownIndex.Add(dropdownIndex, trackIndex);
                    dropdownIndex++;
                }

                trackIndex++;
            }
        }

        if (audioDropdown.options.Count == 0)
            audioDropdown.options.Add(new TMP_Dropdown.OptionData() { text = "Audio not available" });

        updateSettings = false;
    }

    private void UpdateVideoPosition()
    {
        if (!streamAdded || !isPlaybackReady)
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
        return hours + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
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

        if (!isPlaybackReady)
        {
            updateSettings = true;
            videoTracks = GetTracks(streamIndex);
            captions = GetCaptionTrackList(streamIndex);
            audios = GetAudioTrackList(streamIndex);
            UpdateSettingsPanel();
        }

        if (videoTracks != null && videoDropdownIndex.Count > 0)
        {
            foreach (var entry in videoDropdownIndex)
            {
                var track = videoTracks[entry.Value];
                if ((int)eventInfo.param1 == track.width && (int)eventInfo.param2 == track.height)
                {
                    resolutionDropDown.SetValueWithoutNotify(entry.Key);
                    break;
                }
            }
        }
    }

    protected override void EventPlaybackReady(HISPlayerEventInfo eventInfo)
    {
        base.EventPlaybackReady(eventInfo);
        isPlaybackReady = true;

        totalTimeText.text = ConvertTime(GetVideoDuration(streamIndex));
        seekBar.maxValue = GetVideoDuration(streamIndex);

        // Material render mode copies frames into a Unity texture, which works with Widevine L3
        // (software protection). Hardware-protected L1 output can't be read by Unity and would render black.
        if (config != null && config.enableDRM && Application.platform == RuntimePlatform.Android)
        {
            try
            {
                Debug.Log("[HISPlayerAddStreamSample] Widevine security level: " + GetWidevineSecurityLevel(streamIndex));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HISPlayerAddStreamSample] Could not read the Widevine security level: " + e.Message);
            }
        }
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

        ChangeVideoContent(streamIndex, currentVideoIndex);
    }

    protected override void EventAutoTransition(HISPlayerEventInfo eventInfo)
    {
        base.EventAutoTransition(eventInfo);

        currTimeText.text = totalTimeText.text = "Loading";
        isPlaybackReady = false;
        currentVideoIndex = PlaylistLength > 0 ? (currentVideoIndex + 1) % PlaylistLength : 0;
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

        if (!streamAdded) return;
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
        if (!streamAdded) return;
        msWhenNetworkLost = GetVideoPosition(streamIndex);
    }
}
