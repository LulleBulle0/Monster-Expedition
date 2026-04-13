using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject controlsPanel;

    public void StartGame()
    {
        SceneManager.LoadScene("Game");
    }

    public void OpenControls()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    public void CloseControls()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}