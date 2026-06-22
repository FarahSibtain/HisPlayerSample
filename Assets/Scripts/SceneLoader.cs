using L1ve.Manager;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static L1ve.Manager.HISPlayerHandler;
using static System.Net.WebRequestMethods;

public class SceneLoader : MonoBehaviour
{
	[SerializeField] private Button playStream;
	private const string SCENE2 = "StreamScene";

	private void OnEnable()
	{
		playStream.onClick.AddListener(LoadScene);
	}
	private void OnDisable()
	{
		playStream.onClick.RemoveListener(LoadScene);
	}

	public void LoadScene()
    {
		_ = LoadSceneAsync();
	}
	public async Task LoadSceneAsync()
    {
		playStream.gameObject.SetActive(false);		 

		AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(SCENE2, LoadSceneMode.Additive);

		// Wait until the asynchronous scene fully loads
		while (!asyncLoad.isDone)
		{
			await Task.Yield();
		}

		LiveStreamData liveStreamData = new LiveStreamData();
		StreamItem stream = new StreamItem();
        stream.keyContentId = "test1";
		stream.MdbUrl = "https://d33zucwj73hrn1.cloudfront.net/tests/ladder_DRM/stream.mpd";
		liveStreamData.streams = new List<StreamItem> { stream };
        liveStreamData.preroll = null;
        liveStreamData.features = null;

		HISPlayerHandler[] hisPlayerHanlders = FindObjectsByType<HISPlayerHandler>(FindObjectsSortMode.None);
        if (hisPlayerHanlders.Length == 0)
        {
            Debug.LogError("No HISPlayerHandler found in the scene.");
            return;
		}

		hisPlayerHanlders[0].CreateStreams(liveStreamData);
	}

	public void ShowPlayButton()
	{
		playStream.gameObject.SetActive(true);
	}
}
