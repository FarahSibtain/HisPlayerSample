using System;
using HISPlayerAPI;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class HISPlayerScreen
{
    public RawImage rawImage;
    public Button playButton;
    public Button pauseButton;
    public Button muteButton;
    public GameObject UICanvas;
    public bool isMuted = false;
}

public class HISPlayerAddStreamController : HISPlayerManager
{
    [Header("Sample Variables")]

    /// <summary>
    /// List of the screens to render the streams on
    /// </summary>
    [SerializeField]
    public HISPlayerScreen[] screens;

    /// <summary>
    /// Reference to the Add Stream Button GameObject
    /// </summary>
    public GameObject addStreamButtonGO;

    /// <summary>
    /// Reference to the mute sprite
    /// </summary>
    public Sprite muteSprite;

    /// <summary>
    /// Reference to the unmute sprite
    /// </summary>
    public Sprite unmuteSprite;

    /// <summary>
    /// Determines the number the streams that have been added
    /// </summary>
    private int totalScreens = 0;

    /// <summary>
    /// Determines if the runtime platform is Android or iOS
    /// </summary>
    private bool isAndroidiOS = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;

    /// <summary>
    /// List of HLS video samples
    /// </summary>
    private string[] hlsSamples =
{
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=s7PwvPwJ",
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=GdCDsEmW",
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=EersvPwA",
        "https://content.hisplayer.com/getmedia/master.m3u8?contentKey=AdmhwYCr",
    };

    protected override void Awake()
    {
        if (isAndroidiOS)
            Screen.orientation = ScreenOrientation.LandscapeLeft;

        // 1. HISPlayerManager Awake call
        base.Awake();

        // 2. HISPlayer SetUpPlayer 
        SetUpPlayer();
    }

    #region Buttons

    /// <summary>
    /// Add a new stream following the order from the screens array
    /// </summary>
    public void OnAddStream()
    {
        int playerIndex = totalScreens;

        // 1. Prepare the StreamProperties to be added
        StreamProperties stream = new StreamProperties();
        stream.renderMode = HISPlayerRenderMode.RawImage;
        stream.rawImage = screens[playerIndex].rawImage;
        stream.autoPlay = true;
        stream.url = new System.Collections.Generic.List<string>() { hlsSamples[playerIndex] };

		// 2. Add prepared stream
		AddStream(stream);

        // 3. Add a video content to the stream
        //AddVideoContent(playerIndex, hlsSamples[playerIndex]);

        // 4. Initialize the UI for the stream
        InitializeUI(playerIndex);

        // 5. When the limit has been reached, disable the Add Stream Button
        totalScreens++;
        if (totalScreens >= screens.Length)
            addStreamButtonGO.SetActive(false);
    }

    /// <summary>
    /// Play a certain stream
    /// </summary>
    /// <param name="playerIndex">The index of the stream to be played</param>
    public void OnPlay(int playerIndex)
    {
        if (playerIndex >= multiStreamProperties.Count)
            return;

        screens[playerIndex].pauseButton.gameObject.SetActive(true);
        screens[playerIndex].playButton.gameObject.SetActive(false);
        Play(playerIndex);
    }

    /// <summary>
    /// Pause a certain stream
    /// </summary>
    /// <param name="playerIndex">The index of the stream to be paused</param>
    public void OnPause(int playerIndex)
    {
        if (playerIndex >= multiStreamProperties.Count)
            return;

        screens[playerIndex].playButton.gameObject.SetActive(true);
        screens[playerIndex].pauseButton.gameObject.SetActive(false);
        Pause(playerIndex);
    }

    /// <summary>
    /// Mute/Unmute a certain stream
    /// </summary>
    /// <param name="playerIndex">The index of the stream to be muted/unmuted</param>
    public void OnMute(int playerIndex)
    {
        if (playerIndex >= multiStreamProperties.Count)
            return;

        screens[playerIndex].isMuted = !screens[playerIndex].isMuted;
        float volume = screens[playerIndex].isMuted ? 0.0f : 1.0f;
        screens[playerIndex].muteButton.image.sprite = screens[playerIndex].isMuted ? muteSprite : unmuteSprite;
        SetVolume(playerIndex, volume);
    }

    /// <summary>
    /// Initialize the UI of a certain stream
    /// </summary>
    /// <param name="playerIndex">The index of the tream UI to be initialized</param>
    private void InitializeUI(int playerIndex)
    {
        if (playerIndex >= multiStreamProperties.Count)
            return;

        screens[playerIndex].playButton.onClick.AddListener(() => OnPlay(playerIndex));
        screens[playerIndex].pauseButton.onClick.AddListener(() => OnPause(playerIndex));
        screens[playerIndex].muteButton.onClick.AddListener(() => OnMute(playerIndex));

        float volume = screens[playerIndex].isMuted ? 0.0f : 1.0f;
        screens[playerIndex].muteButton.image.sprite = screens[playerIndex].isMuted ? muteSprite : unmuteSprite;
        SetVolume(playerIndex, volume);

        screens[playerIndex].UICanvas.SetActive(true);
    }

    #endregion

    private void OnApplicationFocus(bool focus)
    {
        if (focus)
            for (int i = 0; i < multiStreamProperties.Count; i++)
                OnPlay(i);
        else
            for (int i = 0; i < multiStreamProperties.Count; i++)
                OnPause(i);
    }

    /// <summary>
    /// Unity function called when the application is quitted
    /// </summary>
    private void OnApplicationQuit()
    {
        // Relese the HISPlayer SDK when the application is quitted
        Release();
    }
}