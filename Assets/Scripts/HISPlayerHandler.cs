using HISPlayerAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static L1ve.Manager.HISPlayerHandler;

namespace L1ve.Manager
{
	public class HISPlayerHandler : HISPlayerManager
	{
		public class LiveStreamData
		{
			public PrerollData preroll;
			public List<StreamItem> streams;
			public List<object> features; // empty array, so keep it generic
		}

		public class PrerollData
		{
			public string HlsUrl;
			public string MdbUrl;
			public string KeyContentId;
		}

		public class StreamItem
		{
			public string id;
			public string keyContentId;
			public string title;
			public string HlsUrl;
			public string MdbUrl;
			public string DrmAuthUrl;
			public string NextKeyContentId;
			public string NextMdbUrl;
			public string NextHlsUrl;
		}

		#region Variables and References

		private LiveStreamData _liveStreamData;
		public List<StreamItem> Streams { get; private set; }
		public int SelectedCameraIndex { get; private set; } = 0;
		public Action<int, string> OnVideoOptionChanged;
		public Action OnPlaybackReady;
		public Action OnLocationTitleUpdated;

		private bool _isPlaying;
		private bool _isPlaybackReady;
		private int _playerIndex = 0;
		private long _msWhenNetworkLost;
		//private StreamsNextKeyContentId[] _streamsNextKeyContentId;

		private string _currentResolution = "";
		//private AudioManager _audioManager => AudioManager.Instance;

		private const string KEY_SERVER_URI = "https://drm-widevine-licensing.axprod.net/AcquireLicense";
		private const string DRM_TOKEN_KEY = "X-AxDRM-Message";

		#endregion Variables and References

		#region Unity Functions

		protected override void Awake()
		{
			base.Awake();

			//Camera.main.clearFlags = CameraClearFlags.Skybox;

			//AmplitudeAnalyticsController.Instance?.SetWatchPartySceneType(WatchPartySceneType.Stream);

			_isPlaying = multiStreamProperties[0].autoPlay;
			SetUpPlayer();

			//WebSocketController.OnStreamUpdateItem += OnStreamUpdateItem;

			//_audioManager.ExperienceVolume.OnChanged += OnExperienceVolumeChanged;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (!_isPlaybackReady)
				return;

			if (!hasFocus)
			{
				Pause(_playerIndex);
				return;
			}

			if (_isPlaying)
				Play(_playerIndex);
		}

		private void OnApplicationQuit()
		{
			Release();
		}

		#endregion Unity Functions

		#region Stream Settings

		public void CreateStreams(LiveStreamData liveStreamData)
		{
			_liveStreamData = liveStreamData;
			StartCoroutine(SetupAndStartStream());
		}

		private IEnumerator SetupAndStartStream()
		{
			//if there is a Pre-roll then Play it first. Once it is played then it will be cleared
			if (_liveStreamData.preroll != null && !string.IsNullOrEmpty(_liveStreamData.preroll.MdbUrl))
			{
				PlayPrerollVideo(_liveStreamData.preroll);
			}
			else
			{
				Streams = _liveStreamData.streams;
				if (Streams == null || Streams.Count == 0)
				{
					Debug.LogError("[HISPlayer] No streams available.");
					yield break;
				}

				int lastStreamIndex = Streams.Count - 1;
				Task task = SetDRMFromKeyContentId(Streams[lastStreamIndex].keyContentId);
				yield return new WaitUntil(() => task.IsCompleted);

				//CreateStreamsNextKeyContentIds();

				SelectVideoOption(0);

				//if (Streams.Count > 0)
				//{
				//	AmplitudeAnalyticsController.Instance?.SetInitialPov(Streams[0].title);
				//}

				//AmplitudeAnalyticsController.Instance?.TrackStreamStarted();
				//AmplitudeAnalyticsController.Instance?.TrackStreamEnter();
			}

			//OnExperienceVolumeChanged(_audioManager.ExperienceVolume.Value);
		}

		public void SelectVideoOption(int index, string locationText = "")
		{
			if (index >= Streams.Count)
				index = 0;

			//string currentPov = AmplitudeAnalyticsController.Instance?.CurrentPov;

			//if (!string.IsNullOrEmpty(locationText))
			//{
			//	AmplitudeAnalyticsController.Instance?.TrackLocationChanged(currentPov, locationText);
			//}

			SelectedCameraIndex = index;
			_isPlaybackReady = false;
			_isPlaying = true;
			StartCoroutine(PlayPlayerStreamWhenReady());

			ChangePlayerVideoContent(Streams[index].MdbUrl);
			OnVideoOptionChanged?.Invoke(index, locationText);
		}

		/// <summary>
		/// Creates a separate Player stream for the Pre-roll
		/// </summary>
		/// <param name="mdbUrl"></param>
		private async void PlayPrerollVideo(PrerollData prerollData)
		{
			Debug.Log("Found Pre-roll data, starting Pre-roll stream first. Pre-roll MdbUrl: " + prerollData.MdbUrl);

			// First set the DRM token for the Pre-roll stream
			await SetDRMFromKeyContentId(prerollData.KeyContentId);

			_isPlaying = true;
			StartCoroutine(PlayPlayerStreamWhenReady());

			ChangePlayerVideoContent(prerollData.MdbUrl);
			//UIScreensManager.Instance.ShowHideNavigationPanel(false);
		}

		private async Task SetDRMFromKeyContentId(string keyContentId)
		{
			string drmTokenValue = string.Empty;
			if (string.IsNullOrEmpty(keyContentId))
			{
				Debug.Log("Content Key ID is null");
			}
			else
			{
				Debug.Log("Content Key ID: " + keyContentId);

				//drmTokenValue = await RequestHandler.Instance.GetTokenAsync(keyContentId);
				drmTokenValue = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ2ZXJzaW9uIjoxLCJjb21fa2V5X2lkIjoiZmUzZjBhYmMtZGU0ZC00MzZmLWIzNjYtYjJlNTAwYWZiYjk5IiwibWVzc2FnZSI6eyJ0eXBlIjoiZW50aXRsZW1lbnRfbWVzc2FnZSIsInZlcnNpb24iOjIsImNvbnRlbnRfa2V5c19zb3VyY2UiOnsiaW5saW5lIjpbeyJpZCI6IjU3OWFkNzJiLTVmNmYtNDAwMy1hNzY4LTQ1ODEyMTk4YmM4OCJ9XX19fQ.xaWc_R8SvmXp4KUES-KTk4B8dOrvmxmY8WT3ooziaSc";
				Debug.Log("DRM Token: " + drmTokenValue);
			}

			DRM_Token drm_Token = new DRM_Token() { tokenKey = DRM_TOKEN_KEY, tokenValue = drmTokenValue };
			multiStreamProperties[_playerIndex].DRMTokens = new List<DRM_Token>() { drm_Token };
		}

		private void ChangePlayerVideoContent(string mdbUrl)
		{
			var drmTokens = multiStreamProperties[_playerIndex].DRMTokens;
			if (drmTokens == null || drmTokens.Count == 0)
			{
				ChangeVideoContent(_playerIndex, mdbUrl, KEY_SERVER_URI);
			}
			else
			{
				ChangeVideoContent(_playerIndex, mdbUrl, KEY_SERVER_URI, drmTokens[0].tokenKey, drmTokens[0].tokenValue);
			}
		}

		public void LeaveStream()
		{
			//AmplitudeAnalyticsController.Instance?.TrackStreamExit();
			//AmplitudeAnalyticsController.Instance?.StopWatchTracking();
			//AmplitudeAnalyticsController.Instance?.SetWatchPartySceneType(WatchPartySceneType.Home);
			//WebSocketController.OnStreamUpdateItem -= OnStreamUpdateItem;
			//string currentSceneName = gameObject.scene.name;
			//_audioManager.ExperienceVolume.OnChanged -= OnExperienceVolumeChanged;
			//L1veSceneManager.Load360SceneRequested?.Invoke(currentSceneName);
			//_ = AwsWebSocketConnector.Instance.UnsubscribeFromStreamChannel();

			if (SceneManager.sceneCount > 1)
			{
				UnloadScene();

				SceneLoader[] sceneLoaders = FindObjectsByType<SceneLoader>(FindObjectsSortMode.None);
				if (sceneLoaders.Length == 0)
				{
					Debug.LogError("No sceneLoaders found in the scene.");
					return;
				}

				sceneLoaders[0].ShowPlayButton();
			}
			else
			{
				Debug.LogWarning("Cannot unload the only loaded scene.");
				return;
			}
		}

		private async void UnloadScene()
		{
			Stop(_playerIndex);

			await Task.Yield();			

			await Task.Yield();

			_ = SceneManager.UnloadSceneAsync(gameObject.scene);

			var unloadOp = Resources.UnloadUnusedAssets();
			while (!unloadOp.isDone)
			{
				await Task.Yield();
			}

			// Force Garbage Collection to reclaim memory immediately
			GC.Collect();
		}

		private void OnDisable()
		{
			Release();
		}

		//private void CreateStreamsNextKeyContentIds()
		//{
		//	_streamsNextKeyContentId = new StreamsNextKeyContentId[Streams.Count];
		//	for (int i = 0; i < Streams.Count; i++)
		//	{
		//		_streamsNextKeyContentId[i] = new StreamsNextKeyContentId { eventstreamid = Streams[i].id };
		//	}
		//}

		public void OnExperienceVolumeChanged(float value) => SetVolume(_playerIndex, value);

		//private void OnExperienceVolumeChanged(Observable<float> value) => OnExperienceVolumeChanged(value.Value);

		#endregion Stream Settings

		#region Web Socket & Logic

		//private void OnStreamUpdateItem(StreamUpdateItemResponse response)
		//{
		//	if (response == null) return;

		//	Debug.Log("### OnStreamUpdateItem response.eventstreamid -> : " + response.eventstreamid);
		//	Debug.Log("### OnStreamUpdateItem response.keyContentId -> : " + response.keyContentId);
		//	Debug.Log("### OnStreamUpdateItem response.title -> : " + response.title);
		//	Debug.Log("### OnStreamUpdateItem response.mdbUrl -> : " + response.mdbUrl);
		//	Debug.Log("### OnStreamUpdateItem response.nextKeyContentId -> : " + response.nextKeyContentId);
		//	Debug.Log("### OnStreamUpdateItem response.nextMdbUrl -> : " + response.nextMdbUrl);

		//	if (Streams == null || Streams.Count == 0)
		//	{
		//		Debug.Log("No streams available to update");
		//		return;
		//	}

		//	UpdateLocalStreamData(response);

		//	if (Streams[SelectedCameraIndex].id == response.eventstreamid)
		//	{
		//		SelectVideoOption(SelectedCameraIndex);
		//	}
		//}

		//private void UpdateLocalStreamData(StreamUpdateItemResponse response)
		//{
		//	for (int i = 0; i < Streams.Count; i++)
		//	{
		//		if (Streams[i].id == response.eventstreamid)
		//		{
		//			Streams[i].keyContentId = response.keyContentId;
		//			Streams[i].MdbUrl = response.mdbUrl;
					
		//			if (!string.IsNullOrEmpty(response.title) && Streams[i].title != response.title)
		//			{
		//				Streams[i].title = response.title;
		//				OnLocationTitleUpdated?.Invoke();
		//			}

		//			if (_streamsNextKeyContentId != null && i < _streamsNextKeyContentId.Length)
		//			{
		//				_streamsNextKeyContentId[i].nextKeyContentId = response.nextKeyContentId;
		//				_streamsNextKeyContentId[i].nextMdbUrl = response.nextMdbUrl;
		//			}
		//		}
		//	}
		//}

		private IEnumerator PlayPlayerStreamWhenReady()
		{
			yield return new WaitUntil(() => _isPlaybackReady);

			if (_isPlaying)
			{
				// Only advance the transition when we are in the VideoWaitForPlaying state,
				// which means we are entering the stream for the first time.
				// Camera switches and playlist rollovers also call this coroutine but should
				// NOT trigger the transition effect (they arrive here with VideoIdle as the state).
				//if (SceneTransitionManager.Instance.State.CurrentState == SceneTransitionManager.StateId.VideoWaitForPlaying)
				//{
				//	SceneTransitionManager.Instance.TriggerNextState();
				//}
				
				Seek(_playerIndex, 0);
				Play(_playerIndex);
			}
		}

		private void PlayNextStream()
		{
			if (_liveStreamData.preroll != null && !string.IsNullOrEmpty(_liveStreamData.preroll.MdbUrl))
			{
				_liveStreamData.preroll = null;
				//UIScreensManager.Instance.ShowHideNavigationPanel(true);
				StartCoroutine(SetupAndStartStream());
				return;
			}

			//if (string.IsNullOrEmpty(Streams[SelectedCameraIndex].NextMdbUrl) && string.IsNullOrEmpty(_streamsNextKeyContentId[SelectedCameraIndex].nextMdbUrl))
			//{
			//	// Case where there is no next MDB URL in the stream response and in the websocket command
			//	return;
			//}

			if (!string.IsNullOrEmpty(Streams[SelectedCameraIndex].NextMdbUrl))
			{
				Streams[SelectedCameraIndex].MdbUrl = Streams[SelectedCameraIndex].NextMdbUrl;
				Streams[SelectedCameraIndex].NextMdbUrl = string.Empty;
			}
			else
			{
				//if (!string.IsNullOrEmpty(_streamsNextKeyContentId[SelectedCameraIndex].nextMdbUrl))
				//{
				//	Streams[SelectedCameraIndex].keyContentId = _streamsNextKeyContentId[SelectedCameraIndex].nextKeyContentId;
				//	Streams[SelectedCameraIndex].MdbUrl = _streamsNextKeyContentId[SelectedCameraIndex].nextMdbUrl;
				//}
			}			

			_isPlaybackReady = false;
			_isPlaying = false;

			StartCoroutine(PlayPlayerStreamWhenReady());

			ChangePlayerVideoContent(Streams[SelectedCameraIndex].MdbUrl);
		}

		#endregion Web Socket & Logic

		#region HISPlayer Events Override

		protected override void EventVideoSizeChange(HISPlayerEventInfo eventInfo)
		{
			base.EventVideoSizeChange(eventInfo);

			HISPlayerTrack[] videoTracks = GetTracks(_playerIndex);

			if (videoTracks != null)
			{
				for (int index = 0; index < videoTracks.Length; index++)
				{
					if ((int)eventInfo.param1 == videoTracks[index].width &&
						(int)eventInfo.param2 == videoTracks[index].height)
					{
						_currentResolution = $"{videoTracks[index].width}x{videoTracks[index].height}";
					}
				}
			}
		}

		protected override void EventPlaybackReady(HISPlayerEventInfo eventInfo)
		{
			base.EventPlaybackReady(eventInfo);
			_isPlaybackReady = true;
			OnPlaybackReady?.Invoke();
		}
		protected override void EventPlaybackSeek(HISPlayerEventInfo eventInfo)
		{
			base.EventPlaybackSeek(eventInfo);
			FindAnyObjectByType<HiSPlayerStreamControls>()?.EventSeekReceived();
		}

		protected override void EventEndOfPlaylist(HISPlayerEventInfo eventInfo)
		{
			base.EventEndOfPlaylist(eventInfo);

			PlayNextStream();
		}

		protected override void EventEndOfContent(HISPlayerEventInfo eventInfo)
		{
			base.EventEndOfContent(eventInfo);

			PlayNextStream();
		}

		protected override void EventAutoTransition(HISPlayerEventInfo eventInfo)
		{
			base.EventAutoTransition(eventInfo);

			PlayNextStream();
		}

		protected override void EventNetworkConnected(HISPlayerEventInfo eventInfo)
		{
			base.EventNetworkConnected(eventInfo);

			Seek(_playerIndex, _msWhenNetworkLost);
			Play(_playerIndex);
		}

		protected override void ErrorInfo(HISPlayerErrorInfo errorInfo)
		{
			base.ErrorInfo(errorInfo);
			string error = Environment.NewLine + errorInfo.stringInfo;
			Debug.LogError("Error occured while playing stream: " + error);

			if (errorInfo.errorType == HISPlayerError.HISPLAYER_ERROR_PLAYBACK_DURATION_LIMIT_REACHED)
			{
				_isPlaying = false;
			}
		}

		protected override void ErrorNetworkFailed(HISPlayerErrorInfo errorInfo)
		{
			base.ErrorNetworkFailed(errorInfo);
			_msWhenNetworkLost = GetVideoPosition(_playerIndex);
			Debug.Log("MS WHEN NETWORK LOST: " + _msWhenNetworkLost);
		}
		#endregion HISPlayer Events Override

		#region UI & Statistics API

		/// <summary>
		/// Returns the current network bandwidth formatted in Mbps for UI display.
		/// </summary>
		public string GetHisPlayerNetworkBandwidth()
		{
			return (GetNetworkBandwidth() / 1000.0f).ToString(".0") + " Mbps";
		}

		/// <summary>
		/// Returns a formatted string containing resolution, bitrate, and framerate for all available tracks.
		/// </summary>
		public string GetHisPlayerTrackDetails()
		{
			string trackBitrate = "";
			var videoTracks = GetTracks(_playerIndex);
			if (videoTracks == null) return "No tracks found";

			foreach (var track in videoTracks)
			{
				string trackStats = $"{track.width}x{track.height}   {track.bitrate / (1024.0f * 1024.0f):0.00} Mbps         {track.framerate:0.0}\n";
				if (_currentResolution.Equals($"{track.width}x{track.height}"))
					trackStats = "*" + trackStats;
				trackBitrate += trackStats;
			}
			return trackBitrate;
		}

		#endregion UI & Statistics API

		#region HISPlayer Controls

		public float PlaybackSpeedRate
		{
			get => GetPlaybackSpeedRate(_playerIndex);
			set => SetPlaybackSpeedRate(_playerIndex, value);
		}
		public bool IsPlaybackReady { get { return _isPlaybackReady; } }

		public void OnRestart()
		{
			_isPlaying = true;
			Seek(_playerIndex, 0);
			Play(_playerIndex);			
		}

		public bool OnTogglePlayPause()
		{
			Debug.Log("Toggle Play/Pause. Currently Playing: " + _isPlaying);
			if (_isPlaying)
			{
				Pause(_playerIndex);
			}
			else
			{
				Play(_playerIndex);
			}

			_isPlaying = !_isPlaying;
			return _isPlaying;
		}
		public long OnForward(int ms)
		{
			var milliseconds = GetVideoPosition(_playerIndex) + ms;
			Seek(_playerIndex, milliseconds);
			return milliseconds;
		}
		public void OnStop()
		{
			_isPlaying = false;
			Stop(_playerIndex);
		}

		public void SetMute(bool mute)
		{
			SetVolume(_playerIndex, mute ? 0.0f : 1.0f);
		}

		public void OnPreviousNextPlayback(bool isPrevious)
		{
			if (isPrevious)
			{
				SelectedCameraIndex--;
				if (SelectedCameraIndex < 0)
					SelectedCameraIndex = Streams.Count - 1;
			}
			else
			{
				SelectedCameraIndex++;
				if (SelectedCameraIndex >= Streams.Count)
					SelectedCameraIndex = 0;
			}
			_isPlaybackReady = false;
			_isPlaying = true;
			
			ChangePlayerVideoContent(Streams[SelectedCameraIndex].MdbUrl);			
		}

		public long GetVideoDuration()
		{
			return GetVideoDuration(_playerIndex);
		}

		public void SeekPlayer(long milliseconds)
		{
			Seek(_playerIndex, milliseconds);
		}

		public long GetVideoPosition()
		{
			return GetVideoPosition(_playerIndex);
		}
		#endregion
	}
}