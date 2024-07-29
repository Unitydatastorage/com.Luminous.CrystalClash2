using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class Loading : MonoBehaviour
{

    void Start()
    {
        StartCoroutine(LoadSceneAsync("Game")); 
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        
        while (!asyncLoad.isDone)
        {
            
            
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
