using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class RoomDoorTransition : MonoBehaviour
{
    [Header("Scene Destination")]
    [SerializeField] private string targetSceneName = "DiningRoom_Scene"; // Change this per door in the Inspector

    [Header("Door Animation & Timing")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private float delayBeforeLoading = 1.0f;

    [Header("UI Prompt")]
    [SerializeField] private GameObject promptUI;

    private bool playerInRange = false;
    private bool isOpening = false;

    // Static variable to pass the target room name to the loading scene
    public static string SceneToLoad = "DiningRoom_Scene";

    private void Start()
    {
        if (promptUI != null) promptUI.SetActive(false);
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

        // Save this door's specific destination globally
        SceneToLoad = targetSceneName;

        // Load the standalone LoadingScene where your camera and render texture live
        SceneManager.LoadScene("LoadingScene");
    }
}