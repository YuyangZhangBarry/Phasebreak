using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared load to Main Menu + destroy persistent run objects (player / managers).
/// </summary>
public static class MenuNavigationHelper
{
    public const string MainMenuSceneName = "MainMenu";

    public static void LoadMainMenuAndCleanupRun()
    {
        Time.timeScale = 1f;

        if (PlayerStatsManager.Instance != null)
            Object.DestroyImmediate(PlayerStatsManager.Instance.gameObject);

        foreach (PlayerStatsManager psm in Object.FindObjectsByType<PlayerStatsManager>(FindObjectsSortMode.None))
        {
            if (psm != null)
                Object.DestroyImmediate(psm.gameObject);
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }
}
