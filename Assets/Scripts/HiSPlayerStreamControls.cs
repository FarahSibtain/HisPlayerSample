using L1ve.Manager;
using System;
using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HiSPlayerStreamControls : MonoBehaviour
{    
    [SerializeField] private HISPlayerHandler _hisPlayerHandler;
	//[SerializeField] private DevStatistics _statsDialog;

	[Header("Buttons")]
    [SerializeField] private Button _stopBtn;
    [SerializeField] private Button _muteBtn;
    [SerializeField] private Button _previousStreamBtn;
    [SerializeField] private Button _backBtn;
    [SerializeField] private Button _playPauseToggle;
    [SerializeField] private Button _restartBtn;
    [SerializeField] private Button _forwardBtn;
    [SerializeField] private Button _nextBtn;
    [SerializeField] private Button _speedBtn;
    [SerializeField] private Button _subtitlesBtn;
    [SerializeField] private Button _statsBtn;
    //[SerializeField] private Button _statsWindowCloseBtn;

	[Header("Subtitles")]
	[SerializeField] private Image _playPauseImage;
	[SerializeField] private Image _muteImage;

	[Header("Video Positioning")]
	[SerializeField] private TextMeshProUGUI _currTimeText;
	[SerializeField] private TextMeshProUGUI _totalTimeText;
	[SerializeField] private Slider _seekBar;

	[Header("Misc")]	
    [SerializeField] private bool _mute;
	[SerializeField] private TextMeshProUGUI speedRateText;

	[Header("Resources")]
	public Sprite playSprite;                   
	public Sprite pauseSprite;                  
	public Sprite restartSprite;                
	public Sprite _muteSprite;                   
	public Sprite _unmuteSprite;

	private long milliseconds = 0;
	private bool _isSeeking = false;

	private void Awake()
	{
#if PRODUCTION_BUILD
    gameObject.SetActive(false);
#else
		_playPauseImage.sprite = pauseSprite;
		_muteImage.sprite = _mute ? _muteSprite : _unmuteSprite;
		StartCoroutine(StartSomeValues());
#endif
	}
	void OnEnable()
    {
        Debug.Assert(_hisPlayerHandler != null, "HISPlayerHandler reference is missing.");

		// Add listeners to buttons
        _stopBtn.onClick.AddListener(OnStopClicked);
        _muteBtn.onClick.AddListener(OnToggleMute);
        _previousStreamBtn.onClick.AddListener(OnPreviousStreamClicked);
        _backBtn.onClick.AddListener(() => OnForward(-5000));
		_playPauseToggle.onClick.AddListener(OnTogglePlayPause);
        _restartBtn.onClick.AddListener(OnRestart);
        _forwardBtn.onClick.AddListener(() => OnForward(5000));
        _nextBtn.onClick.AddListener(OnNextStreamClicked);
        _speedBtn.onClick.AddListener(OnChangeSpeedRate);
        //_subtitlesBtn.onClick.AddListener(OnToggleSubtitles);
  //      _statsBtn.onClick.AddListener(_statsDialog.OpenStatistics);
		//_statsWindowCloseBtn.onClick.AddListener(_statsDialog.CloseStatistics);
	}

	private void OnDisable()
	{
		_stopBtn.onClick.RemoveListener(OnStopClicked);
		_muteBtn.onClick.RemoveListener(OnToggleMute);
		_previousStreamBtn.onClick.RemoveListener(OnPreviousStreamClicked);
		_backBtn.onClick.RemoveListener(() => OnForward(-5000));
		_playPauseToggle.onClick.RemoveListener(OnTogglePlayPause);
		_restartBtn.onClick.RemoveListener(OnRestart);
		_forwardBtn.onClick.RemoveListener(() => OnForward(5000));
		_nextBtn.onClick.RemoveListener(OnNextStreamClicked);
		_speedBtn.onClick.RemoveListener(OnChangeSpeedRate);
		//_subtitlesBtn.onClick.RemoveListener(OnToggleSubtitles);
		//_statsBtn.onClick.RemoveListener(_statsDialog.OpenStatistics);
		//_statsWindowCloseBtn.onClick.RemoveListener(_statsDialog.CloseStatistics);
	}

	IEnumerator StartSomeValues()
	{
		yield return new WaitUntil(() => _hisPlayerHandler.IsPlaybackReady);

		_totalTimeText.text = ConvertTime(_hisPlayerHandler.GetVideoDuration());
		_seekBar.maxValue = _hisPlayerHandler.GetVideoDuration();
		_muteImage.sprite = _mute ? _muteSprite : _unmuteSprite;		
	}

	private void Update()
	{
		UpdateVideoPosition();
	}	

	public void OnSeekBegin()
	{
		_isSeeking = true;
	}

	public void OnSeekEnd()
	{
		milliseconds = (long)_seekBar.value;
		_hisPlayerHandler.SeekPlayer(milliseconds);		
	}

	private void UpdateVideoPosition()
	{
		if (!_hisPlayerHandler.IsPlaybackReady)
			return;

		if (!_isSeeking)
		{
			long ms = _hisPlayerHandler.GetVideoPosition();
			_currTimeText.text = ConvertTime(ms);
			_seekBar.value = ms;
		}
		else
		{
			float ms = _seekBar.value;
			_currTimeText.text = ConvertTime((long)ms);
		}
	}

	//public void OnToggleSubtitles()
	//{
	//	showSubtitles = !showSubtitles;
	//	EnableCaptions(0, showSubtitles);
	//	subtitlesText.gameObject.SetActive(showSubtitles);
	//	subtitlesIcon.color = showSubtitles ? Color.green : Color.white;
	//}

	public void OnChangeSpeedRate()
	{
		float currentSpeed = _hisPlayerHandler.PlaybackSpeedRate;
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
		_hisPlayerHandler.PlaybackSpeedRate = newSpeed;
	}

	private void OnNextStreamClicked()
	{
		_currTimeText.text = _totalTimeText.text = "Loading";
		_hisPlayerHandler.OnPreviousNextPlayback(false);
		_playPauseImage.sprite = pauseSprite;
	}

	public void OnRestart()
	{
		_hisPlayerHandler.OnRestart();
		_restartBtn.gameObject.SetActive(false);
		_playPauseToggle.gameObject.SetActive(true);
		_playPauseImage.sprite = pauseSprite;
		var videoDuration = _hisPlayerHandler.GetVideoDuration();
		_totalTimeText.text = ConvertTime(videoDuration);
		_seekBar.maxValue = videoDuration;
	}

	public void OnTogglePlayPause()
	{		
		_playPauseImage.sprite = _hisPlayerHandler.OnTogglePlayPause() ? pauseSprite : playSprite;
	}

	public void OnForward(int ms)
	{
		milliseconds = _hisPlayerHandler.OnForward(ms);
	}

	private void OnPreviousStreamClicked()
	{
		_currTimeText.text = _totalTimeText.text = "Loading";
		_hisPlayerHandler.OnPreviousNextPlayback(true);
		_playPauseImage.sprite = pauseSprite;
	}

	private void OnToggleMute()
	{
		_mute = !_mute;		
		_muteImage.sprite = _mute ? _muteSprite : _unmuteSprite;
        _hisPlayerHandler.SetMute(_mute);
	}

	private void OnStopClicked()
	{
		_playPauseImage.sprite = playSprite;		
		milliseconds = 0;
		_hisPlayerHandler.OnStop();
	}

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

	public void EventSeekReceived()
	{
		_isSeeking = false;
	}
}
