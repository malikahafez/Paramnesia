using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Required if using a Slider or UI Text
using System.Collections;
using TMPro; // Include if using TextMeshPro

public class RoomDoorTransition : MonoBehaviour
{
    [Header("Scene Destination")]
    [SerializeField] private string targetSceneName = "DiningRoom_Scene";

    [Header("Door Animation & Timing")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private float delayBeforeLoading = 1.0f;

    [Header("UI Prompt & Loading")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private GameObject loadingScreenCanvas;
    [SerializeField] private TMP_Text progressText; // Optional: Drag your LoadingText here
    [SerializeField] private Slider progressBar;     // Optional: Drag your Slider here

    private bool playerInRange = false;
    private bool isOpening = false;

    private void Start()
    {
        if (promptUI != null) promptUI.SetActive(false);
        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(false);
    }

    private void Update()
    {
        if (playerInRange && !isOpening && Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(OpenDoorSequence());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isOpening)
        {
            playerInRange = true;
            if (promptUI != null) promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }

   private IEnumerator OpenDoorSequence()
    {
        isOpening = true;
        if (promptUI != null) promptUI.SetActive(false);

        // Play door opening animation
        if (doorAnimator != null)
        {
            doorAnimator.Play("DoorOpen");
        }

        yield return new WaitForSeconds(delayBeforeLoading);

        // Turn on the loading screen canvas
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(true);
        }

        // Start loading the target scene asynchronously, but don't activate it immediately 
        // so we can control the display speed of the progress bar
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);
        operation.allowSceneActivation = false;

        float displayProgress = 0f;

        while (!operation.isDone)
        {
            // Unity's actual progress caps at 0.9 until allowSceneActivation is true
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);

            // Smoothly interpolate our displayed progress towards the actual progress
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime * 2f); // Adjust '2f' to make it faster or slower

            if (progressBar != null)
            {
                progressBar.value = displayProgress;
            }

            if (progressText != null)
            {
                progressText.text = "LOADING " + (displayProgress * 100f).ToString("F0") + "%";
            }

            // Once our fake/smooth progress catches up to 100%, allow the scene to open
            if (displayProgress >= 0.99f && operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}