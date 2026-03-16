using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject winPanel;      // Drag Win Panel here
    public GameObject losePanel;     // Drag Lose Panel here

    private static GameManager instance;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        // Hide all panels at start
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Make sure game is running
        Time.timeScale = 1f;
    }

    // Call this from FinishButton when player wins
    public static void WinGame()
    {
        if (instance != null && instance.winPanel != null)
        {
            instance.winPanel.SetActive(true);
            Time.timeScale = 0f; // Pause the game
            Debug.Log("YOU WIN!");
        }
    }

    // Call this from KillZone/TrapController when player dies
    public static void LoseGame()
    {
        if (instance != null && instance.losePanel != null)
        {
            instance.losePanel.SetActive(true);
            Time.timeScale = 0f; // Pause the game
            Debug.Log("GAME OVER!");
        }
    }

    // Button: Restart the game
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Button: Quit the game
    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
