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
	[SerializeField] private GameObject addScene;
    //const string SCENE1 = "MainScene";
    //const string SCENE2 = "MainScene2";

    public void LoadScene(string sceneName)
    {
		_ = LoadSceneAsync(sceneName);
	}
	public async Task LoadSceneAsync(string sceneName)
    {
		addScene.SetActive(false);		 

		await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);		

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

	public void UnloadMyScene()
	{
		addScene.SetActive(true);
	}

	//public void LoadMainScene()
 //   {
 //       if (SceneLoader.IsSceneInBuild(SCENE1))
 //           LoadScene(SCENE1);
 //       else LoadScene(SCENE2);
 //   }

 //   public static bool IsSceneInBuild(string sceneName)
 //   {
 //       int sceneCount = SceneManager.sceneCountInBuildSettings;

 //       for (int i = 0; i < sceneCount; i++)
 //       {
 //           string path = SceneUtility.GetScenePathByBuildIndex(i);
 //           string name = System.IO.Path.GetFileNameWithoutExtension(path);

 //           if (name == sceneName)
 //           {
 //               return true;
 //           }
 //       }

 //       return false;
 //   }
}
