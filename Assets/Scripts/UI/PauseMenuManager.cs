using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject pauseMenuCanvas;

    [Header("Scene Names")]
    [SerializeField] private string firstGameSceneName = "Level1"; // <-- Put your true starting scene name here!
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private bool isPaused = false;

    private void Start()
    {
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void ResumeGame()
    {
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void PauseGame()
    {
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Use this for "New Game" to start completely fresh from the top
    public void StartNewGame()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ResetInventory();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(firstGameSceneName);
    }

    // Use this if you want a separate "Restart Room" button
    public void RestartCurrentScene()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ResetInventory();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ResetInventory();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}