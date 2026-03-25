using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Game Over screen. Can be pre-placed in a scene via Editor setup.
/// No longer used at runtime — death now loads the GameOver scene directly.
/// Kept for backward compatibility if manually placed in scenes.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI gameOverText;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        panelRoot.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestart);
    }

    public void Show()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnRestart()
    {
        if (PlayerStatsManager.Instance != null)
            Object.Destroy(PlayerStatsManager.Instance.gameObject);

        UnityEngine.SceneManagement.SceneManager.LoadScene("Level_01");
    }
}
