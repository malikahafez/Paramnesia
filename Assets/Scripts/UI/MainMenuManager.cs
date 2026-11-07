using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Transition")]
    [SerializeField] private string firstGameSceneName = "Level1"; // Match your gameplay scene name

    public void StartGame()
    {
        SceneManager.LoadScene(firstGameSceneName);
    }

    public void OpenOptions()
    {
        Debug.Log("Options menu clicked.");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}