using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class LoadingSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;

    [Header("Timing")]
    [SerializeField] private float minimumLoadingTime = 3.0f; // Forces the loading screen to stay visible for at least 3 seconds

    private void Start()
    {
        string sceneToLoad = RoomDoorTransition.SceneToLoad;
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            sceneToLoad = "DiningRoom_Scene";
        }

        StartCoroutine(LoadYourGameAsync(sceneToLoad));
    }

    private IEnumerator LoadYourGameAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float timer = 0f;
        float displayProgress = 0f;

        while (!operation.isDone)
        {
            timer += Time.deltaTime;

            // Calculate progress based on both real load progress and our minimum timer
            float rawProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(timer / minimumLoadingTime);
            float targetProgress = Mathf.Min(rawProgress, timeProgress);

            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime * 2f);

            if (progressBar != null)
            {
                progressBar.value = displayProgress;
            }

            if (progressText != null)
            {
                progressText.text = "LOADING " + (displayProgress * 100f).ToString("F0") + "%";
            }

            // Once both the minimum time has passed and the scene is loaded, allow activation
            if (timer >= minimumLoadingTime && operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}